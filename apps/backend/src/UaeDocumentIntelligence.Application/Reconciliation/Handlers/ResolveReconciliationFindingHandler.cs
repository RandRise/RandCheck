using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed class ResolveReconciliationFindingHandler(IReconciliationRepository repository,
    IExecutionContext execution, IClock clock)
{
    public async Task<ReconciliationCaseView> HandleAsync(Guid caseId, Guid runId,
        Guid findingId, Guid concurrencyToken, ResolveFindingCommand command,
        CancellationToken cancellationToken)
    {
        var item = await repository.GetAsync(execution.OrganizationId, caseId, cancellationToken)
            ?? throw new ReconciliationNotFoundException();
        if (concurrencyToken == Guid.Empty || item.ConcurrencyToken != concurrencyToken)
            throw new ReconciliationPreconditionException("stale_reconciliation_version",
                "The reconciliation case changed. Reload it before retrying.");
        if (!Enum.TryParse<FindingResolutionAction>(command.Action, true, out var action) ||
            !Enum.IsDefined(action))
            throw new ReconciliationValidationException("invalid_resolution_action",
                "Resolution action must be Resolved, Accepted, or Dismissed.");
        if (item.Status == ReconciliationCaseStatus.Closed)
            throw new ReconciliationConflictException("case_closed", "A closed reconciliation case cannot change.");
        var latest = item.Runs.SingleOrDefault(run => run.Id == runId)?.Findings
            .SingleOrDefault(finding => finding.Id == findingId)?.Resolutions
            .OrderByDescending(resolution => resolution.SequenceNumber).FirstOrDefault();
        if (latest?.Action == action && latest.Reason == command.Reason?.Trim())
            return ReconciliationView.From(item);
        try
        {
            item.ResolveFinding(runId, findingId, Guid.NewGuid(), execution.ActorId,
                action, command.Reason ?? string.Empty, clock.UtcNow);
        }
        catch (InvalidOperationException)
        {
            throw new ReconciliationNotFoundException();
        }
        catch (ArgumentException exception)
        {
            throw new ReconciliationValidationException("invalid_resolution_reason",
                exception.Message);
        }
        var outcome = await repository.SaveChangesAsync(cancellationToken);
        if (outcome == ReconciliationSaveOutcome.ConcurrencyConflict)
            throw new ReconciliationPreconditionException("stale_reconciliation_version",
                "The reconciliation case changed. Reload it before retrying.");
        if (outcome == ReconciliationSaveOutcome.ConstraintConflict)
        {
            var current = await repository.GetAsync(execution.OrganizationId, caseId, cancellationToken);
            if (current is not null && current.ConcurrencyToken != concurrencyToken)
                throw new ReconciliationPreconditionException("stale_reconciliation_version",
                    "The reconciliation case changed. Reload it before retrying.");
            throw new ReconciliationConflictException("resolution_conflict",
                "The finding resolution could not be saved.");
        }
        return ReconciliationView.From(item);
    }
}
