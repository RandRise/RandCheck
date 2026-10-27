using UaeDocumentIntelligence.Application.Abstractions;

namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed class GetReconciliationCaseHandler(IReconciliationRepository repository,
    IExecutionContext execution)
{
    public async Task<ReconciliationCaseView> HandleAsync(Guid caseId,
        CancellationToken cancellationToken)
    {
        var item = await repository.GetAsync(execution.OrganizationId, caseId, cancellationToken)
            ?? throw new ReconciliationNotFoundException();
        return ReconciliationView.From(item);
    }
}
