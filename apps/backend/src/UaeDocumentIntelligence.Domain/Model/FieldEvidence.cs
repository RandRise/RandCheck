namespace UaeDocumentIntelligence.Domain.Model;

public sealed class FieldEvidence
{
    private FieldEvidence() { }

    internal FieldEvidence(Guid id, Guid organizationId, Guid machineFieldValueId, int pageNumber,
        string? quotedText, int? characterStart, int? characterEnd, string? boundingPolygon,
        string? coordinateUnit)
    {
        if (id == Guid.Empty) throw new ArgumentException("Evidence ID is required.", nameof(id));
        if (pageNumber < 1) throw new ArgumentOutOfRangeException(nameof(pageNumber));
        if (characterStart.HasValue != characterEnd.HasValue)
            throw new ArgumentException("Character offsets must be supplied together.");
        if (characterStart < 0 || characterEnd < characterStart)
            throw new ArgumentOutOfRangeException(nameof(characterStart), "Character range is invalid.");
        if (!string.IsNullOrWhiteSpace(boundingPolygon) && string.IsNullOrWhiteSpace(coordinateUnit))
            throw new ArgumentException("Coordinate unit is required with a bounding polygon.", nameof(coordinateUnit));
        Id = id;
        OrganizationId = organizationId;
        MachineFieldValueId = machineFieldValueId;
        PageNumber = pageNumber;
        QuotedText = quotedText;
        CharacterStart = characterStart;
        CharacterEnd = characterEnd;
        BoundingPolygon = boundingPolygon;
        CoordinateUnit = coordinateUnit;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid MachineFieldValueId { get; private set; }
    public int PageNumber { get; private set; }
    public string? QuotedText { get; private set; }
    public int? CharacterStart { get; private set; }
    public int? CharacterEnd { get; private set; }
    public string? BoundingPolygon { get; private set; }
    public string? CoordinateUnit { get; private set; }
}
