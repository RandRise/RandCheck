using UaeDocumentIntelligence.Domain.Common;

namespace UaeDocumentIntelligence.Domain.Model;

public sealed class ReconciliationRun
{
    private readonly List<ReconciliationDocumentSnapshot> _documentSnapshots = [];
    private readonly List<ReconciliationFinding> _findings = [];
    private ReconciliationRun() { }

    internal ReconciliationRun(Guid id, Guid organizationId, Guid reconciliationCaseId,
        int runNumber, string ruleSetVersion, ReconciliationTolerancePolicy tolerancePolicy,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Run ID is required.", nameof(id));
        Guard.Positive(runNumber, nameof(runNumber));
        ArgumentNullException.ThrowIfNull(tolerancePolicy);
        tolerancePolicy.Validate();
        Id = id;
        OrganizationId = organizationId;
        ReconciliationCaseId = reconciliationCaseId;
        RunNumber = runNumber;
        RuleSetVersion = Guard.Required(ruleSetVersion, nameof(ruleSetVersion), 50);
        QuantityTolerance = tolerancePolicy.Quantity;
        UnitPriceTolerance = tolerancePolicy.UnitPrice;
        TaxTolerance = tolerancePolicy.Tax;
        LineTotalTolerance = tolerancePolicy.LineTotal;
        DocumentTotalTolerance = tolerancePolicy.DocumentTotal;
        Status = ReconciliationRunStatus.Running;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ReconciliationCaseId { get; private set; }
    public int RunNumber { get; private set; }
    public string RuleSetVersion { get; private set; } = string.Empty;
    public ReconciliationRunStatus Status { get; private set; }
    public decimal QuantityTolerance { get; private set; }
    public decimal UnitPriceTolerance { get; private set; }
    public decimal TaxTolerance { get; private set; }
    public decimal LineTotalTolerance { get; private set; }
    public decimal DocumentTotalTolerance { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public IReadOnlyCollection<ReconciliationDocumentSnapshot> DocumentSnapshots => _documentSnapshots.AsReadOnly();
    public IReadOnlyCollection<ReconciliationFinding> Findings => _findings.AsReadOnly();

    internal ReconciliationDocumentSnapshot AddDocumentSnapshot(Guid id,
        ReconciliationDocumentLink link, DateTimeOffset now)
    {
        RequireRunning();
        if (_documentSnapshots.Any(snapshot => snapshot.Role == link.Role))
            throw new InvalidOperationException("Document role snapshot already exists.");
        var snapshot = new ReconciliationDocumentSnapshot(id, OrganizationId, Id,
            link.Role, link.DocumentId, link.ReviewSessionId, link.ExtractionRunId,
            link.Sha256, now);
        _documentSnapshots.Add(snapshot);
        return snapshot;
    }

    public ReconciliationFieldSnapshot AddFieldSnapshot(Guid id, Guid documentSnapshotId,
        string fieldKey, int occurrenceIndex, FieldValueType valueType, string valueText,
        Guid? machineFieldValueId, Guid? reviewActionId, int? pageNumber,
        string? quotedText, DateTimeOffset now)
    {
        RequireRunning();
        var document = _documentSnapshots.SingleOrDefault(item => item.Id == documentSnapshotId)
            ?? throw new InvalidOperationException("Document snapshot does not belong to this run.");
        return document.AddField(id, fieldKey, occurrenceIndex, valueType, valueText,
            machineFieldValueId, reviewActionId, pageNumber, quotedText, now);
    }

    public ReconciliationFinding AddFinding(Guid id, string ruleCode, string ruleVersion,
        ReconciliationFindingSeverity severity, ReconciliationDocumentRole? leftRole,
        ReconciliationDocumentRole? rightRole, Guid? leftFieldSnapshotId,
        Guid? rightFieldSnapshotId, string? fieldKey, int? occurrenceIndex,
        string? leftValue, string? rightValue, decimal? difference,
        decimal? tolerance, string explanation, DateTimeOffset now)
    {
        RequireRunning();
        if (leftFieldSnapshotId.HasValue && !ContainsField(leftFieldSnapshotId.Value))
            throw new InvalidOperationException("Left field snapshot does not belong to this run.");
        if (rightFieldSnapshotId.HasValue && !ContainsField(rightFieldSnapshotId.Value))
            throw new InvalidOperationException("Right field snapshot does not belong to this run.");
        var finding = new ReconciliationFinding(id, OrganizationId, Id, ruleCode,
            ruleVersion, severity, leftRole, rightRole, leftFieldSnapshotId,
            rightFieldSnapshotId, fieldKey, occurrenceIndex, leftValue, rightValue,
            difference, tolerance, explanation, now);
        _findings.Add(finding);
        return finding;
    }

    public FindingResolution AddResolution(Guid findingId, Guid resolutionId, Guid actorId,
        FindingResolutionAction action, string reason, DateTimeOffset now)
    {
        if (Status != ReconciliationRunStatus.Completed)
            throw new InvalidOperationException("Only completed-run findings can be resolved.");
        var finding = _findings.SingleOrDefault(item => item.Id == findingId)
            ?? throw new InvalidOperationException("Finding does not belong to this run.");
        return finding.AddResolution(resolutionId, actorId, action, reason, now);
    }

    internal void Complete(DateTimeOffset now)
    {
        RequireRunning();
        Status = ReconciliationRunStatus.Completed;
        CompletedAtUtc = now;
    }

    private bool ContainsField(Guid id) => _documentSnapshots.Any(document =>
        document.Fields.Any(field => field.Id == id));

    private void RequireRunning()
    {
        if (Status != ReconciliationRunStatus.Running)
            throw new InvalidOperationException("Reconciliation run is immutable after completion.");
    }
}

public sealed class ReconciliationDocumentSnapshot
{
    private readonly List<ReconciliationFieldSnapshot> _fields = [];
    private ReconciliationDocumentSnapshot() { }

    internal ReconciliationDocumentSnapshot(Guid id, Guid organizationId,
        Guid reconciliationRunId, ReconciliationDocumentRole role, Guid documentId,
        Guid reviewSessionId, Guid extractionRunId, string sha256, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Snapshot ID is required.", nameof(id));
        Id = id;
        OrganizationId = organizationId;
        ReconciliationRunId = reconciliationRunId;
        Role = role;
        DocumentId = documentId;
        ReviewSessionId = reviewSessionId;
        ExtractionRunId = extractionRunId;
        Sha256 = sha256;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ReconciliationRunId { get; private set; }
    public ReconciliationDocumentRole Role { get; private set; }
    public Guid DocumentId { get; private set; }
    public Guid ReviewSessionId { get; private set; }
    public Guid ExtractionRunId { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public IReadOnlyCollection<ReconciliationFieldSnapshot> Fields => _fields.AsReadOnly();

    internal ReconciliationFieldSnapshot AddField(Guid id, string fieldKey,
        int occurrenceIndex, FieldValueType valueType, string valueText,
        Guid? machineFieldValueId, Guid? reviewActionId, int? pageNumber,
        string? quotedText, DateTimeOffset now)
    {
        if (_fields.Any(field => field.FieldKey == fieldKey &&
            field.OccurrenceIndex == occurrenceIndex))
            throw new InvalidOperationException("Field snapshot identity already exists.");
        var field = new ReconciliationFieldSnapshot(id, OrganizationId,
            ReconciliationRunId, Id, fieldKey, occurrenceIndex, valueType, valueText,
            machineFieldValueId, reviewActionId, pageNumber, quotedText, now);
        _fields.Add(field);
        return field;
    }
}

public sealed class ReconciliationFieldSnapshot
{
    private ReconciliationFieldSnapshot() { }

    internal ReconciliationFieldSnapshot(Guid id, Guid organizationId,
        Guid reconciliationRunId, Guid documentSnapshotId, string fieldKey,
        int occurrenceIndex, FieldValueType valueType, string valueText,
        Guid? machineFieldValueId, Guid? reviewActionId, int? pageNumber,
        string? quotedText, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Field snapshot ID is required.", nameof(id));
        if (occurrenceIndex < 0) throw new ArgumentOutOfRangeException(nameof(occurrenceIndex));
        if (!machineFieldValueId.HasValue && !reviewActionId.HasValue)
            throw new ArgumentException("A reviewed field snapshot requires a machine field or review action source.");
        if (pageNumber <= 0) throw new ArgumentOutOfRangeException(nameof(pageNumber));
        if (quotedText is not null && !pageNumber.HasValue)
            throw new ArgumentException("Quoted evidence requires a page number.", nameof(quotedText));
        Id = id;
        OrganizationId = organizationId;
        ReconciliationRunId = reconciliationRunId;
        DocumentSnapshotId = documentSnapshotId;
        FieldKey = Guard.Required(fieldKey, nameof(fieldKey), 100);
        OccurrenceIndex = occurrenceIndex;
        ValueType = valueType;
        ValueText = Guard.Required(valueText, nameof(valueText), 4000);
        MachineFieldValueId = machineFieldValueId;
        ReviewActionId = reviewActionId;
        PageNumber = pageNumber;
        QuotedText = quotedText;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ReconciliationRunId { get; private set; }
    public Guid DocumentSnapshotId { get; private set; }
    public string FieldKey { get; private set; } = string.Empty;
    public int OccurrenceIndex { get; private set; }
    public FieldValueType ValueType { get; private set; }
    public string ValueText { get; private set; } = string.Empty;
    public Guid? MachineFieldValueId { get; private set; }
    public Guid? ReviewActionId { get; private set; }
    public int? PageNumber { get; private set; }
    public string? QuotedText { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
