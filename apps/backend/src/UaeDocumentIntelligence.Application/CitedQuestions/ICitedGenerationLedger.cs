using System.Text.Json.Serialization;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.CitedQuestions;

public sealed record ReserveCitedGeneration(Guid OperationId, string RequestSha256,
    CitedGenerationRate Rate, int MaximumInputTokens, int MaximumOutputTokens,
    // Preserve the exact serialized generation command and its historical fingerprint.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    CitedSpendWorkload Workload = CitedSpendWorkload.Generation);
public sealed record CitedSpendView(Guid Id, Guid OperationId, CitedSpendState State,
    decimal ReservedUsd, decimal? ActualUsd, bool Replayed,
    CitedSpendWorkload Workload = CitedSpendWorkload.Generation);

/// <summary>
/// Internal server accounting only, not a public billing/administration API.
/// Settlement must come from trusted verified provider usage or operator evidence,
/// never request-body cost values. Current implementation exposes no HTTP routes.
/// </summary>
public interface ICitedGenerationLedger
{
    Task ValidateCallerAsync(CancellationToken ct);
    Task<CitedSpendView> ReserveAsync(ReserveCitedGeneration command, bool enabled, CancellationToken ct);
    Task<CitedSpendView> GetAsync(Guid reservationId, CancellationToken ct);
    // Only the unique caller receiving true may dispatch; a replay returns false.
    // Commit precedes dispatch. A crash in between deliberately strands a hold.
    Task<bool> TryMarkDispatchedAsync(Guid reservationId, bool enabled, CancellationToken ct);
    Task MarkUncertainAsync(Guid reservationId, CancellationToken ct);
    Task CancelBeforeDispatchAsync(Guid reservationId, CancellationToken ct);
    Task SettleAsync(Guid reservationId, decimal actualUsd, string evidenceReference, CancellationToken ct);
}
