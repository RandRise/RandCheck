using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UaeDocumentIntelligence.Api;
using UaeDocumentIntelligence.Api.Security;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    private async Task<ApiRoleFixture> ApiRoleAsync()
    {
        var role = "phase6_api_" + Guid.NewGuid().ToString("N"); var password = Guid.NewGuid().ToString("N");
        await ExecuteAsync($"CREATE ROLE {role} LOGIN PASSWORD '{password}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS");
        try
        {
            await using var owner = _database.CreateContext(); await ApiDatabasePolicy.ConfigureAsync(owner, role, default);
            var connection = new NpgsqlConnectionStringBuilder(_database.TestConnectionString) { Username = role, Password = password };
            return new(this, role, connection.ConnectionString);
        }
        catch { await ExecuteAsync($"DROP OWNED BY {role}; DROP ROLE {role}"); throw; }
    }
    private sealed record ApiRoleFixture(PostgresPersistenceTests Test, string Role, string Connection) : IAsyncDisposable
    {
        public AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Connection).Options);
        public WebApplicationFactory<global::Program> Host(WebApplicationFactory<global::Program> host) => host.WithWebHostBuilder(b =>
            b.UseSetting("ConnectionStrings:Database", Connection).UseSetting("ApiDatabase:Enabled", "true").UseSetting("ApiDatabase:DatabaseRole", Role));
        public ValueTask DisposeAsync() => new(Test.ExecuteAsync($"DROP OWNED BY {Role}; DROP ROLE {Role}"));
    }
    private static HttpClient WithAuthority(WebApplicationFactory<global::Program> host, HttpClient original)
    { var client = host.CreateClient(); client.DefaultRequestHeaders.Authorization = original.DefaultRequestHeaders.Authorization; return client; }

    [Fact]
    public async Task Phase6_api_database_restricted_login_supports_membership_source_retention_and_deletion()
    {
        using var source = await DeletionFixtureAsync(); await using var role = await ApiRoleAsync();
        using var host = role.Host(source.Host); using var admin = WithAuthority(host, source.Admin); using var user = WithAuthority(host, source.User);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/session")).StatusCode);
        var member = await SecurityAdd(admin, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, (await SecurityChange(admin, member, "Reviewer", false, true)).StatusCode);
        var grantResponse = await user.PostAsync($"/api/documents/{source.Source.DocumentId}/source-access", null);
        Assert.Equal(HttpStatusCode.OK, grantResponse.StatusCode);
        var grant = (await grantResponse.Content.ReadFromJsonAsync<DocumentSourceEndpoints.AccessGrant>())!;
        using var download = SourceRequest(grant); Assert.Equal(HttpStatusCode.OK, (await user.SendAsync(download)).StatusCode);
        var plan = (await admin.GetFromJsonAsync<DocumentDeletionPreview>(DeletePath(source.Source.DocumentId)))!;
        var deleted = await admin.PostAsJsonAsync(DeletePath(source.Source.DocumentId), new RetentionEndpoints.DeleteDocument(plan.DocumentVersion, plan.PolicyVersion));
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode); Assert.False(File.Exists(source.Source.File));
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/organization/audit")).StatusCode);
        await using var db = role.Db(); await ApiDatabasePolicy.VerifyAsync(db, role.Role, true, default);
    }

    [Theory]
    [InlineData("membership")]
    [InlineData("identity_update")]
    [InlineData("audit_delete")]
    [InlineData("grant_option")]
    [InlineData("missing_read")]
    [InlineData("private_write")]
    [InlineData("extra_table")]
    [InlineData("outside_schema")]
    [InlineData("sequence")]
    [InlineData("createdb")]
    [InlineData("definer")]
    [InlineData("zero_column_table")]
    [InlineData("schema_usage")]
    public async Task Phase6_api_database_drift_blocks_the_next_HTTP_request(string change)
    {
        var org = await SecurityBootstrap(); await using var role = await ApiRoleAsync();
        using var baseHost = SecurityHost(); using var host = role.Host(baseHost); using var admin = SecurityHttp(host, org.Subject);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/session")).StatusCode);
        var extraRole = "phase6_extra_" + Guid.NewGuid().ToString("N");
        var sql = change switch
        {
            "membership" => $"CREATE ROLE {extraRole} NOLOGIN; GRANT {extraRole} TO {role.Role}",
            "identity_update" => $"GRANT UPDATE (organization_id) ON documents TO {role.Role}",
            "audit_delete" => $"GRANT DELETE ON security_audit_events TO {role.Role}",
            "grant_option" => $"GRANT SELECT ON documents TO {role.Role} WITH GRANT OPTION",
            "missing_read" => $"REVOKE SELECT ON documents FROM {role.Role}",
            "private_write" => $"GRANT INSERT ON cited_spend_events TO {role.Role}",
            "outside_schema" => $"CREATE SCHEMA api_extra_schema; CREATE TABLE api_extra_schema.items(id int); GRANT USAGE ON SCHEMA api_extra_schema TO {role.Role}; GRANT SELECT ON api_extra_schema.items TO {role.Role}",
            "sequence" => $"CREATE SEQUENCE api_extra_sequence; GRANT USAGE ON api_extra_sequence TO {role.Role}",
            "createdb" => $"ALTER ROLE {role.Role} CREATEDB",
            "definer" => "CREATE FUNCTION public.api_extra_definer() RETURNS int LANGUAGE sql SECURITY DEFINER SET search_path=pg_catalog AS 'SELECT 1'",
            "zero_column_table" => $"CREATE TABLE api_extra_zero(); GRANT INSERT ON api_extra_zero TO {role.Role}",
            "schema_usage" => $"REVOKE USAGE ON SCHEMA public FROM PUBLIC, {role.Role}",
            _ => $"CREATE TABLE api_extra_test(id int); GRANT SELECT ON api_extra_test TO {role.Role}"
        };
        try
        {
            await ExecuteAsync(sql); var response = await admin.GetAsync("/api/session");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain(role.Role, body); Assert.DoesNotContain(role.Connection, body);
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/health")).StatusCode);
        }
        finally
        {
            if (change == "membership") await ExecuteAsync($"REVOKE {extraRole} FROM {role.Role}; DROP ROLE {extraRole}");
            if (change == "extra_table") await ExecuteAsync("DROP TABLE api_extra_test");
            if (change == "outside_schema") await ExecuteAsync("DROP SCHEMA api_extra_schema CASCADE");
            if (change == "sequence") await ExecuteAsync("DROP SEQUENCE api_extra_sequence");
            if (change == "definer") await ExecuteAsync("DROP FUNCTION public.api_extra_definer()");
            if (change == "zero_column_table") await ExecuteAsync("DROP TABLE api_extra_zero");
        }
    }

    [Fact]
    public async Task Phase6_api_database_grants_deny_identity_history_accounting_and_worker_authority()
    {
        await using var role = await ApiRoleAsync(); await using var db = role.Db();
        foreach (var sql in new[] { "UPDATE documents SET organization_id=gen_random_uuid()", "UPDATE actors SET is_active=false",
            "UPDATE organization_memberships SET actor_id=gen_random_uuid()", "DELETE FROM review_actions", "UPDATE security_audit_events SET action='changed'",
            "UPDATE cited_pilot_budgets SET settled_usd=1", "UPDATE cited_pilot_budgets SET requires_reconciliation=false",
            "DELETE FROM document_deletions", "UPDATE extraction_jobs SET attempt_count=100", "UPDATE extraction_runs SET estimated_cost=0",
            "INSERT INTO cited_spend_events DEFAULT VALUES", "TRUNCATE documents CASCADE", "CREATE TABLE public.api_forbidden(id int)" })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql)); Assert.Equal("42501", error.SqlState);
        }
        await db.Database.ExecuteSqlRawAsync("SELECT id FROM cited_pilot_budgets WHERE id=1 FOR SHARE");
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE cited_pilot_budgets SET id=2"));
        await using var owner = _database.CreateContext(); await ApiDatabasePolicy.ConfigureAsync(owner, role.Role, default);
        await ApiDatabasePolicy.VerifyAsync(db, role.Role, true, default);
    }

    [Fact]
    public async Task Phase6_api_database_private_cli_applies_verifies_and_refuses_wrong_database()
    {
        await using var role = await ApiRoleAsync();
        var dll = Path.Combine(AppContext.BaseDirectory, "EnterpriseAdmin.dll");
        if (!File.Exists(dll))
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs/continuation.md"))) root = root.Parent;
            var configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
            dll = Path.Combine(root!.FullName, "apps/backend/tools/EnterpriseAdmin/bin", configuration, "net8.0/EnterpriseAdmin.dll");
        }
        Assert.True(File.Exists(dll));
        var file = Path.Combine(Path.GetTempPath(), "p6-api-role-" + Guid.NewGuid().ToString("N") + ".json");
        async Task<int> Run(string command)
        {
            var start = new System.Diagnostics.ProcessStartInfo("dotnet")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var value in new[] { dll, command, file }) start.ArgumentList.Add(value);
            start.Environment["PHASE6_ADMIN_CONNECTION"] = _database.TestConnectionString;
            using var process = System.Diagnostics.Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(); Assert.DoesNotContain(_database.TestConnectionString, await output + await error); return process.ExitCode;
        }
        try
        {
            await File.WriteAllTextAsync(file, JsonSerializer.Serialize(new { databaseRole = role.Role, expectedDatabase = "document_intelligence_test" }));
            Assert.Equal(0, await Run("api-role")); Assert.Equal(0, await Run("verify-api-role"));
            await File.WriteAllTextAsync(file, JsonSerializer.Serialize(new { databaseRole = role.Role, expectedDatabase = "wrong_database" }));
            Assert.Equal(1, await Run("api-role"));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public async Task Phase6_api_database_grant_audit_outage_rolls_back_configuration()
    {
        var role = "phase6_api_" + Guid.NewGuid().ToString("N"); await ExecuteAsync($"CREATE ROLE {role} NOLOGIN");
        await ExecuteAsync("""
            CREATE FUNCTION api_grant_audit_outage() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
              IF NEW.action='api.database-role-configured' THEN RAISE EXCEPTION 'test outage'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER api_grant_audit_outage BEFORE INSERT ON security_audit_events FOR EACH ROW EXECUTE FUNCTION api_grant_audit_outage();
            """);
        try
        {
            await using var db = _database.CreateContext(); await Assert.ThrowsAsync<DbUpdateException>(() => ApiDatabasePolicy.ConfigureAsync(db, role, default));
            Assert.False(await ScalarAsync<bool>("SELECT has_table_privilege(@role,'documents','SELECT')", ("role", role)));
        }
        finally { await ExecuteAsync($"DROP TRIGGER api_grant_audit_outage ON security_audit_events; DROP FUNCTION api_grant_audit_outage(); DROP OWNED BY {role}; DROP ROLE {role}"); }
    }
}
