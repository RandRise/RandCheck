using System.Text;
using System.Text.Json;
using UaeDocumentIntelligence.Application.Reconciliation;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Tests;

public sealed partial class ReconciliationHandlerTests
{
    [Theory]
    [InlineData("1,23", "123", null, "Confirmed", "CouldNotCheck")]
    [InlineData("١٢٣", "123", null, "Confirmed", "CouldNotCheck")]
    [InlineData("1,23", "123", "1261.00", "Corrected", "DifferenceFound")]
    [InlineData("1260.00", "9999.00", null, "Confirmed", "NoDifference")]
    public async Task Pair_projection_uses_checked_raw_or_explicit_correction_never_normalized_fallback(
        string raw, string normalized, string? corrected, string sourceKind, string status)
    {
        var context = PairHandlerFixture(raw, normalized, corrected);
        var result = await context.Handler.HandleAsync(context.Case.Id, context.Case.ConcurrencyToken,
            PairCommand(), CancellationToken.None);
        var run = Assert.Single(result.Runs);
        var field = Assert.Single(run.Fields.Where(field => field.Role == "Invoice" && field.Key == "total"));
        Assert.Equal(corrected ?? raw, field.Value);
        Assert.Equal(raw, field.Quote);
        Assert.Equal(sourceKind, field.SourceKind);
        Assert.NotNull(field.ReviewActionId);
        Assert.Equal(status, Assert.Single(run.Checks!.Where(check => check.FieldKey == "total")).Status);
        if (corrected is not null)
            Assert.Equal(1m, Assert.Single(run.Checks!.Where(check => check.FieldKey == "total")).SignedDifference);
        Assert.Equal(context.Actor, run.EvaluationActorId);
        Assert.Equal("FullOrder", run.OrderScope);
        Assert.All(run.Fields, field => Assert.Contains(run.Sources!, source => source.Id == field.DocumentSnapshotId && source.Role == field.Role));
    }

    [Fact]
    public async Task Completed_legacy_review_with_unchecked_fields_cannot_trust_matching_values()
    {
        var context = PairHandlerFixture(uncheckedInvoice: true);
        var result = await context.Handler.HandleAsync(context.Case.Id, context.Case.ConcurrencyToken, PairCommand(), CancellationToken.None);
        var run = Assert.Single(result.Runs);
        Assert.All(run.Fields.Where(field => field.Role == "Invoice"), field => Assert.Equal("Unchecked", field.SourceKind));
        Assert.Equal("CouldNotCheck", Assert.Single(run.Checks!.Where(check => check.FieldKey == "total")).Status);
        Assert.NotEqual("Complete", run.Coverage!.Status);
    }

    [Fact]
    public async Task Invoice_identity_and_order_reference_remain_separate_in_pair_projection()
    {
        var context = PairHandlerFixture();
        var result = await context.Handler.HandleAsync(context.Case.Id, context.Case.ConcurrencyToken, PairCommand(), CancellationToken.None);
        var run = Assert.Single(result.Runs);
        Assert.Equal("SINV-0001", Assert.Single(run.Fields.Where(field => field.Role == "Invoice" && field.Key == "invoice_number")).Value);
        Assert.Equal("SPO-0001", Assert.Single(run.Fields.Where(field => field.Role == "Invoice" && field.Key == "purchase_order_number")).Value);
        Assert.All(run.Checks!, check => Assert.Equal("NoDifference", check.Status));
        Assert.Empty(run.Findings);
    }

    [Fact]
    public async Task Maximum_valid_Arabic_warning_list_is_frozen_without_truncation_and_blocks_coverage()
    {
        var context = PairHandlerFixture(warningCount: 100);
        var result = await context.Handler.HandleAsync(context.Case.Id, context.Case.ConcurrencyToken, PairCommand(), CancellationToken.None);
        var run = Assert.Single(result.Runs);
        var invoice = Assert.Single(run.Sources!.Where(source => source.Role == "Invoice"));
        Assert.True(invoice.WarningsJson!.Length > 100000);
        using var warnings = JsonDocument.Parse(invoice.WarningsJson);
        Assert.Equal(100, warnings.RootElement.GetArrayLength());
        Assert.All(warnings.RootElement.EnumerateArray(), warning => Assert.Equal(new string('ع', 300), warning.GetProperty("SafeSummary").GetString()));
        Assert.Equal("Unverified", invoice.RowCoverageStatus);
        Assert.NotEqual("Complete", run.Coverage!.Status);
        Assert.Equal("CouldNotCheck", Assert.Single(run.Checks!.Where(check => check.FieldKey == "total")).Status);
    }

    [Theory]
    [InlineData(FieldValueType.String)]
    [InlineData(FieldValueType.Identifier)]
    public async Task Wrong_reviewed_numeric_type_is_unavailable_even_with_valid_ascii_text(FieldValueType type)
    {
        var context = PairHandlerFixture(corrected: "1260.00", correctedType: type);
        var result = await context.Handler.HandleAsync(context.Case.Id, context.Case.ConcurrencyToken, PairCommand(), CancellationToken.None);
        Assert.Equal("CouldNotCheck", Assert.Single(Assert.Single(result.Runs).Checks!.Where(check => check.FieldKey == "total")).Status);
    }

    [Fact]
    public async Task Generic_TRN_does_not_claim_supplier_identity()
    {
        var context = PairHandlerFixture(genericTrn: true);
        var result = await context.Handler.HandleAsync(context.Case.Id, context.Case.ConcurrencyToken, PairCommand(), CancellationToken.None);
        Assert.Equal("CouldNotCheck", Assert.Single(Assert.Single(result.Runs).Checks!.Where(check => check.FieldKey == "supplier_trn")).Status);
    }

    [Theory]
    [InlineData(ReviewedFieldSourceKind.Unchecked)]
    [InlineData(ReviewedFieldSourceKind.Cleared)]
    public async Task Known_untrusted_duplicate_identity_cannot_disappear_into_arbitrary_unique_line_match(ReviewedFieldSourceKind kind)
    {
        var context = PairHandlerFixture(shadowKind: kind);
        var result = await context.Handler.HandleAsync(context.Case.Id, context.Case.ConcurrencyToken, PairCommand(), CancellationToken.None);
        var run = Assert.Single(result.Runs);
        var shadow = Assert.Single(run.Fields.Where(field => field.Role == "Invoice" && field.Key == "line_item_code" && field.Occurrence == 2));
        Assert.Equal("SRV-01", shadow.Value);
        Assert.Equal("SRV-01", shadow.Quote);
        Assert.Equal(kind.ToString(), shadow.SourceKind);
        if (kind == ReviewedFieldSourceKind.Cleared) Assert.NotNull(shadow.ReviewActionId);
        Assert.DoesNotContain(run.Checks!, check => check.FieldKey is "line_quantity" or "line_unit_price" or "line_total" && check.Status != "CouldNotCheck");
        Assert.Contains(run.Checks!, check => check.FieldKey.StartsWith("line_") && check.Status == "CouldNotCheck");
    }

    [Fact]
    public async Task Changed_scope_retry_requires_current_ETag_and_preserves_old_frozen_run()
    {
        var context = PairHandlerFixture();
        var token = context.Case.ConcurrencyToken;
        await context.Handler.HandleAsync(context.Case.Id, token, PairCommand(), CancellationToken.None);
        var old = context.Case.Runs.Single();
        var frozen = JsonSerializer.Serialize(new { old.Checks, old.DocumentSnapshots, old.OrderScope });
        await Assert.ThrowsAsync<ReconciliationPreconditionException>(() => context.Handler.HandleAsync(context.Case.Id, token,
            PairCommand("Partial"), CancellationToken.None));
        Assert.Single(context.Case.Runs);
        await context.Handler.HandleAsync(context.Case.Id, context.Case.ConcurrencyToken, PairCommand("Partial"), CancellationToken.None);
        Assert.Equal(2, context.Case.Runs.Count);
        Assert.Equal(frozen, JsonSerializer.Serialize(new { old.Checks, old.DocumentSnapshots, old.OrderScope }));
        Assert.Equal(InvoiceOrderScope.Partial, context.Case.Runs.Single(run => run.Id != old.Id).OrderScope);
    }

    private static EvaluateReconciliationCaseCommand PairCommand(string scope = "FullOrder") =>
        new(RuleSetVersion: "invoice-po-1.0", OrderScope: scope);

    private static (ReconciliationCase Case, EvaluateReconciliationCaseHandler Handler, Guid Actor) PairHandlerFixture(
        string raw = "1260.00", string? normalized = null, string? corrected = null, bool uncheckedInvoice = false,
        int warningCount = 0, FieldValueType correctedType = FieldValueType.Decimal, bool genericTrn = false,
        ReviewedFieldSourceKind? shadowKind = null)
    {
        var org = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var po = PairReviewedDocument(org, actor, "purchase_order", "1260.00", null, null, false, 0, FieldValueType.Decimal, false, null);
        var invoice = PairReviewedDocument(org, actor, "invoice", raw, normalized, corrected, uncheckedInvoice, warningCount, correctedType, genericTrn, shadowKind);
        var item = new ReconciliationCase(Guid.NewGuid(), org, actor, "Independent reviewed pair", Now);
        Link(item, ReconciliationDocumentRole.PurchaseOrder, po);
        Link(item, ReconciliationDocumentRole.Invoice, invoice);
        return (item, new EvaluateReconciliationCaseHandler(new FakeReconciliationRepository(item), new FakeDocumentRepository(po, invoice),
            new FakeExecutionContext(org, actor), new FakeClock(Now)), actor);
    }

    private static Document PairReviewedDocument(Guid org, Guid actor, string role, string total, string? normalized,
        string? corrected, bool uncheckedFields, int warningCount, FieldValueType correctedType, bool genericTrn, ReviewedFieldSourceKind? shadowKind)
    {
        var document = new Document(Guid.NewGuid(), org, actor, role + ".pdf", Guid.NewGuid().ToString("N") + ".pdf",
            "application/pdf", 100, new string(role == "invoice" ? 'e' : 'f', 64), Now);
        var extraction = document.QueueExtraction(Guid.NewGuid(), 1, "local", "local-1.6.0", "2.0", Guid.NewGuid().ToString("N"), Now);
        extraction.Start(Now);
        var values = new List<(string Key, FieldValueType Type, string Value, int Occurrence)>
        {
            (role == "invoice" ? "invoice_number" : "purchase_order_number", FieldValueType.Identifier, role == "invoice" ? "SINV-0001" : "SPO-0001", 0),
            (genericTrn ? "trn" : "supplier_trn", FieldValueType.Identifier, "000000000000701", 0),
            ("currency", FieldValueType.Currency, "AED", 0), ("total", FieldValueType.Decimal, total, 0),
            ("vat", FieldValueType.Decimal, "60.00", 0), ("document_total_basis", FieldValueType.String, "gross", 0),
            ("tax_basis", FieldValueType.String, "net", 0), ("tax_currency", FieldValueType.Currency, "AED", 0),
            ("line_item_code", FieldValueType.Identifier, "SRV-01", 1), ("line_description", FieldValueType.String, "Training", 1),
            ("line_quantity", FieldValueType.Decimal, "2", 1), ("line_unit_price", FieldValueType.Decimal, "600.00", 1),
            ("line_tax", FieldValueType.Decimal, "60.00", 1), ("line_total", FieldValueType.Decimal, "1260.00", 1),
            ("line_unit", FieldValueType.String, "EA", 1), ("line_price_basis", FieldValueType.String, "1 EA", 1),
            ("line_price_amount_basis", FieldValueType.String, "net", 1), ("line_total_basis", FieldValueType.String, "gross", 1)
        };
        if (role == "invoice") values.Add(("purchase_order_number", FieldValueType.Identifier, "SPO-0001", 0));
        if (shadowKind.HasValue)
            values.AddRange(values.Where(value => value.Occurrence == 1).Select(value => (value.Key, value.Type, value.Value, 2)).ToArray());
        var text = string.Join("\n", values.Select(value => value.Value));
        extraction.AddPage(Guid.NewGuid(), 1, text, 0);
        var offset = 0;
        foreach (var value in values)
        {
            var field = extraction.AddField(Guid.NewGuid(), value.Key, value.Occurrence, value.Type, value.Value, .95m, Now,
                value.Key == "total" ? normalized : value.Value);
            var end = offset + Encoding.UTF8.GetByteCount(value.Value);
            extraction.AddEvidence(field, Guid.NewGuid(), 1, value.Value, offset, end);
            offset = end + 1;
        }
        for (var index = 0; index < warningCount; index++)
            extraction.AddWarning(Guid.NewGuid(), "independent_warning_" + index, ExtractionWarningSeverity.Info, new string('ع', 300));
        extraction.RecordProviderMetrics(1, 1, null, null, null);
        extraction.Succeed(role, "en", Now, "fixture", "independent-reviewed-inputs");
        document.RecordExtractionSuccess(extraction, Now);
        var review = document.OpenReview(Guid.NewGuid(), actor, Now);
        if (!uncheckedFields)
        {
            var sequence = 0;
            foreach (var field in extraction.Fields)
            {
                var shadow = field.FieldKey == "line_item_code" && field.OccurrenceIndex == 2;
                if (shadow && shadowKind == ReviewedFieldSourceKind.Unchecked) continue;
                var correction = field.FieldKey == "total" && corrected is not null;
                review.AddAction(Guid.NewGuid(), actor, ++sequence, shadow && shadowKind == ReviewedFieldSourceKind.Cleared ? ReviewActionType.Cleared : correction ? ReviewActionType.Corrected : ReviewActionType.Confirmed,
                    field.FieldKey, field.OccurrenceIndex, field.Id, correction ? correctedType : null,
                    correction ? corrected : null, shadow && shadowKind == ReviewedFieldSourceKind.Cleared ? "Explicit independent clearing" : correction ? "Explicit independent correction" : null, Now);
            }
        }
        // Simulates a persisted legacy Completed record with action-null fields, without treating it as valid admission.
        review.Complete(uncheckedFields ? [] : extraction.Fields.Where(field => !(shadowKind == ReviewedFieldSourceKind.Unchecked && field.FieldKey == "line_item_code" && field.OccurrenceIndex == 2)).ToArray(), Now);
        return document;
    }
}
