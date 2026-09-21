using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace UaeDocumentIntelligence.Api.Tests;

public sealed class CitedRequestBoundaryTests
{
    private const string Marker = "private-invoice-body-7391";
    private const string InvalidRequest = "{\"question\":\"\",\"requestKey\":\"key\",\"indexIds\":[],\"topK\":5}";

    [Theory]
    [InlineData("/api/cited-indexes", false)]
    [InlineData("/api/cited-retrievals", false)]
    [InlineData("/api/cited-questions", false)]
    [InlineData("/api/cited-sources", false)]
    [InlineData("/api/cited-questions", true)]
    [InlineData("/api/cited-questions/reviewed", false)]
    [InlineData("/api/cited-questions/reviewed", true)]
    [InlineData("/api/cited-questions/semantic", false)]
    [InlineData("/api/cited-questions/semantic", true)]
    [InlineData("/api/cited-cases/00000000-0000-4000-8000-000000000001/sources", false)]
    [InlineData("/api/cited-cases/00000000-0000-4000-8000-000000000001/sources", true)]
    public async Task Oversize_rejections_are_safe_and_correlated(string path, bool streamed)
    {
        var logs = new CapturedLogs();
        using var factory = Factory(logs);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Request-ID", "boundary-test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Marker);
        var bytes = Encoding.UTF8.GetBytes(Marker.PadRight(CitedRequestBoundary.MaxBodyBytes + 1));
        using HttpContent content = streamed ? new StreamedContent(bytes) : new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var response = await client.PostAsync(path, content);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal("boundary-test", response.Headers.GetValues("X-Request-ID").Single());
        Assert.Contains("cited_request_too_large", await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(Marker, await response.Content.ReadAsStringAsync());
        AssertSafeLogs(logs);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Exact_limit_reaches_normal_validation(bool streamed)
    {
        using var factory = Factory(new CapturedLogs());
        using var client = factory.CreateClient();
        var bytes = Encoding.UTF8.GetBytes(InvalidRequest.PadRight(CitedRequestBoundary.MaxBodyBytes));
        using HttpContent content = streamed ? new StreamedContent(bytes) : new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var response = await client.PostAsync("/api/cited-questions", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_cited_request", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("{\"private-invoice-body-7391\":")]
    [InlineData("{\"indexIds\":[\"private-invoice-body-7391\"]}")]
    [InlineData("{\"question\":\"private-invoice-body-7391\",\"requestKey\":\"private-invoice-body-7391\",\"extra\":{\"private-invoice-body-7391\":[}")]
    [InlineData("null")]
    [InlineData("")]
    public async Task Invalid_json_diagnostics_are_not_logged(string body)
    {
        var logs = new CapturedLogs();
        using var factory = Factory(logs);
        using var client = factory.CreateClient();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/cited-questions", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.DoesNotContain(Marker, await response.Content.ReadAsStringAsync());
        AssertSafeLogs(logs);
    }

    [Fact]
    public async Task Unsupported_content_type_is_safe()
    {
        var logs = new CapturedLogs();
        using var factory = Factory(logs);
        using var client = factory.CreateClient();
        using var content = new StringContent(Marker);
        using var response = await client.PostAsync("/api/cited-indexes", content);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.NotEmpty(logs.Entries);
        Assert.DoesNotContain(logs.Entries, entry => entry.Contains(Marker, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Limit_counts_utf8_bytes_not_characters()
    {
        using var factory = Factory(new CapturedLogs());
        using var client = factory.CreateClient();
        var body = "{\"question\":\"" + new string('ع', 33000) + "\"}";
        Assert.True(body.Length < CitedRequestBoundary.MaxBodyBytes);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/cited-questions", content);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Maximum_escaped_question_and_corpus_fit_inside_transport_limit()
    {
        using var factory = Factory(new CapturedLogs());
        using var client = factory.CreateClient();
        var body = System.Text.Json.JsonSerializer.Serialize(new
        {
            question = new string('ع', 4000),
            requestKey = new string('k', 100),
            indexIds = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToArray(),
            topK = 0 // Force normal validation to fail before persistence access.
        });
        Assert.Contains("\\u", body);
        Assert.True(Encoding.UTF8.GetByteCount(body) < CitedRequestBoundary.MaxBodyBytes);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/cited-questions", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_cited_request", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Declared_oversize_is_rejected_without_reading()
    {
        await using var services = Services();
        var context = Context(services, new NeverReadStream());
        context.Request.ContentLength = CitedRequestBoundary.MaxBodyBytes + 1;
        await CitedRequestBoundary.InvokeAsync(context, _ => throw new Exception("Must not invoke next"));
        Assert.Equal(413, context.Response.StatusCode);
    }

    [Fact]
    public async Task Unknown_length_read_stops_at_limit_plus_one()
    {
        await using var services = Services();
        using var body = new MemoryStream(new byte[CitedRequestBoundary.MaxBodyBytes * 3]);
        var context = Context(services, body);
        await CitedRequestBoundary.InvokeAsync(context, _ => throw new Exception("Must not invoke next"));
        Assert.Equal(413, context.Response.StatusCode);
        Assert.Equal(CitedRequestBoundary.MaxBodyBytes + 1, body.Position);
        Assert.Same(body, context.Request.Body);
    }

    [Fact]
    public async Task Original_body_is_restored_even_if_downstream_fails()
    {
        await using var services = Services();
        using var body = new MemoryStream(Encoding.UTF8.GetBytes(InvalidRequest));
        var context = Context(services, body);
        string? received = null;
        var replaced = false;
        await CitedRequestBoundary.InvokeAsync(context, async ctx =>
        {
            replaced = !ReferenceEquals(body, ctx.Request.Body);
            received = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
            throw new InvalidOperationException();
        });
        Assert.True(replaced);
        Assert.Equal(InvalidRequest, received);
        Assert.Equal(500, context.Response.StatusCode);
        Assert.Same(body, context.Request.Body);
        Assert.True(body.CanRead);
    }

    [Fact]
    public async Task Downstream_client_cancellation_is_not_converted_to_500()
    {
        await using var services = Services();
        using var cancellation = new CancellationTokenSource();
        var context = Context(services, Stream.Null);
        context.RequestAborted = cancellation.Token;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CitedRequestBoundary.InvokeAsync(context, _ =>
        {
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        }));
        Assert.NotEqual(500, context.Response.StatusCode);
        Assert.Same(Stream.Null, context.Request.Body);
    }

    [Fact]
    public async Task Started_response_is_aborted_instead_of_appending_error_details()
    {
        await using var services = Services();
        var context = Context(services, Stream.Null);
        var lifetime = new RequestLifetime();
        context.Features.Set<IHttpRequestLifetimeFeature>(lifetime);
        await CitedRequestBoundary.InvokeAsync(context, ctx =>
        {
            ctx.Features.Set<IHttpResponseFeature>(new StartedResponse());
            throw new InvalidOperationException("private-after-start");
        });
        Assert.True(lifetime.Aborted);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Same(Stream.Null, context.Request.Body);
    }

    [Theory]
    [InlineData("/api/documents")]
    [InlineData("/api/cited-questions-other")]
    public async Task Other_routes_are_not_read_or_limited(string path)
    {
        await using var services = Services();
        using var body = new NeverReadStream();
        var context = Context(services, body);
        context.Request.Path = path;
        context.Request.ContentLength = 10 * 1024 * 1024;
        var invoked = false;
        await CitedRequestBoundary.InvokeAsync(context, ctx =>
        {
            invoked = true;
            Assert.Same(body, ctx.Request.Body);
            return Task.CompletedTask;
        });
        Assert.True(invoked);
        Assert.False(context.Response.Headers.ContainsKey("Cache-Control"));
    }

    [Fact]
    public async Task Smaller_host_limit_is_preserved()
    {
        await using var services = Services();
        var context = Context(services, Stream.Null);
        var feature = new BodySizeFeature { MaxRequestBodySize = 1024 };
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(feature);
        await CitedRequestBoundary.InvokeAsync(context, _ => Task.CompletedTask);
        Assert.Equal(1024, feature.MaxRequestBodySize);
    }

    [Fact]
    public async Task Stalled_body_has_a_bounded_read_deadline()
    {
        await using var services = Services();
        var context = Context(services, new StalledStream());
        await CitedRequestBoundary.InvokeAsync(context, _ => throw new Exception("Must not invoke next"));
        Assert.Equal(408, context.Response.StatusCode);
        Assert.True(context.Response.Headers.CacheControl.ToString() == "no-store");
    }

    [Fact]
    public async Task Client_cancellation_is_not_reported_as_a_server_read_timeout()
    {
        await using var services = Services();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = Context(services, new StalledStream());
        context.RequestAborted = cancellation.Token;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CitedRequestBoundary.InvokeAsync(context, _ => throw new Exception("Must not invoke next")));
        Assert.NotEqual(408, context.Response.StatusCode);
    }

    [Theory]
    [InlineData(65536, 200)]
    [InlineData(65537, 413)]
    public async Task Real_kestrel_enforces_chunked_body_boundary(int length, int status)
    {
        var logs = new CapturedLogs();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders().AddConsole().AddFilter<CapturedLogs>((_, _) => true).AddProvider(logs);
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        var receivedChunked = false;
        app.Use(async (context, next) =>
        {
            receivedChunked = context.Request.Headers.TransferEncoding.ToString() == "chunked";
            await next();
        });
        app.UseCitedResponsePolicy();
        app.MapPost("/api/cited-questions", () => Results.Ok());
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new Uri(address) };
            using var content = new StreamedContent(Encoding.UTF8.GetBytes(Marker.PadRight(length)));
            using var response = await client.PostAsync("/api/cited-questions", content);
            Assert.Equal(status, (int)response.StatusCode);
            Assert.True(response.Headers.CacheControl!.NoStore);
            Assert.True(receivedChunked);
            Assert.DoesNotContain(logs.Entries, entry => entry.Contains(Marker, StringComparison.Ordinal));
            if (status == 413) AssertSafeLogs(logs);
        }
        finally { await app.StopAsync(); }
    }

    private static WebApplicationFactory<Program> Factory(CapturedLogs logs) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UsePhase5DevelopmentIdentity().ConfigureLogging(logging =>
                logging.AddFilter<CapturedLogs>((_, _) => true).AddProvider(logs)));

    private static ServiceProvider Services() => new ServiceCollection().AddLogging().AddRouting().BuildServiceProvider();
    private static DefaultHttpContext Context(IServiceProvider services, Stream body)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = "/api/cited-questions";
        context.Request.Body = body;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static void AssertSafeLogs(CapturedLogs logs)
    {
        Assert.Contains(logs.Entries, entry => entry.Contains("Cited request rejected:"));
        Assert.DoesNotContain(logs.Entries, entry => entry.Contains(Marker, StringComparison.Ordinal));
    }

    private sealed class StreamedContent(byte[] bytes) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
    }

    private sealed class NeverReadStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new Exception("Body must not be read");
    }

    private sealed class StalledStream : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }

    private sealed class BodySizeFeature : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly => false;
        public long? MaxRequestBodySize { get; set; }
    }

    private sealed class StartedResponse : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }

    private sealed class RequestLifetime : IHttpRequestLifetimeFeature
    {
        public CancellationToken RequestAborted { get; set; }
        public bool Aborted { get; private set; }
        public void Abort() => Aborted = true;
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        public ConcurrentQueue<string> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Capture(this, categoryName);
        public void Dispose() { }
        private sealed class Capture(CapturedLogs owner, string category) : ILogger
        {
            public bool IsEnabled(LogLevel logLevel) => true;
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            { owner.Entries.Enqueue($"scope: {state}"); return null; }
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                owner.Entries.Enqueue($"{category}: {formatter(state, exception)} {exception}");
                if (state is IEnumerable<KeyValuePair<string, object?>> fields)
                    foreach (var field in fields) owner.Entries.Enqueue($"{field.Key}: {field.Value}");
            }
        }
    }
}
