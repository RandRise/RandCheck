using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.CitedQuestions;

// Internal configuration only. No public request can supply enablement or prices.
public sealed record CitedGenerationSettings(bool Enabled = false, CitedGenerationRate? Rate = null,
    bool OpenAiSyntheticPilot = false, bool LowReasoningPilot = false, bool SourceAwarePilot = false);
public sealed record PrivateAnswerModel(string ProviderAlias, string Model, string Version);
public sealed record PrivateAnswerPreparation
{
    public required string SchemaVersion { get; init; }
    public required string RequestSha256 { get; init; }
    public required PrivateAnswerModel Model { get; init; }
    public required string PromptSha256 { get; init; }
    public required string PromptVersion { get; init; }
    public required string CounterVersion { get; init; }
    public required int InputTokens { get; init; }
    public required int MaximumOutputTokens { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenAiPreparation? OpenAi { get; init; }
}
public sealed record PrivateAnswerResult
{
    public required string SchemaVersion { get; init; }
    public required string RequestSha256 { get; init; }
    public required string PromptSha256 { get; init; }
    public required PrivateAnswerModel Model { get; init; }
    public required string Outcome { get; init; }
    public required byte[] ProposalUtf8 { get; init; }
    public required int InputTokens { get; init; }
    public required int OutputTokens { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenAiOutcome? OpenAi { get; init; }
    public override string ToString() => "Private answer result (content omitted)";
}
public interface ICitedAnswerClient
{
    Task<PrivateAnswerPreparation> PrepareAsync(ReadOnlyMemory<byte> requestUtf8, CancellationToken ct);
    // requestUtf8 is the bounded dispatch envelope containing source bytes and preparation receipt.
    Task<PrivateAnswerResult> ProposeAsync(ReadOnlyMemory<byte> requestUtf8, CancellationToken ct);
}
public sealed record UnpublishedCitedProposal(Guid ReservationId, CitedProposalValidation Validation, bool Replayed = false)
{
    public bool PublicationAllowed => false;
    public string PaymentPolicyVersion => CitedPaymentClaimPolicy.Version;
    public string ScopePolicyVersion => CitedScopeClaimPolicy.Version;
}

/// <summary>Internal orchestration; live access requires an explicit synthetic pilot profile. No publication.</summary>
public sealed class CitedGenerationCoordinator(ICitedQuestionRepository repository, ICitedRetrievalStore store,
    ICitedGenerationLedger ledger, ICitedAnswerClient client, IExecutionContext execution,
    CitedGenerationSettings settings, ICitedGenerationJournal journal)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record SavedDispatch(byte[] RequestUtf8, PrivateAnswerPreparation Preparation);
    private static readonly byte[] GraphCheck = JsonSerializer.SerializeToUtf8Bytes(new
    {
        schemaVersion = "1.0", status = "InsufficientEvidence", reasonCode = "incomplete_evidence",
        claims = Array.Empty<object>(), unansweredParts = Array.Empty<string>()
    });

    public async Task<UnpublishedCitedProposal> ProposeAsync(Guid retrievalRunId, CancellationToken ct)
    {
        if (!settings.Enabled) throw Failure("generation_disabled");
        var rate = settings.Rate;
        var live = settings.OpenAiSyntheticPilot && rate == CitedOpenAiPilot.Rate;
        if (rate is null || rate.Provider != "test-only" && !live) throw Failure("generation_live_not_ready");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(live ? 60 : 45));
        var token = deadline.Token;
        await ledger.ValidateCallerAsync(token); // Before even private prompt measurement receives evidence.
        var (run, chunks) = await LoadEligible(retrievalRunId, token);
        var byId = chunks.ToDictionary(x => x.Id);
        CitedSourceSafety.RequireSafe(chunks.OrderBy(c => c.IndexId).ThenBy(c => c.Ordinal).Select(c => c.Content));
        var model = new PrivateAnswerModel(rate.Provider, rate.Model, rate.ModelVersion);
        var evidence = new List<object>();
        foreach (var hit in run.Hits.OrderBy(x => x.Rank))
        {
            var chunk = byId[hit.ChunkId];
            if (!settings.SourceAwarePilot)
            {
                // Preserve the historical source bytes and replay bindings exactly.
                evidence.Add(new { run.OrganizationId, retrievalRunId, hitId = hit.Id,
                    spanIds = chunk.Spans.Select(x => x.Id).Order().ToArray(), chunk.Content, chunk.ContentSha256 });
                continue;
            }
            var index = await store.GetIndexHeaderAsync(run.OrganizationId, hit.IndexId, token);
            if (index is null || index.Status != DocumentIndexStatus.Ready || index.OrganizationId != run.OrganizationId ||
                index.Id != chunk.IndexId || index.ReviewSessionId != chunk.ReviewSessionId ||
                index.ExtractionRunId != chunk.ExtractionRunId) throw Failure("evidence_unavailable");
            evidence.Add(new { run.OrganizationId, retrievalRunId, hitId = hit.Id,
                spanIds = chunk.Spans.Select(x => x.Id).Order().ToArray(), chunk.Content, chunk.ContentSha256,
                source = new { indexId = index.Id, index.DocumentId, index.ReviewSessionId, index.ExtractionRunId,
                    index.Filename, chunkKind = chunk.Kind.ToString(), chunk.ReviewActionId,
                    spans = chunk.Spans.OrderBy(x => x.Id).Select(x => new { spanId = x.Id, x.PageNumber,
                        x.ByteStart, x.ByteEnd, x.Quote }).ToArray() } });
        }
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = settings.SourceAwarePilot ? "1.1" : "1.0",
            context = new { execution.OrganizationId, execution.ActorId, retrievalRunId }, model,
            question = run.Question,
            evidence = evidence.ToArray(),
            maximumOutputTokens = CitedGenerationBudgetPolicy.MaximumOutputTokens
        }, Json);
        if (bytes.Length > 65536) throw Failure("answer_request_too_large");
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var saved = await journal.GetSavedAsync(run.Id, token);
        if (saved is not null && saved.Spend.Workload != CitedSpendWorkload.Generation)
            throw Failure("reservation_key_conflict");
        PrivateAnswerPreparation preparation;
        try
        {
            if (saved is not null)
            {
                if (saved.PreparationUtf8 is null) throw Failure("generation_already_dispatched");
                var dispatch = JsonSerializer.Deserialize<SavedDispatch>(saved.PreparationUtf8, Json)
                    ?? throw Failure("generation_saved_result_unavailable");
                if (dispatch.RequestUtf8 is null || !dispatch.RequestUtf8.AsSpan().SequenceEqual(bytes))
                    throw Failure("generation_saved_source_changed");
                preparation = dispatch.Preparation;
            }
            else preparation = await client.PrepareAsync(bytes, token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (CitedWorkflowException) when (saved is not null) { throw; }
        catch { throw Failure("prompt_preparation_failed"); }
        if (preparation is null || preparation.SchemaVersion != "1.0" || preparation.RequestSha256 != hash ||
            preparation.Model != model ||
            (preparation.PromptVersion != (settings.SourceAwarePilot ? "cited-answer-prompt-v4" : "cited-answer-prompt-v3") &&
                !(!settings.SourceAwarePilot && saved?.Spend.State == CitedSpendState.Settled &&
                    preparation.PromptVersion is "cited-answer-prompt-v1" or "cited-answer-prompt-v2")) ||
            (live ? !CitedOpenAiPilot.ValidPreparation(preparation, settings.LowReasoningPilot) :
                preparation.CounterVersion != "test-utf8-bytes-v1" || preparation.OpenAi is not null) || !Sha256(preparation.PromptSha256) ||
            preparation.InputTokens is < 1 or > CitedGenerationBudgetPolicy.MaximumInputTokens ||
            preparation.MaximumOutputTokens != CitedGenerationBudgetPolicy.MaximumOutputTokens)
            throw Failure("invalid_prompt_preparation");
        if (saved is not null && saved.Spend.State != CitedSpendState.Reserved)
        {
            if (saved.Spend.State != CitedSpendState.Settled || saved.OutcomeUtf8 is null)
                throw Failure("generation_already_dispatched");
            try
            {
                var prior = JsonSerializer.Deserialize<PrivateAnswerResult>(saved.OutcomeUtf8, Json);
                CheckResult(prior, model, hash, preparation);
                if (prior!.Outcome != "completed") throw Failure("answer_proposal_not_accepted");
                (run, chunks) = await LoadEligible(retrievalRunId, token);
                return new(saved.Spend.Id, ValidateProposal(prior!.ProposalUtf8, run, chunks), true);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { throw Failure("answer_proposal_not_accepted"); }
        }
        // Scope/eligibility can change while awaiting preflight. Recheck before admission.
        _ = await LoadEligible(retrievalRunId, token);
        var dispatchBytes = JsonSerializer.SerializeToUtf8Bytes(new { requestUtf8 = bytes, preparation }, Json);
        if (dispatchBytes.Length > 98304) throw Failure("answer_request_too_large");
        var reservationHash = Convert.ToHexString(SHA256.HashData(dispatchBytes)).ToLowerInvariant();
        // Stable one-attempt-per-retrieval identity; client retries cannot mint fresh operation IDs.
        var reservation = await journal.ReservePreparedAsync(new(run.Id, reservationHash, rate,
            preparation.InputTokens, CitedGenerationBudgetPolicy.MaximumOutputTokens), dispatchBytes, true, token);
        if (!await ledger.TryMarkDispatchedAsync(reservation.Id, true, token))
            throw Failure("generation_already_dispatched");
        var settled = false;
        try
        {
            var result = await client.ProposeAsync(dispatchBytes, token);
            CheckResult(result, model, hash, preparation);
            var accounting = live ? CitedOpenAiPilot.Inspect(result, preparation) :
                ((decimal?)(decimal.Ceiling(result.InputTokens * rate.MaximumInputUsdPerMillion +
                    result.OutputTokens * rate.MaximumOutputUsdPerMillion) / 1_000_000m),
                    (string?)("test-usage:" + reservationHash), false);
            // Save known usage even when the proposal is refused, incomplete or invalid.
            await journal.RecordOutcomeAsync(reservation.Id, JsonSerializer.SerializeToUtf8Bytes(result, Json),
                accounting.Item1, accounting.Item2, accounting.Item3, token);
            if (accounting.Item1 is null) throw Failure("generation_outcome_uncertain");
            settled = true;
            if (result.Outcome != "completed") throw Failure("answer_proposal_not_accepted");
            (run, chunks) = await LoadEligible(retrievalRunId, token);
            var validation = ValidateProposal(result.ProposalUtf8, run, chunks);
            return new(reservation.Id, validation); // Never calls run.Complete or publishes claims.
        }
        catch (Exception error)
        {
            if (!settled)
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try { await ledger.MarkUncertainAsync(reservation.Id, cleanup.Token); }
                catch { /* A committed Dispatched/Settled state remains safe; never release or retry. */ }
            }
            if (error is OperationCanceledException && ct.IsCancellationRequested) throw;
            // Do not propagate downstream exception messages, parser input, or arbitrary error codes.
            throw Failure(settled ? "answer_proposal_not_accepted" : "generation_outcome_uncertain");
        }
    }

    private CitedProposalValidation ValidateProposal(byte[] bytes, CitedRetrievalRun run,
        IReadOnlyList<DocumentChunk> chunks)
    {
        var validation = CitedAnswerValidator.Validate(bytes, run, chunks, settings.SourceAwarePilot);
        CitedPaymentClaimPolicy.Validate(run.Question, validation);
        CitedScopeClaimPolicy.Validate(run.Question, validation);
        return validation;
    }

    private async Task<(CitedRetrievalRun, IReadOnlyList<DocumentChunk>)> LoadEligible(Guid id, CancellationToken ct)
    {
        var run = await repository.GetRunAsync(execution.OrganizationId, id, ct);
        if (run is null || run.ActorId != execution.ActorId) throw Failure("retrieval_not_found", 404);
        if (run.Status != CitedRetrievalStatus.Retrieved || run.Hits.Count is < 1 or > 20)
            throw Failure("generation_evidence_not_ready");
        foreach (var source in run.Sources)
        {
            var header = await store.GetIndexHeaderAsync(execution.OrganizationId, source.IndexId, ct);
            if (header is null || header.Status != DocumentIndexStatus.Ready) throw Failure("evidence_unavailable");
        }
        var chunks = await store.GetHitChunksAsync(run, ct);
        // Reuse graph/provenance/hash validation without treating this local check as an answer.
        _ = CitedAnswerValidator.Validate(GraphCheck, run, chunks);
        return (run, chunks);
    }

    private static CitedWorkflowException Failure(string code, int status = 409) =>
        new(code, "The internal answer proposal could not be completed.", status);
    private static bool Sha256(string? value) => value is { Length: 64 } &&
        value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void CheckResult(PrivateAnswerResult? result, PrivateAnswerModel model,
        string hash, PrivateAnswerPreparation preparation)
    {
        if (model.ProviderAlias == "openai")
        {
            if (result is null || result.SchemaVersion != "1.0" || result.RequestSha256 != hash ||
                result.PromptSha256 != preparation.PromptSha256 || result.Model != model || result.ProposalUtf8 is null)
                throw Failure("invalid_answer_envelope");
            _ = CitedOpenAiPilot.Inspect(result, preparation);
            return;
        }
        if (result is null || result.SchemaVersion != "1.0" || result.RequestSha256 != hash ||
            result.PromptSha256 != preparation.PromptSha256 || result.Model != model || result.Outcome != "completed" ||
            result.ProposalUtf8 is null || result.ProposalUtf8.Length is < 1 or > 65536 ||
            result.InputTokens < 1 || result.InputTokens > preparation.InputTokens ||
            result.OutputTokens is < 1 or > CitedGenerationBudgetPolicy.MaximumOutputTokens || result.OpenAi is not null)
            throw Failure("invalid_answer_envelope");
    }
}
