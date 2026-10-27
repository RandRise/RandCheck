using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UaeDocumentIntelligence.Application.Reconciliation;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Theory]
    [InlineData("1261.00", "DifferencesFound")]
    [InlineData("1,23", "ChecksIncomplete")]
    [InlineData("1260.00", "AllRequiredChecksPassed")]
    public async Task Results_real_api_compares_exact_reviewed_pair_and_preserves_checks_after_decisions(string total, string outcome)
    {
        using var identity = await SourceFixtureAsync("Staff");
        var org = await SourceOrganization(identity);
        var fixture = await SupplierPairStoredFixture(org, identity.Member.ActorId, "1260.00", total);
        // A later completed review must not silently replace the version explicitly selected below.
        await using (var db = _database.CreateContext())
        {
            var invoice = await LoadDocumentAsync(db, fixture.DocumentLinks.Single(l => l.Role == ReconciliationDocumentRole.Invoice).DocumentId);
            var now = DateTimeOffset.UtcNow;
            var extraction = invoice.QueueExtraction(Guid.NewGuid(), 2, "local", "local-1.6.0", "2.0", Guid.NewGuid().ToString(), now);
            extraction.Start(now);
            extraction.AddPage(Guid.NewGuid(), 1, "9999", 0);
            var field = extraction.AddField(Guid.NewGuid(), "total", 0, FieldValueType.Decimal, "9999", .9m, now);
            extraction.AddEvidence(field, Guid.NewGuid(), 1, "9999", 0, 4);
            extraction.RecordProviderMetrics(1, 1, null, null, null);
            extraction.Succeed("invoice", "en", now, "fixture", "later-review");
            invoice.RecordExtractionSuccess(extraction, now);
            var review = invoice.OpenReview(Guid.NewGuid(), identity.Member.ActorId, now);
            review.AddAction(Guid.NewGuid(), identity.Member.ActorId, 1, ReviewActionType.Confirmed, "total", 0, field.Id, null, null, null, now);
            review.Complete(extraction.Fields, now);
            await db.SaveChangesAsync();
        }
        await using var role = await ApiRoleAsync();
        using var host = role.Host(identity.Host);
        using var client = WithAuthority(host, identity.User);
        using var created = await client.PostAsJsonAsync("/api/reconciliation-cases",
            new { name = "Results verification", requestKey = Guid.NewGuid().ToString(), workflowKind = "InvoicePurchaseOrder" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var current = (await created.Content.ReadFromJsonAsync<ReconciliationCaseView>())!;
        async Task<ReconciliationCaseView> Write(HttpMethod method, string path, object body, Guid token)
        {
            using var request = new HttpRequestMessage(method, $"/api/reconciliation-cases/{current.Id}/{path}") { Content = JsonContent.Create(body) };
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{token:D}\"");
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<ReconciliationCaseView>())!;
        }
        foreach (var link in fixture.DocumentLinks)
        {
            current = await Write(HttpMethod.Put, $"draft-documents/{link.Role}", new { link.DocumentId, requestKey = Guid.NewGuid().ToString() }, current.ConcurrencyToken);
            current = await Write(HttpMethod.Put, $"documents/{link.Role}", new { link.DocumentId, link.ReviewSessionId }, current.ConcurrencyToken);
        }
        var evaluationToken = current.ConcurrencyToken;
        current = await Write(HttpMethod.Post, "evaluations", new { ruleSetVersion = "invoice-po-2.0" }, evaluationToken);
        var run = Assert.Single(current.Runs);
        Assert.Equal(outcome, run.Coverage!.Outcome);
        Assert.All(run.Sources!, source => Assert.Contains(fixture.DocumentLinks, link =>
            source.DocumentId == link.DocumentId && source.ReviewSessionId == link.ReviewSessionId && source.ExtractionRunId == link.ExtractionRunId));
        Assert.Equal(total, Assert.Single(run.Fields.Where(f => f.Role == "Invoice" && f.Key == "total")).Value);
        Assert.Equal(run.Checks!.Count, run.Coverage.PassedChecks + run.Coverage.Differences + run.Coverage.CouldNotCheck);
        if (run.Findings.Count > 0)
        {
            current = await Write(HttpMethod.Post, $"runs/{run.Id}/findings/{run.Findings[0].Id}/resolutions",
                new { action = "Accepted", reason = "Requested supplier clarification; saved check is unchanged" }, current.ConcurrencyToken);
            var after = Assert.Single(current.Runs);
            // Relational collections have no promised order; compare every saved property by stable ID.
            Assert.Equal(run.Checks.OrderBy(c => c.Id), after.Checks!.OrderBy(c => c.Id));
            Assert.Equal(run.Sources!.OrderBy(s => s.Id), after.Sources!.OrderBy(s => s.Id));
            Assert.Equal(run.Fields.OrderBy(f => f.Id), after.Fields.OrderBy(f => f.Id));
            Assert.Equal(run.Coverage, after.Coverage);
        }
        using var stale = new HttpRequestMessage(HttpMethod.Post, $"/api/reconciliation-cases/{current.Id}/evaluations") { Content = JsonContent.Create(new { ruleSetVersion = "invoice-po-2.0" }) };
        stale.Headers.TryAddWithoutValidation("If-Match", $"\"{evaluationToken:D}\"");
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await client.SendAsync(stale)).StatusCode);
        var reopened = (await client.GetFromJsonAsync<ReconciliationCaseView>($"/api/reconciliation-cases/{current.Id}"))!;
        Assert.Single(reopened.Runs);
        Assert.Equal(run.Id, reopened.Runs[0].Id);
        Assert.Equal(run.Coverage, reopened.Runs[0].Coverage);
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM security_audit_events WHERE action='reconciliation.evaluated' AND target_id=@id", ("id", run.Id)));
        // Opt-in artifact contains only the synthetic reviewed fixture, never credentials or uploaded private data.
        if (Environment.GetEnvironmentVariable("RESULTS_STAGE_EVIDENCE") is { Length: > 0 } directory)
            await File.WriteAllTextAsync(Path.Combine(directory, $"real-api-{outcome}.json"), JsonSerializer.Serialize(reopened, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
}
