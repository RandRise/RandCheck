namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed record ReconciliationCheckView(Guid Id, string Code, string FieldKey, int? Occurrence,
    string? LineIdentity, string Status, string ReasonCode, string Explanation,
    Guid? LeftFieldSnapshotId, Guid? RightFieldSnapshotId, string? LeftValue, string? RightValue,
    decimal? SignedDifference, decimal? Tolerance, string? Unit, string? Currency, string? Basis, Guid? FindingId);
