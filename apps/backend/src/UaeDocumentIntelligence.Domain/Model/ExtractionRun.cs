using UaeDocumentIntelligence.Domain.Common;

namespace UaeDocumentIntelligence.Domain.Model;

public sealed class ExtractionRun
{
    private readonly List<MachineFieldValue> _fields = [];
    private ExtractionRun() { }

    internal ExtractionRun(Guid id, Guid organizationId, Guid documentId, int runNumber,
        string providerName, string providerVersion, string schemaVersion, string correlationId,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Run ID is required.", nameof(id));
        Guard.Positive(runNumber, nameof(runNumber));
        Id = id;
        OrganizationId = organizationId;
        DocumentId = documentId;
        RunNumber = runNumber;
        ProviderName = Guard.Required(providerName, nameof(providerName), 100);
        ProviderVersion = Guard.Required(providerVersion, nameof(providerVersion), 100);
        ExtractionSchemaVersion = Guard.Required(schemaVersion, nameof(schemaVersion), 50);
        CorrelationId = Guard.Required(correlationId, nameof(correlationId), 100);
        Status = ExtractionRunStatus.Queued;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid DocumentId { get; private set; }
    public int RunNumber { get; private set; }
    public ExtractionRunStatus Status { get; private set; }
    public string? DocumentType { get; private set; }
    public string? Language { get; private set; }
    public string ProviderName { get; private set; } = string.Empty;
    public string ProviderVersion { get; private set; } = string.Empty;
    public string? ModelVersion { get; private set; }
    public string ExtractionSchemaVersion { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public long? DurationMs { get; private set; }
    public string? FailureCode { get; private set; }
    public string? SafeFailureSummary { get; private set; }
    public IReadOnlyCollection<MachineFieldValue> Fields => _fields.AsReadOnly();

    public void Start(DateTimeOffset now)
    {
        if (Status != ExtractionRunStatus.Queued) throw new InvalidOperationException("Only queued runs can start.");
        Status = ExtractionRunStatus.Running;
        StartedAtUtc = now;
    }

    public MachineFieldValue AddField(Guid id, string fieldKey, int occurrenceIndex,
        FieldValueType valueType, string valueText, decimal confidence, DateTimeOffset now,
        string? normalizedValue = null)
    {
        if (Status != ExtractionRunStatus.Running) throw new InvalidOperationException("Fields can only be added while running.");
        if (_fields.Any(x => x.FieldKey == fieldKey && x.OccurrenceIndex == occurrenceIndex))
            throw new InvalidOperationException("Field occurrence already exists.");
        var field = new MachineFieldValue(id, OrganizationId, Id, fieldKey, occurrenceIndex,
            valueType, valueText, normalizedValue, confidence, now);
        _fields.Add(field);
        return field;
    }

    public FieldEvidence AddEvidence(MachineFieldValue field, Guid id, int pageNumber,
        string? quotedText = null, int? characterStart = null, int? characterEnd = null,
        string? boundingPolygon = null, string? coordinateUnit = null)
    {
        RequireRunning();
        if (!_fields.Contains(field)) throw new InvalidOperationException("Field does not belong to this run.");
        return field.AddEvidence(id, pageNumber, quotedText, characterStart, characterEnd,
            boundingPolygon, coordinateUnit);
    }

    public void Succeed(string documentType, string language, DateTimeOffset now, string? modelVersion = null)
    {
        RequireRunning();
        DocumentType = Guard.Required(documentType, nameof(documentType), 50);
        Language = Guard.Required(language, nameof(language), 20);
        ModelVersion = string.IsNullOrWhiteSpace(modelVersion)
            ? null
            : Guard.Required(modelVersion, nameof(modelVersion), 100);
        Complete(ExtractionRunStatus.Succeeded, now);
    }

    public void Fail(string failureCode, string safeSummary, DateTimeOffset now)
    {
        RequireRunning();
        FailureCode = Guard.Required(failureCode, nameof(failureCode), 100);
        SafeFailureSummary = Guard.Required(safeSummary, nameof(safeSummary), 500);
        Complete(ExtractionRunStatus.Failed, now);
    }

    public void Cancel(string failureCode, string safeSummary, DateTimeOffset now)
    {
        RequireRunning();
        FailureCode = Guard.Required(failureCode, nameof(failureCode), 100);
        SafeFailureSummary = Guard.Required(safeSummary, nameof(safeSummary), 500);
        Complete(ExtractionRunStatus.Cancelled, now);
    }

    private void Complete(ExtractionRunStatus status, DateTimeOffset now)
    {
        Status = status;
        CompletedAtUtc = now;
        DurationMs = Math.Max(0, (long)(now - StartedAtUtc!.Value).TotalMilliseconds);
    }

    private void RequireRunning()
    {
        if (Status != ExtractionRunStatus.Running) throw new InvalidOperationException("Run must be running.");
    }
}
