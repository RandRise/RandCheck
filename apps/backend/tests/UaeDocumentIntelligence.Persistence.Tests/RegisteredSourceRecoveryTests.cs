using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Persistence;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    private static RegisteredSourceRecovery SourceRecovery(AppDbContext db, DeletionFixture f, string? root = null) =>
        new(db, f.Journal, new StorageOptions { SecurityMode = "Enforced", UploadDirectory = root ?? Path.Combine(f.Source.Root, "uploads") });

    [Theory]
    [InlineData("missing")]
    [InlineData("hash")]
    [InlineData("size")]
    [InlineData("locked")]
    [InlineData("foreign-key")]
    [InlineData("traversal")]
    public async Task Phase6_registered_source_verification_refuses_damage_and_unsafe_paths_without_repair(string change)
    {
        using var f = await DeletionFixtureAsync(); await using var db = _database.CreateContext();
        var controls = await new DocumentRecoveryGuard(db, f.Journal).ControlSnapshotAsync(default);
        var auditCount = await db.SecurityAuditEvents.CountAsync();
        if (change == "missing") File.Delete(f.Source.File);
        if (change == "hash") await File.WriteAllTextAsync(f.Source.File, new string('x', SourceFixture.Content.Length));
        if (change == "size") await File.WriteAllTextAsync(f.Source.File, "short");
        if (change is "foreign-key" or "traversal")
        {
            var key = change == "traversal" ? "../PRIVATE.txt" : $"organizations/{Guid.NewGuid():N}/documents/{f.Source.DocumentId:N}/source.txt";
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE documents SET storage_key={key} WHERE id={f.Source.DocumentId}");
        }
        using var held = change == "locked" ? new FileStream(f.Source.File, FileMode.Open, FileAccess.Read, FileShare.None) : null;
        await Assert.ThrowsAnyAsync<Exception>(() => SourceRecovery(db, f).VerifyAsync(PostgresDatabaseFixture.TestDatabaseName, controls, true, default));
        Assert.Equal(change != "missing", File.Exists(f.Source.File));
        Assert.Equal(1, await db.Documents.CountAsync()); Assert.Equal(auditCount, await db.SecurityAuditEvents.CountAsync());
        if (change == "hash") Assert.Equal(new string('x', SourceFixture.Content.Length), await File.ReadAllTextAsync(f.Source.File));
    }

    [Theory]
    [InlineData("documents")]
    [InlineData("bytes")]
    [InlineData("source-size")]
    public async Task Phase6_registered_source_verification_refuses_partial_or_oversized_coverage(string limit)
    {
        using var f = await DeletionFixtureAsync(); await using var db = _database.CreateContext();
        var original = await db.Documents.AsNoTracking().SingleAsync();
        var controls = await new DocumentRecoveryGuard(db, f.Journal).ControlSnapshotAsync(default);
        var count = limit == "documents" ? 1000 : limit == "bytes" ? 103 : 1;
        for (var i = 0; i < count; i++)
        {
            var id = Guid.NewGuid();
            db.Documents.Add(new Document(id, original.OrganizationId, original.CreatedByActorId, "Synthetic.txt",
                $"organizations/{original.OrganizationId:N}/documents/{id:N}/source.txt", "text/plain",
                limit == "documents" ? 1 : limit == "bytes" ? 10485760 : 10485761, original.Sha256, DateTimeOffset.UtcNow));
        }
        await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SourceRecovery(db, f)
            .VerifyAsync(PostgresDatabaseFixture.TestDatabaseName, controls, true, default));
        Assert.Contains("limits exceeded", error.Message); // Refuses the complete set before opening missing synthetic files.
        Assert.Equal(count + 1, await db.Documents.CountAsync()); Assert.True(File.Exists(f.Source.File));
    }

    [Fact]
    public async Task Phase6_registered_source_private_cli_is_read_only_offline_scoped_and_refuses_RLS_filtering()
    {
        using var f = await DeletionFixtureAsync(); await using var db = _database.CreateContext();
        var controls = await new DocumentRecoveryGuard(db, f.Journal).ControlSnapshotAsync(default);
        var config = await SourceRecoveryConfig(f, controls, false);
        await using var reader = await AuditReaderAsync();
        await ExecuteAsync($"GRANT SELECT ON documents,document_deletions,organization_retention_policies,retention_holds,organization_capacity_policies TO {reader.Role}");
        var denied = await RecoveryProcess("dotnet", reader.ConnectionString, RecoveryDll(), "verify-sources", config);
        Assert.Equal(1, denied.Exit);
        config = await SourceRecoveryConfig(f, controls, true);
        var before = await db.SecurityAuditEvents.CountAsync();
        var success = await RecoveryProcess("dotnet", reader.ConnectionString, RecoveryDll(), "verify-sources", config);
        Assert.Equal(0, success.Exit); Assert.Empty(success.Error);
        var receipt = JsonSerializer.Deserialize<RegisteredSourceVerification>(success.Output)!;
        Assert.Equal(new RegisteredSourceVerification(1, System.Text.Encoding.UTF8.GetByteCount(SourceFixture.Content)), receipt);
        Assert.Equal(before, await db.SecurityAuditEvents.CountAsync());
        Assert.DoesNotContain(f.Source.Root, success.Output + denied.Error);
        Assert.DoesNotContain(SourceFixture.Content, success.Output + denied.Error);
        await Assert.ThrowsAsync<InvalidOperationException>(() => SourceRecovery(db, f).VerifyAsync("phase5_do_not_connect", controls, true, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => SourceRecovery(db, f).VerifyAsync("wrong_database", controls, true, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => SourceRecovery(db, f, "relative").VerifyAsync(PostgresDatabaseFixture.TestDatabaseName, controls, true, default));
        using (var canceled = new CancellationTokenSource())
        {
            canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SourceRecovery(db, f).VerifyAsync(PostgresDatabaseFixture.TestDatabaseName, controls, true, canceled.Token));
        }
        await ExecuteAsync("ALTER TABLE documents ENABLE ROW LEVEL SECURITY; CREATE POLICY recovery_hidden ON documents USING (false)");
        try
        {
            var hidden = await RecoveryProcess("dotnet", reader.ConnectionString, RecoveryDll(), "verify-sources", config);
            Assert.Equal(1, hidden.Exit); Assert.Empty(hidden.Output);
        }
        finally { await ExecuteAsync("DROP POLICY recovery_hidden ON documents; ALTER TABLE documents DISABLE ROW LEVEL SECURITY"); }
        await RetentionHold(f.Source, null);
        Assert.Equal(1, (await RecoveryProcess("dotnet", reader.ConnectionString, RecoveryDll(), "verify-sources", config)).Exit);
        var current = await new DocumentRecoveryGuard(db, f.Journal).ControlSnapshotAsync(default);
        config = await SourceRecoveryConfig(f, current, true);
        Assert.Equal(0, (await RecoveryProcess("dotnet", reader.ConnectionString, RecoveryDll(), "verify-sources", config)).Exit);
        Assert.True(File.Exists(f.Source.File));
    }

    [Fact]
    public async Task Phase6_local_backup_drill_restores_disposable_database_and_exact_registered_source()
    {
        using var f = await DeletionFixtureAsync();
        await RetentionHold(f.Source, null);
        string controls;
        await using (var db = _database.CreateContext()) controls = await new DocumentRecoveryGuard(db, f.Journal).ControlSnapshotAsync(default);
        var config = await SourceRecoveryConfig(f, controls, true);
        // No API or worker may write through the capture/restore interval.
        f.Host.Dispose(); f.Source.Host.Dispose();
        var connection = new NpgsqlConnectionStringBuilder(_database.TestConnectionString);
        if (connection.Database != "document_intelligence_test" || PostgresDatabaseFixture.TestDatabaseName != "document_intelligence_test")
            throw new InvalidOperationException("Backup drill may restore only the disposable test database.");
        static string Pg(string name) => Environment.GetEnvironmentVariable("TEST_POSTGRES_BIN") is { Length: > 0 } root
            ? Path.Combine(root, name + (OperatingSystem.IsWindows() ? ".exe" : "")) : name;
        var script = Path.Combine(AuditRepositoryRoot(), "tools/phase6_retained_fingerprints.sql");
        async Task<string> Fingerprints()
        {
            var result = await RecoveryProcess(Pg("psql"), connection.ConnectionString, "-X", "-A", "-t", "-v", "ON_ERROR_STOP=1", "-f", script);
            Assert.Equal(0, result.Exit); return result.Output;
        }
        var before = await Fingerprints();
        var dump = Path.Combine(f.Source.Root, "synthetic-backup.dump");
        var backupSource = Path.Combine(f.Source.Root, "synthetic-source.backup");
        File.Copy(f.Source.File, backupSource);
        var captured = await RecoveryProcess(Pg("pg_dump"), connection.ConnectionString,
            "--format=custom", "--no-owner", "--no-acl", "--file", dump);
        Assert.Equal(0, captured.Exit); Assert.Empty(captured.Error);
        var dumpHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(dump)));
        await File.WriteAllTextAsync(f.Source.File, "damaged local fixture");
        await ExecuteAsync("UPDATE organization_retention_policies SET document_days=999");
        Assert.Equal(1, (await RecoveryProcess("dotnet", connection.ConnectionString, RecoveryDll(), "verify-sources", config)).Exit);
        // This destructive restore is test-only, guarded above and by the database fixture.
        var restored = await RecoveryProcess(Pg("pg_restore"), connection.ConnectionString,
            "--clean", "--if-exists", "--exit-on-error", "--no-owner", "--no-acl", "--dbname", "document_intelligence_test", dump);
        Assert.Equal(0, restored.Exit);
        Assert.Equal(1, (await RecoveryProcess("dotnet", connection.ConnectionString, RecoveryDll(), "verify-sources", config)).Exit);
        File.Copy(f.Source.File, Path.Combine(f.Source.Root, "damaged-source-evidence.txt"));
        File.Delete(f.Source.File); File.Copy(backupSource, f.Source.File); // Exact bytes; no database identity/hash edits.
        var verified = await RecoveryProcess("dotnet", connection.ConnectionString, RecoveryDll(), "verify-sources", config);
        Assert.Equal(0, verified.Exit); Assert.Empty(verified.Error);
        var after = await Fingerprints(); Assert.Equal(before, after);
        Assert.Equal(SourceFixture.Content, await File.ReadAllTextAsync(f.Source.File));
        Assert.Equal(dumpHash, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(dump))));
        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "phase6-local-recovery-snapshot.json"), JsonSerializer.Serialize(new {
            Scenario = "Synthetic offline database and registered-source restore", Database = "document_intelligence_test",
            BackupFormat = "PostgreSQL custom", BackupSha256 = dumpHash, BackupBytes = new FileInfo(dump).Length,
            RestorePassed = true, CorruptDatabaseRefused = true, MissingSourceRepairRefused = true,
            ExactSourceRestored = true, TableFingerprintsMatch = true, AuthoritativeControlsPreserved = true,
            Verification = JsonSerializer.Deserialize<RegisteredSourceVerification>(verified.Output),
            Before = before, After = after
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task<string> SourceRecoveryConfig(DeletionFixture f, string controls, bool offline)
    {
        var control = Path.Combine(f.Source.Root, "source-controls.json");
        await File.WriteAllTextAsync(control, controls);
        var config = Path.Combine(f.Source.Root, "source-recovery.json");
        await File.WriteAllTextAsync(config, JsonSerializer.Serialize(new {
            JournalDirectory = Path.Combine(f.Source.Root, "deletion-journal"), f.Journal.JournalId,
            UploadDirectory = Path.Combine(f.Source.Root, "uploads"), QuarantineDirectory = Path.Combine(f.Source.Root, "quarantine"),
            ExpectedDatabase = PostgresDatabaseFixture.TestDatabaseName, ControlFile = control, OfflineMaintenanceConfirmed = offline }));
        return config;
    }

    private static string RecoveryDll()
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
        return Path.Combine(AuditRepositoryRoot(), "apps/backend/tools/EnterpriseRecovery/bin", configuration, "net8.0/EnterpriseRecovery.dll");
    }

    private static async Task<(int Exit, string Output, string Error)> RecoveryProcess(string executable, string connection, params string[] arguments)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["PHASE6_RECOVERY_CONNECTION"] = connection;
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        start.Environment["PGHOST"] = parsed.Host; start.Environment["PGPORT"] = parsed.Port.ToString();
        start.Environment["PGUSER"] = parsed.Username; start.Environment["PGPASSWORD"] = parsed.Password;
        start.Environment["PGDATABASE"] = parsed.Database; start.Environment["PGCONNECT_TIMEOUT"] = "10";
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { if (!process.HasExited) process.Kill(true); throw; }
        return (process.ExitCode, await output, await error);
    }
}
