namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed record SetReconciliationDraftDocumentCommand(Guid? DocumentId, string RequestKey);
