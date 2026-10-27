namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed record LinkReconciliationDocumentCommand(string Role, Guid DocumentId,
    Guid ReviewSessionId);
