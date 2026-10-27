using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Api.Tests;

public sealed partial class ReconciliationApiContractTests
{
    [Fact]
    public async Task Report_preserves_exact_decimal_text_frozen_sources_and_decision_sequences()
    {
        var (item, run) = ReportCase();
        using var factory = WithReport(item); using var client = factory.CreateClient();
        using var response = await RequestReport(client, item.Id, run.Id, item.ConcurrencyToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var report = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(item.ConcurrencyToken, report.GetProperty("caseVersion").GetGuid());
        Assert.Equal(item.Id, report.GetProperty("caseId").GetGuid());
        Assert.Equal(Now, report.GetProperty("capturedAtUtc").GetDateTimeOffset());
        var saved = report.GetProperty("run");
        Assert.Equal("999999999999999999.123456", saved.GetProperty("checks")[0].GetProperty("signedDifference").GetString());
        Assert.Equal("0.01", saved.GetProperty("tolerances").GetProperty("documentTotal").GetString());
        Assert.Equal(run.DocumentSnapshots.Single().ReviewSessionId, saved.GetProperty("sources")[0].GetProperty("reviewSessionId").GetGuid());
        Assert.Equal("DifferencesFound", saved.GetProperty("coverage").GetProperty("outcome").GetString());
        Assert.Equal(new[] { 1, 2 }, saved.GetProperty("findings")[0].GetProperty("resolutions").EnumerateArray().Select(value => value.GetProperty("sequenceNumber").GetInt32()));
    }

    [Fact]
    public async Task Report_rejects_missing_weak_and_changed_versions_without_upgrading_the_request()
    {
        var (item, run) = ReportCase();
        using var factory = WithReport(item); using var client = factory.CreateClient();
        var path = $"/api/reconciliation-cases/{item.Id}/runs/{run.Id}/report";
        Assert.Equal((HttpStatusCode)428, (await client.GetAsync(path)).StatusCode);
        using var weak = new HttpRequestMessage(HttpMethod.Get, path);
        weak.Headers.TryAddWithoutValidation("If-Match", $"W/\"{item.ConcurrencyToken}\"");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(weak)).StatusCode);
        using var changed = await RequestReport(client, item.Id, run.Id, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.PreconditionFailed, changed.StatusCode);
        Assert.Equal("report_version_changed", (await changed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Report_rejects_foreign_case_or_foreign_run_without_saved_content()
    {
        var (item, run) = ReportCase();
        using var factory = WithReport(item); using var client = factory.CreateClient();
        using var wrongCase = await RequestReport(client, Guid.NewGuid(), run.Id, item.ConcurrencyToken);
        using var wrongRun = await RequestReport(client, item.Id, Guid.NewGuid(), item.ConcurrencyToken);
        Assert.Equal(HttpStatusCode.NotFound, wrongCase.StatusCode); Assert.Equal(HttpStatusCode.NotFound, wrongRun.StatusCode);
        var foreign = new ReconciliationCase(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Private foreign name", Now);
        using var otherFactory = WithReport(foreign); using var other = otherFactory.CreateClient();
        using var denied = await RequestReport(other, foreign.Id, run.Id, foreign.ConcurrencyToken);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.DoesNotContain("Private foreign name", await denied.Content.ReadAsStringAsync());
    }

    private WebApplicationFactory<Program> WithReport(ReconciliationCase item) =>
        WithRepository(new FakeRepository(item)).WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IReconciliationReportRepository>();
            services.AddSingleton<IReconciliationReportRepository>(new ReportRepository(item));
        }));

    private static Task<HttpResponseMessage> RequestReport(HttpClient client, Guid caseId, Guid runId, Guid version)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/reconciliation-cases/{caseId}/runs/{runId}/report");
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version:D}\"");
        return client.SendAsync(request);
    }

    private static (ReconciliationCase Item, ReconciliationRun Run) ReportCase()
    {
        var item = new ReconciliationCase(Guid.NewGuid(), OrganizationId, ActorId, "Invoice check", Now);
        item.LinkReviewedDocument(Guid.NewGuid(), ReconciliationDocumentRole.Invoice, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('a', 64), Now);
        var run = item.BeginEvaluation(Guid.NewGuid(), new Dictionary<ReconciliationDocumentRole, Guid> { [ReconciliationDocumentRole.Invoice] = Guid.NewGuid() }, "invoice-po-2.0", ReconciliationTolerancePolicy.Default, Now);
        var finding = run.AddFinding(Guid.NewGuid(), "Total", "2.0", ReconciliationFindingSeverity.Warning,
            ReconciliationDocumentRole.Invoice, ReconciliationDocumentRole.PurchaseOrder, null, null, "total", null,
            "0", "999999999999999999.123456", 999999999999999999.123456m, 0.01m, "Amounts differ", Now);
        run.AddCheck(Guid.NewGuid(), "Total", "total", null, null, ReconciliationCheckStatus.DifferenceFound,
            "amounts_differ", "Amounts differ", null, null, "0", "999999999999999999.123456", 999999999999999999.123456m,
            0.01m, null, "AED", "ExcludingTax", finding.Id, Now);
        item.CompleteEvaluation(run, Now);
        item.ResolveFinding(run.Id, finding.Id, Guid.NewGuid(), ActorId, FindingResolutionAction.Accepted, "Agreed change", Now);
        item.ResolveFinding(run.Id, finding.Id, Guid.NewGuid(), ActorId, FindingResolutionAction.Resolved, "Follow-up recorded", Now.AddMinutes(1));
        return (item, run);
    }

    private sealed class ReportRepository(ReconciliationCase item) : IReconciliationReportRepository
    {
        public Task<ReconciliationCase?> ReadAsync(Guid organizationId, Guid caseId, Guid runId, Guid expectedVersion, CancellationToken cancellationToken) =>
            Task.FromResult(item.OrganizationId == organizationId && item.Id == caseId ? item : null);
    }
}
