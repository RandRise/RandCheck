using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using UaeDocumentIntelligence.Api.Security;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    private static readonly Guid SecurityTenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SecurityAudience = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid SecurityClient = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly RSA SecurityRsa = RSA.Create(2048);
    private static readonly RsaSecurityKey SecurityKey = new(SecurityRsa) { KeyId = "phase6-postgres-offline" };
    private static string SecurityIssuer => $"https://login.microsoftonline.com/{SecurityTenant:D}/v2.0";

    private WebApplicationFactory<global::Program> SecurityHost(string? python = null,
        WebApplicationFactory<global::Program>? factory = null) =>
        (factory ?? new WebApplicationFactory<global::Program>()).WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing").UseSetting("Security:Mode", "Entra")
                .UseSetting("Security:TenantId", SecurityTenant.ToString()).UseSetting("Security:Audience", SecurityAudience.ToString())
                .UseSetting("Security:AllowedClientIds:0", SecurityClient.ToString())
                .UseSetting("ConnectionStrings:Database", _database.TestConnectionString)
                .UseSetting("DevelopmentData:Seed", "false").UseSetting("ExtractionWorker:Enabled", "false")
                .UseSetting("StorageMaintenance:Enabled", "false").UseSetting("Logging:LogLevel:Default", "Warning")
                .UseSetting("AiService:BaseUrl", python ?? "http://127.0.0.1:1")
                .UseSetting("AiService:WorkerToken", "test-only-cited-token");
            builder.ConfigureServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.Configure<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>(options =>
                    options.XmlRepository = new SecurityTestKeys());
                services.PostConfigure<JwtBearerOptions>("Enterprise", options =>
                {
                    var metadata = new OpenIdConnectConfiguration { Issuer = SecurityIssuer };
                    metadata.SigningKeys.Add(SecurityKey);
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
                });
            });
        });

    private static HttpClient SecurityHttp(WebApplicationFactory<global::Program> host, Guid subject)
    {
        var client = host.CreateClient();
        var token = new JwtSecurityToken(SecurityIssuer, SecurityAudience.ToString(),
            [new Claim("ver", "2.0"), new Claim("tid", SecurityTenant.ToString()), new Claim("oid", subject.ToString()),
             new Claim("azp", SecurityClient.ToString()), new Claim("scp", "DocumentIntelligence.Access"),
             new Claim("roles", "DocumentIntelligence.Operator")], // deliberately cannot confer DB rights
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(30), new SigningCredentials(SecurityKey, SecurityAlgorithms.RsaSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    private AppDbContext SecurityDb(SecurityAuditContext audit) => new(
        new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_database.TestConnectionString).Options, audit);

    private sealed class SecurityTestKeys : Microsoft.AspNetCore.DataProtection.Repositories.IXmlRepository
    {
        private readonly System.Collections.Concurrent.ConcurrentBag<System.Xml.Linq.XElement> _keys = [];
        public IReadOnlyCollection<System.Xml.Linq.XElement> GetAllElements() => _keys.ToArray();
        public void StoreElement(System.Xml.Linq.XElement element, string friendlyName) => _keys.Add(new(element));
    }

    private async Task<(BootstrapOrganization Organization, Guid Subject)> SecurityBootstrap()
    {
        var subject = Guid.NewGuid();
        var audit = new SecurityAuditContext();
        await using var db = SecurityDb(audit);
        var service = new EnterpriseMembershipService(db, new RequestIdentity(), audit);
        var result = await service.BootstrapAsync("test-" + Guid.NewGuid().ToString("N"), "Fictional enterprise",
            SecurityTenant, subject, "Private administrator name", Guid.NewGuid().ToString("N"), default);
        return (result, subject);
    }

    private static async Task<MembershipView> SecurityAdd(HttpClient admin, Guid subject, string role = "Staff", bool manage = false)
    {
        var response = await admin.PostAsJsonAsync("/api/organization/memberships",
            new MembershipEndpoints.AddMember(SecurityTenant, subject, "Sensitive display name", manage ? "Admin" : role));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MembershipView>())!;
    }

    private static Task<HttpResponseMessage> SecurityChange(HttpClient admin, MembershipView member, string role, bool manage, bool active) =>
        admin.PutAsJsonAsync($"/api/organization/memberships/{member.Id}",
            new MembershipEndpoints.ChangeMember(member.Version, role, active));

    [Fact]
    public async Task Phase6_browser_session_returns_database_access_and_rechecks_revocation()
    {
        var a = await SecurityBootstrap();
        using var host = SecurityHost();
        using var admin = SecurityHttp(host, a.Subject);
        var initial = await admin.GetFromJsonAsync<SessionEndpoints.SessionView>("/api/session");
        Assert.Equal(a.Organization.OrganizationId, initial!.OrganizationId);
        Assert.Equal("Fictional enterprise", initial.OrganizationName);
        Assert.Equal("Admin", initial.Role);
        Assert.Equal("Admin", initial.Role);
        Assert.Equal(SecurityTenant, initial.TenantId);
        var subject = Guid.NewGuid();
        var member = await SecurityAdd(admin, subject);
        using var viewer = SecurityHttp(host, subject);
        var view = await viewer.GetFromJsonAsync<SessionEndpoints.SessionView>("/api/session");
        Assert.Equal(member.ActorId, view!.ActorId);
        Assert.Equal("Staff", view.Role); // the signed token's Operator role is ignored
        Assert.Equal("Staff", view.Role);
        var changed = await SecurityChange(admin, member, "Admin", false, true);
        changed.EnsureSuccessStatusCode();
        member = (await changed.Content.ReadFromJsonAsync<MembershipView>())!;
        Assert.Equal("Admin", (await viewer.GetFromJsonAsync<SessionEndpoints.SessionView>("/api/session"))!.Role);
        (await SecurityChange(admin, member, "Admin", false, false)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/session")).StatusCode);
        using var stranger = SecurityHttp(host, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync("/api/session")).StatusCode);
    }

    [Fact]
    public async Task Phase6_bootstrap_membership_permissions_revocation_and_audit_are_enforced_over_HTTP()
    {
        var a = await SecurityBootstrap();
        var b = await SecurityBootstrap();
        using var host = SecurityHost();
        using var admin = SecurityHttp(host, a.Subject);
        using var otherAdmin = SecurityHttp(host, b.Subject);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/documents")).StatusCode);
        var subject = Guid.NewGuid();
        var viewer = await SecurityAdd(admin, subject);
        using var user = SecurityHttp(host, subject);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/organization/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/organization/memberships")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SecurityChange(user, viewer, "Staff", true, true)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SecurityChange(otherAdmin, viewer, "Staff", true, true)).StatusCode);
        var duplicate = await otherAdmin.PostAsJsonAsync("/api/organization/memberships",
            new MembershipEndpoints.AddMember(SecurityTenant, subject, "Sensitive name", "Staff"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var revokedResponse = await SecurityChange(admin, viewer, "Staff", false, false);
        Assert.Equal(HttpStatusCode.OK, revokedResponse.StatusCode);
        var revoked = (await revokedResponse.Content.ReadFromJsonAsync<MembershipView>())!;
        Assert.Equal(viewer.ActorId, revoked.ActorId);
        Assert.NotEqual(viewer.Version, revoked.Version);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/documents")).StatusCode); // same valid JWT
        Assert.Equal(HttpStatusCode.Conflict, (await SecurityChange(admin, viewer, "Staff", false, true)).StatusCode);
        var moved = await SecurityAdd(otherAdmin, subject);
        Assert.NotEqual(viewer.ActorId, moved.ActorId);
        Assert.Equal(HttpStatusCode.Conflict, (await SecurityChange(admin, revoked, "Staff", false, true)).StatusCode);
        var auditResponse = await admin.GetAsync("/api/organization/audit");
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
        var body = await auditResponse.Content.ReadAsStringAsync();
        var events = JsonDocument.Parse(body).RootElement.EnumerateArray().ToArray();
        Assert.All(events, e => Assert.Equal(a.Organization.OrganizationId, e.GetProperty("organizationId").GetGuid()));
        Assert.Contains(events, e => e.GetProperty("action").GetString() == "membership.changed" && e.GetProperty("beforeState").GetString()!.Contains("active=True") && e.GetProperty("afterState").GetString()!.Contains("active=False"));
        Assert.Contains(events, e => e.GetProperty("outcome").GetString() == "Denied");
        Assert.Contains(events, e => e.GetProperty("outcome").GetString() == "Failed");
        Assert.DoesNotContain("Sensitive", body);
        Assert.DoesNotContain("Bearer", body);
        Assert.DoesNotContain(subject.ToString(), body);
        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "phase6-audit-example.json"),
            JsonSerializer.Serialize(events, new JsonSerializerOptions { WriteIndented = true }));
        await using var db = _database.CreateContext();
        Assert.Equal(2, await db.OrganizationMemberships.CountAsync(m => m.ActorId == viewer.ActorId || m.ActorId == moved.ActorId));
        Assert.Empty(await db.CitedSpendEvents.ToListAsync());
    }

    [Fact]
    public async Task Phase6_concurrent_administrator_removal_preserves_one_active_administrator()
    {
        var a = await SecurityBootstrap();
        using var host = SecurityHost();
        using var admin = SecurityHttp(host, a.Subject);
        var first = Assert.Single((await admin.GetFromJsonAsync<MembershipView[]>("/api/organization/memberships"))!);
        var secondSubject = Guid.NewGuid();
        var second = await SecurityAdd(admin, secondSubject, "Admin", true);
        using var secondAdmin = SecurityHttp(host, secondSubject);
        var responses = await Task.WhenAll(SecurityChange(admin, first, "Staff", false, false),
            SecurityChange(secondAdmin, second, "Staff", false, false));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        await using var db = _database.CreateContext();
        Assert.Equal(1, await db.OrganizationMemberships.CountAsync(m => m.OrganizationId == a.Organization.OrganizationId && m.IsActive && m.Role == OrganizationAccessRole.Admin));
        Assert.Equal(1, await db.SecurityAuditEvents.CountAsync(e => e.Action == "membership.changed"));
    }

    [Fact]
    public async Task Phase6_audit_insert_failure_rolls_back_membership_actor_and_identity()
    {
        var a = await SecurityBootstrap();
        using var host = SecurityHost();
        using var admin = SecurityHttp(host, a.Subject);
        var subject = Guid.NewGuid();
        var before = await ScalarAsync<long>("SELECT count(*) FROM security_audit_events");
        await ExecuteAsync("""
            CREATE FUNCTION phase6_test_reject_audit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                IF NEW.action='membership.granted' THEN RAISE EXCEPTION 'test-only audit outage'; END IF;
                RETURN NEW; END $$;
            CREATE TRIGGER phase6_test_audit_outage BEFORE INSERT ON security_audit_events
                FOR EACH ROW EXECUTE FUNCTION phase6_test_reject_audit();
            """);
        try
        {
            var response = await admin.PostAsJsonAsync("/api/organization/memberships",
                new MembershipEndpoints.AddMember(SecurityTenant, subject, "Never committed", "Staff"));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            await using var db = _database.CreateContext();
            Assert.False(await db.EnterpriseIdentities.AnyAsync(i => i.ObjectId == subject));
            Assert.False(await db.Actors.AnyAsync(i => i.DisplayName == "Never committed"));
            Assert.Equal(1, await db.OrganizationMemberships.CountAsync());
            Assert.Equal(before + 2, await db.SecurityAuditEvents.LongCountAsync());
            Assert.Single(await db.SecurityAuditEvents.Where(e => e.Action == "request.admitted").ToArrayAsync());
            Assert.Single(await db.SecurityAuditEvents.Where(e => e.Action == "request.failed").ToArrayAsync());
        }
        finally { await ExecuteAsync("DROP TRIGGER phase6_test_audit_outage ON security_audit_events; DROP FUNCTION phase6_test_reject_audit()"); }
        await SecurityAdd(admin, subject); // exact operation can be retried safely after recovery
    }

    [Fact]
    public async Task Phase6_audit_is_append_only_in_EF_SQL_and_migration_rollback()
    {
        await SecurityBootstrap();
        await using var db = _database.CreateContext();
        var item = await db.SecurityAuditEvents.FirstAsync();
        db.SecurityAuditEvents.Remove(item);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        foreach (var sql in new[] { "UPDATE security_audit_events SET reason_code='rewritten'", "DELETE FROM security_audit_events", "TRUNCATE security_audit_events" })
            await AssertSqlStateAsync("55000", sql);
        await using var fresh = _database.CreateContext();
        var migrations = (await fresh.Database.GetAppliedMigrationsAsync()).ToArray();
        var auditMigration = Array.IndexOf(migrations, "20260911162600_AddEnterpriseMembershipAndAudit");
        Assert.True(auditMigration > 0);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => fresh.GetService<IMigrator>().MigrateAsync(migrations[auditMigration - 1]));
        Assert.Equal(2, await fresh.SecurityAuditEvents.CountAsync());
    }

    [Fact]
    public async Task Phase6_authenticated_organizations_isolate_documents_reviews_metrics_cases_sources_questions_and_citations()
    {
        var seed = await SeedGraphAsync(); // retained-style System actors remain unchanged
        var a = await SecurityAttachFixtureHuman(seed.OrganizationA);
        var b = await SecurityAttachFixtureHuman(seed.OrganizationB);
        await ExecuteAsync("UPDATE review_sessions SET status='Completed',completed_at_utc=@now WHERE id=@id",
            ("now", DateTimeOffset.UtcNow), ("id", seed.SessionA));
        await using var python = await PrivateCitedServer.StartAsync();
        using var host = SecurityHost(python.Url);
        using var userA = SecurityHttp(host, a);
        using var userB = SecurityHttp(host, b);
        var own = await userA.GetAsync($"/api/documents/{seed.DocumentA}");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        var indexResponse = await userA.PostAsJsonAsync("/api/cited-indexes", new { documentId = seed.DocumentA, reviewSessionId = seed.SessionA });
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        var index = JsonDocument.Parse(await indexResponse.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
        var questionResponse = await userA.PostAsJsonAsync("/api/cited-questions", new { question = "total", requestKey = "phase6-private-question", indexIds = new[] { index }, topK = 5 });
        Assert.Equal(HttpStatusCode.OK, questionResponse.StatusCode);
        var question = JsonDocument.Parse(await questionResponse.Content.ReadAsStringAsync()).RootElement;
        var run = question.GetProperty("retrieval").GetProperty("id").GetGuid();
        var evidenceHit = question.GetProperty("retrieval").GetProperty("hits").EnumerateArray()
            .First(h => h.GetProperty("reviewActionId").ValueKind != JsonValueKind.Null || h.GetProperty("spans").GetArrayLength() > 0);
        var hit = evidenceHit.GetProperty("hitId").GetGuid();
        var spanQuery = evidenceHit.GetProperty("spans").GetArrayLength() > 0
            ? "?spanId=" + evidenceHit.GetProperty("spans")[0].GetProperty("id").GetGuid() : "";
        Assert.Equal(HttpStatusCode.OK, (await userA.GetAsync($"/api/cited-questions/{run}/hits/{hit}/evidence" + spanQuery)).StatusCode);
        var sameOrganizationSubject = await SecurityAttachFixtureHuman(seed.OrganizationA);
        using var sameOrganizationPeer = SecurityHttp(host, sameOrganizationSubject);
        Assert.Equal(HttpStatusCode.OK, (await sameOrganizationPeer.GetAsync($"/api/documents/{seed.DocumentA}")).StatusCode);
        foreach (var path in new[] { $"/api/cited-retrievals/{run}", $"/api/cited-questions/{run}", $"/api/cited-questions/{run}/hits/{hit}/evidence" + spanQuery })
            Assert.Equal(HttpStatusCode.NotFound, (await sameOrganizationPeer.GetAsync(path)).StatusCode);
        var caseResponse = await userA.PostAsJsonAsync("/api/reconciliation-cases", new { name = "Private case title" });
        Assert.True(caseResponse.IsSuccessStatusCode, await caseResponse.Content.ReadAsStringAsync());
        var caseId = JsonDocument.Parse(await caseResponse.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
        var foreign = new[] { $"/api/documents/{seed.DocumentA}", $"/api/documents/{seed.DocumentA}/review",
            $"/api/documents/{seed.DocumentA}/review-history", $"/api/documents/{seed.DocumentA}/processing",
            $"/api/reconciliation-cases/{caseId}", $"/api/cited-retrievals/{run}", $"/api/cited-questions/{run}",
            $"/api/cited-questions/{run}/hits/{hit}/evidence" + spanQuery };
        foreach (var path in foreign)
        {
            var response = await userB.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain("org-a-document", await response.Content.ReadAsStringAsync());
        }
        userB.DefaultRequestHeaders.Add("X-Organization-ID", seed.OrganizationA.ToString());
        var listing = await userB.GetStringAsync("/api/documents?organizationId=" + seed.OrganizationA);
        var listed = Assert.Single(JsonDocument.Parse(listing).RootElement.EnumerateArray());
        Assert.Equal(seed.DocumentB, listed.GetProperty("id").GetGuid());
        Assert.DoesNotContain(seed.DocumentA.ToString(), listing);
        var metrics = await userB.GetStringAsync("/api/metrics");
        Assert.Equal(1, JsonDocument.Parse(metrics).RootElement.GetProperty("totalDocuments").GetInt32());
        Assert.Equal(0, JsonDocument.Parse(metrics).RootElement.GetProperty("review").GetProperty("completedSessionCount").GetInt32());
        var sources = await userB.GetStringAsync("/api/cited-sources");
        Assert.Empty(JsonDocument.Parse(sources).RootElement.GetProperty("sources").EnumerateArray());
        var ownSources = await userA.GetStringAsync("/api/cited-sources");
        Assert.Equal(seed.DocumentA, Assert.Single(JsonDocument.Parse(ownSources).RootElement.GetProperty("sources").EnumerateArray()).GetProperty("documentId").GetGuid());
        var guessed = await userB.PostAsJsonAsync("/api/cited-questions", new { question = "Private question content", requestKey = "foreign", indexIds = new[] { index }, topK = 5 });
        Assert.False(guessed.IsSuccessStatusCode);
        await using var db = _database.CreateContext();
        var events = await db.SecurityAuditEvents.ToArrayAsync();
        Assert.Contains(events, e => e.Action == "evidence.index-changed" && e.OrganizationId == seed.OrganizationA);
        Assert.Contains(events, e => e.Action == "question.saved" && e.OrganizationId == seed.OrganizationA);
        Assert.Contains(events, e => e.Action == "reconciliation.case-changed" && e.OrganizationId == seed.OrganizationA);
        var serialized = JsonSerializer.Serialize(events);
        Assert.DoesNotContain("Private question content", serialized);
        Assert.DoesNotContain("Private case title", serialized);
        Assert.DoesNotContain("org-a-document", serialized);
        Assert.Equal(ActorKind.System, (await db.Actors.SingleAsync(x => x.Id == seed.ActorA)).Kind);
        Assert.Empty(await db.CitedSpendEvents.ToListAsync());
    }

    private async Task<Guid> SecurityAttachFixtureHuman(Guid organization)
    {
        var subject = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        await using var db = _database.CreateContext();
        var identity = new EnterpriseIdentity(Guid.NewGuid(), SecurityTenant, subject, now);
        var actor = new Actor(Guid.NewGuid(), organization, ActorKind.Human, "New fixture human", now, $"entra:{SecurityTenant:D}:{subject:D}");
        db.EnterpriseIdentities.Add(identity); db.Actors.Add(actor);
        db.OrganizationMemberships.Add(new OrganizationMembership(Guid.NewGuid(), organization, identity.Id, actor.Id, OrganizationAccessRole.Staff, now));
        await db.SaveChangesAsync();
        return subject;
    }

    [Fact]
    public async Task Phase6_review_changes_record_actor_state_and_request_without_private_values()
    {
        var seed = await SeedGraphAsync();
        var subject = await SecurityAttachFixtureHuman(seed.OrganizationB);
        using var host = SecurityHost();
        using var reviewer = SecurityHttp(host, subject);
        reviewer.DefaultRequestHeaders.Add("X-Request-ID", "phase6-review-flow");
        var started = await reviewer.PostAsync($"/api/documents/{seed.DocumentB}/reviews", null);
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        var workspace = (await started.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("session");
        var reviewId = workspace.GetProperty("id").GetGuid();
        using var action = new HttpRequestMessage(HttpMethod.Post, $"/api/documents/{seed.DocumentB}/reviews/{reviewId}/actions")
        { Content = JsonContent.Create(new { action = "Corrected", machineFieldValueId = seed.FieldB, valueType = "Decimal", value = "123.00", reason = "PRIVATE REVIEW REASON" }) };
        action.Headers.TryAddWithoutValidation("If-Match", started.Headers.ETag!.Tag);
        var corrected = await reviewer.SendAsync(action);
        Assert.Equal(HttpStatusCode.OK, corrected.StatusCode);
        using var completion = new HttpRequestMessage(HttpMethod.Post, $"/api/documents/{seed.DocumentB}/reviews/{reviewId}/complete");
        completion.Headers.TryAddWithoutValidation("If-Match", corrected.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.OK, (await reviewer.SendAsync(completion)).StatusCode);
        await using var db = _database.CreateContext();
        var actor = (await new EnterpriseActorStore(db).ResolveAsync($"entra:{SecurityTenant:D}:{subject:D}", default))!;
        var events = await db.SecurityAuditEvents.ToArrayAsync();
        Assert.Contains(events, e => e.Action == "review.started" && e.TargetId == reviewId);
        Assert.Contains(events, e => e.Action == "review.action-recorded" && e.AfterState == "Corrected");
        Assert.Contains(events, e => e.Action == "review.changed" && e.BeforeState == "InProgress" && e.AfterState == "Completed");
        Assert.All(events, e => { Assert.Equal(actor.ActorId, e.ActorId); Assert.Equal(seed.OrganizationB, e.OrganizationId); Assert.Equal("phase6-review-flow", e.RequestId); });
        Assert.DoesNotContain("PRIVATE REVIEW REASON", JsonSerializer.Serialize(events));
        Assert.DoesNotContain("123.00", JsonSerializer.Serialize(events));
    }

    [Fact]
    public async Task Phase6_upload_and_queue_audit_commit_together_and_failed_audit_compensates_file()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs/continuation.md"))) root = root.Parent;
        Assert.NotNull(root);
        var storage = Path.Combine(root!.FullName, "tmp", "phase6-upload-tests", Guid.NewGuid().ToString("N"));
        var a = await SecurityBootstrap();
        using var host = SecurityHost().WithWebHostBuilder(b => b.UseSetting("Storage:UploadDirectory", storage));
        using var admin = SecurityHttp(host, a.Subject);
        var subject = Guid.NewGuid();
        var member = await SecurityAdd(admin, subject, "Staff");
        using var user = SecurityHttp(host, subject);
        static MultipartFormDataContent Upload()
        {
            var form = new MultipartFormDataContent();
            var content = new StringContent("PRIVATE SOURCE CONTENT");
            content.Headers.ContentType = new("text/plain");
            form.Add(content, "file", "PRIVATE FILENAME.txt");
            return form;
        }
        using var first = Upload();
        Assert.Equal(HttpStatusCode.Accepted, (await user.PostAsync("/api/documents", first)).StatusCode);
        await using (var db = _database.CreateContext())
        {
            var document = await db.Documents.SingleAsync();
            Assert.Equal(member.ActorId, document.CreatedByActorId);
            Assert.Single(await db.ExtractionJobs.ToArrayAsync());
            var events = await db.SecurityAuditEvents.Where(e => e.ActorId == member.ActorId).ToArrayAsync();
            Assert.Contains(events, e => e.Action == "document.uploaded" && e.TargetId == document.Id);
            Assert.Contains(events, e => e.Action == "processing.queued");
            Assert.DoesNotContain("PRIVATE", JsonSerializer.Serialize(events));
        }
        await ExecuteAsync("""
            CREATE FUNCTION phase6_test_reject_upload_audit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                IF NEW.action='document.uploaded' THEN RAISE EXCEPTION 'test-only audit outage'; END IF;
                RETURN NEW; END $$;
            CREATE TRIGGER phase6_test_upload_outage BEFORE INSERT ON security_audit_events
                FOR EACH ROW EXECUTE FUNCTION phase6_test_reject_upload_audit();
            """);
        try
        {
            using var second = Upload();
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await user.PostAsync("/api/documents", second)).StatusCode);
            await using var db = _database.CreateContext();
            Assert.Equal(1, await db.Documents.CountAsync());
            Assert.Equal(1, await db.ExtractionJobs.CountAsync());
            Assert.Single(Directory.GetFiles(storage, "*", SearchOption.AllDirectories));
        }
        finally { await ExecuteAsync("DROP TRIGGER phase6_test_upload_outage ON security_audit_events; DROP FUNCTION phase6_test_reject_upload_audit()"); }
    }

    [Fact]
    public async Task Phase6_runtime_grants_allow_append_and_access_changes_but_deny_audit_rewrite_and_identity_remap()
    {
        var role = "phase6_test_" + Guid.NewGuid().ToString("N");
        await ExecuteAsync($"CREATE ROLE {role} NOLOGIN NOSUPERUSER NOCREATEROLE NOBYPASSRLS");
        try
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "tools/phase6_runtime_security_grants.sql"))) root = root.Parent;
            Assert.NotNull(root);
            var lines = await File.ReadAllLinesAsync(Path.Combine(root!.FullName, "tools/phase6_runtime_security_grants.sql"));
            var sql = string.Join('\n', lines.Where(l => !l.StartsWith('\\'))).Replace(":'runtime_role'", "'" + role + "'", StringComparison.Ordinal);
            await ExecuteAsync(sql);
            await ExecuteAsync(sql); // reapplication retains only intended column privileges
            Assert.True(await ScalarAsync<bool>("SELECT has_table_privilege(@role,'security_audit_events','SELECT,INSERT')", ("role", role)));
            Assert.False(await ScalarAsync<bool>("SELECT has_table_privilege(@role,'security_audit_events','UPDATE,DELETE,TRUNCATE,TRIGGER')", ("role", role)));
            Assert.False(await ScalarAsync<bool>("SELECT has_table_privilege(@role,'enterprise_identities','UPDATE,DELETE,TRUNCATE')", ("role", role)));
            Assert.False(await ScalarAsync<bool>("SELECT has_column_privilege(@role,'organization_memberships','actor_id','UPDATE')", ("role", role)));
            Assert.True(await ScalarAsync<bool>("SELECT has_column_privilege(@role,'organization_memberships','role','UPDATE')", ("role", role)));
            Assert.True(await ScalarAsync<bool>("SELECT has_column_privilege(@role,'document_deletions','completed_at_utc','UPDATE')", ("role", role)));
            Assert.False(await ScalarAsync<bool>("SELECT has_column_privilege(@role,'document_deletions','document_id','UPDATE')", ("role", role)));
            Assert.False(await ScalarAsync<bool>("SELECT has_table_privilege(@role,'document_deletions','DELETE,TRUNCATE,TRIGGER')", ("role", role)));
            await AssertSqlStateAsync("42501", $"SET ROLE {role}; DELETE FROM security_audit_events");
            await AssertSqlStateAsync("42501", $"SET ROLE {role}; UPDATE organization_memberships SET actor_id=gen_random_uuid()");
            var owner = sql.Replace("'" + role + "'", "current_user", StringComparison.Ordinal);
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => ExecuteAsync(owner));
        }
        finally
        {
            // This unique NOLOGIN role was created above, owns no objects, and has grants only in the disposable DB.
            await ExecuteAsync($"DROP OWNED BY {role}; DROP ROLE {role}");
        }
    }

    [Fact]
    public async Task Phase6_private_bootstrap_executable_provisions_atomically_and_refuses_retained_database()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs/continuation.md"))) root = root.Parent;
        Assert.NotNull(root);
        var dll = Path.Combine(AppContext.BaseDirectory, "EnterpriseAdmin.dll");
        if (!File.Exists(dll))
        {
            var configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
            dll = Path.Combine(root!.FullName, "apps/backend/tools/EnterpriseAdmin/bin", configuration, "net8.0/EnterpriseAdmin.dll");
        }
        Assert.True(File.Exists(dll), "Build the referenced EnterpriseAdmin project before running the persistence assembly directly.");
        var input = Path.Combine(root!.FullName, "tmp", "phase6-provision-" + Guid.NewGuid().ToString("N") + ".json");
        var subject = Guid.NewGuid();
        await File.WriteAllTextAsync(input, JsonSerializer.Serialize(new
        { slug = "cli-fictional", name = "CLI fictional organization", tenantId = SecurityTenant, objectId = subject, administratorName = "Private CLI name" }));
        async Task<(int Exit, string Output, string Error)> Run(string connection)
        {
            var start = new System.Diagnostics.ProcessStartInfo("dotnet")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { dll, "bootstrap", input }) start.ArgumentList.Add(arg);
            start.Environment["PHASE6_ADMIN_CONNECTION"] = connection;
            using var process = System.Diagnostics.Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (process.ExitCode, await output, await error);
        }
        try
        {
            var success = await Run(_database.TestConnectionString);
            Assert.Equal(0, success.Exit);
            var result = JsonSerializer.Deserialize<BootstrapOrganization>(success.Output)!;
            await using var db = _database.CreateContext();
            Assert.Equal(result.OrganizationId, (await db.Organizations.SingleAsync()).Id);
            Assert.Equal(2, await db.SecurityAuditEvents.CountAsync());
            Assert.Equal(1, (await Run(_database.TestConnectionString)).Exit);
            var forbidden = new Npgsql.NpgsqlConnectionStringBuilder(_database.TestConnectionString) { Database = "phase5_e2e_20260908", Port = 1 };
            Assert.Equal(1, (await Run(forbidden.ConnectionString)).Exit);
            Assert.DoesNotContain("Private CLI name", success.Output + success.Error);
            Assert.Equal(1, await db.Organizations.CountAsync());
        }
        finally { File.Delete(input); }
    }
}
