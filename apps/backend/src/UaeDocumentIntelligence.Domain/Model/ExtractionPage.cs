using System.Text;

namespace UaeDocumentIntelligence.Domain.Model;

public sealed class ExtractionPage
{
    public const int MaxCanonicalTextBytes = 1_048_576;

    private ExtractionPage() { }

    internal ExtractionPage(Guid id, Guid organizationId, Guid extractionRunId, int pageNumber,
        string canonicalText, short rotationDegrees)
    {
        if (id == Guid.Empty) throw new ArgumentException("Page ID is required.", nameof(id));
        if (organizationId == Guid.Empty || extractionRunId == Guid.Empty)
            throw new ArgumentException("Organization and extraction run IDs are required.");
        if (pageNumber < 1) throw new ArgumentOutOfRangeException(nameof(pageNumber));
        if (rotationDegrees is not (0 or 90 or 180 or 270))
            throw new ArgumentOutOfRangeException(nameof(rotationDegrees));
        ArgumentNullException.ThrowIfNull(canonicalText);

        var normalized = canonicalText.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n').Normalize(NormalizationForm.FormC);
        if (!string.Equals(normalized, canonicalText, StringComparison.Ordinal))
            throw new ArgumentException("Page text must already use NFC and LF newlines.", nameof(canonicalText));
        if (Encoding.UTF8.GetByteCount(canonicalText) > MaxCanonicalTextBytes)
            throw new ArgumentException("Page text exceeds the UTF-8 byte limit.", nameof(canonicalText));

        Id = id;
        OrganizationId = organizationId;
        ExtractionRunId = extractionRunId;
        PageNumber = pageNumber;
        CanonicalText = canonicalText;
        RotationDegrees = rotationDegrees;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ExtractionRunId { get; private set; }
    public int PageNumber { get; private set; }
    public string CanonicalText { get; private set; } = string.Empty;
    public short RotationDegrees { get; private set; }
}
