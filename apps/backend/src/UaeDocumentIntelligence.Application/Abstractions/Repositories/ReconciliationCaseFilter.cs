using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Abstractions;

public sealed record ReconciliationCaseFilter(string? Search = null,
    ReconciliationCaseCategory? Category = null, ReconciliationCaseStatus? Status = null,
    DateTimeOffset? CreatedFrom = null, DateTimeOffset? CreatedTo = null,
    ReconciliationWorkflowKind? WorkflowKind = null);
