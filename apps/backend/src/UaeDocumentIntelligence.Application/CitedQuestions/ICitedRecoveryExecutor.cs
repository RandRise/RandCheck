using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.CitedQuestions;

// No caller-supplied cost, organization, operator, permission or assessment capability.
public sealed record ExecuteCitedRecovery(Guid RequestId, Guid ReservationId, CitedSpendState ExpectedState,
    int ExpectedEventSequence, CitedRecoveryAction Action, string? EvidenceReference);
public sealed record CitedRecoveryResult(Guid AuditId, Guid ReservationId, Guid OperatorActorId,
    CitedSpendState State, int EventSequence, decimal? ActualUsd, bool Replayed);

public interface ICitedRecoveryExecutionAuthorization
{
    Task<bool> CanExecuteAsync(Guid organizationId, Guid operatorActorId, CancellationToken ct);
}
public sealed class DenyCitedRecoveryExecutionAuthorization : ICitedRecoveryExecutionAuthorization
{
    public Task<bool> CanExecuteAsync(Guid organizationId, Guid operatorActorId, CancellationToken ct) => Task.FromResult(false);
}
public interface ICitedRecoveryExecutor
{
    // Internal only; the stable request ID must be preserved after an uncertain commit.
    Task<CitedRecoveryResult> ExecuteAsync(ExecuteCitedRecovery command, CancellationToken ct);
}
