using UaeDocumentIntelligence.Domain.Common;

namespace UaeDocumentIntelligence.Domain.Model;

public sealed class ReviewAction
{
    private ReviewAction() { }

    internal ReviewAction(Guid id, Guid organizationId, Guid reviewSessionId, Guid extractionRunId,
        Guid? machineFieldValueId, Guid actorId, string fieldKey, int occurrenceIndex, int sequenceNumber,
        ReviewActionType action, FieldValueType? correctedValueType, string? correctedValueText,
        string? reason, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || actorId == Guid.Empty) throw new ArgumentException("Action and actor IDs are required.");
        if (occurrenceIndex < 0) throw new ArgumentOutOfRangeException(nameof(occurrenceIndex));
        Guard.Positive(sequenceNumber, nameof(sequenceNumber));
        var hasCompleteValue = correctedValueType.HasValue && Enum.IsDefined(correctedValueType.Value) &&
            !string.IsNullOrWhiteSpace(correctedValueText);
        var hasAnyValue = correctedValueType.HasValue || correctedValueText is not null;
        var hasReason = !string.IsNullOrWhiteSpace(reason);
        var valid = action switch
        {
            ReviewActionType.Confirmed => machineFieldValueId.HasValue && !hasAnyValue,
            ReviewActionType.Corrected => machineFieldValueId.HasValue && hasCompleteValue && hasReason,
            ReviewActionType.Added => !machineFieldValueId.HasValue && hasCompleteValue && hasReason,
            ReviewActionType.Cleared => !hasAnyValue && hasReason,
            _ => false
        };
        if (!valid) throw new ArgumentException("Review action does not satisfy its value, source, and reason requirements.");
        Id = id;
        OrganizationId = organizationId;
        ReviewSessionId = reviewSessionId;
        ExtractionRunId = extractionRunId;
        MachineFieldValueId = machineFieldValueId;
        ActorId = actorId;
        FieldKey = Guard.Required(fieldKey, nameof(fieldKey), 100);
        OccurrenceIndex = occurrenceIndex;
        SequenceNumber = sequenceNumber;
        Action = action;
        CorrectedValueType = correctedValueType;
        CorrectedValueText = correctedValueText;
        Reason = hasReason ? Guard.Required(reason, nameof(reason), 500) : null;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ReviewSessionId { get; private set; }
    public Guid ExtractionRunId { get; private set; }
    public Guid? MachineFieldValueId { get; private set; }
    public Guid ActorId { get; private set; }
    public string FieldKey { get; private set; } = string.Empty;
    public int OccurrenceIndex { get; private set; }
    public int SequenceNumber { get; private set; }
    public ReviewActionType Action { get; private set; }
    public FieldValueType? CorrectedValueType { get; private set; }
    public string? CorrectedValueText { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
