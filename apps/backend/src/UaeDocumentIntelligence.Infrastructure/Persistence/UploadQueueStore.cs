using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal sealed class UploadQueueStore(AppDbContext db) : IUploadQueueStore
{
    public async Task<ExtractionJob> AddAsync(Document document, Guid extractionRunId, Guid jobId,
        CancellationToken cancellationToken)
    {
        if (!db.Database.IsNpgsql())
            throw new InvalidOperationException("The durable extraction queue requires PostgreSQL.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
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
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return job;
    }
}
