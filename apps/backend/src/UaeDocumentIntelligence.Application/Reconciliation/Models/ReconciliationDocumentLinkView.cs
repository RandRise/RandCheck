namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed record ReconciliationDocumentLinkView(Guid Id, string Role, Guid DocumentId,
    Guid ReviewSessionId, Guid ExtractionRunId, string Sha256);
