namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed record ReconciliationFindingView(Guid Id, string RuleCode, string RuleVersion,
    string Severity, string? LeftRole, string? RightRole, string? FieldKey,
    int? Occurrence, Guid? LeftFieldSnapshotId, Guid? RightFieldSnapshotId,
    string? LeftValue, string? RightValue, decimal? Difference,
    decimal? Tolerance, string Explanation, IReadOnlyList<FindingResolutionView> Resolutions);
