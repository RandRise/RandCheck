using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UaeDocumentIntelligence.Api.Deployment;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    private ReadinessInput ReadinessCandidate(DeletionFixture f, ApiRoleFixture api, string worker, string controls) => new("Production",
        JsonSerializer.SerializeToElement(new {
            SourceKeyProtection = SourceKeys(f.Source.Root),
            ApiDatabase = new { Enabled = true, DatabaseRole = api.Role },
            ConnectionStrings = new { Database = api.Connection, WorkerDatabase = new NpgsqlConnectionStringBuilder(_database.TestConnectionString) { Username = worker, Password = "" }.ConnectionString },
            ExtractionWorker = new { SecurityMode = "Enforced", DatabaseRole = worker, Enabled = false },
            Storage = new { SecurityMode = "Enforced", UploadDirectory = Path.Combine(f.Source.Root, "uploads"), QuarantineDirectory = Path.Combine(f.Source.Root, "quarantine") },
            DocumentDeletion = new { Enabled = true, JournalDirectory = Path.Combine(f.Source.Root, "deletion-journal"), f.Journal.JournalId }
        }), new(), "document_intelligence_test", controls);

    private async Task<string> ReadinessDatabaseFingerprint()
    {
        await using var connection = await _database.OpenConnectionAsync(); var tables = new List<string>();
        await using (var command = new NpgsqlCommand("SELECT tablename FROM pg_tables WHERE schemaname='public' ORDER BY tablename", connection))
        await using (var reader = await command.ExecuteReaderAsync()) while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
        var result = new List<string>();
        foreach (var table in tables)
        {
            var quoted = new NpgsqlCommandBuilder().QuoteIdentifier(table);
            await using var command = new NpgsqlCommand($"SELECT md5(coalesce(string_agg(row_to_json(t)::text,'' ORDER BY row_to_json(t)::text),'')) FROM public.{quoted} t", connection);
            result.Add(table + ":" + await command.ExecuteScalarAsync());
        }
        return string.Join('\n', result);
    }

    [Fact]
    public async Task Phase6_readiness_observes_restricted_grants_controls_and_coverage_without_database_or_source_mutation()
    {
        using var f = await DeletionFixtureAsync(); await using var api = await ApiRoleAsync(); var worker = await CreateWorkerRoleAsync();
        try
        {
            var org = await SourceOrganization(f.Source); await SetCapacity(org, 10, 100000);
            var controls = Path.Combine(f.Source.Root, "readiness-controls.json");
            await using (var owner = _database.CreateContext())
            {
                await WorkerActorProvisioning.ProvisionAsync(owner, org, worker, default);
                await File.WriteAllTextAsync(controls, await new DocumentRecoveryGuard(owner, f.Journal).ControlSnapshotAsync(default));
            }
            var input = ReadinessCandidate(f, api, worker, controls); var before = await ReadinessDatabaseFingerprint();
            var source = await File.ReadAllBytesAsync(f.Source.File);
            var report = await ReadinessResources.VerifyAsync(input, default);
            Assert.All(report.Checks.Where(c => c.Id.StartsWith("observed.")), c => Assert.Equal("Pass", c.Status));
            Assert.False(report.DeploymentVerified); Assert.Contains(report.Checks, c => c.Id == "live.keys" && c.Status == "VerificationRequired");
            Assert.Equal(before, await ReadinessDatabaseFingerprint()); Assert.Equal(source, await File.ReadAllBytesAsync(f.Source.File));
            var config = Path.Combine(f.Source.Root, "readiness.json"); await File.WriteAllTextAsync(config, JsonSerializer.Serialize(input));
            var process = await ReadinessCommand("verify-resources", config);
            Assert.Equal(1, process.Code); // Identity/scanner settings intentionally absent; observed resources can still pass.
            Assert.All(JsonSerializer.Deserialize<ReadinessReport>(process.Text)!.Checks.Where(c => c.Id.StartsWith("observed.")), c => Assert.Equal("Pass", c.Status));
            Assert.DoesNotContain(f.Source.Root, process.Text); Assert.DoesNotContain(api.Connection, process.Text);
            Assert.Equal(before, await ReadinessDatabaseFingerprint());
            Assert.Empty(Directory.GetFiles(Path.Combine(f.Source.Root, "keys")));
        }
        finally { await DropWorkerRoleAsync(worker); }
    }

    [Fact]
    public async Task Phase6_readiness_detects_missing_capacity_actors_stale_controls_and_api_grant_drift()
    {
        using var f = await DeletionFixtureAsync(); await using var api = await ApiRoleAsync(); var worker = await CreateWorkerRoleAsync();
        try
        {
            var controls = Path.Combine(f.Source.Root, "readiness-controls.json");
            await using (var owner = _database.CreateContext()) await File.WriteAllTextAsync(controls, await new DocumentRecoveryGuard(owner, f.Journal).ControlSnapshotAsync(default));
            var input = ReadinessCandidate(f, api, worker, controls);
            Assert.Equal("Blocked", (await ReadinessResources.VerifyAsync(input, default)).Checks.Single(c => c.Id == "observed.capacity-actors").Status);
            var org = await SourceOrganization(f.Source); await SetCapacity(org, 10, 100000);
            await using (var owner = _database.CreateContext()) await WorkerActorProvisioning.ProvisionAsync(owner, org, worker, default);
            var report = await ReadinessResources.VerifyAsync(input, default);
            Assert.Equal("Pass", report.Checks.Single(c => c.Id == "observed.capacity-actors").Status);
            Assert.Equal("Blocked", report.Checks.Single(c => c.Id == "observed.recovery-controls").Status);
            await ExecuteAsync($"GRANT UPDATE ON organization_capacity_policies TO {api.Role}");
            report = await ReadinessResources.VerifyAsync(input, default);
            Assert.Equal("Blocked", report.Checks.Single(c => c.Id == "observed.api-grants").Status);
            Assert.Equal("Blocked", report.Checks.Single(c => c.Id == "observed.capacity-actors").Status);
        }
        finally { await DropWorkerRoleAsync(worker); }
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("oversized")]
    public async Task Phase6_readiness_cli_refuses_ambiguous_or_unbounded_input_without_echoing_diagnostics(string kind)
    {
        var config = Path.Combine(Path.GetTempPath(), "readiness-invalid-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            await File.WriteAllTextAsync(config, kind switch {
                "duplicate" => "{\"Environment\":\"Production\",\"environment\":\"PRIVATE_MARKER\",\"Backend\":{},\"Browser\":{},\"ExpectedDatabase\":\"test\"}",
                "unknown" => "{\"Environment\":\"Production\",\"Backend\":{},\"Browser\":{},\"ExpectedDatabase\":\"test\",\"Secret\":\"PRIVATE_MARKER\"}",
                _ => new string('X', 1048577) });
            var result = await ReadinessCommand("inspect", config);
            Assert.Equal(3, result.Code); Assert.DoesNotContain("PRIVATE_MARKER", result.Text); Assert.DoesNotContain(config, result.Text);
        }
        finally { File.Delete(config); }
    }

    private static async Task<(int Code, string Text)> ReadinessCommand(string command, string config)
    {
        var dll = Path.Combine(AppContext.BaseDirectory, "EnterpriseReadiness.dll");
        if (!File.Exists(dll))
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs/continuation.md"))) root = root.Parent;
            var configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
            dll = Path.Combine(root!.FullName, "apps/backend/tools/EnterpriseReadiness/bin", configuration, "net8.0/EnterpriseReadiness.dll");
        }
        var start = new System.Diagnostics.ProcessStartInfo("dotnet")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { dll, command, config }) start.ArgumentList.Add(arg);
        using var process = System.Diagnostics.Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(); return (process.ExitCode, await output + await error);
    }
}
