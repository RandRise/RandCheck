using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Api.Tests;

public sealed class ReviewApiContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ValidSha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 10, 0, 0, TimeSpan.Zero);
    private readonly WebApplicationFactory<Program> _factory;

    public ReviewApiContractTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UsePhase5DevelopmentIdentity());
    }

    [Fact]
    public async Task Start_returns_201_workspace_and_strong_etag()
    {
        var (document, _) = CreateReadyDocument();
        using var factory = WithDocument(document);
        using var client = factory.CreateClient();

        using var response = await client.PostAsync($"/api/documents/{document.Id}/reviews", null);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var session = Assert.Single(document.ReviewSessions);
        Assert.Equal($"\"{session.ConcurrencyToken:D}\"", response.Headers.ETag?.Tag);
        Assert.Equal(document.Id, json.GetProperty("documentId").GetGuid());
        Assert.Equal("InProgress", json.GetProperty("session").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Get_returns_200_current_workspace_and_etag()
    {
        var (document, _) = CreateReadyDocument();
        var session = document.OpenReview(Guid.NewGuid(), TestActor, Now);
        using var factory = WithDocument(document);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/documents/{document.Id}/review");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"\"{session.ConcurrencyToken:D}\"", response.Headers.ETag?.Tag);
    }

    [Fact]
    public async Task History_returns_append_only_actions_in_sequence()
    {
        var (document, run) = CreateReadyDocument();
        var session = document.OpenReview(Guid.NewGuid(), TestActor, Now);
        var field = Assert.Single(run.Fields);
        session.AddAction(Guid.NewGuid(), TestActor, 1, ReviewActionType.Corrected,
            field.FieldKey, field.OccurrenceIndex, field.Id, FieldValueType.Decimal,
            "125.00", "Invoice evidence", Now.AddMinutes(1));
        using var factory = WithDocument(document);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/documents/{document.Id}/review-history");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var exportedSession = Assert.Single(json.GetProperty("sessions").EnumerateArray());
        var action = Assert.Single(exportedSession.GetProperty("actions").EnumerateArray());
        Assert.Equal("Corrected", action.GetProperty("action").GetString());
        Assert.Equal("Invoice evidence", action.GetProperty("reason").GetString());
        Assert.Equal(1, action.GetProperty("sequenceNumber").GetInt32());
    }

    [Fact]
    public async Task Action_returns_400_for_weak_or_malformed_etag()
    {
        var (document, run) = CreateReadyDocument();
        var session = document.OpenReview(Guid.NewGuid(), TestActor, Now);
        var field = Assert.Single(run.Fields);
        using var factory = WithDocument(document);
        using var client = factory.CreateClient();
        using var request = ActionRequest(document.Id, session.Id, field.Id);
        request.Headers.TryAddWithoutValidation("If-Match", $"W/\"{session.ConcurrencyToken:D}\"");

        using var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_review_etag", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Get_returns_404_without_revealing_document_presence()
    {
        using var factory = WithDocument(null);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/documents/{Guid.NewGuid()}/review");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("review_not_found", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Action_returns_428_when_if_match_is_missing()
    {
        var (document, run) = CreateReadyDocument();
        var session = document.OpenReview(Guid.NewGuid(), TestActor, Now);
        var field = Assert.Single(run.Fields);
        using var factory = WithDocument(document);
        using var client = factory.CreateClient();
        using var request = ActionRequest(document.Id, session.Id, field.Id);

        using var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal((HttpStatusCode)428, response.StatusCode);
        Assert.Equal("review_precondition_required", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Action_returns_412_for_stale_strong_etag()
    {
        var (document, run) = CreateReadyDocument();
        var session = document.OpenReview(Guid.NewGuid(), TestActor, Now);
        var field = Assert.Single(run.Fields);
        using var factory = WithDocument(document);
        using var client = factory.CreateClient();
        using var request = ActionRequest(document.Id, session.Id, field.Id);
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{Guid.NewGuid():D}\"");

        using var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal("stale_review_version", problem.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("999", null, "invalid_review_action")]
    [InlineData("Corrected", "999", "invalid_value_type")]
    public async Task Action_returns_400_for_undefined_numeric_enum_values(
        string action, string? valueType, string expectedCode)
    {
        var (document, run) = CreateReadyDocument();
        var session = document.OpenReview(Guid.NewGuid(), TestActor, Now);
        var field = Assert.Single(run.Fields);
        using var factory = WithDocument(document);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/documents/{document.Id}/reviews/{session.Id}/actions")
        {
            Content = JsonContent.Create(new
            {
                action,
                machineFieldValueId = field.Id,
                valueType,
                value = valueType is null ? null : "105.00",
                reason = valueType is null ? null : "reason"
            })
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{session.ConcurrencyToken:D}\"");

        using var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedCode, problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Complete_returns_409_with_pending_fields_when_review_is_incomplete()
    {
        var (document, _) = CreateReadyDocument(fieldCount: 2);
        var session = document.OpenReview(Guid.NewGuid(), TestActor, Now);
        using var factory = WithDocument(document);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/documents/{document.Id}/reviews/{session.Id}/complete");
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{session.ConcurrencyToken:D}\"");

        using var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("review_incomplete", problem.GetProperty("code").GetString());
        Assert.Equal(2, problem.GetProperty("pendingFields").GetArrayLength());
    }

    private WebApplicationFactory<Program> WithDocument(Document? document)
    {
        var repository = new FakeRepository(document);
        return _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDocumentRepository>();
            services.RemoveAll<IExecutionContext>();
            services.RemoveAll<IClock>();
            services.AddSingleton<IDocumentRepository>(repository);
            services.AddSingleton<IExecutionContext>(new FakeExecutionContext(
                document?.OrganizationId ?? TestOrganization, TestActor));
            services.AddSingleton<IClock>(new FakeClock(Now));
        }));
    }

    private static HttpRequestMessage ActionRequest(Guid documentId, Guid sessionId, Guid fieldId) =>
        new(HttpMethod.Post, $"/api/documents/{documentId}/reviews/{sessionId}/actions")
        {
            Content = JsonContent.Create(new
            {
                action = "Confirmed",
                machineFieldValueId = fieldId
            })
        };

    private static (Document Document, ExtractionRun Run) CreateReadyDocument(int fieldCount = 1)
    {
        var document = new Document(Guid.NewGuid(), TestOrganization, TestActor, "invoice.pdf",
            "storage.pdf", "application/pdf", 100, ValidSha, Now);
        var run = document.QueueExtraction(Guid.NewGuid(), 1, "mock", "1", "invoice-v1", "request", Now);
        run.Start(Now);
        run.AddPage(Guid.NewGuid(), 1, string.Empty, 0);
        for (var index = 0; index < fieldCount; index++)
        {
            var field = run.AddField(Guid.NewGuid(), $"field_{index}", 0, FieldValueType.Decimal,
                $"{100 + index}.00", 0.9m, Now);
            run.AddEvidence(field, Guid.NewGuid(), 1);
        }
        run.RecordProviderMetrics(1, 1, null, null, null);
        run.Succeed("invoice", "en", Now, "mock-model", "v1");
        document.RecordExtractionSuccess(run, Now);
        return (document, run);
    }

    private static readonly Guid TestOrganization = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid TestActor = Guid.Parse("20000000-0000-0000-0000-000000000001");

    private sealed class FakeRepository(Document? document) : IDocumentRepository
    {
        public Task<Document?> GetAsync(Guid organizationId, Guid documentId, CancellationToken token) =>
            Task.FromResult(document is not null && document.OrganizationId == organizationId &&
                document.Id == documentId ? document : null);
        public Task<Document?> GetForReviewAsync(Guid organizationId, Guid documentId, CancellationToken token) =>
            GetAsync(organizationId, documentId, token);
        public Task<ReviewSaveOutcome> SaveReviewChangesAsync(CancellationToken token) =>
            Task.FromResult(ReviewSaveOutcome.Saved);
        public Task AddAsync(Document value, CancellationToken token) => Task.CompletedTask;
        public Task<IReadOnlyList<Document>> ListAsync(Guid organizationId, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<Document>>([]);
        public Task<int> NextRunNumberAsync(Guid organizationId, Guid documentId, CancellationToken token) =>
            Task.FromResult(1);
        public Task<DocumentMetrics> GetMetricsAsync(Guid organizationId, CancellationToken token) =>
            Task.FromResult(new DocumentMetrics(0, 0, 0, null));
        public Task SaveChangesAsync(CancellationToken token) => Task.CompletedTask;
    }

    private sealed record FakeExecutionContext(Guid OrganizationId, Guid ActorId) : IExecutionContext;
    private sealed record FakeClock(DateTimeOffset UtcNow) : IClock;
}
