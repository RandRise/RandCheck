using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.Documents.Upload;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Api.Tests;

public sealed class ApiContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiContractTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UsePhase5DevelopmentIdentity());
    }

    [Fact]
    public async Task Health_route_returns_camel_case_contract_and_normalized_request_id()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation("X-Request-ID", "  reviewer-request-42  ");

        using var response = await client.SendAsync(request);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("reviewer-request-42", response.Headers.GetValues("X-Request-ID").Single());
        Assert.Equal("ok", json.GetProperty("status").GetString());
        Assert.Equal("document-intelligence-api", json.GetProperty("service").GetString());
        Assert.False(json.TryGetProperty("Status", out _));
    }

    [Fact]
    public async Task Invalid_request_id_is_replaced_with_normalized_32_character_guid()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation("X-Request-ID", new string('x', 101));

        using var response = await client.SendAsync(request);
        var requestId = response.Headers.GetValues("X-Request-ID").Single();

        Assert.Equal(32, requestId.Length);
        Assert.True(Guid.TryParseExact(requestId, "N", out _));
    }

    [Fact]
    public async Task Cors_exposes_etag_to_allowed_browser_origin()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost:3000");

        using var response = await client.SendAsync(request);

        Assert.Equal("http://localhost:3000",
            response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        var exposed = response.Headers.GetValues("Access-Control-Expose-Headers")
            .SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries));
        Assert.Contains(exposed, value => string.Equals(value, "ETag", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Upload_route_returns_415_problem_contract_for_unsupported_content_type()
    {
        using var client = _factory.CreateClient();
        using var content = CreateUpload("application/octet-stream");

        using var response = await client.PostAsync("/api/documents", content);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(415, problem.GetProperty("status").GetInt32());
        Assert.Equal("Unsupported file type", problem.GetProperty("detail").GetString());
        Assert.False(problem.TryGetProperty("Status", out _));
    }

    [Fact]
    public async Task Upload_route_returns_413_when_storage_rejects_declared_size()
    {
        var queue = new CapturingUploadQueueStore();
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDocumentStorage>();
            services.AddScoped<IDocumentStorage, RejectingStorage>();
            services.RemoveAll<IUploadQueueStore>();
            services.AddSingleton<IUploadQueueStore>(queue);
        }));
        using var client = factory.CreateClient();
        using var content = CreateUpload("application/pdf");

        using var response = await client.PostAsync("/api/documents", content);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(413, problem.GetProperty("status").GetInt32());
        Assert.Equal("File is empty or exceeds the upload limit", problem.GetProperty("detail").GetString());
        Assert.Null(queue.Document);
    }

    [Fact]
    public async Task Upload_route_returns_202_and_location_without_invoking_provider()
    {
        var queue = new CapturingUploadQueueStore();
        var extraction = new RejectIfCalledExtractionClient();
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDocumentStorage>();
            services.RemoveAll<IUploadQueueStore>();
            services.RemoveAll<IExtractionClient>();
            services.AddSingleton<IDocumentStorage, SuccessfulStorage>();
            services.AddSingleton<IUploadQueueStore>(queue);
            services.AddSingleton<IExtractionClient>(extraction);
        }));
        using var client = factory.CreateClient();
        using var content = CreateUpload("application/pdf");

        using var response = await client.PostAsync("/api/documents", content);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal($"/api/documents/{document.GetProperty("id").GetGuid()}",
            response.Headers.Location?.OriginalString);
        Assert.Equal("Processing", document.GetProperty("status").GetString());
        Assert.Equal(0, extraction.CallCount);
        Assert.NotNull(queue.Document);
        Assert.Equal(document.GetProperty("id").GetGuid(), queue.Document.Id);
        Assert.Equal(ExtractionJobStatus.Pending, queue.Job!.Status);
        Assert.Equal(ExtractionRunStatus.Queued, Assert.Single(queue.Document.ExtractionRuns).Status);
    }

    [Fact]
    public async Task Processing_status_route_returns_public_string_state_and_safe_failure_fields()
    {
        var queue = new StatusQueue();
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IExtractionJobQueue>();
            services.AddSingleton<IExtractionJobQueue>(queue);
        }));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/documents/{Guid.NewGuid()}/processing");
        var status = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Queued", status.GetProperty("status").GetString());
        Assert.Equal(2, status.GetProperty("attemptCount").GetInt32());
        Assert.Equal("provider_timeout", status.GetProperty("errorCode").GetString());
        Assert.Equal("The extraction provider timed out.", status.GetProperty("errorMessage").GetString());
    }

    [Fact]
    public async Task Unknown_route_returns_not_found()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/not-a-route");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static MultipartFormDataContent CreateUpload(string contentType)
    {
        var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent([1, 2, 3, 4]);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(file, "file", "invoice.pdf");
        return multipart;
    }

    private sealed class RejectingStorage : IDocumentStorage
    {
        public Task<StoredFile> SaveAsync(Stream content, string originalFilename, long declaredSize,
            Guid organizationId, Guid documentId, CancellationToken cancellationToken) =>
            throw new DocumentTooLargeException();

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class SuccessfulStorage : IDocumentStorage
    {
        public Task<StoredFile> SaveAsync(Stream content, string originalFilename, long declaredSize,
            Guid organizationId, Guid documentId, CancellationToken cancellationToken) =>
            Task.FromResult(new StoredFile(
                $"organizations/{organizationId:N}/documents/{documentId:N}/source.pdf",
                @"C:\safe-test-storage\source.pdf", declaredSize,
                "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"));

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CapturingUploadQueueStore : IUploadQueueStore
    {
        public Document? Document { get; private set; }
        public ExtractionJob? Job { get; private set; }

        public Task<ExtractionJob> AddAsync(Document document, Guid extractionRunId, Guid jobId,
            CancellationToken cancellationToken)
        {
            Document = document;
            Job = new ExtractionJob(jobId, document.OrganizationId, document.Id, extractionRunId,
                DateTimeOffset.UtcNow);
            return Task.FromResult(Job);
        }
    }

    private sealed class RejectIfCalledExtractionClient : IExtractionClient
    {
        public string ProviderName => "local";
        public string ProviderVersion => "local-1.0.0";
        public string SchemaVersion => "2.0";
        public int CallCount { get; private set; }

        public Task<ExtractionResult> ExtractAsync(WorkerExtractionRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            throw new InvalidOperationException("Upload must not call the provider.");
        }
    }

    private sealed class StatusQueue : IExtractionJobQueue
    {
        public Task<ExtractionJobSnapshot?> GetLatestAsync(Guid organizationId, Guid documentId,
            CancellationToken cancellationToken) => Task.FromResult<ExtractionJobSnapshot?>(new(
                Guid.NewGuid(), Guid.NewGuid(), ExtractionJobStatus.RetryScheduled, 2,
                "provider_timeout", "The extraction provider timed out.", null,
                DateTimeOffset.UtcNow.AddSeconds(30), null, DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow, null));
        public Task<bool> CancelAsync(Guid organizationId, Guid jobId, string code, string safeSummary,
            CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<QueuedReprocess?> QueueReprocessAsync(Guid organizationId, Guid documentId,
            Guid runId, Guid jobId, string providerAlias, string providerVersion, string schemaVersion,
            string correlationId, CancellationToken cancellationToken) =>
            Task.FromResult<QueuedReprocess?>(new(jobId, runId, 2));
        public Task<bool> TerminalizeOneExpiredAsync(CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<ClaimedExtractionJob?> ClaimAsync(string owner, CancellationToken cancellationToken) =>
            Task.FromResult<ClaimedExtractionJob?>(null);
        public Task<bool> HeartbeatAsync(Guid jobId, string owner, Guid leaseToken,
            CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> CompleteAsync(ClaimedExtractionJob claim, ExtractionResult result,
            CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> RecordFailureAsync(ClaimedExtractionJob claim, string code, string safeSummary,
            bool retryable, CancellationToken cancellationToken) => Task.FromResult(false);
    }
}
