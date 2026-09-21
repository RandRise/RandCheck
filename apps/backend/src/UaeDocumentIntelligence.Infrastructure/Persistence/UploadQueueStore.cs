using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using UaeDocumentIntelligence.Infrastructure.Services;
using UaeDocumentIntelligence.Application.Documents.Upload;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal sealed class UploadQueueStore(AppDbContext db, IOptions<StorageOptions>? storage = null, SecurityAuditContext? audit = null,
    IOptions<CapacityOptions>? capacity = null) : IUploadQueueStore
{
    public async Task CheckCapacityAsync(Guid organizationId, long bytes, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        await OrganizationCapacity.LockAsync(db, organizationId, ct);
        await OrganizationCapacity.CheckLockedAsync(db, organizationId, bytes, capacity?.Value.RequirePolicy == true, ct);
        // Advisory preflight only. Admission rechecks after scanning, in its commit transaction.
        await tx.CommitAsync(ct);
    }
    public Task<ExtractionJob> AddAsync(Document document, Guid extractionRunId, Guid jobId, CancellationToken ct) =>
        AddCoreAsync(document, extractionRunId, jobId, null, ct);
    public Task<ExtractionJob> AddScannedAsync(Document document, Guid extractionRunId, Guid jobId, FileScanReceipt scan, CancellationToken ct) =>
        AddCoreAsync(document, extractionRunId, jobId, scan, ct);

    private async Task<ExtractionJob> AddCoreAsync(Document document, Guid extractionRunId, Guid jobId,
        FileScanReceipt? scan, CancellationToken cancellationToken)
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
        if (scan is not null && audit?.IsBound == true)
            db.SecurityAuditEvents.Add(audit.Event("document.scan-clean", "document", document.Id, "Succeeded", "clamav-instream-v1",
                after: $"sha256={scan.Sha256};bytes={scan.SizeBytes}"));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return job;
    }
}
