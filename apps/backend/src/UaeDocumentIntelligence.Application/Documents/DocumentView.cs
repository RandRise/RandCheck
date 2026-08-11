using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Documents;

public sealed record FieldView(string Name, string Value, decimal Confidence, int Page);

public sealed record DocumentView(
    Guid Id,
    string OriginalFilename,
    string ContentType,
    long SizeBytes,
    string Sha256,
    string Status,
    string? DocumentType,
    string? Language,
    string? Extractor,
    string? ErrorMessage,
    long? ProcessingMs,
    DateTimeOffset CreatedAt,
    IReadOnlyList<FieldView> Fields)
{
    public static DocumentView From(Document document)
    {
        var selected = document.SelectedExtractionRun();
        var latest = document.ExtractionRuns.OrderByDescending(x => x.RunNumber).FirstOrDefault();
        var status = document.ProcessingStatus switch
        {
            DocumentProcessingStatus.ReadyForReview => "Ready",
            DocumentProcessingStatus.ExtractionFailed => "Failed",
            _ => "Processing"
        };
        var fields = selected?.Fields.Select(x => new FieldView(
            x.FieldKey, x.ValueText, x.Confidence, x.Evidence.OrderBy(e => e.PageNumber).FirstOrDefault()?.PageNumber ?? 1
        )).OrderBy(x => x.Name).ToList() ?? [];
        return new DocumentView(document.Id, document.OriginalFilename, document.DeclaredContentType,
            document.SizeBytes, document.Sha256, status, selected?.DocumentType, selected?.Language,
            selected?.ModelVersion ?? selected?.ProviderName, latest?.SafeFailureSummary,
            latest?.DurationMs, document.CreatedAtUtc, fields);
    }
}
