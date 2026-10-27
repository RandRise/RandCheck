namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed record ReconciliationCaseView(Guid Id, string Name, string Status,
    Guid ConcurrencyToken, IReadOnlyList<ReconciliationDocumentLinkView> Documents,
    IReadOnlyList<ReconciliationRunView> Runs, string Reference = "", string Category = "Uncategorized",
    string WorkflowKind = "Legacy", IReadOnlyList<ReconciliationDraftDocumentView>? DraftDocuments = null);
