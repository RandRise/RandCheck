using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.CitedQuestions;

public sealed record NoEvidenceAnswerView(Guid Id, string Status, string ReasonCode, string Message,
    string PolicyVersion);
public sealed record CitedQuestionView(string Stage, bool AnswerGenerationEnabled,
    RetrievalView Retrieval, NoEvidenceAnswerView? Answer, string Question, IReadOnlyList<Guid> IndexIds,
    ReviewedAnswerView? ReviewedAnswer = null);

/// <summary>Provider-free question entry point. Nonempty retrieval is evidence, not an answer.</summary>
public sealed class CitedQuestionHandler(RetrieveEvidenceHandler retrieval, ICitedQuestionRepository repository,
    IExecutionContext execution, IClock clock, ICitedReviewedQuestionStore? reviewed = null)
{
    public const string NoEvidencePolicy = "no-hits-refusal-v1";

    public async Task<CitedQuestionView> HandleAsync(string question, string key, IReadOnlyList<Guid> indexIds,
        int topK, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        var evidence = await retrieval.HandleAsync(question, key, indexIds, topK, deadline.Token);
        return await CompleteRetrievedAsync(evidence, deadline.Token);
    }

    public async Task<CitedQuestionView> CompleteRetrievedAsync(RetrievalView evidence, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        var run = await LoadAsync(evidence.Id, deadline.Token);
        if (run.Status == CitedRetrievalStatus.Retrieved && run.Hits.Count == 0)
        {
            run.Complete(Guid.NewGuid(), CitedAnswerStatus.InsufficientEvidence, "no_relevant_evidence",
                [], [], "evidence-only", "none", NoEvidencePolicy, run.RetrievalDurationMs!.Value,
                0, null, null, clock.UtcNow);
            if (await repository.SaveChangesAsync(deadline.Token) != CitedQuestionSaveOutcome.Saved)
            {
                run = await LoadAsync(run.Id, deadline.Token);
                if (!IsNoEvidenceAnswer(run))
                    throw new CitedWorkflowException("answer_conflict", "The question changed while saving its result; retry.");
            }
            evidence = await retrieval.GetAsync(run.Id, deadline.Token) with { Replayed = evidence.Replayed };
        }
        return View(run, evidence);
    }

    public async Task<CitedQuestionView> GetAsync(Guid id, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        // GET is read-only: it never completes an interrupted POST.
        var evidence = await retrieval.GetAsync(id, deadline.Token);
        var run = await LoadAsync(id, deadline.Token);
        if (run.PolicyVersion == CitedReviewedQuestionHandler.Policy)
        {
            if (reviewed is null) throw new CitedWorkflowException("reviewed_answer_unavailable", "Reviewed answers are unavailable.");
            await reviewed.EnsureReadAccessAsync(execution.OrganizationId, execution.ActorId, id, deadline.Token);
        }
        return View(run, evidence);
    }

    private async Task<CitedRetrievalRun> LoadAsync(Guid id, CancellationToken ct)
    {
        var run = await repository.GetRunAsync(execution.OrganizationId, id, ct);
        if (run is null || run.ActorId != execution.ActorId)
            throw new CitedWorkflowException("retrieval_not_found", "Retrieval was not found.", 404);
        return run;
    }

    private static bool IsNoEvidenceAnswer(CitedRetrievalRun run) =>
        run.Status == CitedRetrievalStatus.Completed && run.Hits.Count == 0 &&
        run.Answer is { Status: CitedAnswerStatus.InsufficientEvidence, ReasonCode: "no_relevant_evidence",
            ProviderAlias: "evidence-only", ModelVersion: "none", PromptVersion: NoEvidencePolicy } &&
        run.Answer.Claims.Count == 0;

    private static CitedQuestionView View(CitedRetrievalRun run, RetrievalView evidence)
    {
        if (run.PolicyVersion == CitedReviewedQuestionHandler.Policy && run.Status == CitedRetrievalStatus.Completed &&
            run.Answer is { ProviderAlias: CitedReviewedQuestionHandler.Provider, ModelVersion: "none" } answer)
        {
            using var policy = System.Text.Json.JsonDocument.Parse(run.PolicyJson);
            var missing = policy.RootElement.GetProperty("missingParts").EnumerateArray().Select(x => x.GetString()!).ToArray();
            return new("ReviewedAnswer", false, evidence, null, run.Question, run.Sources.Select(x => x.IndexId).ToArray(),
                new(answer.Id, answer.Status.ToString(), answer.ReasonCode, answer.PromptVersion,
                    answer.Claims.OrderBy(c => c.Ordinal).Select(c => new ReviewedClaimView(c.Text,
                        c.Citations.OrderBy(x => x.HitId).Select(x => new ReviewedCitationView(x.HitId, x.SpanId)).ToArray())).ToArray(), missing));
        }
        if (IsNoEvidenceAnswer(run))
            return new("InsufficientEvidence", false, evidence, new(run.Answer!.Id,
                "InsufficientEvidence", "no_relevant_evidence",
                "The selected retrieval policy found no matching evidence in the selected documents.", NoEvidencePolicy),
                run.Question, run.Sources.Select(x => x.IndexId).ToArray());
        if (run.Status == CitedRetrievalStatus.Completed)
            throw new CitedWorkflowException("answer_mode_unavailable", "This endpoint currently supports evidence-only questions.");
        return new(run.Status == CitedRetrievalStatus.Retrieved ? "EvidenceReady" : run.Status.ToString(),
            false, evidence, null, run.Question, run.Sources.Select(x => x.IndexId).ToArray());
    }
}
