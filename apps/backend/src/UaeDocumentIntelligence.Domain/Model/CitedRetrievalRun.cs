using System.Text.Json;
using UaeDocumentIntelligence.Domain.Common;

namespace UaeDocumentIntelligence.Domain.Model;

public enum CitedRetrievalStatus { Running, Completed, Failed, Retrieved }
public enum CitedAnswerStatus { Answered, PartiallyAnswered, InsufficientEvidence }

public sealed record CitationInput(Guid HitId, Guid? SpanId);
public sealed record CitedClaimInput(string Text, IReadOnlyList<CitationInput> Citations);

/// <summary>One question against an immutable, explicitly selected set of document indexes.</summary>
public sealed class CitedRetrievalRun
{
    private readonly List<RetrievalSource> _sources = [];
    private readonly List<RetrievalHit> _hits = [];
    private CitedRetrievalRun() { }

    public CitedRetrievalRun(Guid id, Guid organizationId, Guid actorId, string question,
        string requestKey, string policyVersion, string policyJson,
        IReadOnlyCollection<CitedDocumentIndex> indexes, DateTimeOffset now)
    {
        CitedGuard.Id(id);
        CitedGuard.Id(organizationId);
        CitedGuard.Id(actorId);
        if (indexes.Count is < 1 or > 100 || indexes.Select(x => x.DocumentId).Distinct().Count() != indexes.Count)
            throw new ArgumentException("Select one index per document, from 1 to 100 documents.");
        if (indexes.Any(x => x.OrganizationId != organizationId || x.Status != DocumentIndexStatus.Ready))
            throw new ArgumentException("All selected indexes must be ready and belong to this organization.");
        using var policy = JsonDocument.Parse(policyJson);
        if (policy.RootElement.ValueKind != JsonValueKind.Object || policyJson.Length > 8192)
            throw new ArgumentException("Retrieval policy must be a bounded JSON object.");
        Id = id;
        OrganizationId = organizationId;
        ActorId = actorId;
        Question = Guard.Required(question, nameof(question), 4000);
        RequestKey = Guard.Required(requestKey, nameof(requestKey), 100);
        PolicyVersion = Guard.Required(policyVersion, nameof(policyVersion), 100);
        PolicyJson = policyJson;
        CreatedAtUtc = now;
        ConcurrencyToken = Guid.NewGuid();
        foreach (var index in indexes.OrderBy(x => x.Id))
            _sources.Add(new RetrievalSource(Guid.NewGuid(), this, index));
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ActorId { get; private set; }
    public string Question { get; private set; } = string.Empty;
    public string RequestKey { get; private set; } = string.Empty;
    public string PolicyVersion { get; private set; } = string.Empty;
    public string PolicyJson { get; private set; } = string.Empty;
    public CitedRetrievalStatus Status { get; private set; }
    public Guid ConcurrencyToken { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public long? RetrievalDurationMs { get; private set; }
    public string? FailureCode { get; private set; }
    public IReadOnlyCollection<RetrievalSource> Sources => _sources.AsReadOnly();
    public IReadOnlyCollection<RetrievalHit> Hits => _hits.AsReadOnly();
    public CitedAnswer? Answer { get; private set; }

    public RetrievalHit AddHit(Guid id, DocumentChunk chunk, int rank, double? lexicalScore,
        double? vectorScore, double? fusedScore)
    {
        RequireRunning();
        CitedGuard.Id(id);
        if (chunk.OrganizationId != OrganizationId || !_sources.Any(x => x.IndexId == chunk.IndexId))
            throw new ArgumentException("Retrieved chunk is outside the frozen corpus.");
        if (rank is < 1 or > 100 || _hits.Any(x => x.Id == id || x.Rank == rank || x.ChunkId == chunk.Id))
            throw new ArgumentException("Hit ID, chunk, and bounded rank must be unique.");
        foreach (var score in new[] { lexicalScore, vectorScore, fusedScore })
            if (score.HasValue && !double.IsFinite(score.Value))
                throw new ArgumentException("Retrieval scores must be finite.");
        var hit = new RetrievalHit(id, this, chunk, rank, lexicalScore, vectorScore, fusedScore);
        _hits.Add(hit);
        ConcurrencyToken = Guid.NewGuid();
        return hit;
    }

    public void Complete(Guid answerId, CitedAnswerStatus status, string? reasonCode,
        IReadOnlyList<CitedClaimInput> claims, IReadOnlyCollection<DocumentChunk> retrievedChunks,
        string providerAlias, string modelVersion, string promptVersion,
        long retrievalDurationMs, long generationDurationMs, decimal? estimatedCost,
        string? costCurrency, DateTimeOffset now)
    {
        RequireAnswerable();
        if (now < CreatedAtUtc || retrievalDurationMs < 0 ||
            (Status == CitedRetrievalStatus.Retrieved && RetrievalDurationMs != retrievalDurationMs))
            throw new ArgumentException("Completion time and retrieval duration are invalid.");
        // Construct and validate the whole answer before mutating the run.
        var answer = new CitedAnswer(answerId, this, status, reasonCode, claims, retrievedChunks,
            providerAlias, modelVersion, promptVersion, generationDurationMs, estimatedCost, costCurrency);
        Answer = answer;
        RetrievalDurationMs = retrievalDurationMs;
        Status = CitedRetrievalStatus.Completed;
        CompletedAtUtc = now;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void Fail(string code, DateTimeOffset now)
    {
        RequireAnswerable();
        if (now < CreatedAtUtc) throw new ArgumentException("Completion precedes request.");
        FailureCode = Guard.Required(code, nameof(code), 100);
        Status = CitedRetrievalStatus.Failed;
        CompletedAtUtc = now;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void MarkRetrieved(long durationMs)
    {
        RequireRunning();
        if (durationMs < 0) throw new ArgumentOutOfRangeException(nameof(durationMs));
        RetrievalDurationMs = durationMs;
        Status = CitedRetrievalStatus.Retrieved;
        ConcurrencyToken = Guid.NewGuid();
    }

    private void RequireAnswerable()
    {
        if (Status is not (CitedRetrievalStatus.Running or CitedRetrievalStatus.Retrieved))
            throw new InvalidOperationException("Terminal answer history is immutable.");
    }

    private void RequireRunning()
    {
        if (Status != CitedRetrievalStatus.Running)
            throw new InvalidOperationException("Terminal retrieval runs are immutable.");
    }
}

public sealed class RetrievalSource
{
    private RetrievalSource() { }
    internal RetrievalSource(Guid id, CitedRetrievalRun run, CitedDocumentIndex index)
    {
        Id = id;
        OrganizationId = run.OrganizationId;
        RetrievalRunId = run.Id;
        IndexId = index.Id;
    }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid RetrievalRunId { get; private set; }
    public Guid IndexId { get; private set; }
}

public sealed class RetrievalHit
{
    private RetrievalHit() { }
    internal RetrievalHit(Guid id, CitedRetrievalRun run, DocumentChunk chunk, int rank,
        double? lexicalScore, double? vectorScore, double? fusedScore)
    {
        Id = id;
        OrganizationId = run.OrganizationId;
        RetrievalRunId = run.Id;
        IndexId = chunk.IndexId;
        ChunkId = chunk.Id;
        Rank = rank;
        LexicalScore = lexicalScore;
        VectorScore = vectorScore;
        FusedScore = fusedScore;
    }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid RetrievalRunId { get; private set; }
    public Guid IndexId { get; private set; }
    public Guid ChunkId { get; private set; }
    public int Rank { get; private set; }
    public double? LexicalScore { get; private set; }
    public double? VectorScore { get; private set; }
    public double? FusedScore { get; private set; }
}

public sealed class CitedAnswer
{
    private static readonly HashSet<string> Reasons = ["no_relevant_evidence", "incomplete_evidence",
        "conflicting_evidence", "unsupported_scope", "unsafe_source"];
    private readonly List<AnswerClaim> _claims = [];
    private CitedAnswer() { }
    internal CitedAnswer(Guid id, CitedRetrievalRun run, CitedAnswerStatus status, string? reasonCode,
        IReadOnlyList<CitedClaimInput> claims, IReadOnlyCollection<DocumentChunk> chunks,
        string providerAlias, string modelVersion, string promptVersion,
        long generationDurationMs, decimal? estimatedCost, string? costCurrency)
    {
        CitedGuard.Id(id);
        if (!Enum.IsDefined(status) || claims.Count > 100 ||
            (status == CitedAnswerStatus.InsufficientEvidence ? claims.Count != 0 : claims.Count == 0))
            throw new ArgumentException("Answered results require claims; refusals must not have claims.");
        if (status == CitedAnswerStatus.Answered ? reasonCode != null :
            reasonCode is null || !Reasons.Contains(reasonCode))
            throw new ArgumentException("Answer reason does not match its status.");
        if (generationDurationMs < 0 || estimatedCost < 0 ||
            (estimatedCost.HasValue != (costCurrency is not null)) ||
            (costCurrency is not null && (costCurrency.Length != 3 ||
                costCurrency.Any(c => c is < 'A' or > 'Z'))))
            throw new ArgumentException("Generation metrics and cost currency are invalid.");
        Id = id;
        OrganizationId = run.OrganizationId;
        RetrievalRunId = run.Id;
        Status = status;
        ReasonCode = reasonCode;
        ProviderAlias = Guard.Required(providerAlias, nameof(providerAlias), 100);
        ModelVersion = Guard.Required(modelVersion, nameof(modelVersion), 100);
        PromptVersion = Guard.Required(promptVersion, nameof(promptVersion), 100);
        GenerationDurationMs = generationDurationMs;
        EstimatedCost = estimatedCost;
        CostCurrency = costCurrency;
        for (var ordinal = 0; ordinal < claims.Count; ordinal++)
        {
            var input = claims[ordinal];
            if (input.Citations.Count is < 1 or > 100 ||
                input.Citations.Distinct().Count() != input.Citations.Count)
                throw new ArgumentException("Each claim requires unique citations.");
            var claim = new AnswerClaim(Guid.NewGuid(), this, ordinal, input.Text);
            foreach (var citation in input.Citations)
            {
                var hit = run.Hits.SingleOrDefault(x => x.Id == citation.HitId)
                    ?? throw new ArgumentException("Citation must reference a hit in this retrieval run.");
                var chunk = chunks.SingleOrDefault(x => x.Id == hit.ChunkId &&
                    x.OrganizationId == run.OrganizationId && x.IndexId == hit.IndexId)
                    ?? throw new ArgumentException("Citation chunk does not match the retrieved source.");
                if (citation.SpanId.HasValue ? !chunk.Spans.Any(x => x.Id == citation.SpanId) :
                    chunk.Kind != DocumentChunkKind.ReviewedFact)
                    throw new ArgumentException("Citation must use a verified span or review provenance.");
                claim.AddCitation(new AnswerCitation(Guid.NewGuid(), claim, hit, citation.SpanId));
            }
            _claims.Add(claim);
        }
    }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid RetrievalRunId { get; private set; }
    public CitedAnswerStatus Status { get; private set; }
    public string? ReasonCode { get; private set; }
    public string ProviderAlias { get; private set; } = string.Empty;
    public string ModelVersion { get; private set; } = string.Empty;
    public string PromptVersion { get; private set; } = string.Empty;
    public long GenerationDurationMs { get; private set; }
    public decimal? EstimatedCost { get; private set; }
    public string? CostCurrency { get; private set; }
    public IReadOnlyCollection<AnswerClaim> Claims => _claims.AsReadOnly();
}

public sealed class AnswerClaim
{
    private readonly List<AnswerCitation> _citations = [];
    private AnswerClaim() { }
    internal AnswerClaim(Guid id, CitedAnswer answer, int ordinal, string text)
    {
        Id = id;
        OrganizationId = answer.OrganizationId;
        AnswerId = answer.Id;
        RetrievalRunId = answer.RetrievalRunId;
        Ordinal = ordinal;
        Text = Guard.Required(text, nameof(text), 4000);
    }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid AnswerId { get; private set; }
    public Guid RetrievalRunId { get; private set; }
    public int Ordinal { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public IReadOnlyCollection<AnswerCitation> Citations => _citations.AsReadOnly();
    internal void AddCitation(AnswerCitation citation) => _citations.Add(citation);
}

public sealed class AnswerCitation
{
    private AnswerCitation() { }
    internal AnswerCitation(Guid id, AnswerClaim claim, RetrievalHit hit, Guid? spanId)
    {
        Id = id;
        OrganizationId = claim.OrganizationId;
        ClaimId = claim.Id;
        RetrievalRunId = claim.RetrievalRunId;
        HitId = hit.Id;
        ChunkId = hit.ChunkId;
        SpanId = spanId;
    }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ClaimId { get; private set; }
    public Guid RetrievalRunId { get; private set; }
    public Guid HitId { get; private set; }
    public Guid ChunkId { get; private set; }
    public Guid? SpanId { get; private set; }
}
