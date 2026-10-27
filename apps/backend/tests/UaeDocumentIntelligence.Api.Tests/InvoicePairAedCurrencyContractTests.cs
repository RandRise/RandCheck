using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Api.Tests;

public sealed partial class ReconciliationApiContractTests
{
    [Theory]
    [InlineData("invoice-po-1.0", "USD", true, "aed_currency_required")]
    [InlineData("invoice-po-2.0", "USD", true, "aed_currency_required")]
    [InlineData("invoice-po-1.0", "AED", false, "aed_currency_unverified")]
    [InlineData("invoice-po-2.0", "AED", false, "aed_currency_unverified")]
    public async Task Saved_pair_currency_refusal_returns_plain_error_and_keeps_read_recovery(
        string policy, string invoiceCurrency, bool quote, string expectedCode)
    {
        var item = new ReconciliationCase(Guid.NewGuid(), OrganizationId, ActorId, "AED check", Now,
            workflowKind: ReconciliationWorkflowKind.InvoicePurchaseOrder);
        var documents = new List<Document>();
        foreach (var role in new[] { ReconciliationDocumentRole.Invoice, ReconciliationDocumentRole.PurchaseOrder })
        {
            var invoice = role == ReconciliationDocumentRole.Invoice;
            var value = invoice ? invoiceCurrency : "AED";
            var document = new Document(Guid.NewGuid(), OrganizationId, ActorId, role + ".pdf",
                Guid.NewGuid() + ".pdf", "application/pdf", 100, new string(invoice ? 'a' : 'b', 64), Now);
            var extraction = document.QueueExtraction(Guid.NewGuid(), 1, "mock", "1", "2.0", Guid.NewGuid().ToString("N"), Now);
            extraction.Start(Now);
            extraction.AddPage(Guid.NewGuid(), 1, value, 0);
            var field = extraction.AddField(Guid.NewGuid(), "currency", 0, FieldValueType.Currency, value, .9m, Now);
            if (!invoice || quote) extraction.AddEvidence(field, Guid.NewGuid(), 1, value, 0, 3);
            else extraction.AddEvidence(field, Guid.NewGuid(), 1);
            extraction.RecordProviderMetrics(1, 1, null, null, null);
            extraction.Succeed(invoice ? "invoice" : "purchase_order", "en", Now, "mock", "1");
            document.RecordExtractionSuccess(extraction, Now);
            var review = document.OpenReview(Guid.NewGuid(), ActorId, Now);
            review.AddAction(Guid.NewGuid(), ActorId, 1, ReviewActionType.Confirmed, "currency", 0, field.Id, null, null, null, Now);
            review.Complete(extraction.Fields, Now);
            item.SetDraftDocument(role, document.Id, Now);
            item.LinkReviewedDocument(Guid.NewGuid(), role, document.Id, review.Id, extraction.Id, document.Sha256, Now);
            documents.Add(document);
        }
        var token = item.ConcurrencyToken;
        var before = JsonSerializer.Serialize(item);
        using var factory = WorkflowApiFactory(new FakeRepository(item), new WorkflowApiRequests(), documents.ToArray());
        using var client = factory.CreateClient();
        using var request = PairEvaluationRequest(item, token, new { ruleSetVersion = policy, orderScope = policy == "invoice-po-1.0" ? "Unknown" : null });
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expectedCode, problem.GetProperty("code").GetString());
        Assert.Contains("Your saved work is kept", problem.GetProperty("detail").GetString());
        Assert.Equal(before, JsonSerializer.Serialize(item));
        using var recovered = await client.GetAsync($"/api/reconciliation-cases/{item.Id}");
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal($"\"{token:D}\"", recovered.Headers.ETag!.Tag);
        var saved = await recovered.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, saved.GetProperty("runs").GetArrayLength());
        Assert.Equal(2, saved.GetProperty("documents").GetArrayLength());
    }
}
