using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Api.Tests;

public sealed partial class ReconciliationApiContractTests
{
    [Theory]
    [InlineData("invoice-po-1.0", "0")]
    [InlineData("invoice-po-1.0", "fullorder")]
    [InlineData("invoice-po-1.0", "Unrecognized")]
    [InlineData("unknown-policy", "FullOrder")]
    [InlineData("1.0", "FullOrder")]
    public async Task Pair_policy_scope_rejects_unsupported_or_noncanonical_names_before_mutation(string policy, string scope)
    {
        var item = new ReconciliationCase(Guid.NewGuid(), OrganizationId, ActorId, "Independent policy input", Now);
        using var factory = WithRepository(new FakeRepository(item));
        using var client = factory.CreateClient();
        using var request = PairEvaluationRequest(item, item.ConcurrencyToken, new { ruleSetVersion = policy, orderScope = scope });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(item.Runs);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("code", out _));
    }

    [Theory]
    [InlineData("invoice-po-1.0")]
    [InlineData("invoice-po-2.0")]
    public async Task Pair_policy_evaluation_keeps_strong_ETag_precondition_and_safe_stale_retry(string policy)
    {
        var item = new ReconciliationCase(Guid.NewGuid(), OrganizationId, ActorId, "Independent ETag input", Now);
        using var factory = WithRepository(new FakeRepository(item));
        using var client = factory.CreateClient();
        using var missing = await client.PostAsJsonAsync($"/api/reconciliation-cases/{item.Id}/evaluations",
            new { ruleSetVersion = policy, orderScope = policy == "invoice-po-1.0" ? "FullOrder" : null });
        Assert.Equal((HttpStatusCode)428, missing.StatusCode);
        using var staleRequest = PairEvaluationRequest(item, Guid.NewGuid(), new { ruleSetVersion = policy, orderScope = policy == "invoice-po-1.0" ? "FullOrder" : null });
        using var stale = await client.SendAsync(staleRequest);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Empty(item.Runs);
    }

    [Fact]
    public async Task Pair_policy_GET_exposes_immutable_checked_sources_signed_difference_and_owned_unavailable_finding()
    {
        var item = new ReconciliationCase(Guid.NewGuid(), OrganizationId, ActorId, "Actual engine, synthetic inputs", Now);
        foreach (var role in new[] { ReconciliationDocumentRole.PurchaseOrder, ReconciliationDocumentRole.Invoice })
            item.LinkReviewedDocument(Guid.NewGuid(), role, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                new string(role == ReconciliationDocumentRole.Invoice ? 'a' : 'b', 64), Now);
        var run = item.BeginEvaluation(Guid.NewGuid(), item.DocumentLinks.ToDictionary(link => link.Role, _ => Guid.NewGuid()),
            "invoice-po-1.0", ReconciliationTolerancePolicy.Default, Now);
        run.SetPairContext(ActorId, InvoiceOrderScope.FullOrder);
        foreach (var source in run.DocumentSnapshots)
        {
            run.CaptureSourceState(source.Id, "Succeeded", "local", "local-1.6.0", "fixture", "independent", "[]", 1, true);
            var fields = new[] { ("currency", FieldValueType.Currency, "AED"),
                ("total", FieldValueType.Decimal, source.Role == ReconciliationDocumentRole.Invoice ? "1261.00" : "1260.00"),
                ("document_total_basis", FieldValueType.String, "gross"), ("line_description", FieldValueType.String, "Training") };
            foreach (var field in fields)
                run.AddFieldSnapshot(Guid.NewGuid(), source.Id, field.Item1, field.Item1.StartsWith("line_") ? 1 : 0,
                    field.Item2, field.Item3, Guid.NewGuid(), Guid.NewGuid(), 1, field.Item3, Now, ReviewedFieldSourceKind.Confirmed);
        }
        new InvoicePurchaseOrderRuleEngine().Evaluate(run, Guid.NewGuid, Now);
        item.CompleteEvaluation(run, Now);
        using var factory = WithRepository(new FakeRepository(item));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/reconciliation-cases/{item.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"\"{item.ConcurrencyToken:D}\"", response.Headers.ETag?.ToString());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var frozen = Assert.Single(body.GetProperty("runs").EnumerateArray());
        Assert.Equal("invoice-po-1.0", frozen.GetProperty("ruleSetVersion").GetString());
        Assert.Equal("FullOrder", frozen.GetProperty("orderScope").GetString());
        Assert.Equal(ActorId, frozen.GetProperty("evaluationActorId").GetGuid());
        Assert.Equal("Incomplete", frozen.GetProperty("coverage").GetProperty("status").GetString());
        var checks = frozen.GetProperty("checks").EnumerateArray().ToArray();
        var amount = Assert.Single(checks.Where(check => check.GetProperty("fieldKey").GetString() == "total"));
        Assert.Equal("DifferenceFound", amount.GetProperty("status").GetString());
        Assert.Equal(1m, amount.GetProperty("signedDifference").GetDecimal());
        Assert.Equal("AED", amount.GetProperty("currency").GetString());
        var findings = frozen.GetProperty("findings").EnumerateArray().Select(finding => finding.GetProperty("id").GetGuid()).ToHashSet();
        Assert.All(checks.Where(check => check.GetProperty("status").GetString() != "NoDifference"),
            check => Assert.Contains(check.GetProperty("findingId").GetGuid(), findings));
        var sources = frozen.GetProperty("sources").EnumerateArray().ToArray();
        Assert.Equal(2, sources.Length);
        Assert.All(frozen.GetProperty("fields").EnumerateArray(), field =>
        {
            Assert.Equal("Confirmed", field.GetProperty("sourceKind").GetString());
            Assert.NotEmpty(field.GetProperty("quote").GetString()!);
            Assert.Contains(sources, source => source.GetProperty("id").GetGuid() == field.GetProperty("documentSnapshotId").GetGuid() &&
                source.GetProperty("role").GetString() == field.GetProperty("role").GetString());
        });
    }

    private static HttpRequestMessage PairEvaluationRequest(ReconciliationCase item, Guid etag, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/reconciliation-cases/{item.Id}/evaluations") { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{etag:D}\"");
        return request;
    }
}
