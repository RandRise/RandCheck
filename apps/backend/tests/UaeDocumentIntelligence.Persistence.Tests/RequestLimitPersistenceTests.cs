using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    private sealed class RequestLimitClock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public void Advance() => _ticks += TimeSpan.FromMinutes(1).Ticks;
    }

    [Fact]
    public async Task Phase6_request_limits_bind_actual_memberships_and_revocation_precedes_exhausted_budget()
    {
        var a = await SecurityBootstrap(); var b = await SecurityBootstrap();
        var first = await SecurityAttachFixtureHuman(a.Organization.OrganizationId);
        var peer = await SecurityAttachFixtureHuman(a.Organization.OrganizationId);
        var third = await SecurityAttachFixtureHuman(a.Organization.OrganizationId);
        var foreign = await SecurityAttachFixtureHuman(b.Organization.OrganizationId);
        var clock = new RequestLimitClock(); await using var role = await ApiRoleAsync(); using var original = SecurityHost();
        using var restricted = role.Host(original);
        using var host = restricted.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RequestLimits:Enabled", "true").UseSetting("RequestLimits:ActorRequests", "1")
                .UseSetting("RequestLimits:OrganizationRequests", "2");
            builder.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)));
        });
        using var user = SecurityHttp(host, first); using var other = SecurityHttp(host, peer);
        using var another = SecurityHttp(host, third); using var outside = SecurityHttp(host, foreign);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/session")).StatusCode);
        user.DefaultRequestHeaders.Add("X-Organization-ID", b.Organization.OrganizationId.ToString());
        user.DefaultRequestHeaders.Add("X-Actor-ID", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.TooManyRequests, (await user.GetAsync("/api/session")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync("/api/session")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await another.GetAsync("/api/session")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await outside.GetAsync("/api/session")).StatusCode);
        clock.Advance(); Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/session")).StatusCode);
        await using var db = _database.CreateContext();
        var member = await (from m in db.OrganizationMemberships join i in db.EnterpriseIdentities on m.IdentityId equals i.Id
            where i.ObjectId == first select m).SingleAsync();
        member.ChangeAccess(member.Role, false, DateTimeOffset.UtcNow); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/session")).StatusCode);
    }

    [Fact]
    public async Task Phase6_request_limits_prevent_upload_side_effects_and_allow_retry_after_window()
    {
        using var source = await SourceFixtureAsync("Staff"); var clock = new RequestLimitClock();
        await using var role = await ApiRoleAsync(); using var restricted = role.Host(source.Host);
        using var host = restricted.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RequestLimits:Enabled", "true").UseSetting("RequestLimits:ActorWorkRequests", "1");
            builder.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)));
        });
        using var user = WithAuthority(host, source.User);
        async Task<HttpResponseMessage> Upload()
        {
            using var form = new MultipartFormDataContent(); var text = new StringContent("Disposable limited upload");
            text.Headers.ContentType = new("text/plain"); form.Add(text, "file", "limited.txt");
            return await user.PostAsync("/api/documents", form);
        }
        Assert.Equal(HttpStatusCode.Accepted, (await Upload()).StatusCode);
        await using var db = _database.CreateContext();
        var documents = await db.Documents.CountAsync(); var jobs = await db.ExtractionJobs.CountAsync();
        var audit = await db.SecurityAuditEvents.CountAsync(); var scans = source.Scanner.Calls;
        var files = Directory.GetFiles(source.Root, "*", SearchOption.AllDirectories).Order().ToArray();
        var denied = await Upload(); Assert.Equal(HttpStatusCode.TooManyRequests, denied.StatusCode);
        Assert.NotNull(denied.Headers.RetryAfter);
        Assert.Equal(documents, await db.Documents.CountAsync()); Assert.Equal(jobs, await db.ExtractionJobs.CountAsync());
        Assert.Equal(audit, await db.SecurityAuditEvents.CountAsync()); Assert.Equal(scans, source.Scanner.Calls);
        Assert.Equal(files, Directory.GetFiles(source.Root, "*", SearchOption.AllDirectories).Order().ToArray());
        clock.Advance(); Assert.Equal(HttpStatusCode.Accepted, (await Upload()).StatusCode);
        Assert.Equal(documents + 1, await db.Documents.CountAsync()); Assert.Equal(jobs + 1, await db.ExtractionJobs.CountAsync());
    }

    [Fact]
    public async Task Phase6_request_limits_count_source_download_as_work_before_scan_or_release()
    {
        using var source = await SourceFixtureAsync(); var clock = new RequestLimitClock();
        await using var role = await ApiRoleAsync(); using var restricted = role.Host(source.Host);
        using var host = restricted.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RequestLimits:Enabled", "true").UseSetting("RequestLimits:ActorWorkRequests", "1");
            builder.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)));
        });
        using var user = WithAuthority(host, source.User);
        var response = await user.PostAsync($"/api/documents/{source.DocumentId}/source-access", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var grant = (await response.Content.ReadFromJsonAsync<UaeDocumentIntelligence.Api.DocumentSourceEndpoints.AccessGrant>())!;
        var scans = source.Scanner.Calls; await using var db = _database.CreateContext(); var audit = await db.SecurityAuditEvents.CountAsync();
        using var denied = SourceRequest(grant); Assert.Equal(HttpStatusCode.TooManyRequests, (await user.SendAsync(denied)).StatusCode);
        Assert.Equal(scans, source.Scanner.Calls); Assert.Equal(audit, await db.SecurityAuditEvents.CountAsync());
        clock.Advance(); using var retry = SourceRequest(grant);
        var downloaded = await user.SendAsync(retry); Assert.Equal(HttpStatusCode.OK, downloaded.StatusCode);
        Assert.Equal(SourceFixture.Content, await downloaded.Content.ReadAsStringAsync());
    }
}
