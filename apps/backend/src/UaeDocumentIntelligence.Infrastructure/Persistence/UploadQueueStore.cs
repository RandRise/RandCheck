using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using UaeDocumentIntelligence.Infrastructure.Services;
using UaeDocumentIntelligence.Application.Documents.Upload;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Application.Documents.Processing;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal sealed class UploadQueueStore(AppDbContext db, IOptions<StorageOptions>? storage = null, SecurityAuditContext? audit = null,
    IOptions<CapacityOptions>? capacity = null) : IUploadQueueStore
{
    public async Task<Document?> CheckCapacityForIntentAsync(Guid organizationId, long bytes, ProcessingIntent intent, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        await OrganizationCapacity.LockAsync(db, organizationId, ct);
        var replay = await FindUploadAsync(organizationId, intent, ct);
        if (replay is null)
            await OrganizationCapacity.CheckLockedAsync(db, organizationId, bytes, capacity?.Value.RequirePolicy == true, ct);
        await tx.CommitAsync(ct);
        return replay;
    }
    public async Task CheckCapacityAsync(Guid organizationId, long bytes, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        await OrganizationCapacity.LockAsync(db, organizationId, ct);
        await OrganizationCapacity.CheckLockedAsync(db, organizationId, bytes, capacity?.Value.RequirePolicy == true, ct);
        // Advisory preflight only. Admission rechecks after scanning, in its commit transaction.
        await tx.CommitAsync(ct);
    }
    public async Task<Document?> FindUploadAsync(Guid organizationId, ProcessingIntent intent, CancellationToken ct)
    {
        var receipt = await DocumentProcessingRequests.FindAsync(db, organizationId, "Upload", intent, ct);
        if (receipt is null) return null;
        return await db.Documents.AsNoTracking().AsSplitQuery()
            .Include(x => x.ExtractionRuns).ThenInclude(x => x.Fields).ThenInclude(x => x.Evidence)
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == receipt.DocumentId, ct)
            ?? throw new ProcessingIntentException("operation_source_unavailable", "The original operation's document is no longer eligible.");
    }
    public async Task<ExtractionJob> AddAsync(Document document, Guid extractionRunId, Guid jobId, CancellationToken ct) =>
        (await AddCoreAsync(document, extractionRunId, jobId, null, null, ct)).Job;
    public async Task<ExtractionJob> AddScannedAsync(Document document, Guid extractionRunId, Guid jobId, FileScanReceipt scan, CancellationToken ct) =>
        (await AddCoreAsync(document, extractionRunId, jobId, scan, null, ct)).Job;
    public async Task<Document> AddWithIntentAsync(Document document, Guid extractionRunId, Guid jobId,
        FileScanReceipt? scan, ProcessingIntent intent, CancellationToken ct) =>
        (await AddCoreAsync(document, extractionRunId, jobId, scan, intent, ct)).Document;

    private async Task<(Document Document, ExtractionJob Job)> AddCoreAsync(Document document, Guid extractionRunId, Guid jobId,
        FileScanReceipt? scan, ProcessingIntent? intent, CancellationToken cancellationToken)
    {
        var mode = storage?.Value.SecurityMode ?? "Development";
        if (mode is not ("Development" or "Enforced") || mode == "Enforced" &&
            (scan is null || audit?.IsBound != true || audit.OrganizationId != document.OrganizationId || audit.ActorId != document.CreatedByActorId))
            throw new MalwareScanUnavailableException();
        if (scan is not null && (scan.Policy != "clamav-instream-v1" || scan.Sha256 != document.Sha256 || scan.SizeBytes != document.SizeBytes))
            throw new MalwareScanUnavailableException();
        if (!db.Database.IsNpgsql())
            throw new InvalidOperationException("The durable extraction queue requires PostgreSQL.");
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        await OrganizationCapacity.LockAsync(db, document.OrganizationId, cancellationToken);
        if (intent is not null && await FindUploadAsync(document.OrganizationId, intent, cancellationToken) is { } replay)
        {
            var receipt = await DocumentProcessingRequests.FindAsync(db, document.OrganizationId, "Upload", intent, cancellationToken);
            var originalJob = await db.ExtractionJobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == receipt!.JobId &&
                x.OrganizationId == document.OrganizationId, cancellationToken)
                ?? throw new ProcessingIntentException("operation_source_unavailable", "The original processing job is unavailable.");
            await transaction.CommitAsync(cancellationToken);
            return (replay, originalJob);
        }
        await OrganizationCapacity.CheckLockedAsync(db, document.OrganizationId, document.SizeBytes,
            capacity?.Value.RequirePolicy == true, cancellationToken);
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT clock_timestamp()";
        command.Transaction = transaction.GetDbTransaction();
        var value = await command.ExecuteScalarAsync(cancellationToken);
        var databaseNow = value switch
        {
            DateTimeOffset timestamp => timestamp,
            DateTime timestamp => new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)),
            _ => throw new InvalidOperationException("Database clock was unavailable.")
        };
        var job = new ExtractionJob(jobId, document.OrganizationId, document.Id,
            extractionRunId, databaseNow);
        await db.Documents.AddAsync(document, cancellationToken);
        await db.ExtractionJobs.AddAsync(job, cancellationToken);
        if (intent is not null)
            db.DocumentProcessingRequests.Add(new(document.OrganizationId, intent.ActorId, "Upload", intent.Key,
                intent.PayloadHash, document.Id, extractionRunId, jobId, 1));
        if (scan is not null && audit?.IsBound == true)
            db.SecurityAuditEvents.Add(audit.Event("document.scan-clean", "document", document.Id, "Succeeded", "clamav-instream-v1",
                after: $"sha256={scan.Sha256};bytes={scan.SizeBytes}"));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (document, job);
    }
}
