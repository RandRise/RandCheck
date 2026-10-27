using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed class UpdateReconciliationCaseMetadataHandler(IReconciliationRepository repository,
    IExecutionContext execution, IClock clock)
{
    public async Task<ReconciliationCaseView> HandleAsync(Guid caseId, Guid expectedVersion,
        string name, string? category, CancellationToken cancellationToken)
    {
        var item = await repository.GetAsync(execution.OrganizationId, caseId, cancellationToken)
            ?? throw new ReconciliationNotFoundException();
        if (expectedVersion == Guid.Empty || item.ConcurrencyToken != expectedVersion)
            throw new ReconciliationPreconditionException("stale_reconciliation_version", "The reconciliation case changed. Reload it before retrying.");
        if (item.Status == ReconciliationCaseStatus.Closed)
            throw new ReconciliationConflictException("reconciliation_case_closed", "A closed reconciliation case cannot change.");
        if (category is null)
            throw new ReconciliationValidationException("invalid_case_category", "Category is required when updating a case.");
        var parsedCategory = CaseMetadata.Category(category);
        try
        {
            item.UpdateMetadata(name, parsedCategory, clock.UtcNow);
        }
        catch (ArgumentException exception)
        {
            throw new ReconciliationValidationException("invalid_case_name", exception.Message);
        }
        var outcome = await repository.SaveChangesAsync(cancellationToken);
        if (outcome == ReconciliationSaveOutcome.ConcurrencyConflict)
            throw new ReconciliationPreconditionException("stale_reconciliation_version", "The reconciliation case changed. Reload it before retrying.");
        if (outcome != ReconciliationSaveOutcome.Saved)
            throw new ReconciliationConflictException("reconciliation_metadata_conflict", "The case metadata could not be updated.");
        return ReconciliationView.From(item);
    }
}
