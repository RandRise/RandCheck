using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Persistence.Tests;

// Uses the same exact disposable database as the persistence fixture. Assembly
// the collection prevents competing database resets across test classes.
[Collection("Disposable PostgreSQL")]
public sealed class StaffAdminMigrationTests(PostgresDatabaseFixture database) : IClassFixture<PostgresDatabaseFixture>, IAsyncLifetime
{
    private const string Previous = "20261006164334_AddInvoicePurchaseOrderDraftWorkflow";
    private const string Current = "20261019190302_UseStaffAndAdminRoles";
    private readonly Guid _organization = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await database.InitializeAsync();
        await using var db = database.CreateContext();
        await db.GetService<IMigrator>().MigrateAsync(Previous);
        await Sql("INSERT INTO organizations(id,slug,display_name,created_at_utc) VALUES (@id,'migration-test','Migration test',now())", ("id", _organization));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task Sql(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<string> Scalar(string sql)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (await command.ExecuteScalarAsync())!.ToString()!;
    }

    private async Task Seed(string role, bool admin = false, bool active = true, bool actorActive = true)
    {
        await Sql("""
            INSERT INTO actors(id,organization_id,kind,display_name,is_active,created_at_utc)
                VALUES (@actor,@org,'Human','O''Brien — حساب تجريبي',@actorActive,now());
            INSERT INTO enterprise_identities(id,tenant_id,object_id,created_at_utc)
                VALUES (@identity,gen_random_uuid(),gen_random_uuid(),now());
            INSERT INTO organization_memberships(id,organization_id,identity_id,actor_id,role,can_manage_members,is_active,version,created_at_utc,updated_at_utc)
                VALUES (gen_random_uuid(),@org,@identity,@actor,@role,@admin,@active,gen_random_uuid(),now(),now());
            """, ("actor", Guid.NewGuid()), ("org", _organization), ("identity", Guid.NewGuid()),
            ("role", role), ("admin", admin), ("active", active), ("actorActive", actorActive));
    }

    private static string InventorySql()
    {
        var assembly = typeof(AppDbContext).Assembly;
        using var stream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n => n.EndsWith("StaffAdminInventory.sql")))!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private async Task Apply(string? snapshot)
    {
        await using var db = database.CreateContext();
        await db.Database.OpenConnectionAsync();
        if (snapshot is not null)
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('uaedi.approved_role_inventory', {snapshot}, false)");
        await db.GetService<IMigrator>().MigrateAsync(Current);
    }

    private static string RepositoryRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "tools/migrate-access-roles.py"))) root = root.Parent;
        return root?.FullName ?? throw new InvalidOperationException("Repository not found");
    }

    [Fact]
    public async Task Versioned_cli_script_exactly_matches_the_EF_migration()
    {
        await using var db = database.CreateContext();
        var generated = db.GetService<IMigrator>().GenerateScript(Previous, Current);
        var shipped = await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), "tools/migrate-access-roles.sql"));
        Assert.Equal(generated.Replace("\r\n", "\n"), shipped.Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task Cli_requires_explicit_review_and_applies_only_its_guarded_script()
    {
        await Seed("Reviewer");
        var root = RepositoryRoot();
        var planFile = Path.Combine(Path.GetTempPath(), $"role-test-{Guid.NewGuid()}.json");
        var badScript = Path.ChangeExtension(planFile, ".sql");
        async Task<(int Exit, string Output)> Run(params string[] arguments)
        {
            // CI provides its interpreter explicitly; keep the local Windows
            // virtual-environment fallback without assuming that path on Linux.
            var python = Environment.GetEnvironmentVariable("PHASE5_PYTHON") ??
                (OperatingSystem.IsWindows() ? Path.Combine(root, ".venv/Scripts/python.exe") : "python3");
            var start = new System.Diagnostics.ProcessStartInfo(python)
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Path.Combine(root, "tools/migrate-access-roles.py"));
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            start.ArgumentList.Add("--plan"); start.ArgumentList.Add(planFile);
            start.ArgumentList.Add("--psql");
            start.ArgumentList.Add(Environment.GetEnvironmentVariable("TEST_PSQL") ?? "psql");
            var connection = new NpgsqlConnectionStringBuilder(database.TestConnectionString);
            start.Environment["PGHOST"] = connection.Host;
            start.Environment["PGPORT"] = connection.Port.ToString();
            start.Environment["PGUSER"] = connection.Username;
            start.Environment["PGPASSWORD"] = connection.Password ?? "";
            start.Environment["PGDATABASE"] = connection.Database;
            using var process = System.Diagnostics.Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (process.ExitCode, await output + await error);
        }
        try
        {
            Assert.Equal(0, (await Run("inventory")).Exit);
            Assert.Equal(1, (await Run("inventory")).Exit); // cannot overwrite review evidence
            Assert.Equal(1, (await Run("apply")).Exit);
            var plan = JsonNode.Parse(await File.ReadAllTextAsync(planFile))!;
            plan["approved"] = true;
            await File.WriteAllTextAsync(planFile, plan.ToJsonString());
            await File.WriteAllTextAsync(badScript, "COMMIT; -- " + Current + " uaedi.approved_role_inventory");
            Assert.Equal(1, (await Run("apply", "--script", badScript)).Exit);
            Assert.Equal(Previous, await Scalar("SELECT max(\"MigrationId\") FROM \"__EFMigrationsHistory\""));
            var result = await Run("apply");
            Assert.True(result.Exit == 0, result.Output);
            Assert.Equal("Staff", await Scalar("SELECT role FROM organization_memberships"));
            Assert.DoesNotContain("O'Brien", result.Output);
            Assert.Equal(1, (await Run("apply")).Exit); // no blind second application
            Assert.Equal("1", await Scalar("SELECT count(*) FROM security_audit_events"));
        }
        finally { File.Delete(planFile); File.Delete(badScript); }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("omitted")]
    [InlineData("duplicate")]
    [InlineData("extra")]
    [InlineData("version")]
    [InlineData("identity")]
    [InlineData("actor")]
    [InlineData("other-database")]
    public async Task Unreviewed_or_stale_plan_refuses_without_schema_or_access_changes(string kind)
    {
        await Seed("Viewer");
        var plan = JsonNode.Parse(await Scalar(InventorySql()))!;
        var members = plan["members"]!.AsArray();
        switch (kind)
        {
            case "omitted": members.Clear(); break;
            case "duplicate": members.Add(members[0]!.DeepClone()); break;
            case "extra": var extra = members[0]!.DeepClone(); extra["id"] = Guid.NewGuid().ToString(); members.Add(extra); break;
            case "version": await Sql("UPDATE organization_memberships SET version=gen_random_uuid()"); break;
            case "identity": await Sql("UPDATE enterprise_identities SET object_id=gen_random_uuid()"); break;
            case "actor": await Sql("UPDATE actors SET is_active=false"); break;
            case "other-database": plan["database"] = "other_database"; break;
        }
        var before = await Scalar(InventorySql());
        await Assert.ThrowsAsync<PostgresException>(() => Apply(kind == "missing" ? null : plan.ToJsonString()));
        Assert.Equal(before, await Scalar(InventorySql()));
        Assert.Equal("0", await Scalar("SELECT count(*) FROM security_audit_events"));
        Assert.Equal(Previous, await Scalar("SELECT max(\"MigrationId\") FROM \"__EFMigrationsHistory\""));
    }

    [Fact]
    public async Task Reviewed_migration_preserves_disabled_accounts_identities_and_old_audit_and_records_each_change()
    {
        foreach (var role in new[] { "Viewer", "Reviewer", "Operator", "None" })
            foreach (var admin in new[] { false, true }) await Seed(role, admin);
        await Seed("Viewer", active: false);
        await Seed("Reviewer", actorActive: false);
        await Sql("""
            INSERT INTO security_audit_events(id,organization_id,actor_kind,action,target_type,outcome,reason_code,request_id,before_state)
                VALUES (gen_random_uuid(),@org,'System','historical.event','membership','Succeeded','test','before','Viewer;manage=False;active=True');
            """, ("org", _organization));
        var oldAudit = await Scalar("SELECT to_jsonb(e)::text FROM security_audit_events e");
        var actors = await Scalar("SELECT jsonb_agg(to_jsonb(a) ORDER BY id)::text FROM actors a");
        var identities = await Scalar("SELECT jsonb_agg(to_jsonb(i) ORDER BY id)::text FROM enterprise_identities i");
        var snapshot = await Scalar(InventorySql());
        var plan = JsonNode.Parse(snapshot)!;
        await Apply(snapshot);
        await Apply(null); // already applied is safe and does not append duplicate audit events
        Assert.Equal(oldAudit, await Scalar("SELECT to_jsonb(e)::text FROM security_audit_events e WHERE action='historical.event'"));
        Assert.Equal(actors, await Scalar("SELECT jsonb_agg(to_jsonb(a) ORDER BY id)::text FROM actors a"));
        Assert.Equal(identities, await Scalar("SELECT jsonb_agg(to_jsonb(i) ORDER BY id)::text FROM enterprise_identities i"));
        foreach (var member in plan["members"]!.AsArray())
        {
            var row = JsonNode.Parse(await Scalar($"SELECT to_jsonb(m)::text FROM organization_memberships m WHERE id='{Guid.Parse(member!["id"]!.GetValue<string>())}'"))!;
            Assert.Equal(member["newRole"]!.GetValue<string>(), row["role"]!.GetValue<string>());
            Assert.Equal(member["newActive"]!.GetValue<bool>(), row["is_active"]!.GetValue<bool>());
            Assert.NotEqual(member["version"]!.GetValue<string>(), row["version"]!.GetValue<string>());
            Assert.Null(row["can_manage_members"]);
        }
        Assert.Equal("10", await Scalar("SELECT count(*) FROM security_audit_events WHERE action='membership.roles-migrated'"));
        await Assert.ThrowsAsync<PostgresException>(() => Sql("UPDATE organization_memberships SET role='Viewer'"));
        await using var db = database.CreateContext();
        await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync(Previous));
        Assert.Equal(Current, await Scalar("SELECT max(\"MigrationId\") FROM \"__EFMigrationsHistory\""));
    }

    [Fact]
    public async Task Concurrent_account_change_invalidates_review_after_migration_waits_for_the_lock()
    {
        await Seed("Viewer");
        var snapshot = await Scalar(InventorySql());
        await using var connection = await database.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand("LOCK TABLE organizations, actors, enterprise_identities, organization_memberships IN ACCESS EXCLUSIVE MODE", connection, transaction))
            await command.ExecuteNonQueryAsync();
        var applying = Apply(snapshot);
        // Observe the lock wait rather than assuming a scheduling delay proves it.
        var waiting = false;
        for (var attempt = 0; attempt < 100 && !waiting; attempt++)
        {
            waiting = await Scalar("SELECT EXISTS (SELECT 1 FROM pg_locks WHERE NOT granted AND database=(SELECT oid FROM pg_database WHERE datname=current_database()))") == "True";
            if (!waiting) await Task.Delay(50);
        }
        Assert.True(waiting);
        await using (var command = new NpgsqlCommand("UPDATE organization_memberships SET version=gen_random_uuid()", connection, transaction))
            await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
        await Assert.ThrowsAsync<PostgresException>(() => applying);
        Assert.Equal("Viewer", await Scalar("SELECT role FROM organization_memberships"));
        Assert.Equal("0", await Scalar("SELECT count(*) FROM security_audit_events"));
    }

    [Fact]
    public async Task Audit_failure_rolls_back_all_role_changes_and_allows_safe_retry()
    {
        await Seed("Viewer");
        var snapshot = await Scalar(InventorySql());
        await Sql("""
            CREATE FUNCTION reject_migration_audit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'test outage'; END $$;
            CREATE TRIGGER migration_audit_outage BEFORE INSERT ON security_audit_events FOR EACH ROW EXECUTE FUNCTION reject_migration_audit();
            """);
        await Assert.ThrowsAsync<PostgresException>(() => Apply(snapshot));
        Assert.Equal(snapshot, await Scalar(InventorySql()));
        Assert.Equal("0", await Scalar("SELECT count(*) FROM security_audit_events"));
        await Sql("DROP TRIGGER migration_audit_outage ON security_audit_events; DROP FUNCTION reject_migration_audit()");
        await Apply(snapshot);
        Assert.Equal("Staff", await Scalar("SELECT role FROM organization_memberships"));
        Assert.Equal("1", await Scalar("SELECT count(*) FROM security_audit_events"));
    }
}

[CollectionDefinition("Disposable PostgreSQL", DisableParallelization = true)]
public sealed class DisposablePostgresCollection;
