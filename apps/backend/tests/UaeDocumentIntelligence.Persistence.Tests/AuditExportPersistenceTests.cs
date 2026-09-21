using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public async Task Phase6_audit_export_private_CLI_uses_read_only_login_and_offline_verification()
    {
        var a = await SecurityBootstrap(); await SecurityBootstrap();
        await using var reader = await AuditReaderAsync(); using var folder = new AuditFolder();
        var request = await AuditRequestAsync(a.Organization.OrganizationId);
        var input = Path.Combine(folder.Path, "plan.json");
        var output = Path.Combine(folder.Path, "output"); Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(input, JsonSerializer.Serialize(new
        {
            request.ExpectedDatabase, request.OrganizationId, request.FromUtc, request.BeforeUtc,
            outputDirectory = output, request.MaxRows
        }, AuditArchive.JsonOptions));
        var before = await ScalarAsync<long>("SELECT count(*) FROM security_audit_events");
        var result = await AuditCliAsync(reader.ConnectionString, "export", input);
        Assert.Equal(0, result.Exit);
        Assert.Empty(result.Error);
        Assert.DoesNotContain("Password", result.Output); Assert.DoesNotContain("Private administrator", result.Output);
        var receipt = JsonSerializer.Deserialize<AuditExportReceipt>(result.Output, AuditArchive.JsonOptions)!;
        Assert.Equal(2, receipt.EventCount);
        var archive = Directory.GetDirectories(output).Single();
        var verify = await AuditCliAsync(null, "verify", archive, receipt.ManifestSha256);
        Assert.Equal(0, verify.Exit);
        Assert.Equal(receipt, JsonSerializer.Deserialize<AuditExportReceipt>(verify.Output, AuditArchive.JsonOptions));
        var rows = File.ReadAllLines(Path.Combine(archive, "events.ndjson"))
            .Select(line => JsonSerializer.Deserialize<AuditExportRow>(line, AuditArchive.JsonOptions)!).ToArray();
        Assert.All(rows, row => Assert.Equal(a.Organization.OrganizationId, row.OrganizationId));
        Assert.DoesNotContain("Private administrator", await File.ReadAllTextAsync(Path.Combine(archive, "events.ndjson")));
        Assert.Equal(before, await ScalarAsync<long>("SELECT count(*) FROM security_audit_events"));
        Assert.False(await ScalarAsync<bool>("SELECT has_table_privilege(@role,'security_audit_events','INSERT,UPDATE,DELETE,TRUNCATE')", ("role", reader.Role)));
        Assert.False(await ScalarAsync<bool>("SELECT has_table_privilege(@role,'documents','SELECT')", ("role", reader.Role)));

        var evidence = Path.Combine(AuditRepositoryRoot(), "outputs", "phase-6", "audit-export-20260915", "synthetic-snapshot");
        var copy = Path.Combine(evidence, Path.GetFileName(archive)); Directory.CreateDirectory(copy);
        foreach (var file in new[] { "events.ndjson", "manifest.json" }) File.Copy(Path.Combine(archive, file), Path.Combine(copy, file));
        await File.WriteAllTextAsync(Path.Combine(evidence, "receipt.json"), result.Output);
        Assert.Equal(receipt, await AuditArchive.VerifyAsync(copy, receipt.ManifestSha256, default));
    }

    [Fact]
    public async Task Phase6_audit_export_snapshot_excludes_late_commits_even_with_an_older_event_timestamp()
    {
        var a = await SecurityBootstrap(); var request = await AuditRequestAsync(a.Organization.OrganizationId);
        await using var delayed = _database.CreateContext();
        await using var transaction = await delayed.Database.BeginTransactionAsync();
        var late = ExportEvent(a.Organization.OrganizationId, "late-commit");
        delayed.SecurityAuditEvents.Add(late);
        delayed.Entry(late).Property(e => e.RecordedAtUtc).CurrentValue = request.FromUtc;
        await delayed.SaveChangesAsync();
        await using var snapshot = await AuditExportSnapshot.OpenAsync(_database.TestConnectionString, request, default);
        await transaction.CommitAsync();
        using var folder = new AuditFolder();
        var original = await AuditArchive.WriteAsync(folder.Path, snapshot.Scope, snapshot.ReadAsync(), default);
        Assert.Equal(2, original.EventCount);
        await using var later = await AuditExportSnapshot.OpenAsync(_database.TestConnectionString, request, default);
        var refreshed = await AuditArchive.WriteAsync(folder.Path, later.Scope, later.ReadAsync(), default);
        Assert.Equal(3, refreshed.EventCount);
        Assert.NotEqual(original.DataSha256, refreshed.DataSha256);
        Assert.Equal(3, await ScalarAsync<long>("SELECT count(*) FROM security_audit_events"));
    }

    [Fact]
    public async Task Phase6_audit_export_orders_ties_enforces_time_bounds_and_refuses_truncation()
    {
        var a = await SecurityBootstrap(); var b = await SecurityBootstrap();
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero); var before = from.AddDays(1);
        await using (var db = _database.CreateContext())
        {
            foreach (var (organization, time) in new[] { (a.Organization.OrganizationId, from), (a.Organization.OrganizationId, from),
                (a.Organization.OrganizationId, before), (b.Organization.OrganizationId, from) })
            {
                var item = ExportEvent(organization, "boundary"); db.SecurityAuditEvents.Add(item);
                db.Entry(item).Property(e => e.RecordedAtUtc).CurrentValue = time;
            }
            await db.SaveChangesAsync();
        }
        var request = new AuditExportRequest(PostgresDatabaseFixture.TestDatabaseName, a.Organization.OrganizationId, from, before);
        using var folder = new AuditFolder();
        await using (var snapshot = await AuditExportSnapshot.OpenAsync(_database.TestConnectionString, request, default))
        {
            var result = await AuditArchive.WriteAsync(folder.Path, snapshot.Scope, snapshot.ReadAsync(), default);
            Assert.Equal(2, result.EventCount);
            Assert.Equal(result, await AuditArchive.VerifyAsync(Directory.GetDirectories(folder.Path).Single(), result.ManifestSha256, default));
        }
        using var limited = new AuditFolder();
        await using (var snapshot = await AuditExportSnapshot.OpenAsync(_database.TestConnectionString, request with { MaxRows = 1 }, default))
            await Assert.ThrowsAsync<InvalidOperationException>(() => AuditArchive.WriteAsync(limited.Path, snapshot.Scope, snapshot.ReadAsync(), default));
        Assert.Empty(Directory.GetFileSystemEntries(limited.Path));
        await Assert.ThrowsAsync<InvalidOperationException>(() => AuditExportSnapshot.OpenAsync(_database.TestConnectionString,
            request with { OrganizationId = Guid.NewGuid() }, default));
        var now = await AuditRequestAsync(a.Organization.OrganizationId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => AuditExportSnapshot.OpenAsync(_database.TestConnectionString,
            now with { BeforeUtc = now.BeforeUtc.AddDays(1) }, default));
    }

    [Fact]
    public async Task Phase6_audit_export_refuses_missing_read_permission_and_silent_RLS_filtering()
    {
        var a = await SecurityBootstrap(); await using var reader = await AuditReaderAsync(); using var folder = new AuditFolder();
        var request = await AuditRequestAsync(a.Organization.OrganizationId);
        await ExecuteAsync($"REVOKE SELECT ON security_audit_events FROM {reader.Role}");
        await using (var denied = await AuditExportSnapshot.OpenAsync(reader.ConnectionString, request, default))
            await Assert.ThrowsAsync<PostgresException>(() => AuditArchive.WriteAsync(folder.Path, denied.Scope, denied.ReadAsync(), default));
        Assert.Empty(Directory.GetFileSystemEntries(folder.Path));
        await ExecuteAsync($"GRANT SELECT ON security_audit_events TO {reader.Role}");
        await ExecuteAsync("ALTER TABLE security_audit_events ENABLE ROW LEVEL SECURITY; CREATE POLICY audit_export_test ON security_audit_events USING(false)");
        try
        {
            await using var filtered = await AuditExportSnapshot.OpenAsync(reader.ConnectionString, request, default);
            await Assert.ThrowsAsync<PostgresException>(() => AuditArchive.WriteAsync(folder.Path, filtered.Scope, filtered.ReadAsync(), default));
            Assert.Empty(Directory.GetFileSystemEntries(folder.Path));
        }
        finally { await ExecuteAsync("DROP POLICY audit_export_test ON security_audit_events; ALTER TABLE security_audit_events DISABLE ROW LEVEL SECURITY"); }
    }

    [Fact]
    public async Task Phase6_audit_export_CLI_rejects_bad_scope_without_logging_private_configuration()
    {
        using var folder = new AuditFolder();
        var input = Path.Combine(folder.Path, "plan.json");
        var secret = "test-only-do-not-log";
        var connection = new NpgsqlConnectionStringBuilder(_database.TestConnectionString)
            { Database = "phase5_e2e_20260908", Password = secret, Port = 1 };
        await File.WriteAllTextAsync(input, JsonSerializer.Serialize(new
        {
            expectedDatabase = connection.Database, organizationId = Guid.NewGuid(),
            fromUtc = "2026-01-01T00:00:00Z", beforeUtc = "2026-01-02T00:00:00Z", outputDirectory = folder.Path
        }));
        var result = await AuditCliAsync(connection.ConnectionString, "export", input);
        Assert.Equal(1, result.Exit); Assert.Empty(result.Output);
        Assert.DoesNotContain(secret, result.Error); Assert.DoesNotContain(connection.Database, result.Error);
        Assert.Empty(Directory.GetDirectories(folder.Path));
        await File.WriteAllTextAsync(input, "{\"expectedDatabase\":\"a\",\"expectedDatabase\":\"b\"}");
        Assert.Equal(1, (await AuditCliAsync(null, "export", input)).Exit);
        Assert.Equal(2, (await AuditCliAsync(null, "delete", folder.Path)).Exit);
    }

    private static SecurityAuditEvent ExportEvent(Guid organization, string request) =>
        new(organization, null, "System", "request.admitted", "document", null, "Succeeded", "authorized_post", request);

    private async Task<AuditExportRequest> AuditRequestAsync(Guid organization)
    {
        var utc = await ScalarAsync<DateTime>("SELECT clock_timestamp()");
        return new(PostgresDatabaseFixture.TestDatabaseName, organization, new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new(utc));
    }

    private async Task<AuditReader> AuditReaderAsync()
    {
        var role = "audit_export_" + Guid.NewGuid().ToString("N")[..12];
        await ExecuteAsync($"CREATE ROLE {role} LOGIN PASSWORD 'test-only-audit-export'; GRANT USAGE ON SCHEMA public TO {role}; GRANT SELECT (id) ON organizations TO {role}; GRANT SELECT ON security_audit_events TO {role}");
        var connection = new NpgsqlConnectionStringBuilder(_database.TestConnectionString)
            { Username = role, Password = "test-only-audit-export", Pooling = false };
        return new(this, role, connection.ConnectionString);
    }

    private sealed record AuditReader(PostgresPersistenceTests Owner, string Role, string ConnectionString) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() { await Owner.ExecuteAsync($"DROP OWNED BY {Role}; DROP ROLE {Role}"); }
    }

    private static string AuditRepositoryRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs/continuation.md"))) root = root.Parent;
        return root?.FullName ?? throw new InvalidOperationException("Repository not found.");
    }

    private static async Task<(int Exit, string Output, string Error)> AuditCliAsync(string? connection, params string[] args)
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
        var dll = Path.Combine(AuditRepositoryRoot(), "apps/backend/tools/EnterpriseAudit/bin", configuration, "net8.0/EnterpriseAudit.dll");
        var start = new ProcessStartInfo("dotnet")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(dll); foreach (var arg in args) start.ArgumentList.Add(arg);
        start.Environment.Remove("PHASE6_AUDIT_CONNECTION");
        if (connection is not null) start.Environment["PHASE6_AUDIT_CONNECTION"] = connection;
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try { await process.WaitForExitAsync(deadline.Token); }
        catch { process.Kill(true); throw; }
        return (process.ExitCode, await output, await error);
    }

    private sealed class AuditFolder : IDisposable
    {
        private readonly string _parent = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "audit-export-tests"));
        public string Path { get; }
        public AuditFolder() { Path = System.IO.Path.Combine(_parent, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path); }
        public void Dispose()
        {
            if (System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path)) != _parent) throw new InvalidOperationException();
            Directory.Delete(Path, true);
        }
    }
}
