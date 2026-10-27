namespace UaeDocumentIntelligence.Application.Reconciliation;

/// <summary>Both download formats use this one captured version. CapturedAtUtc is informational;
/// CaseVersion and the run's saved resolution sequences define which decisions are included.</summary>
public sealed record ReconciliationReportView(int SchemaVersion, Guid SnapshotId, Guid CaseId,
    string Reference, string Name, Guid CaseVersion, DateTimeOffset CapturedAtUtc,
    ReconciliationRunView Run);
