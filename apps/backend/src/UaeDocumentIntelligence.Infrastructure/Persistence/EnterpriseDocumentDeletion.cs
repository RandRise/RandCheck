using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

public sealed record DocumentDeletionPreview(Guid DocumentId, Guid DocumentVersion, Guid PolicyVersion,
    bool Enabled, string[] Blockers, int ExtractionRuns, int ExtractionJobs);
public sealed record DocumentDeletionView(Guid DocumentId, Guid DocumentVersion, Guid PolicyVersion,
    string State, DateTimeOffset RequestedAtUtc, DateTimeOffset? CompletedAtUtc);

public sealed partial class EnterpriseRetentionService
{
    public async Task<IReadOnlyList<DocumentDeletionView>> DeletionsAsync(int offset, CancellationToken ct)
    {
        ValidateOffset(offset); await RequireAdministrator(ct);
        return await db.DocumentDeletions.AsNoTracking().Where(x => x.OrganizationId == execution.OrganizationId)
            .OrderBy(x => x.DocumentId).Skip(offset).Take(100).Select(x => new DocumentDeletionView(x.DocumentId,
                x.DocumentVersion, x.PolicyVersion, x.CompletedAtUtc == null ? "PendingFileRemoval" : "Completed", x.RequestedAtUtc, x.CompletedAtUtc)).ToArrayAsync(ct);
    }
    public async Task<DocumentDeletionPreview> DeletionPreviewAsync(Guid documentId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAndAuthorize(ct);
        await LockDocument(documentId, ct);
        var document = await db.Documents.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == execution.OrganizationId && x.Id == documentId, ct)
            ?? throw new RetentionException("document_not_found", 404);
        var policy = await db.OrganizationRetentionPolicies.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == execution.OrganizationId, ct);
        var blockers = await DeletionBlockers(document, policy, ct);
        if (!deletionJournal.Enabled) blockers.Insert(0, "deployment_disabled");
        else deletionJournal.RequireRoot();
        var runs = await db.ExtractionRuns.CountAsync(x => x.OrganizationId == execution.OrganizationId && x.DocumentId == documentId, ct);
        var jobs = await db.ExtractionJobs.CountAsync(x => x.OrganizationId == execution.OrganizationId && x.DocumentId == documentId, ct);
        await tx.CommitAsync(ct);
        return new(documentId, document.ConcurrencyToken, policy?.Version ?? Guid.Empty, deletionJournal.Enabled, blockers.ToArray(), runs, jobs);
    }
    public async Task<DocumentDeletionView> DeleteDocumentAsync(Guid documentId, Guid documentVersion, Guid policyVersion, CancellationToken ct)
    {
        deletionJournal.RequireRoot();
        DocumentDeletion record;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await LockAndAuthorize(ct); await LockDocument(documentId, ct);
            var existing = await db.DocumentDeletions.SingleOrDefaultAsync(x => x.OrganizationId == execution.OrganizationId && x.DocumentId == documentId, ct);
            if (existing is not null)
            {
                if (existing.DocumentVersion != documentVersion || existing.PolicyVersion != policyVersion)
                    throw new RetentionException("deletion_version_conflict", 409);
                record = existing;
            }
            else
            {
                var document = await db.Documents.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == execution.OrganizationId && x.Id == documentId, ct)
                    ?? throw new RetentionException("document_not_found", 404);
                var policy = await db.OrganizationRetentionPolicies.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == execution.OrganizationId, ct);
                if (document.ConcurrencyToken != documentVersion || policy is null || policy.Version != policyVersion)
                    throw new RetentionException("deletion_version_conflict", 409);
                var blockers = await DeletionBlockers(document, policy, ct);
                if (blockers.Count != 0) throw new RetentionException(blockers[0], 409);
                // A prior intent can survive a transaction/audit failure. Reuse it only for exactly the same request.
                var intent = deletionJournal.Find(execution.OrganizationId, documentId) ?? new(Guid.NewGuid(), execution.OrganizationId,
                    document.Id, document.StorageKey, document.Sha256, document.SizeBytes, documentVersion, policyVersion, JournalTimestamp(clock.UtcNow));
                if (intent.DocumentVersion != documentVersion || intent.PolicyVersion != policyVersion || intent.StorageKey != document.StorageKey ||
                    intent.Sha256 != document.Sha256 || intent.SizeBytes != document.SizeBytes)
                    throw new RetentionException("unresolved_deletion_intent", 409);
                deletionJournal.Append(intent); // Durable before any content is removed; never overwritten or auto-pruned.
                record = intent.ToRecord(); db.DocumentDeletions.Add(record);
                await db.SaveChangesAsync(ct); // Audit and tombstone remain in this transaction.
                await db.ExtractionJobs.Where(x => x.OrganizationId == execution.OrganizationId && x.DocumentId == documentId).ExecuteDeleteAsync(ct);
                // Owned pages/fields/evidence/warnings cascade. Any unexpected historical FK rolls everything back.
                await db.ExtractionRuns.Where(x => x.OrganizationId == execution.OrganizationId && x.DocumentId == documentId).ExecuteDeleteAsync(ct);
                if (await db.Documents.Where(x => x.OrganizationId == execution.OrganizationId && x.Id == documentId).ExecuteDeleteAsync(ct) != 1)
                    throw new RetentionException("deletion_version_conflict", 409);
            }
            await tx.CommitAsync(ct);
        }
        // New transaction and fresh authority/hold check for resumable physical removal.
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await LockAndAuthorize(ct);
            await db.Entry(record).ReloadAsync(ct);
            var intent = DeletionIntent.From(record);
            if (deletionJournal.Find(execution.OrganizationId, documentId) != intent) throw new InvalidOperationException("Deletion journal mismatch.");
            if (record.CompletedAtUtc.HasValue)
            {
                if (deletionJournal.SourceExists(intent)) throw new RetentionException("restored_source_requires_recovery_review", 409);
                return DeletionView(record);
            }
            if (await HasHold(documentId, ct)) throw new RetentionException("retention_hold_active", 409);
            await sink.WriteAsync(audit.Event("document.source-purge-authorized", "document", documentId, "Succeeded", "committed-deletion"), ct);
            await deletionJournal.RemoveSourceAsync(intent, ct);
            record.Complete(clock.UtcNow); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        return DeletionView(record);
    }
    private async Task<List<string>> DeletionBlockers(Document d, OrganizationRetentionPolicy? policy, CancellationToken ct)
    {
        var blockers = new List<string>(); var org = execution.OrganizationId;
        if (policy?.DocumentDays is not int days) blockers.Add("retention_not_configured");
        else if (d.CreatedAtUtc > clock.UtcNow.AddDays(-days)) blockers.Add("retention_window_active");
        if (await HasHold(d.Id, ct)) blockers.Add("retention_hold_active");
        if (await db.ExtractionJobs.AnyAsync(x => x.OrganizationId == org && x.DocumentId == d.Id &&
                (x.Status == ExtractionJobStatus.Pending || x.Status == ExtractionJobStatus.Leased || x.Status == ExtractionJobStatus.RetryScheduled), ct) ||
            await db.ExtractionRuns.AnyAsync(x => x.OrganizationId == org && x.DocumentId == d.Id &&
                (x.Status == ExtractionRunStatus.Queued || x.Status == ExtractionRunStatus.Running), ct)) blockers.Add("processing_active");
        if (await db.ReviewSessions.AnyAsync(x => x.OrganizationId == org && x.DocumentId == d.Id, ct) ||
            await db.CitedDocumentIndexes.AnyAsync(x => x.OrganizationId == org && x.DocumentId == d.Id, ct) ||
            await db.ReconciliationDocumentLinks.AnyAsync(x => x.OrganizationId == org && x.DocumentId == d.Id, ct) ||
            await db.ReconciliationCases.AnyAsync(x => x.OrganizationId == org && (x.DraftInvoiceDocumentId == d.Id || x.DraftPurchaseOrderDocumentId == d.Id), ct) ||
            await db.ReconciliationDocumentSnapshots.AnyAsync(x => x.OrganizationId == org && x.DocumentId == d.Id, ct)) blockers.Add("historical_dependencies");
        if (await db.ExtractionRuns.AnyAsync(x => x.OrganizationId == org && x.DocumentId == d.Id &&
                (x.ProviderName != "local" || x.EstimatedCost != null || x.CostCurrency != null || x.PriceBasis != null || x.ModelName != null), ct)) blockers.Add("accounting_dependencies");
        return blockers;
    }
    private Task<bool> HasHold(Guid documentId, CancellationToken ct) => db.RetentionHolds.AnyAsync(x => x.OrganizationId == execution.OrganizationId &&
        x.ReleasedAtUtc == null && (x.DocumentId == null || x.DocumentId == documentId), ct);
    private Task<int> LockDocument(Guid documentId, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync(
        $"SELECT id FROM documents WHERE organization_id={execution.OrganizationId} AND id={documentId} FOR UPDATE", ct);
    private static DocumentDeletionView DeletionView(DocumentDeletion d) => new(d.DocumentId, d.DocumentVersion, d.PolicyVersion,
        d.CompletedAtUtc.HasValue ? "Completed" : "PendingFileRemoval", d.RequestedAtUtc, d.CompletedAtUtc);
    // PostgreSQL stores microseconds; canonicalize before writing either durable copy.
    private static DateTimeOffset JournalTimestamp(DateTimeOffset value) => new(value.UtcTicks - value.UtcTicks % 10, TimeSpan.Zero);
}
