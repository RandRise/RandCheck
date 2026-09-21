using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal sealed class CitedReviewedQuestionStore(AppDbContext db) : ICitedReviewedQuestionStore
{
    public async Task<ReviewedQuestionSaved> CreateAsync(Guid org, Guid actor, string question, string key,
        IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (!db.Database.IsNpgsql())
            throw new CitedWorkflowException("postgresql_required", "Reviewed questions require PostgreSQL.", 503);
        var timer = Stopwatch.StartNew();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var native = (NpgsqlTransaction)transaction.GetDbTransaction();
        // Serialize the same actor/request identity, then pin caller/source eligibility through commit.
        await using (var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@key,0))", connection, native))
        {
            command.CommandTimeout = 15;
            command.Parameters.AddWithValue("key", org + ":" + actor + ":" + key);
            await command.ExecuteNonQueryAsync(ct);
        }
        await using (var command = new NpgsqlCommand(
            "SELECT id FROM actors WHERE organization_id=@org AND id=@actor AND is_active FOR SHARE", connection, native))
        {
            command.CommandTimeout = 15;
            command.Parameters.AddWithValue("org", org);
            command.Parameters.AddWithValue("actor", actor);
            if (await command.ExecuteScalarAsync(ct) is not Guid) throw Missing();
        }
        var existing = await db.CitedRetrievalRuns.AsNoTracking().Include(r => r.Sources)
            .SingleOrDefaultAsync(r => r.OrganizationId == org && r.ActorId == actor && r.RequestKey == key, ct);
        if (existing is not null && (existing.PolicyVersion != CitedReviewedQuestionHandler.Policy ||
            existing.Question != question || !existing.Sources.Select(s => s.IndexId).Order().SequenceEqual(ids.Order())))
            throw new CitedWorkflowException("request_key_reused", "The request key belongs to different inputs.");
        await using (var command = new NpgsqlCommand(
            "SELECT id FROM cited_document_indexes WHERE organization_id=@org AND id=ANY(@ids) AND status='Ready' ORDER BY id FOR SHARE",
            connection, native))
        {
            command.CommandTimeout = 15;
            command.Parameters.AddWithValue("org", org);
            command.Parameters.AddWithValue("ids", ids.ToArray());
            await using var reader = await command.ExecuteReaderAsync(ct);
            var found = 0;
            while (await reader.ReadAsync(ct)) found++;
            if (found != ids.Count) throw Missing();
        }
        if (existing is not null)
        {
            if (existing.Status != CitedRetrievalStatus.Completed)
                throw new CitedWorkflowException("reviewed_answer_unavailable", "The saved reviewed answer is unavailable.");
            await transaction.CommitAsync(ct);
            return new(existing.Id, true);
        }
        var indexes = await db.CitedDocumentIndexes.AsNoTracking().Include(i => i.Chunks).ThenInclude(c => c.Spans)
            .Where(i => i.OrganizationId == org && ids.Contains(i.Id)).OrderBy(i => i.Id).ToArrayAsync(ct);
        if (indexes.Select(i => i.DocumentId).Distinct().Count() != ids.Count)
            throw new ArgumentException("Select one review revision per document.");
        var sources = new List<CitedTypedDocument>();
        foreach (var index in indexes)
            sources.Add(await CitedFactSourceStore.ProjectAsync(db, org, index, ct));
        var answer = CitedFactAnswerEngine.Answer(question, sources);
        var policy = JsonSerializer.Serialize(new {
            sourcePolicy = "reviewed-fields-from-selected-corpus-v1",
            answerPolicy = answer.PolicyVersion, questionPolicy = answer.QuestionPolicyVersion,
            sourceDigests = sources.Select(s => s.SourceSha256).ToArray(), missingParts = answer.MissingParts });
        var run = new CitedRetrievalRun(Guid.NewGuid(), org, actor, question, key,
            CitedReviewedQuestionHandler.Policy, policy, indexes, DateTimeOffset.UtcNow);
        var chunks = indexes.SelectMany(i => i.Chunks).ToDictionary(c => c.Id);
        var hits = new Dictionary<Guid, Guid>();
        foreach (var citation in answer.Claims.SelectMany(c => c.Citations).DistinctBy(c => c.ChunkId))
        {
            var chunk = chunks[citation.ChunkId];
            if (chunk.Kind != DocumentChunkKind.ReviewedFact || chunk.Content != citation.SupportText ||
                chunk.ContentSha256 != citation.ContentSha256)
                throw new CitedWorkflowException("typed_fact_source_invalid", "Reviewed facts are unavailable.");
            var hit = run.AddHit(Guid.NewGuid(), chunk, hits.Count + 1, null, null, null);
            hits.Add(chunk.Id, hit.Id);
        }
        var claims = answer.Claims.Select(c => new CitedClaimInput(c.Text,
            c.Citations.Select(x => new CitationInput(hits[x.ChunkId], null)).ToArray())).ToArray();
        run.Complete(Guid.NewGuid(), Enum.Parse<CitedAnswerStatus>(answer.Status), answer.ReasonCode,
            claims, hits.Keys.Select(id => chunks[id]).ToArray(), CitedReviewedQuestionHandler.Provider, "none",
            answer.PolicyVersion + "/" + answer.QuestionPolicyVersion, timer.ElapsedMilliseconds, 0, null, null, DateTimeOffset.UtcNow);
        db.CitedRetrievalRuns.Add(run);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(run.Id, false);
    }

    public async Task EnsureReadAccessAsync(Guid org, Guid actor, Guid runId, CancellationToken ct)
    {
        if (!await db.Actors.AsNoTracking().AnyAsync(a => a.OrganizationId == org && a.Id == actor && a.IsActive, ct))
            throw Missing();
        var run = await db.CitedRetrievalRuns.AsNoTracking().Include(r => r.Sources).SingleOrDefaultAsync(r => r.Id == runId &&
                r.OrganizationId == org && r.ActorId == actor && r.PolicyVersion == CitedReviewedQuestionHandler.Policy, ct);
        if (run is null) throw Missing();
        var ids = run.Sources.Select(s => s.IndexId).ToArray();
        if (await db.CitedDocumentIndexes.AsNoTracking().CountAsync(i => i.OrganizationId == org && ids.Contains(i.Id) &&
                i.Status == DocumentIndexStatus.Ready, ct) != ids.Length) throw Missing();
    }

    private static CitedWorkflowException Missing() => new("reviewed_source_unavailable", "The reviewed source or question is unavailable.", 404);
}
