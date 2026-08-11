using System.Text.RegularExpressions;
using UaeDocumentIntelligence.Domain.Common;

namespace UaeDocumentIntelligence.Domain.Model;

public sealed partial class ExtractionWarning
{
    private ExtractionWarning() { }

    internal ExtractionWarning(Guid id, Guid organizationId, Guid extractionRunId, string code,
        ExtractionWarningSeverity severity, string safeSummary, int? pageNumber,
        string? fieldKey, int? occurrenceIndex)
    {
        if (id == Guid.Empty) throw new ArgumentException("Warning ID is required.", nameof(id));
        if (organizationId == Guid.Empty || extractionRunId == Guid.Empty)
            throw new ArgumentException("Organization and extraction run IDs are required.");
        if (!CodePattern().IsMatch(code ?? string.Empty))
            throw new ArgumentException("Warning code must be bounded lower snake case.", nameof(code));
        if (pageNumber < 1) throw new ArgumentOutOfRangeException(nameof(pageNumber));
        if (string.IsNullOrWhiteSpace(fieldKey) != !occurrenceIndex.HasValue)
            throw new ArgumentException("Field key and occurrence must be supplied together.");
        if (occurrenceIndex < 0) throw new ArgumentOutOfRangeException(nameof(occurrenceIndex));

        Id = id;
        OrganizationId = organizationId;
        ExtractionRunId = extractionRunId;
        Code = Guard.Required(code, nameof(code), 100);
        Severity = severity;
        SafeSummary = Guard.Required(safeSummary, nameof(safeSummary), 300);
        PageNumber = pageNumber;
        FieldKey = fieldKey;
        OccurrenceIndex = occurrenceIndex;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ExtractionRunId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public ExtractionWarningSeverity Severity { get; private set; }
    public string SafeSummary { get; private set; } = string.Empty;
    public int? PageNumber { get; private set; }
    public string? FieldKey { get; private set; }
    public int? OccurrenceIndex { get; private set; }

    [GeneratedRegex("^[a-z][a-z0-9_]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();
}
