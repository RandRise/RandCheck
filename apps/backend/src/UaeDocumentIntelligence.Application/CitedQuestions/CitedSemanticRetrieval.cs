namespace UaeDocumentIntelligence.Application.CitedQuestions;

/// <summary>Private, provider-free retrieval using an already settled query operation.</summary>
public interface ICitedSemanticRetrievalStore
{
    Task<Guid> FindQueryAsync(string question, IReadOnlyList<Guid> indexIds, string requestKey, CancellationToken ct);
    Task<RetrievalView> RetrieveAsync(Guid queryOperationId, string requestKey, string mode,
        int topK, LexicalPlan? lexicalPlan, CancellationToken ct);
}

public sealed class CitedSemanticQuestionHandler(ICitedSemanticRetrievalStore store, ICitedAiClient ai,
    CitedQuestionHandler questions)
{
    public async Task<CitedQuestionView> HandleAsync(string question, string key, IReadOnlyList<Guid> ids,
        string mode, int topK, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(question) || question.Length > 4000 || string.IsNullOrWhiteSpace(key) ||
            key.Length > 100 || key != key.Trim() || ids is null || ids.Count is < 1 or > 100 ||
            ids.Any(i => i == Guid.Empty) || ids.Distinct().Count() != ids.Count || topK is < 1 or > 20)
            throw new ArgumentException("Invalid semantic question.");
        _ = SemanticRetrievalPolicy.Version(mode);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        var ct = deadline.Token;
        question = question.Trim();
        var operation = await store.FindQueryAsync(question, ids, key, ct);
        var plan = mode == "vector" ? null : await ai.PlanAsync(question, SemanticRetrievalPolicy.CandidateLimit, ct);
        var result = await store.RetrieveAsync(operation, key, mode, topK, plan, ct);
        return await questions.CompleteRetrievedAsync(result, ct);
    }
}

public sealed record SemanticCandidate(Guid ChunkId, double? LexicalScore, double? VectorScore, double? FusedScore);
public sealed record SemanticEvidenceSpan(int PageNumber, int ByteStart, int ByteEnd, string Quote);
public sealed record SemanticEvidence(Guid ChunkId, Guid OrganizationId, Guid IndexId, string Kind,
    string Content, bool IsConfirmed, IReadOnlyList<SemanticEvidenceSpan> Spans);

public static class SemanticRetrievalPolicy
{
    public const string VectorVersion = "pgvector-exact-cosine-v1";
    public const string HybridVersion = "pgvector-english-rrf60-c20-v1";
    public const string NonredundantVersion = "pgvector-english-rrf60-c20-nonredundant-v1";
    public const int CandidateLimit = 20;
    public const int FusionConstant = 60;

    public static string Version(string mode) => mode switch
    {
        "vector" => VectorVersion,
        "hybrid" => HybridVersion,
        "hybrid-nonredundant" => NonredundantVersion,
        _ => throw new CitedWorkflowException("invalid_semantic_mode", "Choose vector or hybrid retrieval.", 400)
    };

    // Only suppress an unchanged, confirmed fact when a higher-ranked selected
    // page already contains its exact source bytes. Never discard a correction,
    // review-only fact, or evidence based on matching text in another document.
    public static IReadOnlyList<SemanticCandidate> SelectNonredundant(IReadOnlyList<SemanticCandidate> ranked,
        IReadOnlyDictionary<Guid, SemanticEvidence> evidence, int topK)
    {
        if (topK is < 1 or > CandidateLimit || ranked.Count > CandidateLimit ||
            ranked.Select(x => x.ChunkId).Distinct().Count() != ranked.Count ||
            ranked.Any(x => !evidence.TryGetValue(x.ChunkId, out var e) || e.ChunkId != x.ChunkId))
            throw new CitedWorkflowException("invalid_semantic_candidates", "Candidate evidence is invalid.");
        var selected = new List<SemanticCandidate>();
        foreach (var item in ranked)
        {
            var fact = evidence[item.ChunkId];
            var separator = fact.Content.IndexOf("]: ", StringComparison.Ordinal);
            var canCollapse = fact.Kind == "ReviewedFact" && fact.IsConfirmed && fact.Spans.Count == 1 &&
                separator > 0 && fact.Content[(separator + 3)..] == fact.Spans[0].Quote;
            if (canCollapse && selected.Any(x => Covers(evidence[x.ChunkId], fact))) continue;
            selected.Add(item);
            if (selected.Count == topK) break;
        }
        return selected;
    }

    private static bool Covers(SemanticEvidence page, SemanticEvidence fact)
    {
        if (page.Kind != "PageText" || page.Spans.Count != 1 || page.OrganizationId != fact.OrganizationId ||
            page.IndexId != fact.IndexId) return false;
        var parent = page.Spans[0];
        var child = fact.Spans[0];
        if (parent.PageNumber != child.PageNumber || parent.ByteStart > child.ByteStart ||
            parent.ByteEnd < child.ByteEnd || child.ByteEnd <= child.ByteStart) return false;
        var bytes = System.Text.Encoding.UTF8.GetBytes(parent.Quote);
        var childBytes = System.Text.Encoding.UTF8.GetBytes(child.Quote);
        return bytes.Length == parent.ByteEnd - parent.ByteStart && childBytes.Length == child.ByteEnd - child.ByteStart &&
            bytes.AsSpan(child.ByteStart - parent.ByteStart, childBytes.Length).SequenceEqual(childBytes);
    }

    // Equal-weight reciprocal rank fusion. Each input is already ordered by its
    // own score and canonical UUID ties; absent candidates contribute zero.
    public static IReadOnlyList<SemanticCandidate> Fuse(IReadOnlyList<(Guid Id, double Score)> lexical,
        IReadOnlyList<StoredEmbeddingHit> vector, int topK)
    {
        if (topK is < 1 or > CandidateLimit || lexical.Count > CandidateLimit || vector.Count > CandidateLimit ||
            lexical.Any(x => x.Id == Guid.Empty || !double.IsFinite(x.Score)) ||
            vector.Any(x => x.ChunkId == Guid.Empty || !double.IsFinite(x.Similarity)) ||
            lexical.Select(x => x.Id).Distinct().Count() != lexical.Count ||
            vector.Select(x => x.ChunkId).Distinct().Count() != vector.Count)
            throw new CitedWorkflowException("invalid_semantic_candidates", "Candidate ranks are invalid.");
        var left = lexical.Select((x, i) => (x.Id, x.Score, Rank: i + 1)).ToDictionary(x => x.Id);
        var right = vector.Select((x, i) => (Id: x.ChunkId, Score: x.Similarity, Rank: i + 1)).ToDictionary(x => x.Id);
        return left.Keys.Union(right.Keys).Select(id =>
        {
            var l = left.GetValueOrDefault(id);
            var r = right.GetValueOrDefault(id);
            return new SemanticCandidate(id, l.Rank == 0 ? null : l.Score, r.Rank == 0 ? null : r.Score,
                (l.Rank == 0 ? 0 : 1d / (FusionConstant + l.Rank)) +
                (r.Rank == 0 ? 0 : 1d / (FusionConstant + r.Rank)));
        }).OrderByDescending(x => x.FusedScore)
          .ThenBy(x => x.ChunkId.ToString(), StringComparer.Ordinal).Take(topK).ToArray();
    }
}
