using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UaeDocumentIntelligence.Api.Security;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Api.Tests;

public sealed partial class EnterpriseSecurityTests
{
    private sealed class BudgetClock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public void Advance(int seconds) => _ticks += TimeSpan.FromSeconds(seconds).Ticks;
    }

    [Fact]
    public void Budgets_admit_atomically_reset_by_monotonic_time_and_do_not_refund_requests()
    {
        var clock = new BudgetClock(); var budgets = new RequestBudgets(new(), clock);
        RequestBudget[] pair = [new("organization", 2, 10), new("actor", 1, 10)];
        using (var first = budgets.TryAcquire(pair, out _)) Assert.NotNull(first);
        Assert.Null(budgets.TryAcquire(pair, out var retry)); Assert.Equal(60, retry);
        // Rejected actor admission did not consume another organization request.
        using (var other = budgets.TryAcquire([pair[0], new("peer", 1, 10)], out _)) Assert.NotNull(other);
        Assert.Null(budgets.TryAcquire([pair[0]], out _));
        clock.Advance(59); Assert.Null(budgets.TryAcquire(pair, out retry)); Assert.Equal(1, retry);
        clock.Advance(1); using var recovered = budgets.TryAcquire(pair, out _); Assert.NotNull(recovered);
    }

    [Fact]
    public void Partition_capacity_never_evicts_live_or_unexpired_budgets_and_recovers_after_expiry()
    {
        var clock = new BudgetClock(); var budgets = new RequestBudgets(new() { MaxPartitions = 2 }, clock);
        using var active = budgets.TryAcquire([new("active", 10, 2)], out _);
        budgets.TryAcquire([new("used", 1, 1)], out _)!.Dispose();
        Assert.Null(budgets.TryAcquire([new("new", 1, 1)], out _));
        Assert.Null(budgets.TryAcquire([new("used", 1, 1)], out _));
        clock.Advance(60);
        using var next = budgets.TryAcquire([new("new", 1, 1)], out _); Assert.NotNull(next);
        // Both remaining entries are active; capacity cannot be evaded by new keys.
        Assert.Null(budgets.TryAcquire([new("another", 1, 1)], out _));
    }

    [Fact]
    public void Parallel_admission_respects_concurrency_and_disposal_releases_exactly_once()
    {
        var budgets = new RequestBudgets(new(), new BudgetClock());
        var leases = new System.Collections.Concurrent.ConcurrentBag<IDisposable>();
        Parallel.For(0, 100, _ => { var lease = budgets.TryAcquire([new("work", 1000, 3)], out _); if (lease is not null) leases.Add(lease); });
        Assert.Equal(3, leases.Count);
        foreach (var lease in leases) { lease.Dispose(); lease.Dispose(); }
        using var recovered = budgets.TryAcquire([new("work", 1000, 3)], out _); Assert.NotNull(recovered);
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Staging", true)]
    [InlineData("Development", false)]
    [InlineData("Testing", false)]
    public void Request_limits_default_to_enforced_outside_local_profiles(string environment, bool enabled)
    {
        using var host = Host(mode: "Locked", environment: environment);
        Assert.Equal(enabled, host.Services.GetRequiredService<RequestLimitOptions>().Enabled);
    }

    [Theory]
    [InlineData("Enabled", "false", "Production")]
    [InlineData("Enabled", "typo", "Testing")]
    [InlineData("WindowSeconds", "0", "Testing")]
    [InlineData("MaxPartitions", "100000000", "Testing")]
    [InlineData("ActorRequests", "-1", "Testing")]
    [InlineData("GlobalConcurrent", "0", "Testing")]
    public void Unsafe_limit_configuration_refuses_startup(string key, string value, string environment)
    {
        using var host = Host(mode: "Locked", environment: environment).WithWebHostBuilder(b => b.UseSetting("RequestLimits:" + key, value));
        Assert.ThrowsAny<InvalidOperationException>(() => host.CreateClient());
    }

    [Fact]
    public async Task Ingress_throttles_before_database_and_body_and_ignores_spoofed_forwarding_headers()
    {
        using var host = Host().WithWebHostBuilder(b => b.UseSetting("RequestLimits:Enabled", "true")
            .UseSetting("RequestLimits:IpRequests", "1").UseSetting("ApiDatabase:Enabled", "true"));
        using var client = host.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/session")).StatusCode);
        var body = new NeverReadBody();
        var denied = await host.Server.SendAsync(context =>
        {
            context.Request.Method = "POST"; context.Request.Path = "/api/cited-questions"; context.Request.Body = body;
            context.Request.Headers["X-Forwarded-For"] = "192.0.2.99";
            context.Request.Headers["Forwarded"] = "for=198.51.100.7";
            context.Request.Headers["X-Organization-ID"] = Guid.NewGuid().ToString();
        });
        Assert.Equal(429, denied.Response.StatusCode); Assert.Equal(0, body.Reads);
        Assert.Equal("60", denied.Response.Headers.RetryAfter.ToString());
        Assert.Equal("no-store", denied.Response.Headers.CacheControl.ToString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task Separate_transport_peers_have_separate_limits_but_share_process_capacity()
    {
        using var host = Host().WithWebHostBuilder(b => b.UseSetting("RequestLimits:Enabled", "true")
            .UseSetting("RequestLimits:IpRequests", "1").UseSetting("RequestLimits:GlobalRequests", "2"));
        using var scope = host.Services.CreateScope();
        async Task<bool> Try(string address)
        {
            var entered = false; var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            context.Request.Path = "/api/session"; context.Connection.RemoteIpAddress = IPAddress.Parse(address);
            context.Response.Body = new MemoryStream();
            await RequestLimitMiddleware.IngressAsync(context, _ => { entered = true; return Task.CompletedTask; });
            if (!entered) Assert.Equal(429, context.Response.StatusCode);
            return entered;
        }
        Assert.True(await Try("192.0.2.1")); Assert.False(await Try("::ffff:192.0.2.1"));
        Assert.True(await Try("192.0.2.2")); Assert.False(await Try("192.0.2.3"));
    }

    [Fact]
    public async Task Actor_and_organization_budgets_use_server_identity_and_recover_after_window()
    {
        var store = new ActorStore(); var original = store.Actor!; var clock = new BudgetClock();
        using var host = Host(store).WithWebHostBuilder(b =>
        {
            b.UseSetting("RequestLimits:Enabled", "true").UseSetting("RequestLimits:ActorRequests", "1")
                .UseSetting("RequestLimits:OrganizationRequests", "2");
            b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)));
        });
        using var client = host.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", Token());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/documents")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Actor-ID", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/api/documents")).StatusCode);
        store.Actor = original with { ActorId = Guid.NewGuid() };
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/documents")).StatusCode);
        store.Actor = original with { ActorId = Guid.NewGuid() };
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/api/documents")).StatusCode);
        store.Actor = original with { OrganizationId = Guid.NewGuid(), ActorId = Guid.NewGuid() };
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/documents")).StatusCode);
        store.Actor = original; clock.Advance(60);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/documents")).StatusCode);
    }

    [Fact]
    public async Task Work_limit_rejects_before_cited_body_read_while_document_reads_remain_available()
    {
        using var host = Host().WithWebHostBuilder(b => b.UseSetting("RequestLimits:Enabled", "true")
            .UseSetting("RequestLimits:ActorWorkRequests", "1"));
        using var client = host.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", Token());
        using var first = await client.PostAsync("/api/cited-questions/reviewed", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);
        var body = new NeverReadBody();
        var denied = await host.Server.SendAsync(context =>
        {
            context.Request.Method = "POST"; context.Request.Path = "/api/cited-questions/reviewed";
            context.Request.Headers.Authorization = "Bearer " + Token(); context.Request.Body = body;
        });
        Assert.Equal(429, denied.Response.StatusCode); Assert.Equal(0, body.Reads);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/documents")).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Body_limits_reject_oversized_authorized_JSON_with_or_without_content_length(bool unknownLength)
    {
        var store = new ActorStore { Actor = new(Guid.NewGuid(), Guid.NewGuid(), OrganizationAccessRole.Operator) };
        using var host = Host(store).WithWebHostBuilder(b => b.UseSetting("RequestLimits:Enabled", "true"));
        using var client = host.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", Token());
        var bytes = System.Text.Encoding.UTF8.GetBytes("{\"name\":\"" + new string('x', RequestBodyLimit.JsonBytes) + "\"}");
        using HttpContent content = unknownLength ? new UnmeasuredContent(bytes) : new ByteArrayContent(bytes);
        content.Headers.ContentType = new("application/json");
        var response = await client.PostAsync("/api/reconciliation-cases", content);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Admission_leases_release_after_failure_or_cancellation(bool canceled)
    {
        using var host = Host().WithWebHostBuilder(b => b.UseSetting("RequestLimits:Enabled", "true")
            .UseSetting("RequestLimits:GlobalConcurrent", "1").UseSetting("RequestLimits:ActorConcurrent", "1"));
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<RequestIdentity>().Bind(Guid.NewGuid(), Guid.NewGuid());
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider }; context.Request.Path = "/api/documents";
        await Assert.ThrowsAnyAsync<Exception>(() => RequestLimitMiddleware.IngressAsync(context,
            c => RequestLimitMiddleware.AuthorizedAsync(c, _ => canceled ? Task.FromCanceled(new CancellationToken(true)) : Task.FromException(new InvalidOperationException("test")))));
        var entered = false;
        await RequestLimitMiddleware.IngressAsync(context, c => RequestLimitMiddleware.AuthorizedAsync(c, _ => { entered = true; return Task.CompletedTask; }));
        Assert.True(entered);
    }

    private sealed class NeverReadBody : MemoryStream
    {
        public int Reads;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { Reads++; throw new InvalidOperationException("A rejected request body was read."); }
    }

    [Theory]
    [InlineData(false, 0, 200, false)]
    [InlineData(false, 1, 413, false)]
    [InlineData(true, 0, 200, false)]
    [InlineData(true, 1, 413, false)]
    [InlineData(true, 0, 200, true)]
    public async Task Request_body_caps_count_actual_chunked_bytes_on_Kestrel(bool upload, int extra, int expected, bool trailingSlash)
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(new RequestLimitOptions());
        await using var app = builder.Build(); app.Use(RequestBodyLimit.InvokeAsync);
        var path = upload ? "/api/documents" : "/api/reconciliation-cases";
        var chunked = false;
        app.MapPost(path, async (HttpContext context) =>
        {
            chunked = context.Request.Headers.TransferEncoding.ToString() == "chunked";
            await context.Request.Body.CopyToAsync(Stream.Null, context.RequestAborted); return Results.Ok();
        });
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new Uri(address) };
            using var content = new UnmeasuredContent(new byte[(upload ? RequestBodyLimit.UploadBytes : RequestBodyLimit.JsonBytes) + extra]);
            var response = await client.PostAsync(path + (trailingSlash ? "/" : ""), content);
            Assert.Equal(expected, (int)response.StatusCode); Assert.True(chunked);
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task Body_read_deadline_restores_original_stream_and_distinguishes_client_cancellation()
    {
        using var host = Host().WithWebHostBuilder(b => b.UseSetting("RequestLimits:Enabled", "true"));
        using var scope = host.Services.CreateScope(); using var body = new SlowBody();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Path = "/api/documents"; context.Request.Method = "POST";
        context.Request.Body = body; context.Response.Body = new MemoryStream();
        var workflowContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        workflowContext.Request.Path = "/api/reconciliation-cases";
        var workflow = Assert.ThrowsAnyAsync<OperationCanceledException>(() => RequestBodyLimit.InvokeAsync(workflowContext, async _ =>
        {
            await Task.Delay(RequestBodyLimit.ReadTimeout + TimeSpan.FromSeconds(1));
            throw new OperationCanceledException("Workflow deadline, not a body-read timeout.");
        }));
        await RequestBodyLimit.InvokeAsync(context, c => c.Request.Body.CopyToAsync(Stream.Null));
        Assert.Equal(408, context.Response.StatusCode); Assert.Same(body, context.Request.Body);
        await workflow; Assert.NotEqual(408, workflowContext.Response.StatusCode);
        context.Response.StatusCode = 200; context.RequestAborted = new CancellationToken(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RequestBodyLimit.InvokeAsync(context, c => c.Request.Body.CopyToAsync(Stream.Null)));
        Assert.NotEqual(408, context.Response.StatusCode); Assert.Same(body, context.Request.Body);
    }

    private sealed class SlowBody : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { await Task.Delay(Timeout.Infinite, cancellationToken); return 0; }
    }
    private sealed class UnmeasuredContent(byte[] bytes) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
    }
}
