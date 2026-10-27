using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Application.Documents.Processing;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal static class DocumentProcessingRequests
{
    internal static async Task<DocumentProcessingRequest?> FindAsync(AppDbContext db, Guid organizationId,
        string kind, ProcessingIntent intent, CancellationToken ct)
    {
        var receipt = await db.DocumentProcessingRequests.AsNoTracking().SingleOrDefaultAsync(x =>
            x.OrganizationId == organizationId && x.ActorId == intent.ActorId && x.Kind == kind && x.Key == intent.Key, ct);
        if (receipt is null) return null;
        if (receipt.PayloadHash != intent.PayloadHash)
            throw new ProcessingIntentException("operation_payload_changed", "This operation key belongs to different inputs. Start a deliberate new operation.");
        if (!await db.Documents.AnyAsync(x => x.OrganizationId == organizationId && x.Id == receipt.DocumentId, ct) ||
            !await db.ExtractionJobs.AnyAsync(x => x.OrganizationId == organizationId && x.Id == receipt.JobId &&
                x.DocumentId == receipt.DocumentId && x.ExtractionRunId == receipt.ExtractionRunId, ct) ||
            await db.ExtractionJobs.AnyAsync(x => x.OrganizationId == organizationId && x.DocumentId == receipt.DocumentId &&
                (x.LastErrorCode == "malware_detected" || x.LastErrorCode == "malware_scan_unavailable" ||
                 x.LastErrorCode == "storage_integrity_failed"), ct))
            throw new ProcessingIntentException("operation_source_unavailable", "The original operation's document is no longer eligible. It cannot be recreated by retrying.");
        return receipt;
    }

    internal static string DisplayStatus(ExtractionJob job) => job.Status switch
    {
        ExtractionJobStatus.Pending or ExtractionJobStatus.RetryScheduled => "Queued",
        ExtractionJobStatus.Succeeded => job.ResultOutcome?.ToString() == "Partial" ? "Partial" : "Ready",
        ExtractionJobStatus.Failed => "Failed",
        ExtractionJobStatus.Cancelled => "Cancelled",
        _ => "Processing"
    };
}
