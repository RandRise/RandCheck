using UaeDocumentIntelligence.Domain.Common;

namespace UaeDocumentIntelligence.Domain.Model;

public sealed class MachineFieldValue
{
    private readonly List<FieldEvidence> _evidence = [];
    private MachineFieldValue() { }

    internal MachineFieldValue(Guid id, Guid organizationId, Guid extractionRunId, string fieldKey,
        int occurrenceIndex, FieldValueType valueType, string valueText, string? normalizedValueText,
        decimal confidence, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Field ID is required.", nameof(id));
        if (occurrenceIndex < 0) throw new ArgumentOutOfRangeException(nameof(occurrenceIndex));
        if (confidence is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(confidence));
        Id = id;
        OrganizationId = organizationId;
        ExtractionRunId = extractionRunId;
        FieldKey = Guard.Required(fieldKey, nameof(fieldKey), 100);
        OccurrenceIndex = occurrenceIndex;
        ValueType = valueType;
        ValueText = valueText ?? throw new ArgumentNullException(nameof(valueText));
        NormalizedValueText = normalizedValueText;
        Confidence = confidence;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ExtractionRunId { get; private set; }
    public string FieldKey { get; private set; } = string.Empty;
    public int OccurrenceIndex { get; private set; }
    public FieldValueType ValueType { get; private set; }
    public string ValueText { get; private set; } = string.Empty;
    public string? NormalizedValueText { get; private set; }
    public decimal Confidence { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public IReadOnlyCollection<FieldEvidence> Evidence => _evidence.AsReadOnly();

    internal FieldEvidence AddEvidence(Guid id, int pageNumber, string? quotedText = null,
        int? characterStart = null, int? characterEnd = null, string? boundingPolygon = null,
        string? coordinateUnit = null)
    {
        var evidence = new FieldEvidence(id, OrganizationId, Id, pageNumber, quotedText,
            characterStart, characterEnd, boundingPolygon, coordinateUnit);
        _evidence.Add(evidence);
        return evidence;
    }
}
