namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed record ReconciliationFieldSourceView(Guid Id, string Key, int Occurrence,
    string ValueType, string Value, Guid? MachineFieldValueId, Guid? ReviewActionId,
    int? Page, string? Quote, string? SourceKind = null, Guid? DocumentSnapshotId = null, string? Role = null);
