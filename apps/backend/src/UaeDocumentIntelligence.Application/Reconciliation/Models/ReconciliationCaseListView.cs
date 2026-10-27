using UaeDocumentIntelligence.Application.Abstractions;

namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed record ReconciliationCaseListView(IReadOnlyList<ReconciliationCaseSummary> Cases,
    int? NextOffset);
