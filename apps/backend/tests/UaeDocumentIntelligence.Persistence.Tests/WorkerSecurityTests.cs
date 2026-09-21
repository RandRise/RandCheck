using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure;
using UaeDocumentIntelligence.Infrastructure.Persistence;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Theory]
    [InlineData("document")]
    [InlineData("run")]
    [InlineData("organization")]
    [InlineData("attempt")]
    [InlineData("owner")]
    [InlineData("token")]
    public async Task Phase6_worker_claim_is_bound_to_persisted_document_run_and_attempt(string change)
    {
        using var services = CreateQueueServices();
        var seed = await SeedQueuedUploadAsync(services);
        using var scope = services.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IExtractionJobQueue>();
        var claim = Assert.IsType<ClaimedExtractionJob>(await queue.ClaimAsync("test-worker", default));
        var forged = change switch
        {
            "document" => claim with { DocumentId = Guid.NewGuid() },
            "run" => claim with { ExtractionRunId = Guid.NewGuid() },
            "organization" => claim with { OrganizationId = Guid.NewGuid() },
            "attempt" => claim with { Attempt = 2 },
            "owner" => claim with { LeaseOwner = "other-worker" },
            _ => claim with { LeaseToken = Guid.NewGuid() }
        };
        Assert.False(await queue.CompleteAsync(forged, CreateReadyResult(forged), default));
        Assert.False(await queue.RecordFailureAsync(forged, "provider_timeout", "safe", true, default));
        Assert.Equal(ExtractionJobStatus.Leased, (await ReadJobAsync(seed.JobId)).Status);
        Assert.True(await queue.CompleteAsync(claim, CreateReadyResult(claim), default));
    }

    [Fact]
    public async Task Phase6_worker_cannot_replace_persisted_provider_identity_using_claim_metadata()
    {
        using var services = CreateQueueServices();
        await SeedQueuedUploadAsync(services);
        using var scope = services.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IExtractionJobQueue>();
        var claim = Assert.IsType<ClaimedExtractionJob>(await queue.ClaimAsync("test-worker", default));
        var forged = claim with { ProviderAlias = "unconfigured", ProviderVersion = "forged" };
        await Assert.ThrowsAsync<InvalidDataException>(() => queue.CompleteAsync(forged, CreateReadyResult(forged), default));
        Assert.True(await queue.CompleteAsync(claim, CreateReadyResult(claim), default));
    }

    private ServiceProvider WorkerServices(string role, string? connection = null)
    {
        var worker = new NpgsqlConnectionStringBuilder(_database.TestConnectionString) { Username = role, Password = "", Pooling = false };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = _database.TestConnectionString,
            ["ConnectionStrings:WorkerDatabase"] = connection ?? worker.ConnectionString,
            ["ExtractionWorker:SecurityMode"] = "Enforced", ["ExtractionWorker:DatabaseRole"] = role,
            ["ExtractionWorker:Enabled"] = "false"
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider();
    }

    private async Task<string> CreateWorkerRoleAsync()
    {
        var role = "p6_worker_" + Guid.NewGuid().ToString("N");
        await ExecuteAsync($"CREATE ROLE {role} LOGIN NOSUPERUSER NOCREATEROLE NOCREATEDB NOREPLICATION NOBYPASSRLS");
        try
        {
            await using var db = _database.CreateContext();
            await WorkerDatabasePolicy.ConfigureAsync(db, role, default);
            await WorkerDatabasePolicy.ConfigureAsync(db, role, default);
            return role;
        }
        catch { await DropWorkerRoleAsync(role); throw; }
    }

    private Task DropWorkerRoleAsync(string role) => ExecuteAsync($"DROP OWNED BY {role}; DROP ROLE {role}");

    [Fact]
    public async Task Phase6_worker_restricted_login_processes_two_organizations_with_atomic_system_audit()
    {
        var role = await CreateWorkerRoleAsync();
        try
        {
            using var seedServices = CreateQueueServices();
            var first = await SeedQueuedUploadAsync(seedServices);
            var second = await SeedQueuedUploadAsync(seedServices);
            await using (var owner = _database.CreateContext())
            {
                await WorkerActorProvisioning.ProvisionAsync(owner, first.OrganizationId, role, default);
                await WorkerActorProvisioning.ProvisionAsync(owner, second.OrganizationId, role, default);
            }
            using var services = WorkerServices(role);
            using var scope = services.CreateScope();
            await using var session = await WorkerQueueSession.OpenAsync(scope.ServiceProvider, default);
            var claim = Assert.IsType<ClaimedExtractionJob>(await session.Queue.ClaimAsync("test-instance", default));
            await Assert.ThrowsAsync<InvalidOperationException>(() => session.Queue.CancelAsync(claim.OrganizationId, claim.JobId, "cancel", "safe", default));
            await Assert.ThrowsAsync<InvalidOperationException>(() => session.Queue.QueueReprocessAsync(claim.OrganizationId, claim.DocumentId, Guid.NewGuid(), Guid.NewGuid(), "local", "local-1.0.0", "2.0", "safe", default));
            Assert.True(await session.Queue.HeartbeatAsync(claim.JobId, claim.LeaseOwner, claim.LeaseToken, default));
            Assert.True(await session.Queue.CompleteAsync(claim, CreateReadyResult(claim), default));
            var next = Assert.IsType<ClaimedExtractionJob>(await session.Queue.ClaimAsync("test-instance", default));
            Assert.NotEqual(claim.OrganizationId, next.OrganizationId);
            Assert.True(await session.Queue.RecordFailureAsync(next, "provider_timeout", "PRIVATE error summary", true, default));
            await ExecuteAsync("UPDATE extraction_jobs SET available_at_utc=clock_timestamp() WHERE id=@id", ("id", next.JobId));
            var retry = Assert.IsType<ClaimedExtractionJob>(await session.Queue.ClaimAsync("test-instance", default));
            Assert.True(await session.Queue.RecordFailureAsync(retry, "provider_rejected", "PRIVATE error summary", false, default));
            await using var check = _database.CreateContext();
            var events = await check.SecurityAuditEvents.Where(e => e.TargetType == "extraction-job").OrderBy(e => e.RecordedAtUtc).ToListAsync();
            Assert.Equal(new[] { "worker.claimed", "worker.completed", "worker.claimed", "worker.retry-scheduled", "worker.claimed", "worker.failed" }, events.Select(e => e.Action));
            foreach (var item in events)
            {
                Assert.Equal("System", item.ActorKind);
                var actor = await check.Actors.SingleAsync(a => a.Id == item.ActorId);
                Assert.Equal(item.OrganizationId, actor.OrganizationId);
                Assert.Equal(WorkerDatabasePolicy.Subject(role), actor.ExternalSubject);
                Assert.DoesNotContain(item.ActorId!.Value, new[] { first.ActorId, second.ActorId });
            }
            var serialized = System.Text.Json.JsonSerializer.Serialize(events);
            Assert.DoesNotContain("PRIVATE", serialized);
            Assert.DoesNotContain(claim.LeaseToken.ToString(), serialized);
            Assert.DoesNotContain("test-instance", serialized);
            Assert.Equal(0, await check.CitedSpendEvents.CountAsync());
        }
        finally { await DropWorkerRoleAsync(role); }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("inactive")]
    [InlineData("human")]
    public async Task Phase6_worker_missing_or_invalid_system_actor_rolls_back_claim(string kind)
    {
        using var services = CreateQueueServices();
        var seed = await SeedQueuedUploadAsync(services);
        const string subject = "service:extraction-worker:test_role";
        await using var db = _database.CreateContext();
        if (kind != "missing")
        {
            var actor = new Actor(Guid.NewGuid(), seed.OrganizationId, kind == "human" ? ActorKind.Human : ActorKind.System,
                "Test service", DateTimeOffset.UtcNow, subject);
            db.Actors.Add(actor); await db.SaveChangesAsync();
            if (kind == "inactive") await ExecuteAsync("UPDATE actors SET is_active=false WHERE id=@id", ("id", actor.Id));
        }
        var queue = new ExtractionJobQueue(db, new WorkerTestClock(), subject);
        await Assert.ThrowsAsync<InvalidOperationException>(() => queue.ClaimAsync("test-worker", default));
        var job = await ReadJobAsync(seed.JobId);
        Assert.Equal(ExtractionJobStatus.Pending, job.Status); Assert.Equal(0, job.AttemptCount);
        await using var check = _database.CreateContext();
        Assert.Empty(await check.SecurityAuditEvents.Where(e => e.TargetType == "extraction-job").ToListAsync());
    }

    [Theory]
    [InlineData("claim")]
    [InlineData("complete")]
    [InlineData("retry")]
    [InlineData("fail")]
    [InlineData("expired")]
    public async Task Phase6_worker_audit_outage_rolls_back_the_entire_transition(string operation)
    {
        using var services = CreateQueueServices();
        var seed = await SeedQueuedUploadAsync(services);
        await using var db = _database.CreateContext();
        await WorkerActorProvisioning.ProvisionAsync(db, seed.OrganizationId, "test_role", default);
        var queue = new ExtractionJobQueue(db, new WorkerTestClock(), WorkerDatabasePolicy.Subject("test_role"));
        var claim = operation == "claim" ? null : await queue.ClaimAsync("test-worker", default);
        if (operation == "expired") await ExecuteAsync("UPDATE extraction_jobs SET attempt_count=3,lease_expires_at_utc=clock_timestamp()-interval '1 second' WHERE id=@id", ("id", seed.JobId));
        await ExecuteAsync("""
            CREATE FUNCTION p6_worker_audit_outage() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
              RAISE EXCEPTION 'test audit failure'; END $$;
            CREATE TRIGGER p6_worker_audit_outage BEFORE INSERT ON security_audit_events FOR EACH STATEMENT EXECUTE FUNCTION p6_worker_audit_outage();
            """);
        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(async () =>
            {
                if (operation == "claim") await queue.ClaimAsync("test-worker", default);
                else if (operation == "expired") await queue.TerminalizeOneExpiredAsync(default);
                else if (operation == "complete") await queue.CompleteAsync(claim!, CreateReadyResult(claim!), default);
                else await queue.RecordFailureAsync(claim!, "provider_timeout", "safe", operation == "retry", default);
            });
            var job = await ReadJobAsync(seed.JobId);
            Assert.Equal(operation == "claim" ? ExtractionJobStatus.Pending : ExtractionJobStatus.Leased, job.Status);
            Assert.Equal(operation == "claim" ? 0 : operation == "expired" ? 3 : 1, job.AttemptCount);
            await using var check = _database.CreateContext();
            Assert.Equal(operation == "claim" ? 0 : 1, await check.SecurityAuditEvents.CountAsync(e => e.TargetType == "extraction-job"));
            Assert.Empty(await check.ExtractionPages.ToListAsync());
        }
        finally { await ExecuteAsync("DROP TRIGGER p6_worker_audit_outage ON security_audit_events; DROP FUNCTION p6_worker_audit_outage()"); }
    }

    [Fact]
    public async Task Phase6_worker_grants_deny_unrelated_tables_identity_rebinding_and_audit_rewrite()
    {
        var role = await CreateWorkerRoleAsync();
        try
        {
            foreach (var sql in new[] {
                "SELECT * FROM organization_memberships", "SELECT * FROM cited_spend_events", "SELECT * FROM review_actions",
                "UPDATE documents SET organization_id=gen_random_uuid()", "UPDATE extraction_jobs SET document_id=gen_random_uuid()",
                "UPDATE extraction_runs SET provider_name='forged'", "INSERT INTO extraction_jobs (id) VALUES (gen_random_uuid())",
                "UPDATE actors SET is_active=false", "DELETE FROM security_audit_events", "TRUNCATE security_audit_events",
                "UPDATE security_audit_events SET outcome='Failed'", "CREATE TABLE worker_escape(id int)" })
                await AssertSqlStateAsync("42501", $"SET ROLE {role}; {sql}");
            await using var db = _database.CreateContext();
            await ExecuteAsync($"GRANT SELECT ON cited_spend_events TO {role}");
            await Assert.ThrowsAsync<InvalidOperationException>(() => WorkerDatabasePolicy.VerifyAsync(db, role, false, default));
            await WorkerDatabasePolicy.ConfigureAsync(db, role, default);
            await ExecuteAsync($"GRANT UPDATE (organization_id) ON documents TO {role}");
            await Assert.ThrowsAsync<InvalidOperationException>(() => WorkerDatabasePolicy.VerifyAsync(db, role, false, default));
        }
        finally { await DropWorkerRoleAsync(role); }
    }

    private sealed class WorkerTestClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }

    [Theory]
    [InlineData("createdb")]
    [InlineData("membership")]
    [InlineData("grant-option")]
    [InlineData("definer")]
    public async Task Phase6_worker_rejects_elevated_roles_and_indirect_authority(string escalation)
    {
        var role = await CreateWorkerRoleAsync();
        var parent = "p6_parent_" + Guid.NewGuid().ToString("N");
        try
        {
            if (escalation == "createdb") await ExecuteAsync($"ALTER ROLE {role} CREATEDB");
            if (escalation == "membership") await ExecuteAsync($"CREATE ROLE {parent} NOLOGIN; GRANT {parent} TO {role}");
            if (escalation == "grant-option") await ExecuteAsync($"GRANT SELECT (id) ON documents TO {role} WITH GRANT OPTION");
            if (escalation == "definer") await ExecuteAsync("CREATE FUNCTION p6_worker_definer() RETURNS integer LANGUAGE sql SECURITY DEFINER AS 'SELECT 1'");
            await using var db = _database.CreateContext();
            await Assert.ThrowsAsync<InvalidOperationException>(() => WorkerDatabasePolicy.VerifyAsync(db, role, false, default));
            using var services = WorkerServices(role);
            using var scope = services.CreateScope();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => WorkerQueueSession.OpenAsync(scope.ServiceProvider, default));
            Assert.Null(error.InnerException);
        }
        finally
        {
            if (escalation == "definer") await ExecuteAsync("DROP FUNCTION IF EXISTS p6_worker_definer()");
            await DropWorkerRoleAsync(role);
            if (escalation == "membership") await ExecuteAsync($"DROP ROLE {parent}");
        }
    }

    [Fact]
    public async Task Phase6_worker_private_cli_provisions_role_and_actor_and_refuses_retained_targets()
    {
        var role = "p6_worker_" + Guid.NewGuid().ToString("N");
        await ExecuteAsync($"CREATE ROLE {role} NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS");
        var file = Path.Combine(Path.GetTempPath(), "p6-worker-" + Guid.NewGuid().ToString("N") + ".json");
        async Task<int> Run(string operation, string connection)
        {
            var start = new System.Diagnostics.ProcessStartInfo("dotnet")
            { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "EnterpriseAdmin.dll"));
            start.ArgumentList.Add(operation); start.ArgumentList.Add(file);
            start.Environment["PHASE6_ADMIN_CONNECTION"] = connection;
            using var process = System.Diagnostics.Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.DoesNotContain(_database.TestConnectionString, await output + await error);
            return process.ExitCode;
        }
        try
        {
            await File.WriteAllTextAsync(file, System.Text.Json.JsonSerializer.Serialize(new { DatabaseRole = role }));
            Assert.Equal(0, await Run("worker-role", _database.TestConnectionString));
            await using var db = _database.CreateContext();
            await WorkerDatabasePolicy.VerifyAsync(db, role, false, default);
            var tenant = await SeedTenantAsync();
            await File.WriteAllTextAsync(file, System.Text.Json.JsonSerializer.Serialize(new { DatabaseRole = role, tenant.OrganizationId }));
            Assert.Equal(0, await Run("worker-actor", _database.TestConnectionString));
            Assert.Equal(0, await Run("worker-actor", _database.TestConnectionString));
            Assert.Equal(1, await db.Actors.CountAsync(a => a.OrganizationId == tenant.OrganizationId && a.ExternalSubject == WorkerDatabasePolicy.Subject(role)));
            var retained = new NpgsqlConnectionStringBuilder(_database.TestConnectionString) { Database = "phase5_refused_without_connecting" }.ConnectionString;
            Assert.Equal(1, await Run("worker-role", retained));
            Assert.Equal(1, await Run("worker-actor", retained));
        }
        finally { File.Delete(file); await DropWorkerRoleAsync(role); }
    }

    [Fact]
    public async Task Phase6_worker_final_lease_expiry_is_audited_and_service_revocation_blocks_completion()
    {
        using var services = CreateQueueServices();
        var seed = await SeedQueuedUploadAsync(services);
        await using var db = _database.CreateContext();
        var actorId = await WorkerActorProvisioning.ProvisionAsync(db, seed.OrganizationId, "test_role", default);
        Assert.Equal(actorId, await WorkerActorProvisioning.ProvisionAsync(db, seed.OrganizationId, "test_role", default));
        var queue = new ExtractionJobQueue(db, new WorkerTestClock(), WorkerDatabasePolicy.Subject("test_role"));
        var claim = Assert.IsType<ClaimedExtractionJob>(await queue.ClaimAsync("test-worker", default));
        await ExecuteAsync("UPDATE actors SET is_active=false WHERE id=@id", ("id", actorId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => queue.CompleteAsync(claim, CreateReadyResult(claim), default));
        Assert.Equal(ExtractionJobStatus.Leased, (await ReadJobAsync(seed.JobId)).Status);
        await using (var provisioner = _database.CreateContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() => WorkerActorProvisioning.ProvisionAsync(provisioner, seed.OrganizationId, "test_role", default));
        // Test-only owner reactivation to exercise terminal expiry; runtime has no actor UPDATE permission.
        await ExecuteAsync("UPDATE actors SET is_active=true WHERE id=@id", ("id", actorId));
        await ExecuteAsync("UPDATE extraction_jobs SET attempt_count=3,lease_expires_at_utc=clock_timestamp()-interval '1 second' WHERE id=@id", ("id", seed.JobId));
        Assert.True(await queue.TerminalizeOneExpiredAsync(default));
        await using var check = _database.CreateContext();
        var events = await check.SecurityAuditEvents.Where(e => e.TargetType == "extraction-job").ToListAsync();
        Assert.Equal(2, events.Count);
        var expired = Assert.Single(events, e => e.Action == "worker.lease-expired");
        Assert.Equal(actorId, expired.ActorId); Assert.Equal("Failed;attempt=3", expired.AfterState);
        Assert.Equal(ExtractionJobStatus.Failed, (await ReadJobAsync(seed.JobId)).Status);
    }
}
