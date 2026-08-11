using UaeDocumentIntelligence.Domain.Common;

namespace UaeDocumentIntelligence.Domain.Model;

public sealed class ExtractionRun
{
    public const int MaxPages = 50;
    public const int MaxFields = 500;
    public const int MaxEvidencePerField = 10;
    public const int MaxWarnings = 100;

    private readonly List<ExtractionPage> _pages = [];
    private readonly List<MachineFieldValue> _fields = [];
    private readonly List<ExtractionWarning> _warnings = [];
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
    public string? ModelName { get; private set; }
    public string? ModelVersion { get; private set; }
    public string ExtractionSchemaVersion { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public long? DurationMs { get; private set; }
    public long? ProviderDurationMs { get; private set; }
    public int? PageCount { get; private set; }
    public decimal? EstimatedCost { get; private set; }
    public string? CostCurrency { get; private set; }
    public string? PriceBasis { get; private set; }
    public string? FailureCode { get; private set; }
    public string? SafeFailureSummary { get; private set; }
    public IReadOnlyCollection<ExtractionPage> Pages => _pages.AsReadOnly();
    public IReadOnlyCollection<MachineFieldValue> Fields => _fields.AsReadOnly();
    public IReadOnlyCollection<ExtractionWarning> Warnings => _warnings.AsReadOnly();

    public void Start(DateTimeOffset now)
    {
        if (Status != ExtractionRunStatus.Queued) throw new InvalidOperationException("Only queued runs can start.");
        Status = ExtractionRunStatus.Running;
        StartedAtUtc = now;
    }

    public ExtractionPage AddPage(Guid id, int pageNumber, string canonicalText, short rotationDegrees)
    {
        RequireRunning();
        if (_pages.Count >= MaxPages) throw new InvalidOperationException("Extraction page limit exceeded.");
        if (pageNumber != _pages.Count + 1)
            throw new InvalidOperationException("Extraction pages must be added contiguously from page one.");
        var page = new ExtractionPage(id, OrganizationId, Id, pageNumber, canonicalText, rotationDegrees);
        _pages.Add(page);
        return page;
    }

    public MachineFieldValue AddField(Guid id, string fieldKey, int occurrenceIndex,
        FieldValueType valueType, string valueText, decimal? confidence, DateTimeOffset now,
        string? normalizedValue = null)
    {
        if (Status != ExtractionRunStatus.Running) throw new InvalidOperationException("Fields can only be added while running.");
        if (_fields.Count >= MaxFields) throw new InvalidOperationException("Extraction field limit exceeded.");
        if (_fields.Any(x => x.FieldKey == fieldKey && x.OccurrenceIndex == occurrenceIndex))
            throw new InvalidOperationException("Field occurrence already exists.");
        var field = new MachineFieldValue(id, OrganizationId, Id, fieldKey, occurrenceIndex,
            valueType, valueText, normalizedValue, confidence, now);
        _fields.Add(field);
        return field;
    }

    public FieldEvidence AddEvidence(MachineFieldValue field, Guid id, int pageNumber,
        string? quotedText = null, int? byteStart = null, int? byteEnd = null,
        string? boundingPolygon = null, string? coordinateUnit = null)
    {
        RequireRunning();
        if (!_fields.Contains(field)) throw new InvalidOperationException("Field does not belong to this run.");
        if (field.Evidence.Count >= MaxEvidencePerField)
            throw new InvalidOperationException("Field evidence limit exceeded.");
        var page = _pages.SingleOrDefault(candidate => candidate.PageNumber == pageNumber)
            ?? throw new InvalidOperationException("Evidence must reference an existing page in this run.");
        return field.AddEvidence(id, page, quotedText, byteStart, byteEnd,
            boundingPolygon, coordinateUnit);
    }

    public ExtractionWarning AddWarning(Guid id, string code, ExtractionWarningSeverity severity,
        string safeSummary, int? pageNumber = null, string? fieldKey = null, int? occurrenceIndex = null)
    {
        RequireRunning();
        if (_warnings.Count >= MaxWarnings) throw new InvalidOperationException("Extraction warning limit exceeded.");
        if (pageNumber.HasValue && _pages.All(page => page.PageNumber != pageNumber.Value))
            throw new InvalidOperationException("Warning page scope must exist in this run.");
        if (!string.IsNullOrWhiteSpace(fieldKey) &&
            _fields.All(field => field.FieldKey != fieldKey || field.OccurrenceIndex != occurrenceIndex))
            throw new InvalidOperationException("Warning field scope must exist in this run.");
        if (_warnings.Any(warning => warning.Code == code && warning.Severity == severity &&
            warning.PageNumber == pageNumber && warning.FieldKey == fieldKey &&
            warning.OccurrenceIndex == occurrenceIndex))
            throw new InvalidOperationException("Duplicate extraction warning identity.");

        var warning = new ExtractionWarning(id, OrganizationId, Id, code, severity, safeSummary,
            pageNumber, fieldKey, occurrenceIndex);
        _warnings.Add(warning);
        _warnings.Sort(CompareWarnings);
        return warning;
    }

    public void RecordProviderMetrics(long providerDurationMs, int pageCount, decimal? estimatedCost,
        string? costCurrency, string? priceBasis)
    {
        RequireRunning();
        if (providerDurationMs < 0) throw new ArgumentOutOfRangeException(nameof(providerDurationMs));
        if (pageCount != _pages.Count) throw new ArgumentException("Metric page count must match normalized pages.", nameof(pageCount));
        if (estimatedCost < 0) throw new ArgumentOutOfRangeException(nameof(estimatedCost));
        if (estimatedCost.HasValue && string.IsNullOrWhiteSpace(costCurrency))
            throw new ArgumentException("Cost currency is required with estimated cost.", nameof(costCurrency));
        if (!estimatedCost.HasValue && (!string.IsNullOrWhiteSpace(costCurrency) || !string.IsNullOrWhiteSpace(priceBasis)))
            throw new ArgumentException("Currency and price basis require an estimated cost.");

        ProviderDurationMs = providerDurationMs;
        PageCount = pageCount;
        EstimatedCost = estimatedCost;
        CostCurrency = string.IsNullOrWhiteSpace(costCurrency)
            ? null
            : Guard.Required(costCurrency.ToUpperInvariant(), nameof(costCurrency), 3);
        PriceBasis = string.IsNullOrWhiteSpace(priceBasis)
            ? null
            : Guard.Required(priceBasis, nameof(priceBasis), 100);
    }

    public void Succeed(string documentType, string language, DateTimeOffset now,
        string modelName, string modelVersion)
    {
        RequireRunning();
        RequireReviewableResult();
        if (_warnings.Any(warning => warning.Severity == ExtractionWarningSeverity.Degraded))
            throw new InvalidOperationException("A ready extraction cannot contain degraded warnings.");
        DocumentType = Guard.Required(documentType, nameof(documentType), 50);
        Language = Guard.Required(language, nameof(language), 20);
        SetModelIdentity(modelName, modelVersion);
        Complete(ExtractionRunStatus.Succeeded, now);
    }

    public void CompletePartial(string documentType, string language, DateTimeOffset now,
        string modelName, string modelVersion)
    {
        RequireRunning();
        RequireReviewableResult();
        if (_warnings.All(warning => warning.Severity != ExtractionWarningSeverity.Degraded))
            throw new InvalidOperationException("A partial extraction requires a degraded warning.");
        DocumentType = Guard.Required(documentType, nameof(documentType), 50);
        Language = Guard.Required(language, nameof(language), 20);
        SetModelIdentity(modelName, modelVersion);
        Complete(ExtractionRunStatus.Partial, now);
    }

    public void Fail(string failureCode, string safeSummary, DateTimeOffset now)
    {
        RequireRunning();
        DiscardNonTerminalResult();
        FailureCode = Guard.Required(failureCode, nameof(failureCode), 100);
        SafeFailureSummary = Guard.Required(safeSummary, nameof(safeSummary), 500);
        Complete(ExtractionRunStatus.Failed, now);
    }

    public void Cancel(string failureCode, string safeSummary, DateTimeOffset now)
    {
        RequireRunning();
        DiscardNonTerminalResult();
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

    private void RequireReviewableResult()
    {
        if (_pages.Count == 0 || _fields.Count == 0 || _fields.Any(field => field.Evidence.Count == 0))
            throw new InvalidOperationException("A reviewable extraction requires pages, fields, and field evidence.");
        if (!ProviderDurationMs.HasValue || PageCount != _pages.Count)
            throw new InvalidOperationException("A reviewable extraction requires provider metrics.");
    }

    private void DiscardNonTerminalResult()
    {
        _pages.Clear();
        _fields.Clear();
        _warnings.Clear();
        ProviderDurationMs = null;
        PageCount = null;
        EstimatedCost = null;
        CostCurrency = null;
        PriceBasis = null;
    }

    private void SetModelIdentity(string modelName, string modelVersion)
    {
        ModelName = Guard.Required(modelName, nameof(modelName), 100);
        ModelVersion = Guard.Required(modelVersion, nameof(modelVersion), 100);
    }

    private static int CompareWarnings(ExtractionWarning left, ExtractionWarning right)
    {
        var result = left.Severity.CompareTo(right.Severity);
        if (result != 0) return result;
        result = Nullable.Compare(left.PageNumber, right.PageNumber);
        if (result != 0) return result;
        result = string.Compare(left.FieldKey, right.FieldKey, StringComparison.Ordinal);
        if (result != 0) return result;
        result = Nullable.Compare(left.OccurrenceIndex, right.OccurrenceIndex);
        return result != 0 ? result : string.Compare(left.Code, right.Code, StringComparison.Ordinal);
    }
}
