using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal sealed class ExtractionJobQueue(AppDbContext db, IClock clock) : IExtractionJobQueue
{
    internal static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(180);
    internal static readonly TimeSpan AttemptDuration = TimeSpan.FromSeconds(120);
    private static readonly HashSet<string> RetryableCodes =
    [
        "provider_network_error", "provider_timeout", "provider_throttled", "provider_unavailable"
    ];

    public async Task<bool> TerminalizeOneExpiredAsync(CancellationToken cancellationToken)
    {
        EnsurePostgres();
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,
            cancellationToken);
        var id = await LockOneIdAsync("""
            SELECT id
            FROM extraction_jobs
            WHERE status = 'Leased'
              AND attempt_count >= 3
              AND lease_expires_at_utc <= clock_timestamp()
            ORDER BY lease_expires_at_utc, id
            FOR UPDATE SKIP LOCKED
            LIMIT 1
            """, [], cancellationToken);
        if (!id.HasValue) return false;

        var now = await DatabaseNowAsync(cancellationToken);
        var job = await db.ExtractionJobs.SingleAsync(x => x.Id == id.Value, cancellationToken);
        var document = await LoadDocumentAsync(job.OrganizationId, job.DocumentId, cancellationToken);
        var run = document.ExtractionRuns.Single(x => x.Id == job.ExtractionRunId);
        job.FailExpired("worker_lease_expired", "The extraction worker lease expired after the final claim.", now);
        var businessNow = BusinessNow(document, run);
        run.Fail("worker_lease_expired", "The extraction worker lease expired after the final claim.", businessNow);
        document.RecordExtractionFailure(run, businessNow);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<ClaimedExtractionJob?> ClaimAsync(string owner, CancellationToken cancellationToken)
    {
        EnsurePostgres();
        if (string.IsNullOrWhiteSpace(owner) || owner.Length > 100)
            throw new ArgumentException("Worker owner must contain 1 to 100 characters.", nameof(owner));
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,
            cancellationToken);
        var id = await LockOneIdAsync("""
            SELECT id
            FROM extraction_jobs
            WHERE attempt_count < 3
              AND (
                (status IN ('Pending','RetryScheduled') AND available_at_utc <= clock_timestamp())
                OR (status = 'Leased' AND lease_expires_at_utc <= clock_timestamp())
              )
            ORDER BY available_at_utc, created_at_utc, id
            FOR UPDATE SKIP LOCKED
            LIMIT 1
            """, [], cancellationToken);
        if (!id.HasValue) return null;

        var now = await DatabaseNowAsync(cancellationToken);
        var job = await db.ExtractionJobs.SingleAsync(x => x.Id == id.Value, cancellationToken);
        var run = await db.ExtractionRuns.SingleAsync(x => x.OrganizationId == job.OrganizationId &&
            x.DocumentId == job.DocumentId && x.Id == job.ExtractionRunId, cancellationToken);
        var document = await db.Documents.SingleAsync(x => x.OrganizationId == job.OrganizationId &&
            x.Id == job.DocumentId, cancellationToken);
        var token = Guid.NewGuid();
        job.Lease(owner, token, now, LeaseDuration);
        if (run.Status == ExtractionRunStatus.Queued)
            run.Start(Latest(clock.UtcNow, run.CreatedAtUtc));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ClaimedExtractionJob(job.Id, job.OrganizationId, job.DocumentId, run.Id,
            document.StorageKey, document.Sha256, document.SizeBytes, document.DeclaredContentType,
            run.ProviderName, run.ProviderVersion, run.ExtractionSchemaVersion, run.CorrelationId,
            job.AttemptCount, token, owner, now.Add(AttemptDuration), job.CreatedAtUtc);
    }

    public async Task<bool> HeartbeatAsync(Guid jobId, string owner, Guid leaseToken,
        CancellationToken cancellationToken)
    {
        EnsurePostgres();
        var rows = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE extraction_jobs
            SET last_heartbeat_at_utc = clock_timestamp(),
                lease_expires_at_utc = clock_timestamp() + interval '180 seconds',
                updated_at_utc = clock_timestamp()
            WHERE id = {jobId}
              AND status = 'Leased'
              AND lease_owner = {owner}
              AND lease_token = {leaseToken}
              AND lease_expires_at_utc > clock_timestamp()
            """, cancellationToken);
        return rows == 1;
    }

    public async Task<bool> CompleteAsync(ClaimedExtractionJob claim, ExtractionResult result,
        CancellationToken cancellationToken)
    {
        EnsurePostgres();
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,
            cancellationToken);
        if (!await LockClaimAsync(claim, cancellationToken)) return false;
        var now = await DatabaseNowAsync(cancellationToken);
        var job = await db.ExtractionJobs.SingleAsync(x => x.Id == claim.JobId, cancellationToken);
        var document = await LoadDocumentAsync(claim.OrganizationId, claim.DocumentId, cancellationToken);
        var run = document.ExtractionRuns.Single(x => x.Id == claim.ExtractionRunId);
        var businessNow = BusinessNow(document, run);
        ValidateIdentity(claim, result);
        ExtractionResultContract.Apply(run, result, businessNow);
        document.RecordExtractionSuccess(run, businessNow);
        job.Succeed(claim.LeaseOwner, claim.LeaseToken,
            result.Outcome == ExtractionOutcome.Ready
                ? ExtractionOutcomeValue.Ready
                : ExtractionOutcomeValue.Partial, now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RecordFailureAsync(ClaimedExtractionJob claim, string code,
        string safeSummary, bool retryable, CancellationToken cancellationToken)
    {
        EnsurePostgres();
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,
            cancellationToken);
        if (!await LockClaimAsync(claim, cancellationToken)) return false;
        var now = await DatabaseNowAsync(cancellationToken);
        var job = await db.ExtractionJobs.SingleAsync(x => x.Id == claim.JobId, cancellationToken);
        var approvedRetry = retryable && RetryableCodes.Contains(code);
        if (approvedRetry && job.AttemptCount < ExtractionJob.MaxAttempts)
        {
            var delay = job.AttemptCount == 1 ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(30);
            job.ScheduleRetry(claim.LeaseOwner, claim.LeaseToken, code, safeSummary, now, delay);
        }
        else
        {
            var document = await LoadDocumentAsync(claim.OrganizationId, claim.DocumentId, cancellationToken);
            var run = document.ExtractionRuns.Single(x => x.Id == claim.ExtractionRunId);
            var businessNow = BusinessNow(document, run);
            job.Fail(claim.LeaseOwner, claim.LeaseToken, code, safeSummary, now);
            run.Fail(code, safeSummary, businessNow);
            document.RecordExtractionFailure(run, businessNow);
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CancelAsync(Guid organizationId, Guid jobId, string code,
        string safeSummary, CancellationToken cancellationToken)
    {
        EnsurePostgres();
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,
            cancellationToken);
        var id = await LockOneIdAsync("""
            SELECT id
            FROM extraction_jobs
            WHERE id = @job_id
              AND organization_id = @organization_id
              AND status IN ('Pending','Leased','RetryScheduled')
            FOR UPDATE
            """,
            [("job_id", jobId), ("organization_id", organizationId)], cancellationToken);
        if (!id.HasValue) return false;
        var now = await DatabaseNowAsync(cancellationToken);
        var job = await db.ExtractionJobs.SingleAsync(x => x.Id == jobId, cancellationToken);
        var document = await LoadDocumentAsync(organizationId, job.DocumentId, cancellationToken);
        var run = document.ExtractionRuns.Single(x => x.Id == job.ExtractionRunId);
        var businessNow = BusinessNow(document, run);
        job.Cancel(code, safeSummary, now);
        run.Cancel(code, safeSummary, businessNow);
        document.RecordExtractionFailure(run, businessNow);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<ExtractionJobSnapshot?> GetLatestAsync(Guid organizationId, Guid documentId,
        CancellationToken cancellationToken) => await db.ExtractionJobs.AsNoTracking()
        .Where(job => job.OrganizationId == organizationId && job.DocumentId == documentId)
        .OrderByDescending(job => job.CreatedAtUtc).ThenByDescending(job => job.Id)
        .Select(job => new ExtractionJobSnapshot(job.Id, job.ExtractionRunId, job.Status,
            job.AttemptCount, job.LastErrorCode, job.LastSafeErrorSummary, job.ResultOutcome,
            job.AvailableAtUtc, job.LeaseExpiresAtUtc, job.CreatedAtUtc, job.UpdatedAtUtc,
            job.CompletedAtUtc))
        .FirstOrDefaultAsync(cancellationToken);

    public async Task<QueuedReprocess?> QueueReprocessAsync(Guid organizationId, Guid documentId,
        Guid runId, Guid jobId, string providerAlias, string providerVersion,
        string schemaVersion, string correlationId, CancellationToken cancellationToken)
    {
        EnsurePostgres();
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,
            cancellationToken);
        var lockedDocumentId = await LockOneIdAsync("""
            SELECT id
            FROM documents
            WHERE id = @document_id AND organization_id = @organization_id
            FOR UPDATE
            """, [("document_id", documentId), ("organization_id", organizationId)],
            cancellationToken);
        if (!lockedDocumentId.HasValue) return null;
        if (await db.ExtractionJobs.AnyAsync(job => job.OrganizationId == organizationId &&
                job.DocumentId == documentId &&
                (job.Status == ExtractionJobStatus.Pending || job.Status == ExtractionJobStatus.Leased ||
                 job.Status == ExtractionJobStatus.RetryScheduled), cancellationToken))
            return null;

        var document = await db.Documents.Include(item => item.ExtractionRuns)
            .SingleAsync(item => item.OrganizationId == organizationId && item.Id == documentId,
                cancellationToken);
        var runNumber = document.ExtractionRuns.Select(run => run.RunNumber).DefaultIfEmpty().Max() + 1;
        var businessNow = Latest(clock.UtcNow, document.UpdatedAtUtc);
        document.QueueExtraction(runId, runNumber, providerAlias, providerVersion, schemaVersion,
            correlationId, businessNow);
        var databaseNow = await DatabaseNowAsync(cancellationToken);
        var job = new ExtractionJob(jobId, organizationId, documentId, runId, databaseNow);
        await db.ExtractionJobs.AddAsync(job, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new QueuedReprocess(jobId, runId, runNumber);
    }

    private async Task<bool> LockClaimAsync(ClaimedExtractionJob claim,
        CancellationToken cancellationToken)
    {
        var id = await LockOneIdAsync("""
            SELECT id
            FROM extraction_jobs
            WHERE id = @job_id
              AND organization_id = @organization_id
              AND status = 'Leased'
              AND lease_owner = @lease_owner
              AND lease_token = @lease_token
              AND lease_expires_at_utc > clock_timestamp()
            FOR UPDATE
            """,
            [("job_id", claim.JobId), ("organization_id", claim.OrganizationId),
             ("lease_owner", claim.LeaseOwner), ("lease_token", claim.LeaseToken)],
            cancellationToken);
        return id.HasValue;
    }

    private async Task<Guid?> LockOneIdAsync(string sql,
        IReadOnlyList<(string Name, object Value)> parameters, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        foreach (var item in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = item.Name;
            parameter.Value = item.Value;
            command.Parameters.Add(parameter);
        }
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid id ? id : null;
    }

    private async Task<DateTimeOffset> DatabaseNowAsync(CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT clock_timestamp()";
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value switch
        {
            DateTimeOffset timestamp => timestamp,
            DateTime timestamp => new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)),
            _ => throw new InvalidOperationException("Database clock was unavailable.")
        };
    }

    private Task<Document> LoadDocumentAsync(Guid organizationId, Guid documentId,
        CancellationToken cancellationToken) => db.Documents
        .AsSplitQuery()
        .Include(x => x.ExtractionRuns).ThenInclude(x => x.Pages)
        .Include(x => x.ExtractionRuns).ThenInclude(x => x.Fields).ThenInclude(x => x.Evidence)
        .Include(x => x.ExtractionRuns).ThenInclude(x => x.Warnings)
        .SingleAsync(x => x.OrganizationId == organizationId && x.Id == documentId, cancellationToken);

    private static void ValidateIdentity(ClaimedExtractionJob claim, ExtractionResult result)
    {
        if (result.SchemaVersion != claim.SchemaVersion || result.Provider.Alias != claim.ProviderAlias ||
            result.Provider.AdapterVersion != claim.ProviderVersion ||
            string.IsNullOrWhiteSpace(result.Provider.Model) || result.Provider.Model.Length > 100 ||
            string.IsNullOrWhiteSpace(result.Provider.ModelVersion) || result.Provider.ModelVersion.Length > 100)
            throw new InvalidDataException("Provider result identity does not match the claimed job.");
    }

    private DateTimeOffset BusinessNow(Document document, ExtractionRun run) =>
        Latest(clock.UtcNow, document.UpdatedAtUtc,
            run.StartedAtUtc ?? run.CreatedAtUtc);

    private static DateTimeOffset Latest(params DateTimeOffset[] values) => values.Max();

    private void EnsurePostgres()
    {
        if (!db.Database.IsNpgsql())
            throw new InvalidOperationException("The durable extraction queue requires PostgreSQL.");
    }
}
