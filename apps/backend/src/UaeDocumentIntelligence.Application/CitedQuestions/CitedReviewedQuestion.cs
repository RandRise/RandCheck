using UaeDocumentIntelligence.Application.Abstractions;

namespace UaeDocumentIntelligence.Application.CitedQuestions;

public sealed record ReviewedQuestionSaved(Guid Id, bool Replayed);
public interface ICitedReviewedQuestionStore
{
    Task<ReviewedQuestionSaved> CreateAsync(Guid organizationId, Guid actorId, string question,
        string requestKey, IReadOnlyList<Guid> indexIds, CancellationToken ct);
    Task EnsureReadAccessAsync(Guid organizationId, Guid actorId, Guid runId, CancellationToken ct);
}
public sealed record ReviewedClaimView(string Text, IReadOnlyList<ReviewedCitationView> Citations);
public sealed record ReviewedCitationView(Guid HitId, Guid? SpanId);
public sealed record ReviewedAnswerView(Guid Id, string Status, string? ReasonCode, string PolicyVersion,
    IReadOnlyList<ReviewedClaimView> Claims, IReadOnlyList<string> MissingParts);

public sealed class CitedReviewedQuestionHandler(ICitedReviewedQuestionStore store,
    CitedQuestionHandler history, IExecutionContext execution)
{
    public const string Policy = "reviewed-facts-corpus-v1";
    public const string Provider = "reviewed-facts";
    public async Task<CitedQuestionView> HandleAsync(string question, string key, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(question) || question.Length > 4000 ||
            string.IsNullOrWhiteSpace(key) || key.Length > 100 || key != key.Trim() ||
            ids is null || ids.Count is < 1 or > 10 || ids.Any(i => i == Guid.Empty) || ids.Distinct().Count() != ids.Count)
            throw new ArgumentException("Supply a bounded question, request key and one to ten unique sources.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        var saved = await store.CreateAsync(execution.OrganizationId, execution.ActorId, question.Trim(), key, ids, deadline.Token);
        var view = await history.GetAsync(saved.Id, deadline.Token);
        return view with { Retrieval = view.Retrieval with { Replayed = saved.Replayed } };
    }
}
