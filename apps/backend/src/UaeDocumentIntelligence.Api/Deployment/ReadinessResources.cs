using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using UaeDocumentIntelligence.Api.Security;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Persistence;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Api.Deployment;

public static class ReadinessResources
{
    // Explicit opt-in command. Only existing configured resources are inspected. No
    // service host, worker, scanner, HTTP request, migration, grant repair or audit write.
    public static async Task<ReadinessReport> VerifyAsync(ReadinessInput input, CancellationToken ct)
    {
        var baseline = EnterpriseReadiness.Inspect(input); var checks = baseline.Checks.ToList();
        var config = EnterpriseReadiness.Configuration(input);
        async Task Probe(string id, Func<Task> action, string detail)
        {
            try { await action(); checks.Add(new(id, "Pass", detail)); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { checks.Add(new(id, "Blocked", detail)); }
        }
        AppDbContext Database(bool worker)
        {
            if (!EnterpriseReadiness.ValidConnection(config, input.ExpectedDatabase, worker)) throw new InvalidOperationException();
            var connection = new NpgsqlConnectionStringBuilder(config.GetConnectionString(worker ? "WorkerDatabase" : "Database")) {
                Pooling = false, Timeout = 5, CommandTimeout = 5, Options = "-c default_transaction_read_only=on", SearchPath = "public",
                ApplicationName = "enterprise-readiness" };
            return new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection.ConnectionString).Options);
        }
        await Probe("observed.api-grants", async () => {
            await using var db = Database(false); await ApiDatabasePolicy.VerifyAsync(db, config["ApiDatabase:DatabaseRole"]!, true, ct);
        }, "The configured API login must satisfy the current catalog allowlist; failures disclose no connection or SQL details.");
        await Probe("observed.worker-grants", async () => {
            await using var db = Database(true); await WorkerDatabasePolicy.VerifyAsync(db, config["ExtractionWorker:DatabaseRole"]!, true, ct);
        }, "The configured worker login must satisfy its distinct catalog allowlist.");
        await Probe("observed.capacity-actors", async () => {
            if (checks.Single(c => c.Id == "observed.api-grants").Status != "Pass") throw new InvalidOperationException();
            await using var db = Database(false);
            var subject = WorkerDatabasePolicy.Subject(config["ExtractionWorker:DatabaseRole"]!);
            if (!await db.Organizations.AnyAsync(ct) || await db.Organizations.AnyAsync(o =>
                !db.OrganizationCapacityPolicies.Any(p => p.OrganizationId == o.Id) ||
                !db.Actors.Any(a => a.OrganizationId == o.Id && a.IsActive && a.Kind == ActorKind.System && a.ExternalSubject == subject), ct))
                throw new InvalidOperationException();
        }, "At least one organization must exist, and every organization must have a capacity policy and active configured System worker actor.");
        await Probe("observed.storage-roots", () => {
            foreach (var key in new[] { "Storage:UploadDirectory", "Storage:QuarantineDirectory" }) RequireDirectory(config[key]);
            return Task.CompletedTask;
        }, "Existing configured storage roots and ancestors must be directories without reparse points; ACLs and encryption are separate live checks.");
        await Probe("observed.source-keys", () => {
            SourceKeyProtection.VerifyResources(SourceKeyProtectionOptions.Read(config, input.Environment), config);
            return Task.CompletedTask;
        }, "The existing protected key directory and wrapping certificates must be usable; verification decrypts descriptors but creates no key or grant.");
        if (config.GetValue<bool>("DocumentDeletion:Enabled"))
            await Probe("observed.recovery-controls", async () => {
                if (checks.Single(c => c.Id == "observed.api-grants").Status != "Pass") throw new InvalidOperationException();
                var file = input.RecoveryControlFile ?? throw new InvalidOperationException();
                if (baseline.Checks.Single(c => c.Id == "recovery.profile").Status != "Pass") throw new InvalidOperationException();
                RequireDirectory(Path.GetDirectoryName(file));
                if (File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint)) throw new InvalidOperationException();
                var bytes = await ReadinessFiles.ReadBoundedAsync(file, ct);
                var journal = new DocumentDeletionJournal(Options.Create(config.GetSection("DocumentDeletion").Get<DocumentDeletionOptions>() ?? new()),
                    Options.Create(config.GetSection("Storage").Get<StorageOptions>() ?? new()));
                await using var db = Database(false);
                await new DocumentRecoveryGuard(db, journal).VerifyControlSnapshotAsync(System.Text.Encoding.UTF8.GetString(bytes), ct);
            }, "Current schema 2 controls and the independent deletion journal must match; a supplied file does not prove off-host freshness.");
        else checks.Add(new("observed.recovery-controls", "VerificationRequired", "Deletion is disabled; a deployed retention/backup/restore procedure still requires evidence."));
        return EnterpriseReadiness.Report(checks);
    }
    private static void RequireDirectory(string? path)
    {
        if (path is null || !Path.IsPathFullyQualified(path) || !Directory.Exists(path)) throw new InvalidOperationException();
        for (var directory = new DirectoryInfo(path); directory is not null; directory = directory.Parent)
            if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidOperationException();
    }
}

public static class ReadinessFiles
{
    public static async Task<byte[]> ReadBoundedAsync(string path, CancellationToken ct)
    {
        const int limit = 1048576;
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        if (file.Length is < 1 or > limit) throw new InvalidDataException();
        using var result = new MemoryStream(); var buffer = new byte[65536]; int count;
        while ((count = await file.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, limit + 1 - (int)result.Length)), ct)) != 0)
        { result.Write(buffer, 0, count); if (result.Length > limit) throw new InvalidDataException(); }
        return result.ToArray();
    }
}
