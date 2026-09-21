using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal sealed partial class CitedEmbeddingStore
{
    private static readonly JsonSerializerOptions SemanticJson = new(JsonSerializerDefaults.Web);

    public Task<RetrievalView> RetrieveAsync(Guid queryOperationId, string requestKey, string mode,
        int topK, LexicalPlan? lexicalPlan, CancellationToken cancellationToken)
    {
        var version = SemanticRetrievalPolicy.Version(mode);
        var hybrid = mode != "vector";
        if (string.IsNullOrWhiteSpace(requestKey) || requestKey.Length > 100 || requestKey != requestKey.Trim() ||
            topK is < 1 or > 20 || hybrid != (lexicalPlan is not null))
            throw Error("invalid_semantic_request", 400);
        return WithOutcome(queryOperationId, "query", async (db, tx, accepted, reservation, artifact, ct) =>
        {
            var timer = Stopwatch.StartNew();
            var question = accepted.Inputs.Single().Text;
            lexicalPlan?.Validate(question, SemanticRetrievalPolicy.CandidateLimit);
            var policy = JsonSerializer.Serialize(new
            {
                schemaVersion = "semantic-retrieval-v1", policyVersion = version, mode, topK,
                queryOperationId, reservationId = reservation.Id, outcomeArtifactId = artifact.Id,
                responseSha256 = accepted.ResponseSha256, questionSha256 = accepted.Inputs[0].Sha256,
                provider = "openai", model = CitedEmbeddingStorage.Model, modelVersion = CitedEmbeddingStorage.Model,
                dimension = CitedEmbeddingStorage.Dimension, distance = "cosine",
                candidateLimit = SemanticRetrievalPolicy.CandidateLimit,
                fusionConstant = hybrid ? (int?)SemanticRetrievalPolicy.FusionConstant : null,
                lexicalPlan
            }, SemanticJson);
            // Serialize same-key callers before checking history. Collisions only serialize
            // unrelated requests; identity/conflict decisions still use full scoped values.
            await using (var lease = Command(db, tx, "SELECT pg_advisory_xact_lock(hashtextextended(@identity, 50317))"))
            {
                lease.Parameters.AddWithValue("identity", $"{accepted.OrganizationId}/{accepted.ActorId}/{requestKey}");
                await lease.ExecuteNonQueryAsync(ct);
            }
            var existing = await db.CitedRetrievalRuns.Include(x => x.Sources).Include(x => x.Hits)
                .SingleOrDefaultAsync(x => x.OrganizationId == accepted.OrganizationId &&
                    x.ActorId == accepted.ActorId && x.RequestKey == requestKey, ct);
            if (existing is not null)
            {
                // PostgreSQL jsonb normalizes property order and spacing; compare the
                // full structure rather than treating its serialization as original bytes.
                if (!JsonNode.DeepEquals(JsonNode.Parse(existing.PolicyJson), JsonNode.Parse(policy)) ||
                    existing.PolicyVersion != version || existing.Question != question ||
                    !existing.Sources.Select(x => x.IndexId).Order().SequenceEqual(accepted.IndexIds.Order()))
                    throw Error("request_key_reused");
                if (existing.Status is not (CitedRetrievalStatus.Retrieved or CitedRetrievalStatus.Completed))
                    throw Error("semantic_retrieval_not_complete");
                return await SemanticView(db, existing, true, ct);
            }
            // Coverage, candidates, hit insertion and Retrieved all share the source locks
            // acquired by WithOutcome. No model/private HTTP or spend mutation occurs here.
            var vectors = await SearchAcceptedAsync(db, tx, accepted,
                hybrid ? SemanticRetrievalPolicy.CandidateLimit : topK, ct);
            IReadOnlyList<SemanticCandidate> ranked;
            if (lexicalPlan is not null)
            {
                var lexical = await SemanticLexicalCandidates(db, tx, accepted, lexicalPlan, ct);
                ranked = SemanticRetrievalPolicy.Fuse(lexical, vectors,
                    mode == "hybrid-nonredundant" ? SemanticRetrievalPolicy.CandidateLimit : topK);
                if (mode == "hybrid-nonredundant")
                {
                    var candidateIds = ranked.Select(x => x.ChunkId).ToArray();
                    var candidates = await db.DocumentChunks.AsNoTracking().Include(x => x.Spans).Where(x =>
                        x.OrganizationId == accepted.OrganizationId && accepted.IndexIds.Contains(x.IndexId) &&
                        candidateIds.Contains(x.Id)).ToArrayAsync(ct);
                    var actionIds = candidates.Where(x => x.ReviewActionId.HasValue).Select(x => x.ReviewActionId!.Value).ToArray();
                    var confirmed = await db.ReviewActions.AsNoTracking().Where(x => x.OrganizationId == accepted.OrganizationId &&
                        actionIds.Contains(x.Id) && x.Action == ReviewActionType.Confirmed).Select(x => x.Id).ToArrayAsync(ct);
                    var evidence = candidates.ToDictionary(x => x.Id, x => new SemanticEvidence(x.Id, x.OrganizationId,
                        x.IndexId, x.Kind.ToString(), x.Content, x.ReviewActionId.HasValue && confirmed.Contains(x.ReviewActionId.Value),
                        x.Spans.Select(s => new SemanticEvidenceSpan(s.PageNumber, s.ByteStart, s.ByteEnd, s.Quote)).ToArray()));
                    ranked = SemanticRetrievalPolicy.SelectNonredundant(ranked, evidence, topK);
                }
            }
            else ranked = vectors.Select(x => new SemanticCandidate(x.ChunkId, null, x.Similarity, null)).ToArray();
            var indexes = await db.CitedDocumentIndexes.AsNoTracking().Where(x =>
                x.OrganizationId == accepted.OrganizationId && accepted.IndexIds.Contains(x.Id)).ToArrayAsync(ct);
            if (indexes.Select(x => x.DocumentId).Distinct().Count() != indexes.Length)
                throw Error("semantic_duplicate_document");
            var run = new CitedRetrievalRun(Guid.NewGuid(), accepted.OrganizationId, accepted.ActorId, question,
                requestKey, version, policy, indexes, DateTimeOffset.UtcNow);
            var ids = ranked.Select(x => x.ChunkId).ToArray();
            var chunks = await db.DocumentChunks.AsNoTracking().Where(x => x.OrganizationId == accepted.OrganizationId &&
                ids.Contains(x.Id) && accepted.IndexIds.Contains(x.IndexId)).ToDictionaryAsync(x => x.Id, ct);
            if (chunks.Count != ids.Length) throw Error("embedding_source_changed");
            for (var i = 0; i < ranked.Count; i++)
            {
                var hit = ranked[i];
                run.AddHit(Guid.NewGuid(), chunks[hit.ChunkId], i + 1, hit.LexicalScore, hit.VectorScore, hit.FusedScore);
            }
            run.MarkRetrieved(timer.ElapsedMilliseconds);
            db.CitedRetrievalRuns.Add(run);
            await db.SaveChangesAsync(ct);
            return await SemanticView(db, run, false, ct);
        }, cancellationToken);
    }

    private static async Task<List<(Guid Id, double Score)>> SemanticLexicalCandidates(AppDbContext db,
        IDbContextTransaction tx, AcceptedEmbeddingOutcome accepted, LexicalPlan plan, CancellationToken ct)
    {
        await using var query = Command(db, tx, """
            SELECT c.id, ts_rank_cd(to_tsvector('english'::regconfig, c.content), q.query)::double precision AS score
            FROM document_chunks c CROSS JOIN websearch_to_tsquery('english'::regconfig, @query) AS q(query)
            WHERE c.organization_id=@org AND c.index_id=ANY(@ids)
              AND to_tsvector('english'::regconfig, c.content) @@ q.query
            ORDER BY score DESC, c.id ASC LIMIT @limit
            """);
        query.Parameters.AddWithValue("org", accepted.OrganizationId);
        query.Parameters.AddWithValue("ids", accepted.IndexIds);
        query.Parameters.AddWithValue("query", plan.SearchText);
        query.Parameters.AddWithValue("limit", SemanticRetrievalPolicy.CandidateLimit);
        var result = new List<(Guid, double)>();
        await using var reader = await query.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add((reader.GetGuid(0), reader.GetDouble(1)));
        return result;
    }

    private static async Task<RetrievalView> SemanticView(AppDbContext db, CitedRetrievalRun run, bool replayed, CancellationToken ct)
    {
        var ids = run.Hits.Select(x => x.ChunkId).ToArray();
        var indexIds = run.Sources.Select(x => x.IndexId).ToArray();
        var chunks = await db.DocumentChunks.AsNoTracking().Include(x => x.Spans)
            .Where(x => x.OrganizationId == run.OrganizationId && ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var indexes = await db.CitedDocumentIndexes.AsNoTracking()
            .Where(x => x.OrganizationId == run.OrganizationId && indexIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var hits = run.Hits.OrderBy(x => x.Rank).Select(hit =>
        {
            var chunk = chunks[hit.ChunkId];
            var index = indexes[hit.IndexId];
            return new EvidenceHit(hit.Id, hit.IndexId, hit.ChunkId, index.DocumentId, index.Filename,
                chunk.ReviewSessionId, chunk.ExtractionRunId, hit.Rank, hit.FusedScore ?? hit.VectorScore ?? hit.LexicalScore,
                chunk.Kind.ToString(), chunk.Content, chunk.ReviewActionId,
                chunk.Spans.OrderBy(x => x.PageNumber).ThenBy(x => x.ByteStart).Select(x => new SpanView(x.Id,
                    x.PageNumber, x.ByteStart, x.ByteEnd, x.Quote, x.BoundingPolygon, x.CoordinateUnit)).ToArray());
        }).ToArray();
        return new(run.Id, run.Status.ToString(), run.PolicyVersion, replayed, run.RetrievalDurationMs, run.FailureCode, hits);
    }
}
