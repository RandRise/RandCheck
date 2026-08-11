using System.Text;
using System.Text.Json;

namespace UaeDocumentIntelligence.Domain.Model;

public sealed class FieldEvidence
{
    private FieldEvidence() { }

    internal FieldEvidence(Guid id, Guid organizationId, Guid extractionRunId,
        Guid machineFieldValueId, ExtractionPage page, string? quotedText, int? byteStart,
        int? byteEnd, string? boundingPolygon,
        string? coordinateUnit)
    {
        if (id == Guid.Empty) throw new ArgumentException("Evidence ID is required.", nameof(id));
        if (organizationId == Guid.Empty || extractionRunId == Guid.Empty || machineFieldValueId == Guid.Empty)
            throw new ArgumentException("Organization, run, and field IDs are required.");
        ArgumentNullException.ThrowIfNull(page);
        if (page.OrganizationId != organizationId || page.ExtractionRunId != extractionRunId)
            throw new ArgumentException("Evidence page must belong to the field's extraction run.", nameof(page));
        ValidateQuote(page.CanonicalText, quotedText, byteStart, byteEnd);
        var polygon = ValidatePolygon(boundingPolygon, coordinateUnit);

        Id = id;
        OrganizationId = organizationId;
        ExtractionRunId = extractionRunId;
        MachineFieldValueId = machineFieldValueId;
        PageNumber = page.PageNumber;
        QuotedText = quotedText;
        ByteStart = byteStart;
        ByteEnd = byteEnd;
        BoundingPolygon = polygon;
        CoordinateUnit = polygon is null ? null : "normalized";
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ExtractionRunId { get; private set; }
    public Guid MachineFieldValueId { get; private set; }
    public int PageNumber { get; private set; }
    public string? QuotedText { get; private set; }
    public int? ByteStart { get; private set; }
    public int? ByteEnd { get; private set; }
    public string? BoundingPolygon { get; private set; }
    public string? CoordinateUnit { get; private set; }

    private static void ValidateQuote(string canonicalText, string? quote, int? byteStart, int? byteEnd)
    {
        var any = quote is not null || byteStart.HasValue || byteEnd.HasValue;
        if (!any) return;
        if (quote is null || !byteStart.HasValue || !byteEnd.HasValue)
            throw new ArgumentException("Quote and UTF-8 byte offsets must be supplied together.");
        if (byteStart < 0 || byteEnd < byteStart)
            throw new ArgumentOutOfRangeException(nameof(byteStart), "UTF-8 byte range is invalid.");

        var bytes = Encoding.UTF8.GetBytes(canonicalText);
        if (byteEnd > bytes.Length)
            throw new ArgumentOutOfRangeException(nameof(byteEnd), "UTF-8 byte range exceeds the page text.");
        try
        {
            var selected = new UTF8Encoding(false, true).GetString(
                bytes.AsSpan(byteStart.Value, byteEnd.Value - byteStart.Value));
            if (!string.Equals(selected, quote, StringComparison.Ordinal))
                throw new ArgumentException("UTF-8 byte range must reproduce the quote exactly.", nameof(quote));
        }
        catch (DecoderFallbackException exception)
        {
            throw new ArgumentException("UTF-8 byte offsets must land on code-point boundaries.", exception);
        }
    }

    private static string? ValidatePolygon(string? polygon, string? coordinateUnit)
    {
        if (string.IsNullOrWhiteSpace(polygon))
        {
            if (!string.IsNullOrWhiteSpace(coordinateUnit))
                throw new ArgumentException("Coordinate unit requires a polygon.", nameof(coordinateUnit));
            return null;
        }
        if (!string.Equals(coordinateUnit, "normalized", StringComparison.Ordinal))
            throw new ArgumentException("Polygon coordinate unit must be normalized.", nameof(coordinateUnit));

        try
        {
            using var document = JsonDocument.Parse(polygon);
            var points = document.RootElement;
            if (points.ValueKind != JsonValueKind.Array || points.GetArrayLength() != 4)
                throw new ArgumentException("A normalized polygon must contain exactly four points.", nameof(polygon));

            var values = points.EnumerateArray().Select(point =>
            {
                if (point.ValueKind != JsonValueKind.Array || point.GetArrayLength() != 2)
                    throw new ArgumentException("Each polygon point must contain x and y.", nameof(polygon));
                var x = point[0].GetDecimal();
                var y = point[1].GetDecimal();
                if (x is < 0 or > 1 || y is < 0 or > 1)
                    throw new ArgumentOutOfRangeException(nameof(polygon), "Polygon coordinates must be within [0,1].");
                return (X: x, Y: y);
            }).ToArray();

            var topLeft = values.OrderBy(point => point.Y).ThenBy(point => point.X).First();
            if (values[0] != topLeft)
                throw new ArgumentException("Polygon must begin with the top-left point.", nameof(polygon));
            decimal twiceArea = 0;
            for (var index = 0; index < values.Length; index++)
            {
                var current = values[index];
                var next = values[(index + 1) % values.Length];
                twiceArea += current.X * next.Y - current.Y * next.X;
            }
            if (twiceArea <= 0)
                throw new ArgumentException("Polygon points must be distinct and clockwise.", nameof(polygon));

            return JsonSerializer.Serialize(values.Select(point => new[] { point.X, point.Y }));
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Polygon must be valid JSON.", nameof(polygon), exception);
        }
    }
}
