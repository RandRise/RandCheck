using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Api.Tests;

public sealed partial class EnterpriseSecurityTests
{
    [Fact]
    public async Task Report_rechecks_membership_and_denies_revoked_access_before_reading_saved_values()
    {
        var actors = new ActorStore(); var reports = new ReportAccessProbe();
        using var factory = Host(actors).WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IReconciliationReportRepository>();
            services.AddSingleton<IReconciliationReportRepository>(reports);
        }));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token());
        client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", $"\"{Guid.NewGuid()}\"");
        var route = $"/api/reconciliation-cases/{Guid.NewGuid()}/runs/{Guid.NewGuid()}/report";
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(route)).StatusCode);
        Assert.Equal(1, reports.Calls);
        Assert.Equal(actors.Actor!.OrganizationId, reports.Organization);
        actors.Actor = null;
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(route)).StatusCode);
        Assert.Equal(1, reports.Calls);
    }

    private sealed class ReportAccessProbe : IReconciliationReportRepository
    {
        public int Calls { get; private set; }
        public Guid Organization { get; private set; }
        public Task<ReconciliationCase?> ReadAsync(Guid organizationId, Guid caseId, Guid runId, Guid expectedVersion, CancellationToken cancellationToken)
        {
            Calls++; Organization = organizationId;
            return Task.FromResult<ReconciliationCase?>(null);
        }
    }
}
