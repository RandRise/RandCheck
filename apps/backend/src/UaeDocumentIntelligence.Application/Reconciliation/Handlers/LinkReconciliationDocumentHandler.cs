using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed class LinkReconciliationDocumentHandler(IReconciliationRepository repository,
    IDocumentRepository documents, IExecutionContext execution, IClock clock)
{
    public async Task<ReconciliationCaseView> HandleAsync(Guid caseId, Guid concurrencyToken,
        LinkReconciliationDocumentCommand command, CancellationToken cancellationToken)
    {
        var item = await repository.GetAsync(execution.OrganizationId, caseId, cancellationToken)
            ?? throw new ReconciliationNotFoundException();
        RequireVersion(item, concurrencyToken);
        if (item.Status == ReconciliationCaseStatus.Closed)
            throw new ReconciliationConflictException("case_closed", "A closed reconciliation case cannot change.");
        var role = item.WorkflowKind == ReconciliationWorkflowKind.InvoicePurchaseOrder
            ? SetReconciliationDraftDocumentHandler.ParseRole(command.Role) : ParseRole(command.Role);
        if (item.WorkflowKind == ReconciliationWorkflowKind.InvoicePurchaseOrder && item.GetDraftDocumentId(role) != command.DocumentId)
            throw new ReconciliationConflictException("draft_document_mismatch", "Select this document in the saved pair before choosing its completed review.");
        var document = await documents.GetForReviewAsync(execution.OrganizationId,
            command.DocumentId, cancellationToken) ?? throw new ReconciliationNotFoundException();
        var session = RequireCompletedReview(document, command.ReviewSessionId);
        var run = document.ExtractionRuns.Single(extraction => extraction.Id == session.ExtractionRunId);
        RequireDocumentType(run.DocumentType, role);

        if (item.DocumentLinks.Any(link => link.Role == role && link.DocumentId == document.Id &&
            link.ReviewSessionId == session.Id && link.ExtractionRunId == run.Id && link.Sha256 == document.Sha256))
            return ReconciliationView.From(item);

        if (item.DocumentLinks.Any(link => link.Role == role))
            item.ReplaceReviewedDocument(Guid.NewGuid(), role, document.Id, session.Id,
                run.Id, document.Sha256, clock.UtcNow);
        else
            item.LinkReviewedDocument(Guid.NewGuid(), role, document.Id, session.Id,
                run.Id, document.Sha256, clock.UtcNow);
        await SaveOrThrow(caseId, concurrencyToken, cancellationToken);
        return ReconciliationView.From(item);
    }

    private async Task SaveOrThrow(Guid caseId, Guid concurrencyToken, CancellationToken cancellationToken)
    {
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
            throw new ReconciliationConflictException("reconciliation_link_conflict",
                "The reviewed document could not be linked.");
        }
    }

    internal static ReconciliationDocumentRole ParseRole(string value) =>
        Enum.TryParse<ReconciliationDocumentRole>(value, true, out var role) && Enum.IsDefined(role)
            ? role
            : throw new ReconciliationValidationException("invalid_document_role",
                "Role must be Quotation, PurchaseOrder, or Invoice.");

    internal static ReviewSession RequireCompletedReview(Document document, Guid reviewSessionId)
    {
        var session = document.ReviewSessions.SingleOrDefault(review => review.Id == reviewSessionId)
            ?? throw new ReconciliationNotFoundException();
        if (session.Status != ReviewStatus.Completed)
            throw new ReconciliationValidationException("review_not_completed",
                "A completed review revision is required for reconciliation.");
        return session;
    }

    private static void RequireDocumentType(string? documentType,
        ReconciliationDocumentRole role)
    {
        var normalized = documentType?.Trim().Replace('-', '_').ToLowerInvariant();
        var valid = role switch
        {
            ReconciliationDocumentRole.Quotation => normalized is "quotation" or "quote",
            ReconciliationDocumentRole.PurchaseOrder => normalized is "purchase_order" or "po",
            ReconciliationDocumentRole.Invoice => normalized == "invoice",
            _ => false
        };
        if (!valid)
            throw new ReconciliationValidationException("document_role_mismatch",
                "The reviewed document type does not match the requested reconciliation role.");
    }

    private static void RequireVersion(ReconciliationCase item, Guid token)
    {
        if (token == Guid.Empty || item.ConcurrencyToken != token)
            throw new ReconciliationPreconditionException("stale_reconciliation_version",
                "The reconciliation case changed. Reload it before retrying.");
    }
}
