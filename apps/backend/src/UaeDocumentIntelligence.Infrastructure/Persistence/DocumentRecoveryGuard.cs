using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

public sealed class DocumentRecoveryGuard(AppDbContext db, DocumentDeletionJournal journal)
{
    public async Task VerifyAsync(Guid? organizationId, bool requireCompleted, CancellationToken ct)
    {
        if (!journal.Enabled) return;
        var intents = journal.ReadAll().Where(x => organizationId == null || x.OrganizationId == organizationId).ToArray();
        var records = await db.DocumentDeletions.AsNoTracking().Where(x => organizationId == null || x.OrganizationId == organizationId).Take(1001).ToArrayAsync(ct);
        if (records.Length > 1000 || records.Any(r => !intents.Contains(DeletionIntent.From(r)))) throw new InvalidOperationException("Deletion journal is incomplete.");
        foreach (var intent in intents)
        {
            var record = records.SingleOrDefault(x => x.Id == intent.Id);
            if (record is null || await db.Documents.AnyAsync(x => x.OrganizationId == intent.OrganizationId && x.Id == intent.DocumentId, ct) ||
                (record.CompletedAtUtc.HasValue || requireCompleted) && journal.SourceExists(intent) || requireCompleted && !record.CompletedAtUtc.HasValue)
                throw new InvalidOperationException("Restore would reintroduce deleted content or an unresolved deletion.");
        }
    }

    // Export from the current authoritative database before restore, and retain outside
    // the application backup set. Exact state comparison detects missing/released holds.
    public async Task<string> ControlSnapshotAsync(CancellationToken ct)
    {
        // A complete offline source check supplies one read-only snapshot for both
        // controls and registered files. Do not commit a transaction owned by it.
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct) : null;
        var policies = await db.OrganizationRetentionPolicies.AsNoTracking().OrderBy(x => x.OrganizationId).ToArrayAsync(ct);
        var holds = await db.RetentionHolds.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(ct);
        var capacity = await db.OrganizationCapacityPolicies.AsNoTracking().OrderBy(x => x.OrganizationId).ToArrayAsync(ct);
        var snapshot = JsonSerializer.Serialize(new { Schema = 2, journal.JournalId, Policies = policies, Holds = holds, CapacityPolicies = capacity });
        if (transaction is not null) await transaction.CommitAsync(ct);
        return snapshot;
    }
    public async Task VerifyControlSnapshotAsync(string expected, CancellationToken ct)
    {
        // Compare JSON values: formatting, escaping and object property order carry no authority.
        // Array membership/order, identity, versions and hold state must match exactly.
        if (!JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(await ControlSnapshotAsync(ct))))
            throw new InvalidOperationException("Recovery controls differ from the authoritative snapshot or use an older schema.");
        await VerifyAsync(null, true, ct);
    }
}
