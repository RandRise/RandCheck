using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

public sealed class RetentionException(string code, int status) : Exception(code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}
public sealed record RetentionPolicyView(Guid Version, int? DocumentDays, int? QuarantineHours);
public sealed record RetentionDocumentView(Guid DocumentId, DateTimeOffset CreatedAtUtc, string Disposition);
public sealed record RetentionPreview(bool DocumentDeletionEnabled, IReadOnlyList<RetentionDocumentView> Documents, int? NextOffset);
public sealed record RetentionHoldView(Guid Id, Guid? DocumentId, string Reason, DateTimeOffset CreatedAtUtc, DateTimeOffset? ReleasedAtUtc, Guid Version);
public sealed record QuarantineCleanupResult(bool Executed, int Eligible, int Deleted, int Preserved, bool Truncated);

public sealed partial class EnterpriseRetentionService(AppDbContext db, IExecutionContext execution, SecurityAuditContext audit,
    ISecurityAuditSink sink, IClock clock, IOptions<StorageOptions> storage, DocumentDeletionJournal deletionJournal)
{
    public async Task<RetentionPolicyView> PolicyAsync(CancellationToken ct)
    {
        await RequireAdministrator(ct);
        var policy = await db.OrganizationRetentionPolicies.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == execution.OrganizationId, ct);
        return policy is null ? new(Guid.Empty, null, null) : View(policy);
    }
    public async Task<RetentionPolicyView> ChangePolicyAsync(Guid version, int? documentDays, int? quarantineHours, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockAndAuthorize(ct);
        var policy = await db.OrganizationRetentionPolicies.SingleOrDefaultAsync(x => x.OrganizationId == execution.OrganizationId, ct);
        if ((policy?.Version ?? Guid.Empty) != version) throw new RetentionException("retention_version_conflict", 409);
        if (policy is null) db.OrganizationRetentionPolicies.Add(policy = new(execution.OrganizationId, documentDays, quarantineHours, clock.UtcNow));
        else policy.Change(documentDays, quarantineHours, clock.UtcNow);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return View(policy);
    }
    public async Task<IReadOnlyList<RetentionHoldView>> HoldsAsync(int offset, CancellationToken ct)
    {
        ValidateOffset(offset); await RequireAdministrator(ct);
        return await db.RetentionHolds.AsNoTracking().Where(x => x.OrganizationId == execution.OrganizationId)
            .OrderBy(x => x.Id).Skip(offset).Take(100)
            .Select(x => new RetentionHoldView(x.Id, x.DocumentId, x.Reason.ToString(), x.CreatedAtUtc, x.ReleasedAtUtc, x.Version)).ToArrayAsync(ct);
    }
    public async Task<RetentionHoldView> PlaceHoldAsync(Guid? documentId, RetentionHoldReason reason, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockAndAuthorize(ct);
        if (documentId.HasValue && !await db.Documents.AnyAsync(x => x.OrganizationId == execution.OrganizationId && x.Id == documentId, ct))
            throw new RetentionException("document_not_found", 404);
        var hold = new RetentionHold(Guid.NewGuid(), execution.OrganizationId, documentId, reason, clock.UtcNow);
        db.RetentionHolds.Add(hold); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return View(hold);
    }
    public async Task<RetentionHoldView> ReleaseHoldAsync(Guid id, Guid version, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockAndAuthorize(ct);
        var hold = await db.RetentionHolds.SingleOrDefaultAsync(x => x.OrganizationId == execution.OrganizationId && x.Id == id, ct)
            ?? throw new RetentionException("hold_not_found", 404);
        if (hold.Version != version || hold.ReleasedAtUtc.HasValue) throw new RetentionException("retention_version_conflict", 409);
        hold.Release(clock.UtcNow); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return View(hold);
    }
    public async Task<RetentionPreview> PreviewAsync(int offset, CancellationToken ct)
    {
        ValidateOffset(offset);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockAndAuthorize(ct);
        var policy = await db.OrganizationRetentionPolicies.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == execution.OrganizationId, ct);
        var cutoff = policy?.DocumentDays is int days ? clock.UtcNow.AddDays(-days) : DateTimeOffset.MinValue;
        var documents = await db.Documents.AsNoTracking().Where(x => x.OrganizationId == execution.OrganizationId)
            .OrderBy(x => x.Id).Skip(offset).Take(101).Select(x => new RetentionDocumentView(x.Id, x.CreatedAtUtc,
                db.RetentionHolds.Any(h => h.OrganizationId == execution.OrganizationId && h.ReleasedAtUtc == null &&
                    (h.DocumentId == null || h.DocumentId == x.Id)) ? "Held" :
                    x.CreatedAtUtc <= cutoff ? "PastRetentionWindow" : "Retained")).ToArrayAsync(ct);
        await transaction.CommitAsync(ct);
        return new(deletionJournal.Enabled, documents.Take(100).ToArray(), documents.Length > 100 ? offset + 100 : null);
    }
    public async Task<QuarantineCleanupResult> CleanupAsync(Guid version, bool execute, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // NO KEY UPDATE serializes policy/hold/member writes while allowing the independent audit FK checks.
        await LockAndAuthorize(ct);
        var policy = await db.OrganizationRetentionPolicies.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == execution.OrganizationId, ct);
        if (policy is null || policy.Version != version) throw new RetentionException("retention_version_conflict", 409);
        if (policy.QuarantineHours is not int hours) throw new RetentionException("quarantine_cleanup_disabled", 409);
        var root = QuarantineFiles.Root(storage.Value);
        var candidates = QuarantineFiles.Candidates(root, execution.OrganizationId);
        var eligible = 0; var deleted = 0; var preserved = 0;
        foreach (var candidate in candidates.Take(100))
        {
            ct.ThrowIfCancellationRequested();
            // Any hold wins. A persisted document is always preserved, even if its policy window elapsed.
            if (await db.RetentionHolds.AnyAsync(h => h.OrganizationId == execution.OrganizationId && h.ReleasedAtUtc == null &&
                    (h.DocumentId == null || h.DocumentId == candidate.DocumentId), ct) ||
                await db.Documents.AnyAsync(d => d.OrganizationId == execution.OrganizationId && d.Id == candidate.DocumentId, ct))
            { preserved++; continue; }
            using var lease = QuarantineFiles.TryLease(root, candidate, clock.UtcNow.AddHours(-hours));
            if (lease is null) { preserved++; continue; }
            eligible++;
            if (!execute) continue;
            await sink.WriteAsync(audit.Event("quarantine.cleanup-authorized", "quarantine-upload", candidate.UploadId,
                "Succeeded", "expired-abandoned-upload", after: $"document={candidate.DocumentId:D};policy={version:D}"), ct);
            try { QuarantineFiles.DeleteStaged(root, candidate); }
            catch (IOException)
            {
                await sink.WriteAsync(audit.Event("quarantine.cleanup-failed", "quarantine-upload", candidate.UploadId, "Failed", "storage_unavailable"), ct);
                throw new RetentionException("quarantine_cleanup_incomplete", 503);
            }
            deleted++;
            // Filesystem and DB cannot commit atomically. A durable authorization without completion is unresolved.
            await sink.WriteAsync(audit.Event("quarantine.cleaned", "quarantine-upload", candidate.UploadId,
                "Succeeded", "staged-file-removed"), ct);
            lease.Dispose();
            // Only empty coordination metadata remains; an uncertain completion keeps its marker.
            var leaseKey = QuarantineFiles.LeaseKey(candidate.Key);
            LocalDocumentStorage.EnsureNoReparsePoints(root, leaseKey);
            try { File.Delete(LocalDocumentStorage.ResolveUnderRoot(root, leaseKey)); }
            catch (IOException) { /* A competing preview may hold the empty marker briefly. */ }
        }
        await transaction.CommitAsync(ct);
        return new(execute, eligible, deleted, preserved, candidates.Count > 100);
    }

    private async Task LockAndAuthorize(CancellationToken ct)
    {
        if (!db.Database.IsNpgsql()) throw new InvalidOperationException("Retention administration requires PostgreSQL.");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM organizations WHERE id={execution.OrganizationId} FOR NO KEY UPDATE", ct);
        await RequireAdministrator(ct);
    }
    private async Task RequireAdministrator(CancellationToken ct)
    {
        if (!audit.IsBound || audit.ActorKind != "Human" || audit.OrganizationId != execution.OrganizationId || audit.ActorId != execution.ActorId ||
            !await (from m in db.OrganizationMemberships.AsNoTracking() join a in db.Actors.AsNoTracking() on m.ActorId equals a.Id
                where m.OrganizationId == execution.OrganizationId && m.ActorId == execution.ActorId && m.IsActive && m.Role == OrganizationAccessRole.Admin &&
                    a.OrganizationId == execution.OrganizationId && a.IsActive && a.Kind == ActorKind.Human select m.Id).AnyAsync(ct))
            throw new RetentionException("retention_administration_denied", 403);
    }
    private static void ValidateOffset(int offset)
    { if (offset is < 0 or > 10000) throw new RetentionException("invalid_retention_offset", 400); }
    private static RetentionPolicyView View(OrganizationRetentionPolicy p) => new(p.Version, p.DocumentDays, p.QuarantineHours);
    private static RetentionHoldView View(RetentionHold h) => new(h.Id, h.DocumentId, h.Reason.ToString(), h.CreatedAtUtc, h.ReleasedAtUtc, h.Version);
}
