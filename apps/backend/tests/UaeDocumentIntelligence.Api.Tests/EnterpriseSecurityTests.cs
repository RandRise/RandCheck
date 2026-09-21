using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using UaeDocumentIntelligence.Api.Security;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Persistence;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Api.Tests;

public sealed partial class EnterpriseSecurityTests
{
    [Theory]
    [InlineData("Production", "", "Locked")]
    [InlineData("Production", "Development", "Locked")]
    [InlineData("Production", "Enforced", "Enforced")]
    [InlineData("Testing", "", "Development")]
    [InlineData("Staging", "typo", "Locked")]
    public void Api_host_enforces_storage_security_mode(string environment, string configured, string expected)
    {
        using var factory = Host(mode: "Locked", environment: environment).WithWebHostBuilder(builder =>
            builder.UseSetting("Storage:SecurityMode", configured));
        Assert.Equal(expected, factory.Services.GetRequiredService<IOptions<StorageOptions>>().Value.SecurityMode);
    }

    [Theory]
    [InlineData("Production", "", "Locked")]
    [InlineData("Production", "Development", "Locked")]
    [InlineData("Production", "Enforced", "Enforced")]
    [InlineData("Testing", "", "Development")]
    [InlineData("Staging", "typo", "Locked")]
    public void Api_host_enforces_worker_security_mode(string environment, string configured, string expected)
    {
        using var factory = Host(mode: "Locked", environment: environment).WithWebHostBuilder(builder =>
            builder.UseSetting("ExtractionWorker:SecurityMode", configured));
        Assert.Equal(expected, factory.Services.GetRequiredService<IOptions<ExtractionWorkerOptions>>().Value.SecurityMode);
    }

    [Theory]
    [InlineData("Production", "", "Locked")]
    [InlineData("Production", "Development", "Locked")]
    [InlineData("Production", "ServiceKeys", "ServiceKeys")]
    [InlineData("Testing", "", "Development")]
    public void Api_host_enforces_private_service_environment_mode(string environment, string configured, string expected)
    {
        using var factory = Host(mode: "Locked", environment: environment).WithWebHostBuilder(builder =>
            builder.UseSetting("AiService:AuthenticationMode", configured));
        Assert.Equal(expected, factory.Services.GetRequiredService<IOptions<AiServiceOptions>>().Value.AuthenticationMode);
    }

    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Audience = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Client = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid Subject = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly RSA SigningRsa = RSA.Create(2048);
    private static readonly RsaSecurityKey SigningKey = new(SigningRsa) { KeyId = "phase6-test-only" };
    private static string Issuer => $"https://login.microsoftonline.com/{Tenant:D}/v2.0";

    private static string Token(string scenario = "valid", string role = "DocumentIntelligence.Viewer")
    {
        var claims = new List<Claim>
        {
            new("ver", "2.0"), new("tid", Tenant.ToString()), new("oid", Subject.ToString()),
            new("azp", Client.ToString()), new("scp", "DocumentIntelligence.Access"), new("roles", role)
        };
        var replacements = new Dictionary<string, (string Type, string? Value)>
        {
            ["tenant"] = ("tid", Guid.NewGuid().ToString()), ["subject"] = ("oid", null),
            ["client"] = ("azp", Guid.NewGuid().ToString()), ["scope"] = ("scp", "other"),
            ["app-only"] = ("scp", null), ["version"] = ("ver", "1.0"),
            ["missing-role"] = ("roles", null), ["fake-role"] = ("roles", "Owner")
        };
        if (replacements.TryGetValue(scenario, out var replacement))
        {
            claims.RemoveAll(x => x.Type == replacement.Type);
            if (replacement.Value is not null) claims.Add(new Claim(replacement.Type, replacement.Value));
        }
        if (scenario == "duplicate-subject") claims.Add(new Claim("oid", Guid.NewGuid().ToString()));
        if (scenario == "forged-context")
        {
            claims.Add(new Claim("organization_id", Guid.NewGuid().ToString()));
            claims.Add(new Claim("actor_id", Guid.NewGuid().ToString()));
        }
        var now = DateTime.UtcNow;
        using var otherRsa = RSA.Create(2048);
        var key = scenario == "signature" ? new RsaSecurityKey(otherRsa) { KeyId = SigningKey.KeyId } : SigningKey;
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            scenario == "issuer" ? "https://untrusted.invalid/v2.0" : Issuer,
            scenario == "audience" ? Guid.NewGuid().ToString() : Audience.ToString(), claims,
            scenario == "future" ? now.AddMinutes(5) : now.AddMinutes(-10),
            scenario == "expired" ? now.AddMinutes(-5) : now.AddMinutes(10),
            scenario == "unsigned" ? null : new SigningCredentials(key, SecurityAlgorithms.RsaSha256)));
    }

    private static WebApplicationFactory<Program> Host(ActorStore? store = null, string mode = "Entra", string environment = "Testing") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment).UseSetting("Security:Mode", mode)
                .UseSetting("Security:TenantId", Tenant.ToString()).UseSetting("Security:Audience", Audience.ToString())
                .UseSetting("Security:AllowedClientIds:0", Client.ToString()).UseSetting("DevelopmentData:Seed", "false")
                .UseSetting("ExtractionWorker:Enabled", "false").UseSetting("StorageMaintenance:Enabled", "false");
            builder.ConfigureServices(services =>
            {
                services.Replace(ServiceDescriptor.Singleton<IEnterpriseActorStore>(store ?? new ActorStore()));
                services.Replace(ServiceDescriptor.Singleton<IDocumentRepository, ScopedDocuments>());
                services.Replace(ServiceDescriptor.Singleton<ISecurityAuditSink, CapturedAudit>());
                // Offline metadata still exercises the production JWT signature/issuer/audience/lifetime validators.
                services.PostConfigure<JwtBearerOptions>("Enterprise", options =>
                {
                    var metadata = new OpenIdConnectConfiguration { Issuer = Issuer };
                    metadata.SigningKeys.Add(SigningKey);
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
                });
            });
        });

    [Theory]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("unsigned")]
    public async Task Invalid_bearer_is_401_before_membership_or_document_access(string scenario)
    {
        var store = new ActorStore();
        using var factory = Host(store);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(scenario));
        using var response = await client.GetAsync("/api/documents");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, store.Calls);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.DoesNotContain("IDX", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("subject")]
    [InlineData("duplicate-subject")]
    [InlineData("client")]
    [InlineData("scope")]
    [InlineData("app-only")]
    [InlineData("version")]
    public async Task Invalid_access_claims_are_403_before_membership_lookup(string scenario)
    {
        var store = new ActorStore();
        using var factory = Host(store);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(scenario));
        using var response = await client.GetAsync("/api/documents");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task Every_business_route_rejects_anonymous_and_query_tokens_and_is_classified()
    {
        using var factory = Host();
        using var client = factory.CreateClient();
        var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(x => x.RoutePattern.RawText!.StartsWith("/api/", StringComparison.Ordinal)).ToArray();
        var classified = new HashSet<string>();
        foreach (var route in routes)
        foreach (var method in route.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods)
        {
            var template = route.RoutePattern.RawText!;
            Assert.Null(route.Metadata.GetMetadata<IAllowAnonymous>());
            Assert.True(EnterprisePermissions.Routes.ContainsKey($"{method} {template}"), template);
            classified.Add($"{method} {template}");
            var path = System.Text.RegularExpressions.Regex.Replace(template, "\\{[^}]+\\}", Guid.NewGuid().ToString());
            using var request = new HttpRequestMessage(new HttpMethod(method), path + "?access_token=" + Token());
            request.Headers.Add("X-Organization-ID", Guid.NewGuid().ToString());
            request.Headers.Add("X-Actor-ID", Guid.NewGuid().ToString());
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        }
        Assert.Equal(EnterprisePermissions.Routes.Keys.Order(), classified.Order());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }

    [Theory]
    [InlineData("Locked")]
    [InlineData("Entra")]
    public async Task No_token_denies_documents_in_production(string mode)
    {
        using var factory = Host(mode: mode, environment: "Production");
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Development_identity_cannot_start_outside_local_environments(string environment)
    {
        using var factory = Host(mode: "Development", environment: environment);
        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }

    [Fact]
    public async Task Server_membership_overrides_forged_claims_headers_and_query_and_is_rechecked()
    {
        var store = new ActorStore();
        using var factory = Host(store);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token("forged-context"));
        client.DefaultRequestHeaders.Add("X-Organization-ID", Guid.NewGuid().ToString());
        using var response = await client.GetAsync("/api/documents?organizationId=" + Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"entra:{Tenant:D}:{Subject:D}", store.LastSubject);
        Assert.Equal(store.Actor!.OrganizationId, ((ScopedDocuments)factory.Services.GetRequiredService<IDocumentRepository>()).LastOrganization);
        store.Actor = null; // Simulates membership revocation while the signed token is still valid.
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/documents")).StatusCode);
        Assert.Equal(2, store.Calls);
    }

    [Theory]
    [InlineData("DocumentIntelligence.Viewer", "/api/documents/11111111-1111-1111-1111-111111111111/reviews")]
    [InlineData("DocumentIntelligence.Viewer", "/api/cited-indexes")]
    [InlineData("DocumentIntelligence.Reviewer", "/api/documents")]
    public async Task Insufficient_role_is_denied_before_body_binding(string role, string route)
    {
        var store = new ActorStore();
        store.Actor = store.Actor! with { Role = Enum.Parse<OrganizationAccessRole>(role.Split('.').Last()) };
        using var factory = Host(store);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(role: "DocumentIntelligence.Operator"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(route, null)).StatusCode);
    }

    [Theory]
    [InlineData("DocumentIntelligence.Viewer", "Read", true)]
    [InlineData("DocumentIntelligence.Viewer", "Ask", true)]
    [InlineData("DocumentIntelligence.Viewer", "Review", false)]
    [InlineData("DocumentIntelligence.Viewer", "Operate", false)]
    [InlineData("DocumentIntelligence.Reviewer", "Read", true)]
    [InlineData("DocumentIntelligence.Reviewer", "Ask", true)]
    [InlineData("DocumentIntelligence.Reviewer", "Review", true)]
    [InlineData("DocumentIntelligence.Reviewer", "Operate", false)]
    [InlineData("DocumentIntelligence.Operator", "Read", true)]
    [InlineData("DocumentIntelligence.Operator", "Ask", true)]
    [InlineData("DocumentIntelligence.Operator", "Review", true)]
    [InlineData("DocumentIntelligence.Operator", "Operate", true)]
    [InlineData("DocumentIntelligence.Viewer", "SourceRead", true)]
    [InlineData("DocumentIntelligence.Reviewer", "SourceRead", true)]
    [InlineData("DocumentIntelligence.Operator", "SourceRead", true)]
    [InlineData("DocumentIntelligence.None", "SourceRead", false)]
    [InlineData("DocumentIntelligence.None", "Retention", false)]
    [InlineData("DocumentIntelligence.Viewer", "Retention", false)]
    [InlineData("DocumentIntelligence.Reviewer", "Retention", false)]
    [InlineData("DocumentIntelligence.Operator", "Retention", false)]
    public void Role_matrix_is_enforced_for_every_route(string role, string permission, bool allowed)
    {
        foreach (var route in EnterprisePermissions.Routes.Where(x => x.Value == permission))
        {
            var parts = route.Key.Split(' ', 2);
            var http = new Microsoft.AspNetCore.Http.DefaultHttpContext();
            http.Request.Method = parts[0];
            http.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask,
                Microsoft.AspNetCore.Routing.Patterns.RoutePatternFactory.Parse(parts[1]), 0, new([]), "test"));
            Assert.Equal(allowed, EnterprisePermissions.Allows(http, Enum.Parse<OrganizationAccessRole>(role.Split('.').Last()), false));
        }
    }

    [Fact]
    public void Identity_has_no_fallback_and_cannot_be_rebound()
    {
        var identity = new RequestIdentity();
        Assert.Throws<InvalidOperationException>(() => identity.OrganizationId);
        Assert.Throws<InvalidOperationException>(() => identity.ActorId);
        identity.Bind(Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => identity.Bind(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task Authenticated_operator_cannot_access_an_unclassified_route()
    {
        using var factory = Host();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(role: "DocumentIntelligence.Operator"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/future-admin-route")).StatusCode);
    }

    [Theory]
    [InlineData("TenantId")]
    [InlineData("Audience")]
    [InlineData("AllowedClientIds")]
    [InlineData("RequiredScope")]
    [InlineData("Mode")]
    public void Invalid_security_configuration_fails_startup(string setting)
    {
        using var factory = Host().WithWebHostBuilder(builder =>
        {
            if (setting == "AllowedClientIds") builder.UseSetting("Security:AllowedClientIds:0", Guid.Empty.ToString());
            else builder.UseSetting("Security:" + setting, setting is "TenantId" or "Audience" ? Guid.Empty.ToString() : "invalid value");
        });
        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }

    [Theory]
    [InlineData("active", true)]
    [InlineData("inactive", false)]
    [InlineData("system", false)]
    [InlineData("missing", false)]
    [InlineData("ambiguous", false)]
    [InlineData("revoked", false)]
    [InlineData("legacy-only", false)]
    public async Task Real_actor_store_denies_inactive_system_missing_and_ambiguous_memberships(string scenario, bool allowed)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        // Minimal relational actor table; the existing PostgreSQL suite owns full schema validation.
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE actors (id TEXT PRIMARY KEY, organization_id TEXT NOT NULL, kind TEXT NOT NULL, external_subject TEXT, display_name TEXT, is_active INTEGER NOT NULL DEFAULT 1, created_at_utc TEXT NOT NULL)");
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE enterprise_identities (id TEXT PRIMARY KEY, tenant_id TEXT, object_id TEXT, created_at_utc TEXT)");
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE organization_memberships (id TEXT PRIMARY KEY, organization_id TEXT, identity_id TEXT, actor_id TEXT, role TEXT, can_manage_members INTEGER, is_active INTEGER, version TEXT, created_at_utc TEXT, updated_at_utc TEXT)");
        var subject = $"entra:{Tenant:D}:{Subject:D}";
        var organization = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        db.Actors.Add(new Actor(actorId, organization, scenario == "system" ? ActorKind.System : ActorKind.Human,
            "Test", DateTimeOffset.UtcNow, scenario == "missing" ? "unrelated" : subject));
        if (scenario != "legacy-only")
        {
            var identity = new EnterpriseIdentity(Guid.NewGuid(), Tenant, scenario == "missing" ? Guid.NewGuid() : Subject, DateTimeOffset.UtcNow);
            db.EnterpriseIdentities.Add(identity);
            var membership = new OrganizationMembership(Guid.NewGuid(), organization, identity.Id, actorId, OrganizationAccessRole.Viewer, false, DateTimeOffset.UtcNow);
            if (scenario == "revoked") membership.ChangeAccess(OrganizationAccessRole.Viewer, false, false, DateTimeOffset.UtcNow);
            db.OrganizationMemberships.Add(membership);
            if (scenario == "ambiguous")
            {
                var other = new Actor(Guid.NewGuid(), Guid.NewGuid(), ActorKind.Human, "Other", DateTimeOffset.UtcNow, subject);
                db.Actors.Add(other);
                db.OrganizationMemberships.Add(new OrganizationMembership(Guid.NewGuid(), other.OrganizationId, identity.Id, other.Id, OrganizationAccessRole.Viewer, false, DateTimeOffset.UtcNow));
            }
        }
        await db.SaveChangesAsync();
        if (scenario == "inactive") await db.Database.ExecuteSqlRawAsync("UPDATE actors SET is_active = 0");
        var result = await new EnterpriseActorStore(db).ResolveAsync(subject, CancellationToken.None);
        Assert.Equal(allowed, result is not null);
        if (allowed) Assert.Equal(new EnterpriseActor(organization, actorId, OrganizationAccessRole.Viewer), result);
    }

    private sealed class ActorStore : IEnterpriseActorStore
    {
        public EnterpriseActor? Actor { get; set; } = new(Guid.NewGuid(), Guid.NewGuid(), OrganizationAccessRole.Viewer);
        public int Calls { get; private set; }
        public string? LastSubject { get; private set; }
        public Task<EnterpriseActor?> ResolveAsync(string externalSubject, CancellationToken cancellationToken)
        {
            Calls++;
            LastSubject = externalSubject;
            return Task.FromResult(Actor);
        }
    }

    [Theory]
    [InlineData("missing-role")]
    [InlineData("fake-role")]
    public async Task Token_roles_do_not_override_database_permissions(string scenario)
    {
        var store = new ActorStore();
        using var factory = Host(store);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(scenario));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/documents")).StatusCode);
        store.Actor = store.Actor! with { Role = OrganizationAccessRole.None };
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(role: "DocumentIntelligence.Operator"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/documents")).StatusCode);
    }

    [Fact]
    public async Task Denial_audit_failure_returns_503_without_admitting_access()
    {
        using var factory = Host().WithWebHostBuilder(b => b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<ISecurityAuditSink, FailingAudit>())));
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/documents")).StatusCode);
    }

    [Fact]
    public async Task Development_identity_cannot_administer_enterprise_memberships_or_read_audit()
    {
        using var factory = Host(mode: "Development");
        using var client = factory.CreateClient();
        foreach (var route in EnterprisePermissions.Routes.Where(r => r.Value is "ManageMembers" or "Audit" or "SourceRead" or "Retention"))
        {
            var parts = route.Key.Split(' ', 2);
            var path = System.Text.RegularExpressions.Regex.Replace(parts[1], "\\{[^}]+\\}", Guid.NewGuid().ToString());
            using var request = new HttpRequestMessage(new HttpMethod(parts[0]), path);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
        }
    }

    private sealed class CapturedAudit : ISecurityAuditSink
    {
        public Task WriteAsync(SecurityAuditEvent item, CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class FailingAudit : ISecurityAuditSink
    {
        public Task WriteAsync(SecurityAuditEvent item, CancellationToken ct) => throw new InvalidOperationException("test-only audit outage");
    }

    private sealed class ScopedDocuments : IDocumentRepository
    {
        public Guid LastOrganization { get; private set; }
        public Task<IReadOnlyList<Document>> ListAsync(Guid organizationId, CancellationToken ct)
        { LastOrganization = organizationId; return Task.FromResult<IReadOnlyList<Document>>([]); }
        public Task AddAsync(Document document, CancellationToken ct) => throw new InvalidOperationException("Unexpected write");
        public Task<Document?> GetAsync(Guid organizationId, Guid id, CancellationToken ct) => Task.FromResult<Document?>(null);
        public Task<Document?> GetForReviewAsync(Guid organizationId, Guid id, CancellationToken ct) => Task.FromResult<Document?>(null);
        public Task<int> NextRunNumberAsync(Guid organizationId, Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken ct) => throw new InvalidOperationException("Unexpected write");
        public Task<ReviewSaveOutcome> SaveReviewChangesAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<DocumentMetrics> GetMetricsAsync(Guid organizationId, CancellationToken ct) => throw new NotSupportedException();
    }
}
