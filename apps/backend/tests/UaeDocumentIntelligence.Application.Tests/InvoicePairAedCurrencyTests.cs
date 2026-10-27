using System.Text.Json;
using UaeDocumentIntelligence.Application.Reconciliation;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Tests;

public sealed partial class ReconciliationHandlerTests
{
    [Fact]
    public async Task Factual_policy_accepts_no_scope_and_preserves_missing_check_status()
    {
        var fixture = AedFixture();
        var result = await fixture.Handler.HandleAsync(fixture.Item.Id, fixture.Item.ConcurrencyToken,
            new(RuleSetVersion: InvoicePurchaseOrderRuleEngine.FactualRuleSetVersion), default);
        var run = Assert.Single(result.Runs);
        Assert.Equal("Unknown", run.OrderScope);
        Assert.Equal(InvoicePurchaseOrderRuleEngine.FactualRuleSetVersion, run.RuleSetVersion);
        Assert.Contains(run.Checks!, check => check.Status == "CouldNotCheck");
        Assert.NotEqual("Complete", run.Coverage!.Status);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("FullOrder")]
    [InlineData("Partial")]
    public async Task Factual_policy_refuses_scope_declarations_before_mutation(string scope)
    {
        var fixture = AedFixture();
        var before = JsonSerializer.Serialize(fixture.Item);
        var error = await Assert.ThrowsAsync<ReconciliationValidationException>(() => fixture.Handler.HandleAsync(
            fixture.Item.Id, fixture.Item.ConcurrencyToken,
            new(RuleSetVersion: InvoicePurchaseOrderRuleEngine.FactualRuleSetVersion, OrderScope: scope), default));
        Assert.Equal("scope_not_used", error.Code);
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Item));
    }

    [Theory]
    [InlineData("USD", "normal")]
    [InlineData("AED", "unchecked")]
    public async Task Factual_policy_retains_AED_and_checked_evidence_gate(string currency, string scenario)
    {
        var fixture = AedFixture(currency, scenario: scenario);
        var before = JsonSerializer.Serialize(fixture.Item);
        await Assert.ThrowsAsync<ReconciliationValidationException>(() => fixture.Handler.HandleAsync(fixture.Item.Id,
            fixture.Item.ConcurrencyToken, new(RuleSetVersion: InvoicePurchaseOrderRuleEngine.FactualRuleSetVersion), default));
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Item));
        Assert.Equal(0, fixture.Repository.SaveCount);
    }

    [Theory]
    [InlineData("USD", true)]
    [InlineData("EUR", false)]
    [InlineData("GBP", true)]
    [InlineData("دولار", false)]
    [InlineData("unknown", true)]
    public async Task Aed_gate_rejects_other_currencies_without_changing_saved_work(string currency, bool invoice)
    {
        var fixture = AedFixture(invoice ? currency : "AED", invoice ? "AED" : currency);
        var before = JsonSerializer.Serialize(fixture.Item);

        var error = await Assert.ThrowsAsync<ReconciliationValidationException>(() =>
            fixture.Handler.HandleAsync(fixture.Item.Id, fixture.Item.ConcurrencyToken, PairCommand(), default));

        Assert.Equal("aed_currency_required", error.Code);
        Assert.Contains(invoice ? "invoice" : "purchase order", error.Message);
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Item));
        Assert.Equal(0, fixture.Repository.SaveCount);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unchecked")]
    [InlineData("cleared")]
    [InlineData("added")]
    [InlineData("duplicate")]
    [InlineData("wrong-type")]
    [InlineData("no-quote")]
    [InlineData("first-quote-missing")]
    [InlineData("normalized-only")]
    public async Task Aed_gate_requires_one_checked_currency_with_source_evidence(string scenario)
    {
        var fixture = AedFixture(scenario: scenario);
        var before = JsonSerializer.Serialize(fixture.Item);

        var error = await Assert.ThrowsAsync<ReconciliationValidationException>(() =>
            fixture.Handler.HandleAsync(fixture.Item.Id, fixture.Item.ConcurrencyToken, PairCommand(), default));

        Assert.Equal(scenario == "normalized-only" ? "aed_currency_required" : "aed_currency_unverified", error.Code);
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Item));
        Assert.Equal(0, fixture.Repository.SaveCount);
    }

    [Theory]
    [InlineData("AED", "normal", "AED")]
    [InlineData(" aed ", "normal", "aed")]
    [InlineData("AEO", "corrected", "AED")]
    public async Task Aed_gate_accepts_reviewed_AED_without_filling_missing_checks(string raw, string scenario, string expected)
    {
        var fixture = AedFixture(raw, scenario: scenario);
        var result = await fixture.Handler.HandleAsync(fixture.Item.Id, fixture.Item.ConcurrencyToken, PairCommand(), default);

        var run = Assert.Single(result.Runs);
        var currency = Assert.Single(run.Fields.Where(field => field.Role == "Invoice" && field.Key == "currency"));
        Assert.Equal(expected, currency.Value);
        Assert.Equal(raw, currency.Quote);
        Assert.NotEqual("Complete", run.Coverage!.Status);
        Assert.Contains(run.Checks!, check => check.Status == "CouldNotCheck");
        Assert.Equal("NoDifference", Assert.Single(run.Checks!.Where(check => check.FieldKey == "currency")).Status);
        Assert.DoesNotContain(run.Checks!, check => check.ReasonCode == "duplicate_source");
        Assert.Equal(1, fixture.Repository.SaveCount);
    }

    [Fact]
    public async Task Aed_gate_rejects_reviewed_foreign_correction_even_when_machine_said_AED()
    {
        var fixture = AedFixture(scenario: "corrected-foreign");
        var error = await Assert.ThrowsAsync<ReconciliationValidationException>(() =>
            fixture.Handler.HandleAsync(fixture.Item.Id, fixture.Item.ConcurrencyToken, PairCommand(), default));
        Assert.Equal("aed_currency_required", error.Code);
        Assert.Empty(fixture.Item.Runs);
        Assert.Equal(0, fixture.Repository.SaveCount);
    }

    [Fact]
    public async Task Aed_gate_uses_pinned_review_not_newer_revision_and_keeps_prior_results()
    {
        var fixture = AedFixture();
        await fixture.Handler.HandleAsync(fixture.Item.Id, fixture.Item.ConcurrencyToken, PairCommand(), default);
        var old = JsonSerializer.Serialize(fixture.Item.Runs);
        var extraction = fixture.Invoice.QueueExtraction(Guid.NewGuid(), 2, "mock", "1", "2.0", Guid.NewGuid().ToString("N"), Now.AddMinutes(1));
        extraction.Start(Now.AddMinutes(1));
        extraction.AddPage(Guid.NewGuid(), 1, "USD", 0);
        var field = extraction.AddField(Guid.NewGuid(), "currency", 0, FieldValueType.Currency, "USD", .9m, Now.AddMinutes(1));
        extraction.AddEvidence(field, Guid.NewGuid(), 1, "USD", 0, 3);
        extraction.RecordProviderMetrics(1, 1, null, null, null);
        extraction.Succeed("invoice", "en", Now.AddMinutes(1), "mock", "1");
        fixture.Invoice.RecordExtractionSuccess(extraction, Now.AddMinutes(1));
        var revision = fixture.Invoice.OpenReview(Guid.NewGuid(), fixture.Actor, Now.AddMinutes(1));
        revision.AddAction(Guid.NewGuid(), fixture.Actor, 1, ReviewActionType.Confirmed,
            "currency", 0, field.Id, null, null, null, Now.AddMinutes(1));
        revision.Complete(extraction.Fields, Now.AddMinutes(1));

        // The new unpinned revision must not replace the source of this comparison.
        var result = await fixture.Handler.HandleAsync(fixture.Item.Id, fixture.Item.ConcurrencyToken, PairCommand(), default);
        Assert.All(result.Runs, run => Assert.Equal("AED", Assert.Single(run.Fields.Where(f => f.Role == "Invoice" && f.Key == "currency")).Value));
        Assert.Equal(old, JsonSerializer.Serialize(fixture.Item.Runs.Take(1)));

        fixture.Item.ReplaceReviewedDocument(Guid.NewGuid(), ReconciliationDocumentRole.Invoice,
            fixture.Invoice.Id, revision.Id, revision.ExtractionRunId, fixture.Invoice.Sha256, Now.AddMinutes(2));
        var before = JsonSerializer.Serialize(fixture.Item);
        await Assert.ThrowsAsync<ReconciliationValidationException>(() =>
            fixture.Handler.HandleAsync(fixture.Item.Id, fixture.Item.ConcurrencyToken, PairCommand(), default));
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Item));
        Assert.Equal(2, fixture.Repository.SaveCount);
    }

    [Fact]
    public async Task Aed_gate_keeps_legacy_pair_policy_compatible_and_stale_requests_read_only()
    {
        var fixture = AedFixture("USD", "USD", legacy: true);
        var token = fixture.Item.ConcurrencyToken;
        var result = await fixture.Handler.HandleAsync(fixture.Item.Id, token, PairCommand(), default);
        Assert.All(Assert.Single(result.Runs).Fields, field => Assert.Equal("USD", field.Value));
        var before = JsonSerializer.Serialize(fixture.Item);
        await Assert.ThrowsAsync<ReconciliationPreconditionException>(() =>
            fixture.Handler.HandleAsync(fixture.Item.Id, token, PairCommand(), default));
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Item));
        Assert.Equal(1, fixture.Repository.SaveCount);
    }

    private static (ReconciliationCase Item, EvaluateReconciliationCaseHandler Handler,
        FakeReconciliationRepository Repository, Document Invoice, Guid Actor) AedFixture(
        string invoiceCurrency = "AED", string poCurrency = "AED", string scenario = "normal", bool legacy = false)
    {
        var org = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var invoice = CurrencyDocument(org, actor, "invoice", invoiceCurrency, scenario);
        var po = CurrencyDocument(org, actor, "purchase_order", poCurrency, "normal");
        var item = new ReconciliationCase(Guid.NewGuid(), org, actor, "Currency admission", Now,
            workflowKind: legacy ? ReconciliationWorkflowKind.Legacy : ReconciliationWorkflowKind.InvoicePurchaseOrder);
        if (!legacy)
        {
            item.SetDraftDocument(ReconciliationDocumentRole.Invoice, invoice.Id, Now);
            item.SetDraftDocument(ReconciliationDocumentRole.PurchaseOrder, po.Id, Now);
        }
        Link(item, ReconciliationDocumentRole.Invoice, invoice);
        Link(item, ReconciliationDocumentRole.PurchaseOrder, po);
        var repository = new FakeReconciliationRepository(item);
        return (item, new(repository, new FakeDocumentRepository(invoice, po), new FakeExecutionContext(org, actor), new FakeClock(Now)), repository, invoice, actor);
    }

    private static Document CurrencyDocument(Guid org, Guid actor, string role, string currency, string scenario)
    {
        var document = new Document(Guid.NewGuid(), org, actor, role + ".pdf", Guid.NewGuid() + ".pdf",
            "application/pdf", 100, new string(role == "invoice" ? 'a' : 'b', 64), Now);
        var run = document.QueueExtraction(Guid.NewGuid(), 1, "mock", "1", "2.0", Guid.NewGuid().ToString("N"), Now);
        run.Start(Now);
        run.AddPage(Guid.NewGuid(), 1, currency, 0);
        if (scenario is not ("missing" or "added"))
        {
            var field = run.AddField(Guid.NewGuid(), "currency", 0,
                scenario == "wrong-type" ? FieldValueType.String : FieldValueType.Currency,
                scenario == "normalized-only" ? "USD" : currency, .9m, Now, "AED");
            if (scenario is "no-quote" or "first-quote-missing")
                run.AddEvidence(field, Guid.NewGuid(), 1);
            else
                run.AddEvidence(field, Guid.NewGuid(), 1, currency, 0, System.Text.Encoding.UTF8.GetByteCount(currency));
            if (scenario == "first-quote-missing")
                run.AddEvidence(field, Guid.NewGuid(), 1, currency, 0, System.Text.Encoding.UTF8.GetByteCount(currency));
        }
        else
        {
            // A real completed extraction still needs some field, even when currency is absent.
            var other = run.AddField(Guid.NewGuid(), "document_number", 0, FieldValueType.Identifier, "DOC-1", .9m, Now);
            run.AddEvidence(other, Guid.NewGuid(), 1);
        }
        run.RecordProviderMetrics(1, 1, null, null, null);
        run.Succeed(role, "en", Now, "mock", "1");
        document.RecordExtractionSuccess(run, Now);
        var review = document.OpenReview(Guid.NewGuid(), actor, Now);
        var sequence = 1;
        foreach (var field in run.Fields)
        {
            if (scenario == "unchecked") continue;
            var corrected = scenario is "corrected" or "corrected-foreign";
            review.AddAction(Guid.NewGuid(), actor, sequence++, scenario == "cleared" ? ReviewActionType.Cleared :
                corrected ? ReviewActionType.Corrected : ReviewActionType.Confirmed,
                field.FieldKey, 0, field.Id, corrected ? FieldValueType.Currency : null,
                corrected ? scenario == "corrected-foreign" ? "USD" : "AED" : null,
                scenario == "cleared" || corrected ? "Checked against source" : null, Now);
        }
        if (scenario is "added" or "duplicate")
            review.AddAction(Guid.NewGuid(), actor, sequence, ReviewActionType.Added, "currency", 0,
                null, FieldValueType.Currency, "AED", "Added from source", Now);
        // Model a historically incomplete review defensively, without changing production completion guards.
        review.Complete(scenario == "unchecked" ? [] : run.Fields, Now);
        return document;
    }
}
