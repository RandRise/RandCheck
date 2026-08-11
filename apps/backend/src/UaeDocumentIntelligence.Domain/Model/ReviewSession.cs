namespace UaeDocumentIntelligence.Domain.Model;

public sealed class ReviewSession
{
    private readonly List<ReviewAction> _actions = [];
    private ReviewSession() { }

    public ReviewSession(Guid id, Guid organizationId, Guid documentId, Guid extractionRunId,
        int revisionNumber, DateTimeOffset now, Guid? assignedActorId = null,
        Guid? supersedesReviewSessionId = null)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || documentId == Guid.Empty || extractionRunId == Guid.Empty)
            throw new ArgumentException("Review identity is required.");
        if (revisionNumber <= 0) throw new ArgumentOutOfRangeException(nameof(revisionNumber));
        Id = id;
        OrganizationId = organizationId;
        DocumentId = documentId;
        ExtractionRunId = extractionRunId;
        AssignedActorId = assignedActorId;
        SupersedesReviewSessionId = supersedesReviewSessionId;
        RevisionNumber = revisionNumber;
        Status = ReviewStatus.Pending;
        ConcurrencyToken = Guid.NewGuid();
        CreatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid DocumentId { get; private set; }
    public Guid ExtractionRunId { get; private set; }
    public Guid? AssignedActorId { get; private set; }
    public Guid? SupersedesReviewSessionId { get; private set; }
    public int RevisionNumber { get; private set; }
    public ReviewStatus Status { get; private set; }
    public Guid ConcurrencyToken { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public IReadOnlyCollection<ReviewAction> Actions => _actions.AsReadOnly();

    public void Start(DateTimeOffset now)
    {
        if (Status != ReviewStatus.Pending) throw new InvalidOperationException("Only pending reviews can start.");
        Status = ReviewStatus.InProgress;
        StartedAtUtc = now;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void Complete(IReadOnlyCollection<MachineFieldValue> requiredFields, DateTimeOffset now)
    {
        if (Status != ReviewStatus.InProgress) throw new InvalidOperationException("Only active reviews can complete.");
        if (requiredFields.Any(x => x.OrganizationId != OrganizationId || x.ExtractionRunId != ExtractionRunId))
            throw new InvalidOperationException("Required review fields must belong to the pinned extraction run.");
        var decidedMachineFields = _actions
            .Where(x => x.MachineFieldValueId.HasValue)
            .GroupBy(x => x.MachineFieldValueId!.Value)
            .Select(x => x.OrderByDescending(action => action.SequenceNumber).First())
            .Where(x => x.Action is ReviewActionType.Confirmed or ReviewActionType.Corrected or ReviewActionType.Cleared)
            .Select(x => x.MachineFieldValueId!.Value)
            .ToHashSet();
        var missing = requiredFields.Where(x => !decidedMachineFields.Contains(x.Id)).Select(x => x.Id).ToList();
        if (missing.Count > 0) throw new IncompleteReviewException(missing);
        Status = ReviewStatus.Completed;
        CompletedAtUtc = now;
        ConcurrencyToken = Guid.NewGuid();
    }

    public ReviewAction AddAction(Guid id, Guid actorId, int sequenceNumber, ReviewActionType action,
        string fieldKey, int occurrenceIndex, Guid? machineFieldValueId, FieldValueType? correctedValueType,
        string? correctedValueText, string? reason, DateTimeOffset now)
    {
        if (Status != ReviewStatus.InProgress) throw new InvalidOperationException("Review must be active.");
        if (_actions.Any(x => x.SequenceNumber == sequenceNumber)) throw new InvalidOperationException("Sequence already exists.");
        var item = new ReviewAction(id, OrganizationId, Id, ExtractionRunId, machineFieldValueId,
            actorId, fieldKey, occurrenceIndex, sequenceNumber, action, correctedValueType,
            correctedValueText, reason, now);
        _actions.Add(item);
        ConcurrencyToken = Guid.NewGuid();
        return item;
    }
}

public sealed class IncompleteReviewException(IReadOnlyList<Guid> missingMachineFieldIds)
    : InvalidOperationException("Every machine field requires a review decision before completion.")
{
    public IReadOnlyList<Guid> MissingMachineFieldIds { get; } = missingMachineFieldIds;
}
