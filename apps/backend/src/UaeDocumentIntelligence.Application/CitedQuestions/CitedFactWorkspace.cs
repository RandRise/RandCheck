using UaeDocumentIntelligence.Application.Abstractions;

namespace UaeDocumentIntelligence.Application.CitedQuestions;

public sealed record CitedFactCorpus(Guid RetrievalRunId, string Question, IReadOnlyList<CitedTypedDocument> Documents);
public interface ICitedFactSourceStore
{
    Task<CitedFactCorpus> LoadAsync(Guid organizationId, Guid actorId, Guid retrievalRunId, CancellationToken ct);
}
public sealed record CitedTypedAnswerView(Guid RetrievalRunId, IReadOnlyList<string> SourceDigests, CitedFactAnswer Answer)
{
    // Separate provider-free fact lookup, not a claim that the old ranked hits contained every operand.
    public string SourcePolicyVersion => "reviewed-fields-from-selected-corpus-v1";
}

/// <summary>Private read-only candidate. No model, spend reservation, publication or public route.</summary>
public sealed class CitedFactWorkspace(ICitedFactSourceStore sources, IExecutionContext execution)
{
    public async Task<CitedTypedAnswerView> AnswerAsync(Guid runId, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        var corpus = await sources.LoadAsync(execution.OrganizationId, execution.ActorId, runId, deadline.Token);
        return new(runId, corpus.Documents.Select(x => x.SourceSha256).ToArray(),
            CitedFactAnswerEngine.Answer(corpus.Question, corpus.Documents));
    }

    public async Task<CitedFactCitation> EvidenceAsync(Guid runId, Guid chunkId, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        var corpus = await sources.LoadAsync(execution.OrganizationId, execution.ActorId, runId, deadline.Token);
        return corpus.Documents.SelectMany(x => x.Facts).SingleOrDefault(x => x.Citation.ChunkId == chunkId)?.Citation
            ?? throw new CitedWorkflowException("typed_fact_not_found", "Reviewed evidence was not found.", 404);
    }
}
