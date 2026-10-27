using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed record ReconciliationRunView(Guid Id, int RunNumber, string Status,
    string RuleSetVersion, IReadOnlyList<ReconciliationFieldSourceView> Fields,
    IReadOnlyList<ReconciliationFindingView> Findings,
    IReadOnlyList<ReconciliationCheckView>? Checks = null,
    ReconciliationCoverageView? Coverage = null,
    IReadOnlyList<ReconciliationSourceStateView>? Sources = null,
    ReconciliationTolerancePolicy? Tolerances = null, string? OrderScope = null,
    Guid? EvaluationActorId = null, DateTimeOffset? EvaluatedAt = null);
