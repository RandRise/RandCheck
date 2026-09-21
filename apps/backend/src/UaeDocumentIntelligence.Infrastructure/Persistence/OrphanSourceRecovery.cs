using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

public sealed record OrphanSource(Guid OrganizationId, Guid DocumentId, string StorageKey,
    long SizeBytes, string Sha256, DateTime LastWriteUtc);
public sealed record OrphanRecoveryPlan(int Schema, Guid Id, string Database, Guid JournalId, string UploadRoot,
    string ControlSha256, DateTimeOffset CreatedAtUtc, int MinimumAgeHours, OrphanSource[] Sources,
    int Preserved, int Unrecognized, bool Truncated);
public sealed record OrphanRecoveryResult(int Removed, int Preserved, int AlreadyAbsent);

// Private offline maintenance only. The caller must stop all uploaders/workers and
// storage writers before inventory and keep them stopped through execution. Row locks
// protect current database state, but cannot fence a file awaiting upload admission.
public sealed class OrphanSourceRecovery(AppDbContext db, DocumentDeletionJournal journal,
    StorageOptions storage, ISecurityAuditSink sink)
{
    private const int MaxEntries = 1000;
    private const int MaxSources = 100;
    private string Root()
    {
        journal.RequireRoot();
        _ = QuarantineFiles.Root(storage);
        if (!Path.IsPathFullyQualified(storage.UploadDirectory)) throw new InvalidOperationException();
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(storage.UploadDirectory));
        LocalDocumentStorage.EnsureNoReparsePoints(root, "organizations");
        if (!Directory.Exists(root)) throw new InvalidOperationException();
        return root;
    }
    private static void RequireOffline(bool confirmed)
    {
        if (!confirmed) throw new InvalidOperationException("Offline maintenance must be explicitly confirmed.");
    }
    private static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static bool CanonicalId(string value, out Guid id) => Guid.TryParseExact(value, "N", out id) && id != Guid.Empty && value == id.ToString("N");
    private static void ValidateSource(OrphanSource source)
    {
        var prefix = $"organizations/{source.OrganizationId:N}/documents/{source.DocumentId:N}/source.";
        if (source.OrganizationId == Guid.Empty || source.DocumentId == Guid.Empty || source.StorageKey is null ||
            !source.StorageKey.StartsWith(prefix, StringComparison.Ordinal) ||
            source.StorageKey[prefix.Length..].Length is < 1 or > 9 ||
            source.StorageKey[prefix.Length..].Any(c => !char.IsAsciiLetterOrDigit(c)) ||
            source.SizeBytes is < 1 or > 10485760 || source.LastWriteUtc.Kind != DateTimeKind.Utc ||
            source.Sha256 is null || source.Sha256.Length != 64 || source.Sha256.Any(c => !(c is >= 'a' and <= 'f' or >= '0' and <= '9')))
            throw new InvalidDataException();
    }
    private async Task<bool> ProtectedAsync(OrphanSource source, CancellationToken ct) =>
        !await db.Organizations.AnyAsync(x => x.Id == source.OrganizationId, ct) ||
        await db.Documents.AnyAsync(x => x.Id == source.DocumentId || x.StorageKey == source.StorageKey, ct) ||
        await db.DocumentDeletions.AnyAsync(x => x.DocumentId == source.DocumentId || x.StorageKey == source.StorageKey, ct) ||
        await db.RetentionHolds.AnyAsync(x => x.OrganizationId == source.OrganizationId && x.ReleasedAtUtc == null &&
            (x.DocumentId == null || x.DocumentId == source.DocumentId), ct) ||
        journal.ReadAll().Any(x => x.DocumentId == source.DocumentId || x.StorageKey == source.StorageKey);

    public async Task<OrphanRecoveryPlan> InventoryAsync(string controls, int minimumAgeHours, bool offlineConfirmed, CancellationToken ct)
    {
        RequireOffline(offlineConfirmed);
        if (minimumAgeHours is < 24 or > 87600) throw new ArgumentOutOfRangeException(nameof(minimumAgeHours));
        var root = Root(); var now = DateTimeOffset.UtcNow;
        await new DocumentRecoveryGuard(db, journal).VerifyControlSnapshotAsync(controls, ct);
        var sources = new List<OrphanSource>(); var visited = 0; var preserved = 0; var unknown = 0; var truncated = false;
        // Traverse only the canonical five-component source layout. Never recurse into
        // unrecognized directories or reparse points. The entry budget is global.
        async Task Walk(string relative, int depth, Guid organization, Guid document)
        {
            var directory = relative.Length == 0 ? root : LocalDocumentStorage.ResolveUnderRoot(root, relative);
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                ct.ThrowIfCancellationRequested();
                if (++visited > MaxEntries || sources.Count >= MaxSources) { truncated = true; return; }
                var name = Path.GetFileName(entry); var key = relative.Length == 0 ? name : relative + "/" + name;
                var attributes = File.GetAttributes(entry);
                if (attributes.HasFlag(FileAttributes.ReparsePoint)) { unknown++; continue; }
                LocalDocumentStorage.EnsureNoReparsePoints(root, key);
                if (depth < 4)
                {
                    var nextOrg = organization; var nextDoc = document;
                    var valid = depth switch { 0 => name == "organizations", 1 => CanonicalId(name, out nextOrg),
                        2 => name == "documents", 3 => CanonicalId(name, out nextDoc), _ => false };
                    if (!valid || !attributes.HasFlag(FileAttributes.Directory)) { unknown++; continue; }
                    await Walk(key, depth + 1, nextOrg, nextDoc);
                    if (truncated) return;
                    continue;
                }
                if (attributes.HasFlag(FileAttributes.Directory) || !name.StartsWith("source.", StringComparison.Ordinal) ||
                    name[7..].Length is < 1 or > 9 || name[7..].Any(c => !char.IsAsciiLetterOrDigit(c))) { unknown++; continue; }
                var info = new FileInfo(entry);
                var candidate = new OrphanSource(organization, document, key, info.Length, "", info.LastWriteTimeUtc);
                if (candidate.SizeBytes is < 1 or > 10485760 || candidate.LastWriteUtc > now.AddHours(-minimumAgeHours).UtcDateTime ||
                    await ProtectedAsync(candidate, ct)) { preserved++; continue; }
                try
                {
                    await using var file = new FileStream(entry, FileMode.Open, FileAccess.Read, FileShare.None, 65536, FileOptions.Asynchronous);
                    if (file.Length != candidate.SizeBytes) { preserved++; continue; }
                    candidate = candidate with { Sha256 = Convert.ToHexString(await SHA256.HashDataAsync(file, ct)).ToLowerInvariant() };
                    sources.Add(candidate);
                }
                catch (IOException) { preserved++; }
            }
        }
        await Walk("", 0, Guid.Empty, Guid.Empty);
        return new(1, Guid.NewGuid(), db.Database.GetDbConnection().Database, journal.JournalId, root,
            Digest(controls), now, minimumAgeHours, sources.ToArray(), preserved, unknown, truncated);
    }

    public async Task<OrphanRecoveryResult> ExecuteAsync(OrphanRecoveryPlan plan, string controls, bool offlineConfirmed, CancellationToken ct)
    {
        RequireOffline(offlineConfirmed); var root = Root(); var now = DateTimeOffset.UtcNow;
        if (plan.Schema != 1 || plan.Id == Guid.Empty || plan.Database != db.Database.GetDbConnection().Database ||
            plan.JournalId != journal.JournalId || plan.UploadRoot != root || plan.ControlSha256 != Digest(controls) ||
            plan.CreatedAtUtc > now || plan.CreatedAtUtc < now.AddHours(-24) || plan.MinimumAgeHours is < 24 or > 87600 ||
            plan.Sources is null || plan.Sources.Length > MaxSources || plan.Sources.Select(x => x.StorageKey).Distinct().Count() != plan.Sources.Length)
            throw new InvalidDataException("Orphan recovery plan is invalid or expired.");
        foreach (var source in plan.Sources)
        {
            ValidateSource(source);
            if (source.LastWriteUtc > plan.CreatedAtUtc.AddHours(-plan.MinimumAgeHours).UtcDateTime) throw new InvalidDataException();
        }
        await new DocumentRecoveryGuard(db, journal).VerifyControlSnapshotAsync(controls, ct);
        var removed = 0; var preserved = 0; var absent = 0;
        foreach (var source in plan.Sources)
        {
            ct.ThrowIfCancellationRequested();
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
            // Allows independent authorization-audit FK checks while serializing holds/admission.
            await OrganizationCapacity.LockAsync(db, source.OrganizationId, ct);
            if (await ProtectedAsync(source, ct)) { preserved++; continue; }
            var path = LocalDocumentStorage.ResolveDocumentSourcePath(root, source.OrganizationId, source.DocumentId, source.StorageKey);
            try
            {
                if (File.GetAttributes(path).HasFlag(FileAttributes.Directory)) throw new InvalidDataException();
            }
            catch (FileNotFoundException) { absent++; continue; }
            catch (DirectoryNotFoundException) { absent++; continue; }
            await using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, 65536, FileOptions.Asynchronous))
            {
                if (file.Length != source.SizeBytes || File.GetLastWriteTimeUtc(path) != source.LastWriteUtc ||
                    Convert.ToHexString(await SHA256.HashDataAsync(file, ct)).ToLowerInvariant() != source.Sha256)
                    throw new InvalidDataException("An inventoried source changed.");
                await sink.WriteAsync(Event("storage.orphan-removal-authorized", "Succeeded", "unregistered-source", plan, source), ct);
            }
            // No online safety claim: Windows requires closing the exclusive read handle
            // before unlink. Stopped writers and private ACLs are required for this boundary.
            try
            {
                ct.ThrowIfCancellationRequested();
                LocalDocumentStorage.EnsureNoReparsePoints(root, source.StorageKey);
                File.Delete(path);
            }
            catch (IOException)
            {
                await sink.WriteAsync(Event("storage.orphan-removal-failed", "Failed", "storage_unavailable", plan, source), ct);
                throw;
            }
            await sink.WriteAsync(Event("storage.orphan-removed", "Succeeded", "source-removed", plan, source), ct);
            await transaction.CommitAsync(ct); removed++;
        }
        return new(removed, preserved, absent);
    }
    private static SecurityAuditEvent Event(string action, string outcome, string reason, OrphanRecoveryPlan plan, OrphanSource source) =>
        new(source.OrganizationId, null, "System", action, "orphan-source", source.DocumentId, outcome, reason, plan.Id.ToString("N"),
            afterState: $"bytes={source.SizeBytes};sha256={source.Sha256}");
}
