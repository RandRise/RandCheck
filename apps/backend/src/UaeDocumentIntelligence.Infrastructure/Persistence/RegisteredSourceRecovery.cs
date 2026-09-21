using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

public sealed record RegisteredSourceVerification(int Documents, long Bytes);

// Private, offline verification only. Never repairs files, changes database rows,
// or turns a source integrity check into permission to bypass malware scanning.
public sealed class RegisteredSourceRecovery(AppDbContext db, DocumentDeletionJournal journal, StorageOptions storage)
{
    public async Task<RegisteredSourceVerification> VerifyAsync(string expectedDatabase, string controls,
        bool offlineMaintenanceConfirmed, CancellationToken cancellationToken)
    {
        if (!offlineMaintenanceConfirmed || storage.SecurityMode != "Enforced" ||
            !Path.IsPathFullyQualified(storage.UploadDirectory) || !Directory.Exists(storage.UploadDirectory) ||
            string.IsNullOrWhiteSpace(expectedDatabase) || expectedDatabase.StartsWith("phase5_", StringComparison.OrdinalIgnoreCase) ||
            db.Database.GetDbConnection().Database != expectedDatabase || !db.Database.IsNpgsql())
            throw new InvalidOperationException("Offline source verification is not configured.");
        journal.RequireRoot();
        LocalDocumentStorage.EnsureNoReparsePoints(storage.UploadDirectory, "recovery-root-check");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        var ct = deadline.Token;
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY; SET LOCAL row_security=off; SET LOCAL statement_timeout='30s'", ct);
        var actualDatabase = await db.Database.SqlQueryRaw<string>("SELECT current_database() AS \"Value\"").SingleAsync(ct);
        if (actualDatabase != expectedDatabase) throw new InvalidOperationException("Recovery database differs.");
        await new DocumentRecoveryGuard(db, journal).VerifyControlSnapshotAsync(controls, ct);
        var sources = await db.Documents.AsNoTracking().OrderBy(d => d.OrganizationId).ThenBy(d => d.Id)
            .Select(d => new { d.Id, d.OrganizationId, d.StorageKey, d.SizeBytes, d.Sha256 }).Take(1001).ToArrayAsync(ct);
        if (sources.Length > 1000 || sources.Any(d => d.SizeBytes is < 1 or > 10485760 ||
            d.Sha256.Length != 64 || d.Sha256.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f'))))
            throw new InvalidOperationException("Registered source verification limits exceeded.");
        var bytes = sources.Sum(d => d.SizeBytes);
        if (bytes > 1073741824) throw new InvalidOperationException("Registered source verification limits exceeded.");
        foreach (var source in sources)
        {
            ct.ThrowIfCancellationRequested();
            var path = LocalDocumentStorage.ResolveDocumentSourcePath(storage.UploadDirectory, source.OrganizationId, source.Id, source.StorageKey);
            await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, 65536, FileOptions.Asynchronous);
            if (file.Length != source.SizeBytes ||
                Convert.ToHexString(await SHA256.HashDataAsync(file, ct)).ToLowerInvariant() != source.Sha256 || file.Length != source.SizeBytes)
                throw new InvalidDataException("Registered source integrity differs.");
        }
        await transaction.CommitAsync(ct);
        return new(sources.Length, bytes);
    }
}
