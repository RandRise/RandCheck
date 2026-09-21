using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

public sealed class CapacityOptions
{
    public bool RequirePolicy { get; set; }
}

public sealed record CapacityUsage(Guid OrganizationId, Guid? PolicyVersion, int? MaxActiveJobs,
    long? MaxStoredBytes, long ActiveJobs, decimal LiveBytes, decimal PendingDeletionBytes);

public static class OrganizationCapacity
{
    // All increasing admissions lock organization first, before document/job locks.
    // Terminal worker transitions only decrease active usage and need no quota writes.
    public static async Task LockAsync(AppDbContext db, Guid organizationId, CancellationToken ct)
    {
        if (!db.Database.IsNpgsql() || db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != System.Data.IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Capacity admission requires a PostgreSQL Read Committed transaction.");
        // Serialize admissions without blocking foreign-key checks in worker audit
        // inserts; FOR UPDATE here could deadlock with a worker holding a document row.
        var rows = await db.Organizations.FromSqlInterpolated($"SELECT * FROM organizations WHERE id={organizationId} FOR NO KEY UPDATE")
            .AsNoTracking().ToArrayAsync(ct);
        if (rows.Length != 1) throw new OrganizationCapacityException(CapacityFailure.Unconfigured);
    }

    public static async Task<CapacityUsage> ReadAsync(AppDbContext db, Guid organizationId, CancellationToken ct)
    {
        var policy = await db.OrganizationCapacityPolicies.AsNoTracking().SingleOrDefaultAsync(p => p.OrganizationId == organizationId, ct);
        var active = await db.ExtractionJobs.LongCountAsync(j => j.OrganizationId == organizationId &&
            (j.Status == ExtractionJobStatus.Pending || j.Status == ExtractionJobStatus.Leased || j.Status == ExtractionJobStatus.RetryScheduled), ct);
        var live = await db.Documents.Where(d => d.OrganizationId == organizationId).SumAsync(d => (decimal)d.SizeBytes, ct);
        var pending = await db.DocumentDeletions.Where(d => d.OrganizationId == organizationId && d.CompletedAtUtc == null)
            .SumAsync(d => (decimal)d.SizeBytes, ct);
        return new(organizationId, policy?.Version, policy?.MaxActiveJobs, policy?.MaxStoredBytes, active, live, pending);
    }

    // The caller holds the organization lock through the admission commit. Read usage
    // afresh after acquiring that lock; no process-local counters or worker settlements.
    public static async Task CheckLockedAsync(AppDbContext db, Guid organizationId, long additionalBytes, bool requirePolicy, CancellationToken ct)
    {
        var usage = await ReadAsync(db, organizationId, ct);
        if (usage.PolicyVersion is null)
        {
            if (requirePolicy) throw new OrganizationCapacityException(CapacityFailure.Unconfigured);
            return;
        }
        if (usage.ActiveJobs >= usage.MaxActiveJobs) throw new OrganizationCapacityException(CapacityFailure.Queue);
        // Reprocessing consumes no additional source bytes and is allowed while a
        // lowered storage limit is exceeded. Uploads must fit the current byte limit.
        if (additionalBytes > 0 && usage.LiveBytes + usage.PendingDeletionBytes + additionalBytes > usage.MaxStoredBytes)
            throw new OrganizationCapacityException(CapacityFailure.Storage);
    }

    public static async Task<CapacityUsage> ConfigureAsync(AppDbContext db, Guid organizationId, int jobs, long bytes, Guid? expectedVersion, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        await LockAsync(db, organizationId, ct);
        var policy = await db.OrganizationCapacityPolicies.SingleOrDefaultAsync(p => p.OrganizationId == organizationId, ct);
        if (policy?.Version != expectedVersion) throw new InvalidOperationException("Capacity policy version changed.");
        var before = policy is null ? null : $"jobs={policy.MaxActiveJobs};bytes={policy.MaxStoredBytes}";
        if (policy is null) db.OrganizationCapacityPolicies.Add(policy = new(organizationId, jobs, bytes, DateTimeOffset.UtcNow));
        else policy.Change(jobs, bytes, DateTimeOffset.UtcNow);
        db.SecurityAuditEvents.Add(new SecurityAuditEvent(organizationId, null, "System", "capacity.policy-changed",
            "capacity-policy", organizationId, "Succeeded", "committed", Guid.NewGuid().ToString("N"),
            beforeState: before, afterState: $"jobs={jobs};bytes={bytes}"));
        await db.SaveChangesAsync(ct);
        var result = await ReadAsync(db, organizationId, ct);
        await tx.CommitAsync(ct); return result;
    }
}
