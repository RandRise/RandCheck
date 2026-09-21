using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using UaeDocumentIntelligence.Infrastructure.Persistence;
using UaeDocumentIntelligence.Infrastructure.Services;

if (args.Length != 2 || args[0] is not ("initialize-journal" or "export-controls" or "verify-restore" or "verify-sources" or "inventory-orphans" or "cleanup-orphans"))
{
    Console.Error.WriteLine("Usage: EnterpriseRecovery initialize-journal|export-controls|verify-restore|verify-sources|inventory-orphans|cleanup-orphans <private-config.json>"); return 2;
}
try
{
    var config = JsonSerializer.Deserialize<RecoveryCommand>(await File.ReadAllTextAsync(args[1]), new JsonSerializerOptions
    { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow }) ?? throw new InvalidOperationException();
    if (args[0] == "initialize-journal")
    {
        DocumentDeletionJournal.Initialize(config.JournalDirectory, config.JournalId);
        Console.WriteLine("Empty deletion journal initialized. Retain it independently of application backups."); return 0;
    }
    var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PHASE6_RECOVERY_CONNECTION") ?? throw new InvalidOperationException());
    if (string.IsNullOrWhiteSpace(config.ExpectedDatabase) || connection.Database != config.ExpectedDatabase ||
        connection.Database.StartsWith("phase5_", StringComparison.OrdinalIgnoreCase) || connection.IncludeErrorDetail || connection.LogParameters)
        throw new InvalidOperationException();
    var journal = new DocumentDeletionJournal(Options.Create(new DocumentDeletionOptions { Enabled = true,
        JournalDirectory = config.JournalDirectory, JournalId = config.JournalId }), Options.Create(new StorageOptions
        { SecurityMode = "Enforced", UploadDirectory = config.UploadDirectory, QuarantineDirectory = config.QuarantineDirectory }));
    journal.RequireRoot();
    var controlPath = PrivateArtifact(config.ControlFile, config);
    var databaseOptions = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection.ConnectionString).Options;
    await using var db = new AppDbContext(databaseOptions);
    var guard = new DocumentRecoveryGuard(db, journal);
    if (args[0] == "export-controls")
    {
        var contents = await guard.ControlSnapshotAsync(default);
        if (Encoding.UTF8.GetByteCount(contents) > 1048576) throw new InvalidOperationException();
        await using var output = new FileStream(controlPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await output.WriteAsync(Encoding.UTF8.GetBytes(contents)); output.Flush(true);
        Console.WriteLine("Recovery controls exported. This file requires private, independent retention and a documented capture time."); return 0;
    }
    if (new FileInfo(controlPath).Length is < 1 or > 1048576) throw new InvalidOperationException();
    var controls = await File.ReadAllTextAsync(controlPath);
    if (args[0] == "verify-sources")
    {
        var recovery = new RegisteredSourceRecovery(db, journal, new StorageOptions { SecurityMode = "Enforced",
            UploadDirectory = config.UploadDirectory, QuarantineDirectory = config.QuarantineDirectory });
        Console.WriteLine(JsonSerializer.Serialize(await recovery.VerifyAsync(config.ExpectedDatabase, controls,
            config.OfflineMaintenanceConfirmed, default))); return 0;
    }
    if (args[0] is "inventory-orphans" or "cleanup-orphans")
    {
        var planPath = PrivateArtifact(config.OrphanPlanFile ?? throw new InvalidOperationException(), config);
        if (planPath.Equals(controlPath, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
        var recovery = new OrphanSourceRecovery(db, journal, new StorageOptions { SecurityMode = "Enforced",
            UploadDirectory = config.UploadDirectory, QuarantineDirectory = config.QuarantineDirectory }, new SecurityAuditSink(databaseOptions));
        if (args[0] == "inventory-orphans")
        {
            var plan = await recovery.InventoryAsync(controls, config.MinimumAgeHours, config.OfflineMaintenanceConfirmed, default);
            await using var output = new FileStream(planPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await output.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(plan)); output.Flush(true);
            Console.WriteLine(JsonSerializer.Serialize(new { Eligible = plan.Sources.Length, plan.Preserved, plan.Unrecognized, plan.Truncated })); return 0;
        }
        if (new FileInfo(planPath).Length is < 1 or > 1048576) throw new InvalidOperationException();
        var input = JsonSerializer.Deserialize<OrphanRecoveryPlan>(await File.ReadAllTextAsync(planPath), new JsonSerializerOptions
        { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow }) ?? throw new InvalidOperationException();
        Console.WriteLine(JsonSerializer.Serialize(await recovery.ExecuteAsync(input, controls, config.OfflineMaintenanceConfirmed, default))); return 0;
    }
    await guard.VerifyControlSnapshotAsync(controls, default);
    Console.WriteLine("Restore checks passed against the supplied authoritative controls and deletion journal. No data was changed."); return 0;
}
catch
{
    Console.Error.WriteLine("Recovery checks failed or configuration is incomplete. Keep the restored deployment offline; inspect authoritative controls and the deletion journal privately.");
    return 1;
}

static string PrivateArtifact(string path, RecoveryCommand config)
{
    if (!Path.IsPathFullyQualified(path)) throw new InvalidOperationException();
    var full = Path.GetFullPath(path);
    foreach (var root in new[] { config.JournalDirectory, config.UploadDirectory, config.QuarantineDirectory })
    {
        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (full.Equals(normalized, StringComparison.OrdinalIgnoreCase) || full.StartsWith(normalized + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException();
    }
    for (var item = new FileInfo(full) as FileSystemInfo; item is not null;
        item = item is FileInfo file ? file.Directory : ((DirectoryInfo)item).Parent)
        if (item.Exists && item.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidOperationException();
    return full;
}

internal sealed record RecoveryCommand(string JournalDirectory, Guid JournalId, string UploadDirectory,
    string QuarantineDirectory, string ExpectedDatabase, string ControlFile, string? OrphanPlanFile = null,
    bool OfflineMaintenanceConfirmed = false, int MinimumAgeHours = 24);
