using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Infrastructure.Tests;

public sealed class PythonExtractionClientTests
{
    [Theory]
    [InlineData("ServiceKeys", true)]
    [InlineData("Locked", false)]
    public async Task Worker_uses_named_credential_or_rejects_before_sending(string mode, bool allowed)
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, ReadyBody);
        using var http = new HttpClient(handler) { BaseAddress = new("https://private.test") };
        var options = CreateOptions();
        options.BaseUrl = "https://private.test";
        options.AuthenticationMode = mode;
        options.ExtractionCredential = new() { KeyId = "worker-rotation-1", Token = new('w', 43) };
        var client = new PythonExtractionClient(http, Options.Create(options));
        if (allowed)
        {
            var result = await client.ExtractAsync(CreateRequest(), default);
            Assert.Equal(ExtractionOutcome.Ready, result.Outcome);
            Assert.Equal("worker-rotation-1", Assert.Single(handler.Request!.Headers.GetValues("X-Service-Key-Id")));
            Assert.Equal(new string('w', 43), Assert.Single(handler.Request.Headers.GetValues("X-Worker-Token")));
        }
        else
        {
            var error = await Assert.ThrowsAsync<ExtractionProviderException>(() => client.ExtractAsync(CreateRequest(), default));
            Assert.Equal("provider_configuration_error", error.Code);
            Assert.Null(handler.Request);
        }
    }

    private const string ReadyBody = """
        {
          "schema_version":"2.0","outcome":"Ready","document_type":"invoice","language":"en",
          "provider":{"alias":"local","adapter_version":"local-1.0.0","model":"tesseract-eng","model_version":"5.3.0"},
          "metrics":{"provider_duration_ms":12,"page_count":1,"estimated_cost":null,"cost_currency":null,"price_basis":null},
          "pages":[{"page_number":1,"canonical_text":"105.00","rotation_degrees":0}],
          "fields":[{"key":"total","occurrence":0,"value_type":"Decimal","value":"105.00","normalized_value":"105.00","confidence":null,
            "evidence":[{"page_number":1,"quote":"105.00","start_utf8":0,"end_utf8":6,"polygon":null,"coordinate_unit":null}]}],
          "warnings":[]
        }
        """;

    [Fact]
    public async Task Trusted_worker_request_is_serialized_exactly_without_an_absolute_path()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, ReadyBody);
        var client = CreateClient(handler);
        var request = CreateRequest();

        await client.ExtractAsync(request, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("/internal/extractions", handler.Request.RequestUri!.AbsolutePath);
        Assert.Equal(request.CorrelationId,
            handler.Request.Headers.GetValues("X-Request-ID").Single());
        Assert.Equal("test-worker-secret",
            handler.Request.Headers.GetValues("X-Worker-Token").Single());
        using var json = JsonDocument.Parse(handler.RequestBody!);
        var root = json.RootElement;
        Assert.Equal("2.0", root.GetProperty("schema_version").GetString());
        Assert.Equal(request.JobId, root.GetProperty("job_id").GetGuid());
        Assert.Equal(request.DocumentId, root.GetProperty("document_id").GetGuid());
        Assert.Equal(request.ExtractionRunId, root.GetProperty("extraction_run_id").GetGuid());
        Assert.Equal(request.StorageKey, root.GetProperty("storage_key").GetString());
        Assert.Equal(request.Sha256, root.GetProperty("sha256").GetString());
        Assert.Equal(request.Bytes, root.GetProperty("bytes").GetInt64());
        Assert.Equal(request.DeclaredContentType,
            root.GetProperty("declared_content_type").GetString());
        Assert.Equal(request.ProviderAlias, root.GetProperty("provider_alias").GetString());
        Assert.Equal(request.Attempt, root.GetProperty("attempt").GetInt32());
        Assert.Equal(request.DeadlineUtc, root.GetProperty("deadline_utc").GetDateTimeOffset());
        Assert.Equal(request.CorrelationId, root.GetProperty("correlation_id").GetString());
        Assert.False(root.TryGetProperty("filePath", out _));
        Assert.False(root.TryGetProperty("file_path", out _));
    }

    [Fact]
    public async Task Schema_2_response_round_trips_provider_pages_fields_evidence_and_metrics()
    {
        var client = CreateClient(new CapturingHandler(HttpStatusCode.OK, ReadyBody));

        var result = await client.ExtractAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal("2.0", result.SchemaVersion);
        Assert.Equal(ExtractionOutcome.Ready, result.Outcome);
        Assert.Equal("local", result.Provider.Alias);
        Assert.Equal("tesseract-eng", result.Provider.Model);
        Assert.Equal(12, result.Metrics.ProviderDurationMs);
        Assert.Null(Assert.Single(result.Fields).Confidence);
        Assert.Equal(6, Assert.Single(Assert.Single(result.Fields).Evidence).ByteEnd);
    }

    [Theory]
    [InlineData("mock", "local-1.0.0", "tesseract-eng", "5.3.0")]
    [InlineData("local", "wrong-adapter", "tesseract-eng", "5.3.0")]
    [InlineData("local", "local-1.0.0", "", "5.3.0")]
    [InlineData("local", "local-1.0.0", "tesseract-eng", "")]
    public async Task Provider_identity_mismatch_or_missing_model_is_rejected_safely(
        string alias, string adapterVersion, string model, string modelVersion)
    {
        var body = ReadyBody
            .Replace("\"alias\":\"local\"", $"\"alias\":\"{alias}\"")
            .Replace("\"adapter_version\":\"local-1.0.0\"", $"\"adapter_version\":\"{adapterVersion}\"")
            .Replace("\"model\":\"tesseract-eng\"", $"\"model\":\"{model}\"")
            .Replace("\"model_version\":\"5.3.0\"", $"\"model_version\":\"{modelVersion}\"");
        var client = CreateClient(new CapturingHandler(HttpStatusCode.OK, body));

        var exception = await Assert.ThrowsAsync<ExtractionProviderException>(() =>
            client.ExtractAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("malformed_provider_result", exception.Code);
        Assert.False(exception.Retryable);
        Assert.DoesNotContain(alias, exception.SafeSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Valid_transient_error_envelope_maps_to_retryable_provider_exception()
    {
        const string body = """
            {"schema_version":"2.0","error":{"code":"provider_unavailable","retryable":true,
            "summary":"The configured provider is temporarily unavailable.","correlation_id":"request-123"}}
            """;
        var client = CreateClient(new CapturingHandler(HttpStatusCode.ServiceUnavailable, body));

        var exception = await Assert.ThrowsAsync<ExtractionProviderException>(() =>
            client.ExtractAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("provider_unavailable", exception.Code);
        Assert.True(exception.Retryable);
        Assert.Equal("The configured provider is temporarily unavailable.", exception.SafeSummary);
    }

    [Fact]
    public async Task Permanent_code_marked_retryable_is_rejected_as_malformed()
    {
        const string body = """
            {"schema_version":"2.0","error":{"code":"unsupported_document","retryable":true,
            "summary":"Unsupported.","correlation_id":"request-123"}}
            """;
        var client = CreateClient(new CapturingHandler(HttpStatusCode.UnprocessableEntity, body));

        var exception = await Assert.ThrowsAsync<ExtractionProviderException>(() =>
            client.ExtractAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("malformed_provider_result", exception.Code);
        Assert.False(exception.Retryable);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "provider_throttled", true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "unsupported_document", false)]
    [InlineData(HttpStatusCode.TooManyRequests, "provider_timeout", true)]
    public async Task Error_code_and_http_status_must_match(HttpStatusCode statusCode,
        string code, bool retryable)
    {
        var body = JsonSerializer.Serialize(new
        {
            schema_version = "2.0",
            error = new
            {
                code,
                retryable,
                summary = "Safe provider summary.",
                correlation_id = "request-123"
            }
        });
        var client = CreateClient(new CapturingHandler(statusCode, body));

        var exception = await Assert.ThrowsAsync<ExtractionProviderException>(() =>
            client.ExtractAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("malformed_provider_result", exception.Code);
        Assert.False(exception.Retryable);
    }

    [Fact]
    public async Task Empty_503_proxy_response_maps_to_safe_retryable_unavailable_error()
    {
        var client = CreateClient(new CapturingHandler(HttpStatusCode.ServiceUnavailable, string.Empty));

        var exception = await Assert.ThrowsAsync<ExtractionProviderException>(() =>
            client.ExtractAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("provider_unavailable", exception.Code);
        Assert.True(exception.Retryable);
        Assert.Equal("The extraction provider is temporarily unavailable.", exception.SafeSummary);
    }

    [Fact]
    public async Task Correlation_mismatch_is_rejected_without_exposing_response_content()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, ReadyBody)
        {
            ResponseCorrelationId = "another-request"
        };
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<ExtractionProviderException>(() =>
            client.ExtractAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("malformed_provider_result", exception.Code);
        Assert.DoesNotContain("another-request", exception.SafeSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Http_request_failure_maps_to_safe_retryable_network_error()
    {
        const string sensitive = @"C:\private\invoice.pdf token-secret";
        var http = new HttpClient(new ThrowingHandler(new HttpRequestException(sensitive)))
        {
            BaseAddress = new Uri("http://localhost")
        };
        var client = new PythonExtractionClient(http, Options.Create(CreateOptions()));

        var exception = await Assert.ThrowsAsync<ExtractionProviderException>(() =>
            client.ExtractAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("provider_network_error", exception.Code);
        Assert.True(exception.Retryable);
        Assert.DoesNotContain("C:\\private", exception.SafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token-secret", exception.SafeSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Response_larger_than_five_mib_is_rejected_before_deserialization()
    {
        var content = new ProbeContent(5 * 1024 * 1024 + 1);
        var client = CreateClient(new ContentHandler(content));

        var exception = await Assert.ThrowsAsync<ExtractionProviderException>(() =>
            client.ExtractAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("malformed_provider_result", exception.Code);
        Assert.False(content.ReadAttempted);
    }

    [Fact]
    public async Task Unknown_length_response_is_stopped_by_bounded_stream_after_five_mib()
    {
        var content = new UnknownLengthContent(5 * 1024 * 1024 + 1);
        var client = CreateClient(new ContentHandler(content));

        var exception = await Assert.ThrowsAsync<ExtractionProviderException>(() =>
            client.ExtractAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("malformed_provider_result", exception.Code);
        Assert.True(content.ReadAttempted);
    }

    private static PythonExtractionClient CreateClient(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return new PythonExtractionClient(http, Options.Create(CreateOptions()));
    }

    private static AiServiceOptions CreateOptions() => new()
    {
        ProviderAlias = "local",
        AdapterVersion = "local-1.0.0",
        SchemaVersion = "2.0",
        WorkerToken = "test-worker-secret"
    };

    private static WorkerExtractionRequest CreateRequest() => new(
        "2.0",
        Guid.Parse("10000000-0000-0000-0000-000000000001"),
        Guid.Parse("20000000-0000-0000-0000-000000000002"),
        Guid.Parse("30000000-0000-0000-0000-000000000003"),
        "organizations/tenant/documents/document/source.pdf",
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
        12345,
        "application/pdf",
        "local",
        2,
        new DateTimeOffset(2026, 8, 11, 12, 2, 0, TimeSpan.Zero),
        "request-123");

    private sealed class CapturingHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }
        public string ResponseCorrelationId { get; init; } = "request-123";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            response.Headers.TryAddWithoutValidation("X-Request-ID", ResponseCorrelationId);
            return response;
        }
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromException<HttpResponseMessage>(exception);
    }

    private sealed class ContentHandler(HttpContent content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            response.Headers.TryAddWithoutValidation("X-Request-ID", "request-123");
            return Task.FromResult(response);
        }
    }

    private sealed class ProbeContent : HttpContent
    {
        private readonly long _length;
        public ProbeContent(long length)
        {
            _length = length;
            Headers.ContentType = new MediaTypeHeaderValue("application/json");
            Headers.ContentLength = length;
        }
        public bool ReadAttempted { get; private set; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            ReadAttempted = true;
            throw new InvalidOperationException("Oversized content must not be read.");
        }
        protected override bool TryComputeLength(out long length) { length = _length; return true; }
    }

    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly byte[] _payload;
        public UnknownLengthContent(int stringBytes)
        {
            _payload = Encoding.UTF8.GetBytes($"\"{new string('x', stringBytes)}\"");
            Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }
        public bool ReadAttempted { get; private set; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            ReadAttempted = true;
            return stream.WriteAsync(_payload).AsTask();
        }
        protected override Task<Stream> CreateContentReadStreamAsync()
        {
            ReadAttempted = true;
            return Task.FromResult<Stream>(new MemoryStream(_payload, writable: false));
        }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }
}
