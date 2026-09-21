using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Infrastructure.Persistence;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public async Task Phase6_capacity_admission_lock_allows_restricted_worker_completion_and_audit()
    {
        var role = await CreateWorkerRoleAsync();
        try
        {
            using var seedServices = CreateQueueServices();
            var seed = await SeedQueuedUploadAsync(seedServices);
            await using (var owner = _database.CreateContext())
                await WorkerActorProvisioning.ProvisionAsync(owner, seed.OrganizationId, role, default);
            await SetCapacity(seed.OrganizationId, 1, 100000);
            using var services = WorkerServices(role);
            using var scope = services.CreateScope();
            await using var session = await WorkerQueueSession.OpenAsync(scope.ServiceProvider, default);
            var claim = Assert.IsType<ClaimedExtractionJob>(await session.Queue.ClaimAsync("capacity-worker", default));
            await using var held = _database.CreateContext();
            await using var tx = await held.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            await OrganizationCapacity.LockAsync(held, seed.OrganizationId, default);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            Assert.True(await session.Queue.CompleteAsync(claim, CreateReadyResult(claim), deadline.Token));
            Assert.True(await held.SecurityAuditEvents.AnyAsync(e => e.OrganizationId == seed.OrganizationId && e.Action == "worker.completed" && e.ActorKind == "System"));
            Assert.Equal(0, (await OrganizationCapacity.ReadAsync(held, seed.OrganizationId, default)).ActiveJobs);
            await tx.CommitAsync();
        }
        finally { await DropWorkerRoleAsync(role); }
    }

    private async Task<CapacityUsage> SetCapacity(Guid organization, int jobs, long bytes, Guid? version = null)
    {
        await using var db = _database.CreateContext();
        return await OrganizationCapacity.ConfigureAsync(db, organization, jobs, bytes, version, default);
    }
    private static async Task<HttpResponseMessage> CapacityUpload(HttpClient client, string content = "capacity upload")
    {
        using var form = new MultipartFormDataContent(); var text = new StringContent(content);
        text.Headers.ContentType = new("text/plain"); form.Add(text, "file", "capacity.txt");
        return await client.PostAsync("/api/documents", form);
    }
    private async Task CancelOrganizationJobs(HttpClient user, Guid organization)
    {
        await using var db = _database.CreateContext();
        foreach (var id in await db.ExtractionJobs.Where(j => j.OrganizationId == organization && j.CompletedAtUtc == null).Select(j => j.Id).ToArrayAsync())
            Assert.Equal(HttpStatusCode.OK, (await user.PostAsync($"/api/extraction-jobs/{id}/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task Phase6_capacity_requires_provisioning_before_scan_and_existing_policy_cannot_be_disabled_by_API_config()
    {
        using var source = await SourceFixtureAsync("Operator"); var organization = await SourceOrganization(source);
        await using var role = await ApiRoleAsync(); using var restricted = role.Host(source.Host);
        using var host = restricted.WithWebHostBuilder(b => b.UseSetting("Capacity:RequirePolicy", "true"));
        using var user = WithAuthority(host, source.User);
        var missing = await CapacityUpload(user); Assert.Equal(HttpStatusCode.ServiceUnavailable, missing.StatusCode);
        Assert.Contains("organization_capacity_unconfigured", await missing.Content.ReadAsStringAsync());
        Assert.Equal(0, source.Scanner.Calls);
        await SetCapacity(organization, 10, SourceFixture.Content.Length + 15);
        Assert.Equal(HttpStatusCode.Accepted, (await CapacityUpload(user)).StatusCode);
        using var localHost = restricted.WithWebHostBuilder(b => b.UseSetting("Capacity:RequirePolicy", "false"));
        using var localUser = WithAuthority(localHost, source.User);
        var full = await CapacityUpload(localUser); Assert.Equal(HttpStatusCode.Conflict, full.StatusCode);
        Assert.Contains("organization_storage_capacity_exceeded", await full.Content.ReadAsStringAsync());
        Assert.Equal(1, source.Scanner.Calls);
        await using var db = _database.CreateContext(); Assert.Equal(2, await db.Documents.CountAsync());
    }

    [Theory]
    [InlineData("queue", false)]
    [InlineData("storage", false)]
    [InlineData("storage", true)]
    public async Task Phase6_capacity_concurrent_uploads_across_hosts_admit_one_and_compensate_loser(string limit, bool repeatableReadDefault)
    {
        using var source = await SourceFixtureAsync("Operator"); var organization = await SourceOrganization(source);
        await CancelOrganizationJobs(source.User, organization);
        await SetCapacity(organization, limit == "queue" ? 1 : 10, limit == "storage" ? SourceFixture.Content.Length + 15 : 100000);
        var arrived = 0; var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.Scanner.AfterScan = async () => { if (Interlocked.Increment(ref arrived) == 2) barrier.TrySetResult(); await barrier.Task.WaitAsync(TimeSpan.FromSeconds(20)); };
        await using var role = await ApiRoleAsync(); using var hostA = role.Host(source.Host); using var hostB = role.Host(source.Host);
        if (repeatableReadDefault) await ExecuteAsync("ALTER DATABASE document_intelligence_test SET default_transaction_isolation='repeatable read'");
        using var a = WithAuthority(hostA, source.User); using var b = WithAuthority(hostB, source.User);
        var results = await Task.WhenAll(CapacityUpload(a), CapacityUpload(b));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Accepted); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Contains("organization_" + limit + "_capacity_exceeded", await results.Single(r => r.StatusCode == HttpStatusCode.Conflict).Content.ReadAsStringAsync());
        source.Scanner.AfterScan = null;
        await using var db = _database.CreateContext(); var usage = await OrganizationCapacity.ReadAsync(db, organization, default);
        Assert.Equal(1, usage.ActiveJobs); Assert.Equal(SourceFixture.Content.Length + 15, usage.LiveBytes);
        Assert.Equal(2, await db.Documents.CountAsync()); Assert.Equal(2, await db.ExtractionJobs.CountAsync());
        Assert.Equal(2, Directory.GetFiles(Path.Combine(source.Root, "uploads"), "*", SearchOption.AllDirectories).Length);
        Assert.Equal(2, await db.SecurityAuditEvents.CountAsync(e => e.Action == "document.uploaded"));
    }

    [Fact]
    public async Task Phase6_capacity_reprocess_race_preserves_history_and_worker_completion_frees_job_capacity()
    {
        using var source = await SourceFixtureAsync("Operator"); var organization = await SourceOrganization(source);
        Assert.Equal(HttpStatusCode.Accepted, (await CapacityUpload(source.User)).StatusCode);
        await CancelOrganizationJobs(source.User, organization); await SetCapacity(organization, 1, 1); // Below existing bytes; reprocessing still fits.
        await using var db = _database.CreateContext(); var documents = await db.Documents.Select(d => d.Id).ToArrayAsync();
        await using var role = await ApiRoleAsync(); using var hostA = role.Host(source.Host); using var hostB = role.Host(source.Host);
        using var a = WithAuthority(hostA, source.User); using var b = WithAuthority(hostB, source.User);
        var results = await Task.WhenAll(a.PostAsync($"/api/documents/{documents[0]}/reprocess", null), b.PostAsync($"/api/documents/{documents[1]}/reprocess", null));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Accepted); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(3, await db.ExtractionRuns.CountAsync()); Assert.Equal(3, await db.ExtractionJobs.CountAsync());
        using var services = CreateQueueServices(); using var scope = services.CreateScope(); var queue = scope.ServiceProvider.GetRequiredService<IExtractionJobQueue>();
        var claim = Assert.IsType<ClaimedExtractionJob>(await queue.ClaimAsync("capacity-worker", default));
        Assert.Equal(1, (await OrganizationCapacity.ReadAsync(db, organization, default)).ActiveJobs);
        Assert.True(await queue.RecordFailureAsync(claim, "provider_timeout", "safe", true, default));
        Assert.Equal(1, (await OrganizationCapacity.ReadAsync(db, organization, default)).ActiveJobs);
        await ExecuteAsync("UPDATE extraction_jobs SET available_at_utc=clock_timestamp() WHERE id=@id", ("id", claim.JobId));
        var retry = Assert.IsType<ClaimedExtractionJob>(await queue.ClaimAsync("capacity-worker", default));
        Assert.True(await queue.CompleteAsync(retry, CreateReadyResult(retry), default));
        Assert.Equal(0, (await OrganizationCapacity.ReadAsync(db, organization, default)).ActiveJobs);
        Assert.Equal(HttpStatusCode.Accepted, (await a.PostAsync($"/api/documents/{documents[0]}/reprocess", null)).StatusCode);
        Assert.Equal(4, await db.ExtractionRuns.CountAsync());
    }

    [Fact]
    public async Task Phase6_capacity_counts_pending_source_removal_until_deletion_completes()
    {
        using var deletion = await DeletionFixtureAsync(); var source = deletion.Source; var organization = await SourceOrganization(source);
        await SetCapacity(organization, 10, SourceFixture.Content.Length); var plan = await DeletePlan(deletion);
        await ExecuteAsync("""
            CREATE FUNCTION capacity_delete_outage() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
              IF NEW.action='document.source-purge-authorized' THEN RAISE EXCEPTION 'test outage'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER capacity_delete_outage BEFORE INSERT ON security_audit_events FOR EACH ROW EXECUTE FUNCTION capacity_delete_outage();
            """);
        try
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Delete(deletion, plan)).StatusCode);
            await using var db = _database.CreateContext(); var usage = await OrganizationCapacity.ReadAsync(db, organization, default);
            Assert.Equal(0, usage.LiveBytes); Assert.Equal(SourceFixture.Content.Length, usage.PendingDeletionBytes);
            Assert.True(File.Exists(source.File)); Assert.Equal(HttpStatusCode.Conflict, (await CapacityUpload(deletion.User)).StatusCode);
        }
        finally { await ExecuteAsync("DROP TRIGGER capacity_delete_outage ON security_audit_events; DROP FUNCTION capacity_delete_outage()"); }
        Assert.Equal(HttpStatusCode.OK, (await Delete(deletion, plan)).StatusCode);
        await using var verify = _database.CreateContext(); Assert.Equal(0, (await OrganizationCapacity.ReadAsync(verify, organization, default)).PendingDeletionBytes);
        Assert.Equal(HttpStatusCode.Accepted, (await CapacityUpload(deletion.User)).StatusCode);
    }

    [Fact]
    public async Task Phase6_capacity_organizations_are_independent_and_audit_outage_rolls_back_policy_update()
    {
        using var a = await SourceFixtureAsync("Operator"); using var b = await SourceFixtureAsync("Operator");
        var orgA = await SourceOrganization(a); var orgB = await SourceOrganization(b);
        var policy = await SetCapacity(orgA, 1, 100000); await SetCapacity(orgB, 10, 100000);
        Assert.Equal(HttpStatusCode.Conflict, (await CapacityUpload(a.User)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await CapacityUpload(b.User)).StatusCode);
        await Assert.ThrowsAsync<InvalidOperationException>(() => SetCapacity(orgA, 10, 100000, Guid.NewGuid()));
        await ExecuteAsync("""
            CREATE FUNCTION capacity_policy_outage() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
              IF NEW.action='capacity.policy-changed' THEN RAISE EXCEPTION 'test outage'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER capacity_policy_outage BEFORE INSERT ON security_audit_events FOR EACH ROW EXECUTE FUNCTION capacity_policy_outage();
            """);
        try { await Assert.ThrowsAsync<DbUpdateException>(() => SetCapacity(orgA, 10, 100000, policy.PolicyVersion)); }
        finally { await ExecuteAsync("DROP TRIGGER capacity_policy_outage ON security_audit_events; DROP FUNCTION capacity_policy_outage()"); }
        await using var db = _database.CreateContext(); var current = await OrganizationCapacity.ReadAsync(db, orgA, default);
        Assert.Equal(policy.PolicyVersion, current.PolicyVersion); Assert.Equal(1, current.MaxActiveJobs);
        Assert.Equal(2, await db.SecurityAuditEvents.CountAsync(e => e.Action == "capacity.policy-changed"));
    }

    [Fact]
    public async Task Phase6_capacity_policy_grants_identity_constraints_and_populated_downgrade_are_protected()
    {
        await using var db = _database.CreateContext(); var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260912200431_AddDocumentDeletion"); await migrator.MigrateAsync();
        var org = await SecurityBootstrap(); await SetCapacity(org.Organization.OrganizationId, 2, 1000);
        await using var role = await ApiRoleAsync(); await using var restricted = role.Db();
        Assert.Equal(2, (await restricted.OrganizationCapacityPolicies.SingleAsync()).MaxActiveJobs);
        foreach (var sql in new[] { "INSERT INTO organization_capacity_policies DEFAULT VALUES", "UPDATE organization_capacity_policies SET max_active_jobs=100", "DELETE FROM organization_capacity_policies" })
            Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => restricted.Database.ExecuteSqlRawAsync(sql))).SqlState);
        foreach (var sql in new[] { "DELETE FROM organization_capacity_policies", "TRUNCATE organization_capacity_policies", "UPDATE organization_capacity_policies SET organization_id=gen_random_uuid()" })
            await AssertSqlStateAsync("55000", sql);
        await AssertSqlStateAsync("23514", "UPDATE organization_capacity_policies SET max_active_jobs=0");
        await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync("20260912200431_AddDocumentDeletion"));
        Assert.Single(await db.OrganizationCapacityPolicies.ToArrayAsync());
    }

    [Fact]
    public async Task Phase6_capacity_private_cli_creates_reads_and_updates_only_the_expected_database_and_version()
    {
        var org = await SecurityBootstrap(); var id = org.Organization.OrganizationId;
        var dll = Path.Combine(AppContext.BaseDirectory, "EnterpriseAdmin.dll");
        if (!File.Exists(dll))
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs/continuation.md"))) root = root.Parent;
            var configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
            dll = Path.Combine(root!.FullName, "apps/backend/tools/EnterpriseAdmin/bin", configuration, "net8.0/EnterpriseAdmin.dll");
        }
        var file = Path.Combine(Path.GetTempPath(), "capacity-cli-" + Guid.NewGuid().ToString("N") + ".json");
        async Task<(int Code, string Text)> Run(string command, object input)
        {
            await File.WriteAllTextAsync(file, JsonSerializer.Serialize(input));
            var start = new System.Diagnostics.ProcessStartInfo("dotnet")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var value in new[] { dll, command, file }) start.ArgumentList.Add(value);
            start.Environment["PHASE6_ADMIN_CONNECTION"] = _database.TestConnectionString;
            using var process = System.Diagnostics.Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(); var text = await output;
            Assert.DoesNotContain(_database.TestConnectionString, text + await error); return (process.ExitCode, text);
        }
        try
        {
            var created = await Run("capacity-policy", new { expectedDatabase = "document_intelligence_test", organizationId = id, maxActiveJobs = 5, maxStoredBytes = 1000 });
            Assert.Equal(0, created.Code); var version = JsonDocument.Parse(created.Text).RootElement.GetProperty("PolicyVersion").GetGuid();
            Assert.Equal(0, (await Run("capacity-status", new { expectedDatabase = "document_intelligence_test", organizationId = id })).Code);
            Assert.Equal(1, (await Run("capacity-policy", new { expectedDatabase = "wrong_database", organizationId = id, maxActiveJobs = 6, maxStoredBytes = 1000, expectedVersion = version })).Code);
            Assert.Equal(1, (await Run("capacity-policy", new { expectedDatabase = "document_intelligence_test", organizationId = id, maxActiveJobs = 6, maxStoredBytes = 1000 })).Code);
            Assert.Equal(0, (await Run("capacity-policy", new { expectedDatabase = "document_intelligence_test", organizationId = id, maxActiveJobs = 6, maxStoredBytes = 1000, expectedVersion = version })).Code);
            await using var db = _database.CreateContext(); Assert.Equal(6, (await db.OrganizationCapacityPolicies.SingleAsync()).MaxActiveJobs);
            Assert.Equal(2, await db.SecurityAuditEvents.CountAsync(e => e.Action == "capacity.policy-changed"));
        }
        finally { File.Delete(file); }
    }
}
