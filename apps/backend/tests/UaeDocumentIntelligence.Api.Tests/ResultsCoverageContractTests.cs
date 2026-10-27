using System.Net.Http.Json;
using System.Text.Json;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Api.Tests;

public sealed partial class ReconciliationApiContractTests
{
    [Theory]
    [InlineData("invoice-po-1.0", true)]
    [InlineData("invoice-po-2.0", true)]
    [InlineData("invoice-po-2.0", false)]
    [InlineData("1.0", true)]
    public async Task Results_empty_or_legacy_coverage_never_claims_all_checks_passed(string policy, bool completed)
    {
        var item = new ReconciliationCase(Guid.NewGuid(), OrganizationId, ActorId, "Empty coverage fixture", Now);
        item.LinkReviewedDocument(Guid.NewGuid(), ReconciliationDocumentRole.Invoice, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('a', 64), Now);
        var run = item.BeginEvaluation(Guid.NewGuid(), item.DocumentLinks.ToDictionary(link => link.Role, _ => Guid.NewGuid()), policy, ReconciliationTolerancePolicy.Default, Now);
        if (completed) item.CompleteEvaluation(run, Now);
        using var factory = WithRepository(new FakeRepository(item));
        using var client = factory.CreateClient();
        var result = await client.GetFromJsonAsync<JsonElement>($"/api/reconciliation-cases/{item.Id}");
        var coverage = result.GetProperty("runs")[0].GetProperty("coverage");
        Assert.Equal("ChecksIncomplete", coverage.GetProperty("outcome").GetString());
        Assert.Equal(0, coverage.GetProperty("passedChecks").GetInt32());
        Assert.NotEqual("Complete", coverage.GetProperty("status").GetString());
    }
}
