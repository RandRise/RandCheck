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

public sealed class ReconciliationApiContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid OrganizationId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ActorId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
    private readonly WebApplicationFactory<Program> _factory;

    public ReconciliationApiContractTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(builder => builder.UsePhase5DevelopmentIdentity());

    [Fact]
    public async Task Create_and_get_return_case_contract_and_strong_etag()
    {
        var repository = new FakeRepository();
        using var factory = WithRepository(repository);
        using var client = factory.CreateClient();

        using var created = await client.PostAsJsonAsync("/api/reconciliation-cases",
            new { name = "August procurement" });
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal($"/api/reconciliation-cases/{id}", created.Headers.Location?.ToString());
        Assert.Equal($"\"{repository.Item!.ConcurrencyToken:D}\"", created.Headers.ETag?.Tag);
        Assert.Equal("Draft", body.GetProperty("status").GetString());

        using var fetched = await client.GetAsync($"/api/reconciliation-cases/{id}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(created.Headers.ETag?.Tag, fetched.Headers.ETag?.Tag);
    }

    [Fact]
    public async Task Mutation_requires_strong_if_match_header()
    {
        var item = new ReconciliationCase(Guid.NewGuid(), OrganizationId, ActorId, "Case", Now);
        using var factory = WithRepository(new FakeRepository(item));
        using var client = factory.CreateClient();

        using var missing = await client.PostAsJsonAsync(
            $"/api/reconciliation-cases/{item.Id}/evaluations", new { });
        var missingProblem = await missing.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((HttpStatusCode)428, missing.StatusCode);
        Assert.Equal("reconciliation_precondition_required",
            missingProblem.GetProperty("code").GetString());

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/reconciliation-cases/{item.Id}/evaluations")
        {
            Content = JsonContent.Create(new { })
        };
        request.Headers.TryAddWithoutValidation("If-Match",
            $"W/\"{item.ConcurrencyToken:D}\"");
        using var weak = await client.SendAsync(request);
        var weakProblem = await weak.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Equal("invalid_reconciliation_etag",
            weakProblem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Get_is_organization_scoped_and_returns_safe_not_found()
    {
        var foreign = new ReconciliationCase(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Foreign", Now);
        using var factory = WithRepository(new FakeRepository(foreign));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/reconciliation-cases/{foreign.Id}");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("reconciliation_not_found", problem.GetProperty("code").GetString());
    }

    private WebApplicationFactory<Program> WithRepository(FakeRepository repository) =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IReconciliationRepository>();
            services.RemoveAll<IExecutionContext>();
            services.RemoveAll<IClock>();
            services.AddSingleton<IReconciliationRepository>(repository);
            services.AddSingleton<IExecutionContext>(new FakeExecutionContext(OrganizationId, ActorId));
            services.AddSingleton<IClock>(new FakeClock(Now));
        }));

    private sealed class FakeRepository(ReconciliationCase? item = null) : IReconciliationRepository
    {
        public ReconciliationCase? Item { get; private set; } = item;
        public Task AddAsync(ReconciliationCase value, CancellationToken token)
        {
            Item = value;
            return Task.CompletedTask;
        }
        public Task<ReconciliationCase?> GetAsync(Guid organizationId, Guid id,
            CancellationToken token) => Task.FromResult(Item is not null &&
            Item.OrganizationId == organizationId && Item.Id == id ? Item : null);
        public Task<ReconciliationSaveOutcome> SaveChangesAsync(CancellationToken token) =>
            Task.FromResult(ReconciliationSaveOutcome.Saved);
    }

    private sealed record FakeExecutionContext(Guid OrganizationId, Guid ActorId) : IExecutionContext;
    private sealed record FakeClock(DateTimeOffset UtcNow) : IClock;
}
