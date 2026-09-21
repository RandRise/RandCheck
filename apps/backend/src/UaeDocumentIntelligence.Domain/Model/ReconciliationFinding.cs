using UaeDocumentIntelligence.Domain.Common;

namespace UaeDocumentIntelligence.Domain.Model;

public sealed class ReconciliationFinding
{
    private readonly List<FindingResolution> _resolutions = [];
    private ReconciliationFinding() { }

    internal ReconciliationFinding(Guid id, Guid organizationId, Guid reconciliationRunId,
        string ruleCode, string ruleVersion, ReconciliationFindingSeverity severity,
        ReconciliationDocumentRole? leftRole, ReconciliationDocumentRole? rightRole,
        Guid? leftFieldSnapshotId, Guid? rightFieldSnapshotId, string? fieldKey,
        int? occurrenceIndex, string? leftValue, string? rightValue,
        decimal? difference, decimal? tolerance, string explanation,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Finding ID is required.", nameof(id));
        if (!leftRole.HasValue && !rightRole.HasValue)
            throw new ArgumentException("A finding must identify at least one document role.");
        if (occurrenceIndex < 0) throw new ArgumentOutOfRangeException(nameof(occurrenceIndex));
        if (difference < 0 || tolerance < 0)
            throw new ArgumentOutOfRangeException(nameof(difference), "Difference and tolerance cannot be negative.");
        if (tolerance.HasValue && !difference.HasValue)
            throw new ArgumentException("A numeric tolerance requires a numeric difference.", nameof(tolerance));
        Id = id;
        OrganizationId = organizationId;
        ReconciliationRunId = reconciliationRunId;
        RuleCode = Guard.Required(ruleCode, nameof(ruleCode), 50);
        RuleVersion = Guard.Required(ruleVersion, nameof(ruleVersion), 50);
        Severity = severity;
        LeftRole = leftRole;
        RightRole = rightRole;
        LeftFieldSnapshotId = leftFieldSnapshotId;
        RightFieldSnapshotId = rightFieldSnapshotId;
        FieldKey = string.IsNullOrWhiteSpace(fieldKey) ? null : Guard.Required(fieldKey, nameof(fieldKey), 100);
        OccurrenceIndex = occurrenceIndex;
        LeftValue = leftValue;
        RightValue = rightValue;
        Difference = difference;
        Tolerance = tolerance;
        Explanation = Guard.Required(explanation, nameof(explanation), 1000);
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ReconciliationRunId { get; private set; }
    public string RuleCode { get; private set; } = string.Empty;
    public string RuleVersion { get; private set; } = string.Empty;
    public ReconciliationFindingSeverity Severity { get; private set; }
    public ReconciliationDocumentRole? LeftRole { get; private set; }
    public ReconciliationDocumentRole? RightRole { get; private set; }
    public Guid? LeftFieldSnapshotId { get; private set; }
    public Guid? RightFieldSnapshotId { get; private set; }
    public string? FieldKey { get; private set; }
    public int? OccurrenceIndex { get; private set; }
    public string? LeftValue { get; private set; }
    public string? RightValue { get; private set; }
    public decimal? Difference { get; private set; }
    public decimal? Tolerance { get; private set; }
    public string Explanation { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public IReadOnlyCollection<FindingResolution> Resolutions => _resolutions.AsReadOnly();

    internal FindingResolution AddResolution(Guid id, Guid actorId,
        FindingResolutionAction action, string reason, DateTimeOffset now)
    {
        if (id == Guid.Empty || actorId == Guid.Empty)
            throw new ArgumentException("Resolution and actor IDs are required.");
        if (!Enum.IsDefined(action)) throw new ArgumentOutOfRangeException(nameof(action));
        var sequence = _resolutions.Select(item => item.SequenceNumber).DefaultIfEmpty(0).Max() + 1;
        var resolution = new FindingResolution(id, OrganizationId, Id, actorId,
            sequence, action, reason, now);
        _resolutions.Add(resolution);
        return resolution;
    }
}

public sealed class FindingResolution
{
    private FindingResolution() { }

    internal FindingResolution(Guid id, Guid organizationId, Guid findingId,
        Guid actorId, int sequenceNumber, FindingResolutionAction action,
        string reason, DateTimeOffset createdAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        FindingId = findingId;
        ActorId = actorId;
        SequenceNumber = sequenceNumber;
        Action = action;
        Reason = Guard.Required(reason, nameof(reason), 500);
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid FindingId { get; private set; }
    public Guid ActorId { get; private set; }
    public int SequenceNumber { get; private set; }
    public FindingResolutionAction Action { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
