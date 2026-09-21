using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using UaeDocumentIntelligence.Api.Security;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Persistence;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Theory]
    [InlineData("http", HttpStatusCode.ServiceUnavailable)]
    [InlineData("https", HttpStatusCode.OK)]
    public async Task Phase6_retention_production_requires_https(string scheme, HttpStatusCode expected)
    {
        using var f = await SourceFixtureAsync();
        using var host = f.Host.WithWebHostBuilder(b => b.UseEnvironment("Production"));
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri($"{scheme}://localhost") });
        client.DefaultRequestHeaders.Authorization = f.Admin.DefaultRequestHeaders.Authorization;
        Assert.Equal(expected, (await client.GetAsync("/api/organization/retention")).StatusCode);
    }
    [Fact]
    public async Task Phase6_retention_concurrent_policy_changes_have_one_winner()
    {
        using var f = await SourceFixtureAsync(); var p = await RetentionPolicy(f);
        var responses = await Task.WhenAll(f.Admin.PutAsJsonAsync("/api/organization/retention", new RetentionEndpoints.ChangePolicy(p.Version, 2, 2)),
            f.Admin.PutAsJsonAsync("/api/organization/retention", new RetentionEndpoints.ChangePolicy(p.Version, 3, 3)));
        Assert.Single(responses.Where(x => x.StatusCode == HttpStatusCode.OK)); Assert.Single(responses.Where(x => x.StatusCode == HttpStatusCode.Conflict));
    }
    [Fact]
    public async Task Phase6_retention_cleanup_waits_for_hold_commit_and_rechecks_it()
    {
        using var f = await SourceFixtureAsync(); var p = await RetentionPolicy(f); var org = await SourceOrganization(f); var file = Stage(f, org);
        await using var db = _database.CreateContext(); await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM organizations WHERE id={org} FOR NO KEY UPDATE");
        var cleanup = f.Admin.PostAsJsonAsync("/api/organization/quarantine/cleanup", new RetentionEndpoints.Cleanup(p.Version, true));
        var waiting = false;
        for (var attempt = 0; attempt < 100 && !waiting; attempt++)
        {
            await using var connection = await _database.OpenConnectionAsync();
            await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock' AND query LIKE '%FOR NO KEY UPDATE%')", connection);
            waiting = (bool)(await command.ExecuteScalarAsync())!;
            if (!waiting) await Task.Delay(25);
        }
        Assert.True(waiting, "Cleanup must be blocked by the organization lock.");
        db.RetentionHolds.Add(new UaeDocumentIntelligence.Domain.Model.RetentionHold(Guid.NewGuid(), org, null, RetentionHoldReason.Investigation, f.Clock.UtcNow));
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        var response = await cleanup; Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, (await response.Content.ReadFromJsonAsync<QuarantineCleanupResult>())!.Deleted); Assert.True(File.Exists(file));
    }
    [Fact]
    public async Task Phase6_retention_empty_schema_can_downgrade_and_reapply()
    {
        await using var db = _database.CreateContext(); var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260911162600_AddEnterpriseMembershipAndAudit");
        Assert.False(await TableExistsAsync("retention_holds"));
        await migrator.MigrateAsync(); Assert.True(await TableExistsAsync("retention_holds"));
    }
    [Fact]
    public async Task Phase6_retention_defaults_preserve_and_policy_hold_release_are_versioned_and_audited()
    {
        using var f = await SourceFixtureAsync();
        var initial = (await f.Admin.GetFromJsonAsync<RetentionPolicyView>("/api/organization/retention"))!;
        Assert.Equal(Guid.Empty, initial.Version); Assert.Null(initial.DocumentDays); Assert.Null(initial.QuarantineHours);
        var preview = (await f.Admin.GetFromJsonAsync<RetentionPreview>("/api/organization/retention/preview"))!;
        Assert.False(preview.DocumentDeletionEnabled); Assert.Equal("Retained", Assert.Single(preview.Documents).Disposition);
        var p = await RetentionPolicy(f);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Admin.PutAsJsonAsync("/api/organization/retention", new RetentionEndpoints.ChangePolicy(Guid.Empty, 2, 2))).StatusCode);
        f.Clock.UtcNow = f.Clock.UtcNow.AddDays(3);
        preview = (await f.Admin.GetFromJsonAsync<RetentionPreview>("/api/organization/retention/preview"))!;
        Assert.Equal("PastRetentionWindow", Assert.Single(preview.Documents).Disposition);
        var hold = await RetentionHold(f, f.DocumentId);
        preview = (await f.Admin.GetFromJsonAsync<RetentionPreview>("/api/organization/retention/preview"))!;
        Assert.Equal("Held", Assert.Single(preview.Documents).Disposition);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Admin.PostAsJsonAsync($"/api/organization/retention/holds/{hold.Id}/release", new RetentionEndpoints.ReleaseHold(Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Admin.PostAsJsonAsync($"/api/organization/retention/holds/{hold.Id}/release", new RetentionEndpoints.ReleaseHold(hold.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Admin.PostAsJsonAsync($"/api/organization/retention/holds/{hold.Id}/release", new RetentionEndpoints.ReleaseHold(hold.Version))).StatusCode);
        var listed = (await f.Admin.GetFromJsonAsync<RetentionHoldView[]>("/api/organization/retention/holds"))!;
        Assert.NotNull(Assert.Single(listed).ReleasedAtUtc); Assert.True(File.Exists(f.File));
        await using var db = _database.CreateContext();
        var events = await db.SecurityAuditEvents.Where(x => x.Action.StartsWith("retention.")).ToArrayAsync();
        Assert.Equal(3, events.Length); Assert.Contains(events, x => x.Action == "retention.hold-released");
        Assert.DoesNotContain("PRIVATE", System.Text.Json.JsonSerializer.Serialize(events));
        Assert.Equal(1, await db.Documents.CountAsync());
    }
    [Fact]
    public async Task Phase6_retention_rejects_foreign_holds_documents_and_nonadministrators()
    {
        using var a = await SourceFixtureAsync(); using var b = await SourceFixtureAsync();
        var hold = await RetentionHold(a, a.DocumentId);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Admin.PostAsJsonAsync("/api/organization/retention/holds", new RetentionEndpoints.PlaceHold(a.DocumentId, "LegalHold"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Admin.PostAsJsonAsync($"/api/organization/retention/holds/{hold.Id}/release", new RetentionEndpoints.ReleaseHold(hold.Version))).StatusCode);
        Assert.Empty((await b.Admin.GetFromJsonAsync<RetentionHoldView[]>("/api/organization/retention/holds"))!);
        var preview = (await b.Admin.GetFromJsonAsync<RetentionPreview>("/api/organization/retention/preview"))!;
        Assert.Equal(b.DocumentId, Assert.Single(preview.Documents).DocumentId);
        foreach (var route in EnterprisePermissions.Routes.Where(x => x.Value == "Retention"))
        {
            var parts = route.Key.Split(' ', 2);
            using var request = new HttpRequestMessage(new HttpMethod(parts[0]), parts[1].Replace("{id:guid}", hold.Id.ToString()));
            Assert.Equal(HttpStatusCode.Forbidden, (await a.User.SendAsync(request)).StatusCode);
        }
    }
    [Theory]
    [InlineData("invalid-policy")]
    [InlineData("invalid-reason")]
    [InlineData("disabled")]
    [InlineData("version")]
    public async Task Phase6_retention_invalid_requests_cannot_enable_cleanup(string scenario)
    {
        using var f = await SourceFixtureAsync();
        var response = scenario switch
        {
            "invalid-policy" => await f.Admin.PutAsJsonAsync("/api/organization/retention", new RetentionEndpoints.ChangePolicy(Guid.Empty, 0, 0)),
            "invalid-reason" => await f.Admin.PostAsJsonAsync("/api/organization/retention/holds", new RetentionEndpoints.PlaceHold(null, "1")),
            "disabled" => await f.Admin.PostAsJsonAsync("/api/organization/quarantine/cleanup", new RetentionEndpoints.Cleanup((await RetentionPolicy(f, null)).Version, true)),
            _ => await f.Admin.PostAsJsonAsync("/api/organization/quarantine/cleanup", new RetentionEndpoints.Cleanup(Guid.NewGuid(), true))
        };
        Assert.Equal(scenario.StartsWith("invalid") ? HttpStatusCode.BadRequest : HttpStatusCode.Conflict, response.StatusCode);
        Assert.True(File.Exists(f.File));
    }
    [Fact]
    public async Task Phase6_quarantine_preview_preserves_and_explicit_cleanup_has_durable_authorization_and_completion()
    {
        using var f = await SourceFixtureAsync(); var p = await RetentionPolicy(f);
        var org = await SourceOrganization(f); var file = Stage(f, org);
        var preview = await Cleanup(f, p, false);
        Assert.Equal(1, preview.Eligible); Assert.Equal(0, preview.Deleted); Assert.True(File.Exists(file));
        var result = await Cleanup(f, p, true);
        Assert.Equal(1, result.Deleted); Assert.False(File.Exists(file)); Assert.True(File.Exists(f.File));
        Assert.False(File.Exists(Path.ChangeExtension(file, ".lease")));
        Assert.Equal(0, (await Cleanup(f, p, true)).Deleted);
        await using var db = _database.CreateContext();
        var events = await db.SecurityAuditEvents.Where(x => x.Action.StartsWith("quarantine.")).ToArrayAsync();
        Assert.Equal(2, events.Length); Assert.Single(events.Where(x => x.Action == "quarantine.cleanup-authorized"));
        Assert.Single(events.Where(x => x.Action == "quarantine.cleaned"));
        Assert.All(events, x => Assert.Equal(org, x.OrganizationId));
    }
    [Theory]
    [InlineData("held")]
    [InlineData("persisted-document")]
    [InlineData("foreign")]
    [InlineData("legacy")]
    [InlineData("young")]
    [InlineData("active")]
    [InlineData("missing-lease")]
    public async Task Phase6_quarantine_preserves_held_referenced_foreign_legacy_young_and_active_uploads(string scenario)
    {
        using var f = await SourceFixtureAsync(); var p = await RetentionPolicy(f); var org = await SourceOrganization(f);
        var file = Stage(f, scenario == "foreign" ? Guid.NewGuid() : org, scenario == "persisted-document" ? f.DocumentId : null);
        var leasePath = Path.ChangeExtension(file, ".lease");
        if (scenario == "held") await RetentionHold(f, null);
        if (scenario == "legacy") { var legacy = Path.Combine(Path.GetDirectoryName(file)!, Guid.NewGuid().ToString("N") + ".upload"); File.Move(file, legacy); file = legacy; }
        if (scenario == "young") File.SetLastWriteTimeUtc(file, f.Clock.UtcNow.UtcDateTime);
        if (scenario == "missing-lease") File.Delete(leasePath);
        using var lease = scenario == "active" ? new FileStream(leasePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None) : null;
        var result = await Cleanup(f, p, true);
        Assert.Equal(0, result.Deleted); Assert.True(File.Exists(file)); Assert.True(File.Exists(f.File));
    }
    [Theory]
    [InlineData("retention.policy-changed")]
    [InlineData("retention.hold-placed")]
    [InlineData("quarantine.cleanup-authorized")]
    [InlineData("quarantine.cleaned")]
    public async Task Phase6_retention_audit_failure_blocks_mutation_or_exposes_unresolved_completion(string action)
    {
        using var f = await SourceFixtureAsync(); var p = await RetentionPolicy(f); var file = Stage(f, await SourceOrganization(f));
        await ExecuteAsync($"""
            CREATE FUNCTION p6_retention_outage() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                IF NEW.action='{action}' THEN RAISE EXCEPTION 'test outage'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER p6_retention_outage BEFORE INSERT ON security_audit_events FOR EACH ROW EXECUTE FUNCTION p6_retention_outage();
            """);
        try
        {
            var response = action switch
            {
                "retention.policy-changed" => await f.Admin.PutAsJsonAsync("/api/organization/retention", new RetentionEndpoints.ChangePolicy(p.Version, 5, 5)),
                "retention.hold-placed" => await f.Admin.PostAsJsonAsync("/api/organization/retention/holds", new RetentionEndpoints.PlaceHold(null, "LegalHold")),
                _ => await f.Admin.PostAsJsonAsync("/api/organization/quarantine/cleanup", new RetentionEndpoints.Cleanup(p.Version, true))
            };
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal(action != "quarantine.cleaned", File.Exists(file)); Assert.True(File.Exists(f.File));
            await using var db = _database.CreateContext();
            Assert.Equal(p.Version, (await db.OrganizationRetentionPolicies.SingleAsync()).Version);
            Assert.Empty(await db.RetentionHolds.ToArrayAsync());
            Assert.False(await db.SecurityAuditEvents.AnyAsync(x => x.Action == "quarantine.cleaned"));
            Assert.Equal(action == "quarantine.cleaned", await db.SecurityAuditEvents.AnyAsync(x => x.Action == "quarantine.cleanup-authorized"));
        }
        finally { await ExecuteAsync("DROP TRIGGER p6_retention_outage ON security_audit_events; DROP FUNCTION p6_retention_outage()"); }
    }
    [Fact]
    public async Task Phase6_retention_history_cannot_be_rewritten_deleted_or_downgraded()
    {
        using var f = await SourceFixtureAsync(); await RetentionPolicy(f); var hold = await RetentionHold(f, null);
        foreach (var sql in new[] { "DELETE FROM retention_holds", "TRUNCATE retention_holds", "DELETE FROM organization_retention_policies", "TRUNCATE organization_retention_policies", "UPDATE retention_holds SET reason='Investigation'" })
            await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(sql));
        Assert.Equal(HttpStatusCode.OK, (await f.Admin.PostAsJsonAsync($"/api/organization/retention/holds/{hold.Id}/release", new RetentionEndpoints.ReleaseHold(hold.Version))).StatusCode);
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync("UPDATE retention_holds SET released_at_utc=NULL"));
        await using var db = _database.CreateContext();
        await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20260911162600_AddEnterpriseMembershipAndAudit"));
        Assert.Equal(1, await db.RetentionHolds.CountAsync());
    }
    private static async Task<RetentionPolicyView> RetentionPolicy(SourceFixture f, int? hours = 1)
    {
        var response = await f.Admin.PutAsJsonAsync("/api/organization/retention", new RetentionEndpoints.ChangePolicy(Guid.Empty, 1, hours));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<RetentionPolicyView>())!;
    }
    private static async Task<RetentionHoldView> RetentionHold(SourceFixture f, Guid? document)
    {
        var response = await f.Admin.PostAsJsonAsync("/api/organization/retention/holds", new RetentionEndpoints.PlaceHold(document, "LegalHold"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<RetentionHoldView>())!;
    }
    private async Task<Guid> SourceOrganization(SourceFixture f)
    { await using var db = _database.CreateContext(); return await db.Documents.Where(x => x.Id == f.DocumentId).Select(x => x.OrganizationId).SingleAsync(); }
    private static string Stage(SourceFixture f, Guid organization, Guid? document = null)
    {
        var root = Path.Combine(f.Root, "quarantine"); Directory.CreateDirectory(root);
        var path = Path.Combine(root, QuarantineFiles.Key(organization, document ?? Guid.NewGuid(), Guid.NewGuid()));
        File.WriteAllBytes(path, [1, 2, 3]); var lease = Path.ChangeExtension(path, ".lease"); File.WriteAllBytes(lease, []);
        File.SetLastWriteTimeUtc(path, f.Clock.UtcNow.AddDays(-2).UtcDateTime); File.SetLastWriteTimeUtc(lease, f.Clock.UtcNow.AddDays(-2).UtcDateTime);
        return path;
    }
    private static async Task<QuarantineCleanupResult> Cleanup(SourceFixture f, RetentionPolicyView policy, bool execute)
    {
        var response = await f.Admin.PostAsJsonAsync("/api/organization/quarantine/cleanup", new RetentionEndpoints.Cleanup(policy.Version, execute));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<QuarantineCleanupResult>())!;
    }
}
