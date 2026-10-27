using UaeDocumentIntelligence.Application.Abstractions;

namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed class ListReconciliationCasesHandler(IReconciliationRepository repository,
    IExecutionContext execution)
{
    public const int PageSize = 50;
    public const int MaximumOffset = 10000;

    public async Task<ReconciliationCaseListView> HandleAsync(int offset,
        CancellationToken cancellationToken, ReconciliationCaseQuery? query = null)
    {
        if (offset is < 0 or > MaximumOffset)
            throw new ReconciliationValidationException("invalid_case_offset",
                "Case offset must be between 0 and 10000.");
        var filter = (query ?? new ReconciliationCaseQuery()).Validate();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        var rows = await repository.ListAsync(execution.OrganizationId, offset, PageSize + 1,
            deadline.Token, filter);
        return new ReconciliationCaseListView(rows.Take(PageSize).ToList(),
            rows.Count > PageSize && offset + PageSize <= MaximumOffset ? offset + PageSize : null);
    }
}
