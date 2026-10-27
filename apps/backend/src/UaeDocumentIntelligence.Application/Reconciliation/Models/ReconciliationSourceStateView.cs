namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed record ReconciliationSourceStateView(Guid Id, string Role, Guid DocumentId,
    Guid ReviewSessionId, Guid ExtractionRunId, string Sha256, string? Outcome, string? Provider,
    string? AdapterVersion, string? ModelName, string? ModelVersion, string? WarningsJson,
    int? ExtractedRowCount, string? RowCoverageStatus);
