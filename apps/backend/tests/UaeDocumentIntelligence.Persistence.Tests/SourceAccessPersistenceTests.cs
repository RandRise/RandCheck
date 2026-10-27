using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UaeDocumentIntelligence.Api;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.Documents.Upload;
using UaeDocumentIntelligence.Infrastructure.Persistence;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Theory]
    [InlineData("http", "Enforced", HttpStatusCode.ServiceUnavailable)]
    [InlineData("https", "Enforced", HttpStatusCode.OK)]
    [InlineData("https", "Locked", HttpStatusCode.ServiceUnavailable)]
    public async Task Phase6_source_production_requires_https_and_enforced_storage(string scheme, string mode, HttpStatusCode expected)
    {
        using var fixture = await SourceFixtureAsync();
        var keys = SourceKeys(fixture.Root);
        using var host = fixture.Host.WithWebHostBuilder(b => WithSourceKeys(b.UseEnvironment("Production").UseSetting("Storage:SecurityMode", mode), keys));
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri($"{scheme}://localhost") });
        client.DefaultRequestHeaders.Authorization = fixture.User.DefaultRequestHeaders.Authorization;
        var response = await client.PostAsync($"/api/documents/{fixture.DocumentId}/source-access", null);
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            var grant = (await response.Content.ReadFromJsonAsync<DocumentSourceEndpoints.AccessGrant>())!;
            using var request = SourceRequest(grant);
            Assert.Equal(SourceFixture.Content, await (await client.SendAsync(request)).Content.ReadAsStringAsync());
        }
        Assert.Equal(expected == HttpStatusCode.OK ? 1 : 0, fixture.Scanner.Calls);
    }

    [Theory]
    [InlineData("Staff")]
    [InlineData("Admin")]
    public async Task Phase6_source_download_has_current_access_attachment_headers_and_safe_audit(string role)
    {
        using var fixture = await SourceFixtureAsync(role);
        var grant = await SourceGrantAsync(fixture);
        Assert.DoesNotContain(grant.AccessToken, grant.Url);
        Assert.Equal(fixture.Clock.UtcNow.AddMinutes(5), grant.ExpiresAtUtc);
        using var request = SourceRequest(grant);
        request.Headers.Range = new(0, 2);
        request.Headers.TryAddWithoutValidation("If-None-Match", "*");
        var response = await fixture.User.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(SourceFixture.Content, await response.Content.ReadAsStringAsync());
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.DoesNotContain("PRIVATE", response.Content.Headers.ContentDisposition.ToString());
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Null(response.Headers.ETag); Assert.Equal(1, fixture.Scanner.Calls);
        await using var db = _database.CreateContext();
        var events = await db.SecurityAuditEvents.Where(e => e.TargetId == fixture.DocumentId).ToArrayAsync();
        Assert.Contains(events, e => e.Action == "document.source-access-issued");
        var download = Assert.Single(events.Where(e => e.Action == "document.source-download-authorized"));
        Assert.Equal(fixture.Member.ActorId, download.ActorId);
        var serialized = JsonSerializer.Serialize(events);
        Assert.DoesNotContain("PRIVATE", serialized); Assert.DoesNotContain(grant.AccessToken, serialized);
        Assert.Equal(0, await db.CitedSpendEvents.CountAsync());
    }

    [Fact]
    public async Task Phase6_source_grants_cannot_be_swapped_between_actors_organizations_or_documents()
    {
        using var first = await SourceFixtureAsync(); using var second = await SourceFixtureAsync();
        var grant = await SourceGrantAsync(first);
        var peerSubject = Guid.NewGuid(); await SecurityAdd(first.Admin, peerSubject);
        using var peer = SecurityHttp(first.Host, peerSubject);
        // Same API/key ring: rejection must come from identity binding, not different protection keys.
        using var otherOrganization = SecurityHttp(first.Host, second.Member.ObjectId);
        using (var request = SourceRequest(grant)) Assert.Equal(HttpStatusCode.NotFound, (await peer.SendAsync(request)).StatusCode);
        using (var request = SourceRequest(grant)) Assert.Equal(HttpStatusCode.NotFound, (await otherOrganization.SendAsync(request)).StatusCode);
        using (var request = SourceRequest(grant with { Url = $"/api/documents/{second.DocumentId}/source" }))
            Assert.Equal(HttpStatusCode.NotFound, (await first.User.SendAsync(request)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await second.User.PostAsync($"/api/documents/{first.DocumentId}/source-access", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await first.User.GetAsync(grant.Url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await first.User.GetAsync(grant.Url + "?accessToken=" + grant.AccessToken)).StatusCode);
        using (var request = SourceRequest(grant))
        {
            request.Headers.TryAddWithoutValidation("X-Document-Access", grant.AccessToken);
            Assert.Equal(HttpStatusCode.NotFound, (await first.User.SendAsync(request)).StatusCode);
        }
        Assert.Equal(0, first.Scanner.Calls); Assert.Equal(0, second.Scanner.Calls);
        // Peer access itself is allowed, but requires a new grant issued to that peer.
        var own = (await (await peer.PostAsync($"/api/documents/{first.DocumentId}/source-access", null)).Content.ReadFromJsonAsync<DocumentSourceEndpoints.AccessGrant>())!;
        using var valid = SourceRequest(own);
        Assert.Equal(HttpStatusCode.OK, (await peer.SendAsync(valid)).StatusCode);
    }

    [Theory]
    [InlineData("expired", HttpStatusCode.NotFound)]
    [InlineData("role", HttpStatusCode.NotFound)]
    [InlineData("revoked", HttpStatusCode.Forbidden)]
    [InlineData("reactivated", HttpStatusCode.NotFound)]
    [InlineData("expired-during-scan", HttpStatusCode.NotFound)]
    [InlineData("revoked-during-scan", HttpStatusCode.NotFound)]
    public async Task Phase6_source_grants_recheck_expiry_and_membership_after_scanning(string change, HttpStatusCode expected)
    {
        using var fixture = await SourceFixtureAsync(); var grant = await SourceGrantAsync(fixture);
        if (change == "expired") fixture.Clock.UtcNow = grant.ExpiresAtUtc;
        // Both roles may read sources, but a role change invalidates old grants
        // through their pinned membership version even while access stays active.
        if (change == "role") Assert.Equal(HttpStatusCode.OK, (await SecurityChange(fixture.Admin, fixture.Member, "Admin", false, true)).StatusCode);
        if (change is "revoked" or "reactivated")
        {
            var changed = await SecurityChange(fixture.Admin, fixture.Member, "Staff", false, false);
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
            if (change == "reactivated")
            {
                var revoked = (await changed.Content.ReadFromJsonAsync<MembershipView>())!;
                Assert.Equal(HttpStatusCode.OK, (await SecurityChange(fixture.Admin, revoked, "Staff", false, true)).StatusCode);
            }
        }
        if (change == "expired-during-scan") fixture.Scanner.AfterScan = () => { fixture.Clock.UtcNow = grant.ExpiresAtUtc; return Task.CompletedTask; };
        if (change == "revoked-during-scan") fixture.Scanner.AfterScan = async () =>
            Assert.Equal(HttpStatusCode.OK, (await SecurityChange(fixture.Admin, fixture.Member, "Staff", false, false)).StatusCode);
        using var request = SourceRequest(grant); var response = await fixture.User.SendAsync(request);
        Assert.Equal(expected, response.StatusCode); Assert.DoesNotContain(SourceFixture.Content, await response.Content.ReadAsStringAsync());
        Assert.Equal(change.EndsWith("during-scan") ? 1 : 0, fixture.Scanner.Calls);
        await using var db = _database.CreateContext();
        Assert.False(await db.SecurityAuditEvents.AnyAsync(e => e.Action == "document.source-download-authorized"));
    }

    [Theory]
    [InlineData("missing", HttpStatusCode.NotFound)]
    [InlineData("tampered", HttpStatusCode.NotFound)]
    [InlineData("size", HttpStatusCode.NotFound)]
    [InlineData("foreign-path", HttpStatusCode.NotFound)]
    [InlineData("infected", HttpStatusCode.UnprocessableEntity)]
    [InlineData("unavailable", HttpStatusCode.ServiceUnavailable)]
    public async Task Phase6_source_rejects_unavailable_changed_and_unsafe_files(string change, HttpStatusCode expected)
    {
        using var fixture = await SourceFixtureAsync(); var grant = await SourceGrantAsync(fixture);
        if (change == "missing") File.Delete(fixture.File);
        if (change == "tampered") await File.WriteAllTextAsync(fixture.File, new string('x', SourceFixture.Content.Length));
        if (change == "size") await File.WriteAllTextAsync(fixture.File, "short");
        if (change == "foreign-path") await ExecuteAsync("UPDATE documents SET storage_key=@key WHERE id=@id", ("key", $"organizations/{Guid.NewGuid():N}/documents/{fixture.DocumentId:N}/source.txt"), ("id", fixture.DocumentId));
        fixture.Scanner.Outcome = change;
        using var request = SourceRequest(grant); var response = await fixture.User.SendAsync(request);
        Assert.Equal(expected, response.StatusCode); Assert.DoesNotContain(SourceFixture.Content, await response.Content.ReadAsStringAsync());
        Assert.Equal(change is "infected" or "unavailable" ? 1 : 0, fixture.Scanner.Calls);
        Assert.Equal(change != "missing", File.Exists(fixture.File));
        await using var db = _database.CreateContext();
        Assert.Contains(await db.SecurityAuditEvents.ToArrayAsync(), e => e.Action == "document.source-access-denied");
        Assert.False(await db.SecurityAuditEvents.AnyAsync(e => e.Action == "document.source-download-authorized"));
    }

    [Theory]
    [InlineData("document.source-access-issued")]
    [InlineData("document.source-download-authorized")]
    public async Task Phase6_source_audit_outage_prevents_credential_or_file_release(string action)
    {
        using var fixture = await SourceFixtureAsync(); var grant = await SourceGrantAsync(fixture);
        await ExecuteAsync($"""
            CREATE FUNCTION phase6_source_audit_outage() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                IF NEW.action='{action}' THEN RAISE EXCEPTION 'test audit unavailable'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER phase6_source_outage BEFORE INSERT ON security_audit_events
                FOR EACH ROW EXECUTE FUNCTION phase6_source_audit_outage();
            """);
        try
        {
            using var request = action.EndsWith("issued") ? new HttpRequestMessage(HttpMethod.Post, $"/api/documents/{fixture.DocumentId}/source-access") : SourceRequest(grant);
            var response = await fixture.User.SendAsync(request);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(SourceFixture.Content, body); Assert.DoesNotContain("accessToken", body);
            Assert.True(File.Exists(fixture.File));
        }
        finally { await ExecuteAsync("DROP TRIGGER phase6_source_outage ON security_audit_events; DROP FUNCTION phase6_source_audit_outage()"); }
    }

    private static HttpRequestMessage SourceRequest(DocumentSourceEndpoints.AccessGrant grant)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, grant.Url);
        request.Headers.Add("X-Document-Access", grant.AccessToken); return request;
    }
    private static async Task<DocumentSourceEndpoints.AccessGrant> SourceGrantAsync(SourceFixture fixture)
    {
        var response = await fixture.User.PostAsync($"/api/documents/{fixture.DocumentId}/source-access", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DocumentSourceEndpoints.AccessGrant>())!;
    }
    private async Task<SourceFixture> SourceFixtureAsync(string role = "Staff",
        WebApplicationFactory<global::Program>? securityFactory = null)
    {
        var fixture = new SourceFixture();
        try
        {
            var bootstrap = await SecurityBootstrap();
            fixture.Host = SecurityHost(factory: securityFactory).WithWebHostBuilder(b => b.UseSetting("Storage:SecurityMode", "Enforced")
                .UseSetting("Storage:UploadDirectory", Path.Combine(fixture.Root, "uploads"))
                .UseSetting("Storage:QuarantineDirectory", Path.Combine(fixture.Root, "quarantine"))
                .ConfigureServices(s => { s.RemoveAll<IMalwareScanner>(); s.AddSingleton<IMalwareScanner>(fixture.Scanner);
                    s.RemoveAll<IClock>(); s.AddSingleton<IClock>(fixture.Clock); }));
            fixture.Admin = SecurityHttp(fixture.Host, bootstrap.Subject);
            var subject = Guid.NewGuid(); fixture.Member = await SecurityAdd(fixture.Admin, subject, "Staff");
            fixture.User = SecurityHttp(fixture.Host, subject);
            using var form = new MultipartFormDataContent(); var content = new StringContent(SourceFixture.Content);
            content.Headers.ContentType = new("text/plain"); form.Add(content, "file", "PRIVATE FILENAME.txt");
            var response = await fixture.User.PostAsync("/api/documents", form); Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            fixture.DocumentId = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
            fixture.File = Directory.GetFiles(Path.Combine(fixture.Root, "uploads"), "*", SearchOption.AllDirectories).Single();
            if (role != "Staff")
            {
                var changed = await SecurityChange(fixture.Admin, fixture.Member, role, false, true);
                Assert.Equal(HttpStatusCode.OK, changed.StatusCode); fixture.Member = (await changed.Content.ReadFromJsonAsync<MembershipView>())!;
            }
            fixture.Scanner.Calls = 0; return fixture;
        }
        catch { fixture.Dispose(); throw; }
    }
    private sealed class SourceFixture : IDisposable
    {
        public const string Content = "PRIVATE ORIGINAL SOURCE";
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "p6-source-" + Guid.NewGuid().ToString("N"));
        public SourceScan Scanner { get; } = new(); public SourceTime Clock { get; } = new();
        public WebApplicationFactory<global::Program> Host { get; set; } = null!;
        public HttpClient Admin { get; set; } = null!; public HttpClient User { get; set; } = null!;
        public MembershipView Member { get; set; } = null!; public Guid DocumentId { get; set; } public string File { get; set; } = "";
        public void Dispose() { User?.Dispose(); Admin?.Dispose(); Host?.Dispose(); if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
    private sealed class SourceTime : IClock { public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow; }
    private sealed class SourceScan : IMalwareScanner
    {
        public int Calls { get; set; } public string Outcome { get; set; } = "clean"; public Func<Task>? AfterScan { get; set; }
        public async Task ScanAsync(Stream content, long length, CancellationToken ct)
        {
            Calls++;
            if (Outcome == "infected") throw new MalwareDetectedException();
            if (Outcome == "unavailable") throw new MalwareScanUnavailableException();
            if (AfterScan is not null) await AfterScan();
        }
    }
}
