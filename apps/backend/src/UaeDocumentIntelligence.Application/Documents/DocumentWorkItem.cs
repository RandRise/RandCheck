using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Documents;

// Work-list data deliberately excludes extraction values, evidence and review history.
public sealed record DocumentWorkItem(Guid Id, string OriginalFilename, long SizeBytes, string Status,
    string? DocumentType, string? Language, string? Extractor, long? ProcessingMs,
    string? ErrorMessage, DateTimeOffset CreatedAt, string? ReviewState,
    Guid? SelectedExtractionRunId, Guid? ReviewExtractionRunId, Guid? LatestCompletedReviewId,
    bool ReviewUsesOlderExtraction, string? LatestExtractionStatus = null)
{
    public static DocumentWorkItem From(Document document)
    {
        var selected = document.SelectedExtractionRun();
        var latest = document.ExtractionRuns.OrderByDescending(x => x.RunNumber).FirstOrDefault();
        var active = document.ReviewSessions.Where(x => x.Status is ReviewStatus.Pending or ReviewStatus.InProgress)
            .OrderByDescending(x => x.RevisionNumber).FirstOrDefault();
        var completed = document.ReviewSessions.Where(x => x.Status == ReviewStatus.Completed && x.ExtractionRunId == selected?.Id)
            .OrderByDescending(x => x.RevisionNumber).FirstOrDefault();
        var latestCompleted = document.ReviewSessions.Where(x => x.Status == ReviewStatus.Completed)
            .OrderByDescending(x => x.RevisionNumber).FirstOrDefault();
        return new(document.Id, document.OriginalFilename, document.SizeBytes, ProcessingState(document.ProcessingStatus),
            selected?.DocumentType, selected?.Language, selected?.ModelName ?? selected?.ProviderName, latest?.DurationMs,
            latest?.SafeFailureSummary, document.CreatedAtUtc, selected is null ? null : active is not null ? "InProgress" : completed is not null ? "Reviewed" : "NeedsReview",
            selected?.Id, active?.ExtractionRunId ?? selected?.Id, latestCompleted?.Id, active is not null && active.ExtractionRunId != selected?.Id,
            latest is null ? null : ExtractionState(latest.Status));
    }

    public static string ProcessingState(DocumentProcessingStatus status) => status switch
    {
        DocumentProcessingStatus.ReadyForReview => "Ready",
        DocumentProcessingStatus.ExtractionFailed => "Failed",
        DocumentProcessingStatus.ProcessingCancelled => "Cancelled",
        _ => "Processing"
    };

    public static string ExtractionState(ExtractionRunStatus status) => status switch
    {
        ExtractionRunStatus.Queued => "Queued", ExtractionRunStatus.Running => "Processing",
        ExtractionRunStatus.Succeeded => "Ready", ExtractionRunStatus.Partial => "Partial",
        ExtractionRunStatus.Failed => "Failed", ExtractionRunStatus.Cancelled => "Cancelled", _ => "Processing"
    };
}
