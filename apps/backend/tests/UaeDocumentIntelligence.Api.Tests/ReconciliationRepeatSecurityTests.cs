using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Api.Tests;

public sealed partial class EnterpriseSecurityTests
{
    [Fact]
    public async Task Saved_case_creation_key_does_not_bypass_role_change_or_revocation()
    {
        var actors = new ActorStore(); actors.Actor = actors.Actor! with { Role = OrganizationAccessRole.Staff };
        var repository = new CreationRequests();
        using var factory = Host(actors).WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IReconciliationRepository>(repository))));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(role: "DocumentIntelligence.Operator"));
        var payload = new { name = "Case", requestKey = "authorized-intent" };
        using var first = await client.PostAsJsonAsync("/api/reconciliation-cases", payload);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var replay = await client.PostAsJsonAsync("/api/reconciliation-cases", payload);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode); Assert.Equal(first.Headers.Location, replay.Headers.Location);
        var lookups = repository.Lookups;
        actors.Actor = actors.Actor! with { Role = (OrganizationAccessRole)0 };
        using var viewer = await client.PostAsJsonAsync("/api/reconciliation-cases", payload);
        Assert.Equal(HttpStatusCode.Forbidden, viewer.StatusCode);
        actors.Actor = null;
        using var revoked = await client.PostAsJsonAsync("/api/reconciliation-cases", payload);
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        Assert.Equal(lookups, repository.Lookups); Assert.Equal(1, repository.Saves);
    }

    private sealed class CreationRequests : IReconciliationRepository
    {
        private ReconciliationCase? _item;
        public int Lookups { get; private set; }
        public int Saves { get; private set; }
        public Task<ReconciliationCase?> GetByCreationRequestAsync(Guid organizationId, Guid actorId, string key, CancellationToken ct)
        {
            Lookups++;
            return Task.FromResult(_item is not null && _item.OrganizationId == organizationId && _item.CreatedByActorId == actorId &&
                _item.CreationRequestKey == key ? _item : null);
        }
        public Task AddAsync(ReconciliationCase item, CancellationToken ct) { _item = item; return Task.CompletedTask; }
        public Task<ReconciliationSaveOutcome> SaveChangesAsync(CancellationToken ct) { Saves++; return Task.FromResult(ReconciliationSaveOutcome.Saved); }
        public Task<ReconciliationCase?> GetAsync(Guid organizationId, Guid caseId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ReconciliationCaseSummary>> ListAsync(Guid organizationId, int offset, int limit,
            CancellationToken ct, ReconciliationCaseFilter? filter = null) => throw new NotSupportedException();
    }
}
