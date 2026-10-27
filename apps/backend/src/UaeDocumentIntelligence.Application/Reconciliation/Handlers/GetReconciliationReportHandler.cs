using UaeDocumentIntelligence.Application.Abstractions;

namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed class GetReconciliationReportHandler(IReconciliationReportRepository repository,
    IExecutionContext execution, IClock clock)
{
    public async Task<ReconciliationReportView> HandleAsync(Guid caseId, Guid runId,
        Guid expectedVersion, CancellationToken cancellationToken)
    {
        var item = await repository.ReadAsync(execution.OrganizationId, caseId, runId, expectedVersion, cancellationToken)
            ?? throw new ReconciliationNotFoundException();
        if (item.ConcurrencyToken != expectedVersion)
            throw new ReconciliationPreconditionException("report_version_changed",
                "The saved results or decisions changed. Refresh results before downloading.");

        var run = ReconciliationView.From(item).Runs.SingleOrDefault(value => value.Id == runId)
            ?? throw new ReconciliationNotFoundException();
        return new(1, Guid.NewGuid(), item.Id, item.Reference, item.Name,
            item.ConcurrencyToken, clock.UtcNow, run);
    }
}
