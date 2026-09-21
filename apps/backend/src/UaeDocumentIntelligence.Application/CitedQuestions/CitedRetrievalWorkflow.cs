using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.CitedQuestions;

public sealed class CitedWorkflowException(string code, string message, int status = 409) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

public sealed record LexicalPlan(string SchemaVersion, string PolicyVersion, string QuestionSha256,
    string Configuration, string SearchText, int TopK)
{
    public const string CurrentVersion = "lexical-or-english-v1";
    public void Validate(string question, int topK)
    {
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(question))).ToLowerInvariant();
        if (SchemaVersion != "1.0" || PolicyVersion != CurrentVersion || Configuration != "english" ||
            QuestionSha256 != digest || TopK != topK || TopK is < 1 or > 20 || SearchText is null || SearchText.Length > 8192)
            throw new CitedWorkflowException("invalid_search_plan", "The private search plan is invalid.", 502);
    }
}

public interface ICitedAiClient
{
    Task<ChunkingResult> ChunkAsync(ChunkingSnapshot snapshot, CancellationToken cancellationToken);
    Task<LexicalPlan> PlanAsync(string question, int topK, CancellationToken cancellationToken);
}

public interface ICitedRetrievalStore
{
    Task<Document?> GetSourceAsync(Guid organizationId, Guid documentId, CancellationToken cancellationToken);
    Task<CitedDocumentIndex?> GetIndexHeaderAsync(Guid organizationId, Guid indexId, CancellationToken cancellationToken);
    Task<CitedDocumentIndex?> FindIndexAsync(Guid organizationId, Guid reviewId, ChunkingProfile profile, CancellationToken cancellationToken);
    Task<CitedRetrievalRun?> FindRequestAsync(Guid organizationId, Guid actorId, string key, CancellationToken cancellationToken);
    Task<IReadOnlyList<DocumentChunk>> GetHitChunksAsync(CitedRetrievalRun run, CancellationToken cancellationToken);
    Task ExecuteLexicalAsync(CitedRetrievalRun run, LexicalPlan plan, CancellationToken cancellationToken);
    Task FailAsync(Guid organizationId, Guid runId, string code, CancellationToken cancellationToken);
}

public sealed record IndexView(Guid Id, Guid DocumentId, Guid ReviewSessionId, string ProfileVersion,
    string Status, int ChunkCount, bool Replayed);
public sealed record EvidenceHit(Guid HitId, Guid IndexId, Guid ChunkId, Guid DocumentId, string Filename,
    Guid ReviewSessionId, Guid ExtractionRunId, int Rank, double? Score,
    string Kind, string Content, Guid? ReviewActionId, IReadOnlyList<SpanView> Spans);
public sealed record SpanView(Guid Id, int PageNumber, int ByteStart, int ByteEnd, string Quote,
    string? Polygon, string? CoordinateUnit);
public sealed record RetrievalView(Guid Id, string Status, string PolicyVersion, bool Replayed,
    long? RetrievalDurationMs, string? FailureCode, IReadOnlyList<EvidenceHit> Hits);

public sealed class IndexReviewedDocumentHandler(ICitedRetrievalStore store, ICitedQuestionRepository repository,
    ICitedAiClient ai, IExecutionContext execution, IClock clock)
{
    public async Task<IndexView> HandleAsync(Guid documentId, Guid reviewId, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        var ct = deadline.Token;
        var document = await store.GetSourceAsync(execution.OrganizationId, documentId, ct)
            ?? throw new CitedWorkflowException("source_not_found", "Source was not found.", 404);
        var review = document.ReviewSessions.SingleOrDefault(x => x.Id == reviewId)
            ?? throw new CitedWorkflowException("source_not_found", "Source was not found.", 404);
        if (review.Status != ReviewStatus.Completed)
            throw new CitedWorkflowException("review_incomplete", "Complete the review before indexing.");
        var profile = new ChunkingProfile();
        var previous = await store.FindIndexAsync(execution.OrganizationId, reviewId, profile, ct);
        if (previous is not null) return View(previous, true);
        var extraction = document.ExtractionRuns.Single(x => x.Id == review.ExtractionRunId);
        var id = Guid.NewGuid();
        var snapshot = CitedIndexAssembly.CreateSnapshot(id, document, review, extraction, profile);
        var suspicious = CitedSourceSafety.Suspicious(new[] { document.OriginalFilename }
            .Concat(snapshot.Pages.OrderBy(p => p.PageNumber).Select(p => p.CanonicalText))
            .Concat(snapshot.Facts.Select(f => f.Value ?? "")));
        var proposal = await ai.ChunkAsync(snapshot, ct);
        CitedDocumentIndex index;
        try { index = CitedIndexAssembly.Assemble(id, document, review, extraction, profile, proposal, clock.UtcNow); }
        catch (ArgumentException) { throw new CitedWorkflowException("invalid_chunks", "The chunking result was rejected.", 502); }
        if (suspicious) index.Retire(true);
        await repository.AddIndexAsync(index, ct);
        if (await repository.SaveChangesAsync(ct) != CitedQuestionSaveOutcome.Saved)
        {
            var winner = await store.FindIndexAsync(execution.OrganizationId, reviewId, profile, ct);
            if (winner is not null) return View(winner, true);
            throw new CitedWorkflowException("index_conflict", "The index could not be saved; retry the request.");
        }
        if (suspicious) throw new CitedWorkflowException("unsafe_source", "The source index was quarantined for review.");
        return View(index, false);
    }

    private static IndexView View(CitedDocumentIndex index, bool replayed)
    {
        if (index.Status != DocumentIndexStatus.Ready)
            throw new CitedWorkflowException("index_ineligible", "The existing index is not eligible for retrieval.");
        return new(index.Id, index.DocumentId, index.ReviewSessionId, index.ProfileVersion,
            index.Status.ToString(), index.Chunks.Count, replayed);
    }
}

public sealed class RetrieveEvidenceHandler(ICitedRetrievalStore store, ICitedQuestionRepository repository,
    ICitedAiClient ai, IExecutionContext execution, IClock clock, ICitedReviewedQuestionStore? reviewed = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<RetrievalView> HandleAsync(string question, string key, IReadOnlyList<Guid> indexIds,
        int topK, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(question) || question.Length > 4000 || string.IsNullOrWhiteSpace(key) ||
            key.Length > 100 || key != key.Trim() || indexIds is null || indexIds.Count is < 1 or > 100 ||
            indexIds.Any(x => x == Guid.Empty) || indexIds.Distinct().Count() != indexIds.Count || topK is < 1 or > 20)
            throw new ArgumentException("Supply a bounded question, request key, unique corpus and hit limit.");
        question = question.Trim();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        var ct = deadline.Token;
        var existing = await store.FindRequestAsync(execution.OrganizationId, execution.ActorId, key, ct);
        if (existing is not null) return await ReplayAsync(existing, question, indexIds, topK, ct);
        var indexes = new List<CitedDocumentIndex>();
        foreach (var id in indexIds)
        {
            var index = await store.GetIndexHeaderAsync(execution.OrganizationId, id, ct)
                ?? throw new CitedWorkflowException("index_not_found", "A selected index was not found.", 404);
            if (index.Status != DocumentIndexStatus.Ready)
                throw new CitedWorkflowException("index_ineligible", "A selected index is no longer eligible.");
            indexes.Add(index);
        }
        var plan = await ai.PlanAsync(question, topK, ct);
        plan.Validate(question, topK);
        var run = new CitedRetrievalRun(Guid.NewGuid(), execution.OrganizationId, execution.ActorId, question,
            key, plan.PolicyVersion, JsonSerializer.Serialize(plan, Json), indexes, clock.UtcNow);
        await repository.AddRunAsync(run, ct);
        if (await repository.SaveChangesAsync(ct) != CitedQuestionSaveOutcome.Saved)
        {
            existing = await store.FindRequestAsync(execution.OrganizationId, execution.ActorId, key, ct);
            if (existing is not null) return await ReplayAsync(existing, question, indexIds, topK, ct);
            throw new CitedWorkflowException("retrieval_conflict", "Retrieval could not start.");
        }
        try { await store.ExecuteLexicalAsync(run, plan, ct); }
        catch
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try { await store.FailAsync(execution.OrganizationId, run.Id, "retrieval_interrupted", cleanup.Token); }
            catch { /* Original error wins. An interrupted Running record is never replayed as success. */ }
            throw;
        }
        return await ViewAsync(run, false, ct);
    }

    public async Task<RetrievalView> GetAsync(Guid id, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        ct = deadline.Token;
        var run = await repository.GetRunAsync(execution.OrganizationId, id, ct);
        if (run is null || run.ActorId != execution.ActorId)
            throw new CitedWorkflowException("retrieval_not_found", "Retrieval was not found.", 404);
        if (run.PolicyVersion == CitedReviewedQuestionHandler.Policy)
        {
            if (reviewed is null) throw new CitedWorkflowException("reviewed_answer_unavailable", "Reviewed answers are unavailable.");
            await reviewed.EnsureReadAccessAsync(execution.OrganizationId, execution.ActorId, id, ct);
        }
        return await ViewAsync(run, true, ct);
    }

    private async Task<RetrievalView> ReplayAsync(CitedRetrievalRun run, string question,
        IReadOnlyList<Guid> ids, int topK, CancellationToken ct)
    {
        var plan = JsonSerializer.Deserialize<LexicalPlan>(run.PolicyJson, Json);
        if (run.Question != question || run.PolicyVersion != LexicalPlan.CurrentVersion || plan?.TopK != topK ||
            !run.Sources.Select(x => x.IndexId).Order().SequenceEqual(ids.Order()))
            throw new CitedWorkflowException("request_key_reused", "The request key belongs to different inputs.");
        if (run.Status == CitedRetrievalStatus.Running)
            throw new CitedWorkflowException("retrieval_in_progress", "The request is running or was interrupted; inspect its status or use a new key.");
        return await ViewAsync(run, true, ct);
    }

    private async Task<RetrievalView> ViewAsync(CitedRetrievalRun run, bool replayed, CancellationToken ct)
    {
        var headers = new Dictionary<Guid, CitedDocumentIndex>();
        // Preserve the audit snapshot, but never treat saved results as an
        // exemption from current source eligibility (including zero-hit runs).
        foreach (var id in run.Sources.Select(x => x.IndexId).Distinct())
        {
            var header = await store.GetIndexHeaderAsync(run.OrganizationId, id, ct);
            if (header is null || header.Status != DocumentIndexStatus.Ready)
                throw new CitedWorkflowException("evidence_unavailable", "A saved source is no longer eligible for retrieval history.");
            headers[id] = header;
        }
        var chunks = (await store.GetHitChunksAsync(run, ct)).ToDictionary(x => x.Id);
        var hits = run.Hits.OrderBy(x => x.Rank).Select(hit =>
        {
            var chunk = chunks[hit.ChunkId];
            var header = headers[hit.IndexId];
            return new EvidenceHit(hit.Id, hit.IndexId, chunk.Id, header.DocumentId, header.Filename,
                chunk.ReviewSessionId, chunk.ExtractionRunId, hit.Rank, hit.FusedScore ?? hit.VectorScore ?? hit.LexicalScore,
                chunk.Kind.ToString(), chunk.Content, chunk.ReviewActionId,
                chunk.Spans.OrderBy(x => x.PageNumber).ThenBy(x => x.ByteStart).Select(x =>
                    new SpanView(x.Id, x.PageNumber, x.ByteStart, x.ByteEnd, x.Quote,
                        x.BoundingPolygon, x.CoordinateUnit)).ToArray());
        }).ToArray();
        return new(run.Id, run.Status.ToString(), run.PolicyVersion, replayed, run.RetrievalDurationMs,
            run.FailureCode, hits);
    }
}
