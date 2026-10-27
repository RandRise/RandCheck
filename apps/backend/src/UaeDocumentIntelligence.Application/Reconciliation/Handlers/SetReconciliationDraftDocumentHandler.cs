using System.Security.Cryptography;
using System.Text.Json;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed class SetReconciliationDraftDocumentHandler(IReconciliationRepository repository,
    IReconciliationDraftRequestStore requests, IDocumentRepository documents, IExecutionContext execution, IClock clock)
{
    public async Task<ReconciliationCaseView> HandleAsync(Guid caseId, string role, Guid expectedVersion,
        SetReconciliationDraftDocumentCommand command, CancellationToken cancellationToken)
    {
        var parsedRole = ParseRole(role);
        if (expectedVersion == Guid.Empty)
            throw new ReconciliationPreconditionException("stale_reconciliation_version", "Reload the saved pair before changing its documents.");
        if (string.IsNullOrWhiteSpace(command.RequestKey) || command.RequestKey.Length > 100 || command.RequestKey != command.RequestKey.Trim())
            throw new ReconciliationValidationException("invalid_request_key", "Request key must contain 1 to 100 characters without surrounding whitespace.");
        if (command.DocumentId == Guid.Empty)
            throw new ReconciliationValidationException("invalid_document_id", "Document ID must be a UUID or null to clear the selection.");
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            caseId,
            role = parsedRole.ToString(),
            command.DocumentId,
            expectedVersion
        }))).ToLowerInvariant();
        var item = await LoadEligibleAsync(caseId, parsedRole, command.DocumentId, cancellationToken);
        var receipt = await requests.FindAsync(execution.OrganizationId, execution.ActorId, command.RequestKey, cancellationToken);
        if (receipt is not null)
            return Replay(item, receipt, parsedRole, command.DocumentId, hash);
        if (item.ConcurrencyToken != expectedVersion)
            throw Stale();
        try
        {
            item.SetDraftDocument(parsedRole, command.DocumentId, clock.UtcNow);
        }
        catch (InvalidOperationException exception)
        {
            throw new ReconciliationConflictException("draft_document_conflict", exception.Message);
        }
        item.RecordDraftIntent(clock.UtcNow);
        await requests.AddAsync(new ReconciliationDraftRequest(Guid.NewGuid(), execution.OrganizationId,
            execution.ActorId, caseId, parsedRole, command.DocumentId, command.RequestKey, hash,
            expectedVersion, item.GetDraftVersion(parsedRole), clock.UtcNow), cancellationToken);
        var result = await repository.SaveChangesAsync(cancellationToken);
        if (result == ReconciliationSaveOutcome.Saved)
            return ReconciliationView.From(item);
        // Unique intent/ETag races roll back both association edits and the receipt.
        receipt = await requests.FindAsync(execution.OrganizationId, execution.ActorId, command.RequestKey, cancellationToken);
        if (receipt is not null)
            return Replay(await LoadEligibleAsync(caseId, parsedRole, command.DocumentId, cancellationToken), receipt, parsedRole, command.DocumentId, hash);
        if (result == ReconciliationSaveOutcome.ConcurrencyConflict ||
            (await repository.GetAsync(execution.OrganizationId, caseId, cancellationToken))?.ConcurrencyToken != expectedVersion)
            throw Stale();
        throw new ReconciliationConflictException("draft_document_conflict", "The document selection could not be saved. Reload the saved pair.");
    }

    public static ReconciliationDocumentRole ParseRole(string role) => role switch
    {
        "Invoice" => ReconciliationDocumentRole.Invoice,
        "PurchaseOrder" => ReconciliationDocumentRole.PurchaseOrder,
        _ => throw new ReconciliationValidationException("invalid_document_role", "Role must be Invoice or PurchaseOrder.")
    };

    private async Task<ReconciliationCase> LoadEligibleAsync(Guid caseId, ReconciliationDocumentRole role,
        Guid? documentId, CancellationToken ct)
    {
        var item = await repository.GetAsync(execution.OrganizationId, caseId, ct) ?? throw new ReconciliationNotFoundException();
        if (item.WorkflowKind != ReconciliationWorkflowKind.InvoicePurchaseOrder)
            throw new ReconciliationConflictException("draft_requires_pair_workflow", "Draft documents require the invoice and PO workflow.");
        if (item.Status == ReconciliationCaseStatus.Closed)
            throw new ReconciliationConflictException("case_closed", "A closed saved pair cannot change.");
        if (documentId is { } id && await documents.GetAsync(execution.OrganizationId, id, ct) is null)
            throw new ReconciliationNotFoundException();
        var other = item.GetDraftDocumentId(role == ReconciliationDocumentRole.Invoice ? ReconciliationDocumentRole.PurchaseOrder : ReconciliationDocumentRole.Invoice);
        if (documentId is not null && documentId == other)
            throw new ReconciliationConflictException("draft_document_conflict", "Invoice and PO must be different documents.");
        return item;
    }

    private static ReconciliationCaseView Replay(ReconciliationCase item, ReconciliationDraftRequest receipt,
        ReconciliationDocumentRole role, Guid? documentId, string hash)
    {
        if (receipt.PayloadHash != hash)
            throw new ReconciliationConflictException("request_key_reused", "The request key belongs to a different document selection.");
        if (receipt.ReconciliationCaseId != item.Id || receipt.Role != role || receipt.DocumentId != documentId ||
            item.GetDraftDocumentId(role) != documentId || item.GetDraftVersion(role) != receipt.DraftVersion)
            throw new ReconciliationConflictException("draft_intent_superseded", "A later selection replaced this request. Reload the saved pair.");
        return ReconciliationView.From(item);
    }

    private static ReconciliationPreconditionException Stale() => new("stale_reconciliation_version", "The saved pair changed. Reload it before retrying.");
}
