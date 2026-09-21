using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Domain.Tests;

public sealed class ReconciliationRuleEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Exact_header_and_line_matches_produce_no_findings()
    {
        var (_, run) = CreateRun();
        AddCompleteDocument(run, ReconciliationDocumentRole.Quotation, 1);
        AddCompleteDocument(run, ReconciliationDocumentRole.PurchaseOrder, 3);
        AddCompleteDocument(run, ReconciliationDocumentRole.Invoice, 7);

        new ReconciliationRuleEngine().Evaluate(run, Guid.NewGuid, Now);

        Assert.Empty(run.Findings);
    }

    [Fact]
    public void Amount_difference_above_tolerance_records_both_sources_and_explanation()
    {
        var (_, run) = CreateRun(documentTotalTolerance: 0.01m);
        AddCompleteDocument(run, ReconciliationDocumentRole.Quotation, 1);
        AddCompleteDocument(run, ReconciliationDocumentRole.PurchaseOrder, 2);
        AddCompleteDocument(run, ReconciliationDocumentRole.Invoice, 3,
            total: "11500.00");

        new ReconciliationRuleEngine().Evaluate(run, Guid.NewGuid, Now);

        var finding = Assert.Single(run.Findings.Where(item => item.RuleCode == "AMT-002"));
        Assert.Equal("10000.00", finding.LeftValue);
        Assert.Equal("11500.00", finding.RightValue);
        Assert.Equal(1500m, finding.Difference);
        Assert.Equal(0.01m, finding.Tolerance);
        Assert.NotNull(finding.LeftFieldSnapshotId);
        Assert.NotNull(finding.RightFieldSnapshotId);
        Assert.Contains("allowed absolute tolerance", finding.Explanation);
    }

    [Theory]
    [InlineData("10000.01", false)]
    [InlineData("10000.02", true)]
    public void Difference_equal_to_tolerance_passes_and_larger_difference_fails(
        string invoiceTotal, bool expectedFinding)
    {
        var (_, run) = CreateRun(documentTotalTolerance: 0.01m);
        AddCompleteDocument(run, ReconciliationDocumentRole.Quotation, 1);
        AddCompleteDocument(run, ReconciliationDocumentRole.PurchaseOrder, 2);
        AddCompleteDocument(run, ReconciliationDocumentRole.Invoice, 3, total: invoiceTotal);

        new ReconciliationRuleEngine().Evaluate(run, Guid.NewGuid, Now);

        Assert.Equal(expectedFinding,
            run.Findings.Any(item => item.RuleCode == "AMT-002"));
    }

    [Fact]
    public void Missing_roles_and_duplicate_source_are_structured_findings()
    {
        var item = CreateCase();
        Link(item, ReconciliationDocumentRole.Quotation, 'a');
        Link(item, ReconciliationDocumentRole.Invoice, 'a');
        var run = Begin(item);
        AddCompleteDocument(run, ReconciliationDocumentRole.Quotation, 1);
        AddCompleteDocument(run, ReconciliationDocumentRole.Invoice, 2);

        new ReconciliationRuleEngine().Evaluate(run, Guid.NewGuid, Now);

        Assert.Contains(run.Findings, finding => finding.RuleCode == "DOC-001" &&
            finding.RightRole == ReconciliationDocumentRole.PurchaseOrder);
        Assert.Contains(run.Findings, finding => finding.RuleCode == "DOC-002" &&
            finding.LeftRole == ReconciliationDocumentRole.Quotation &&
            finding.RightRole == ReconciliationDocumentRole.Invoice);
    }

    [Fact]
    public void Line_matching_uses_identity_not_row_position_and_reports_quantity_and_missing_line()
    {
        var (_, run) = CreateRun();
        AddCompleteDocument(run, ReconciliationDocumentRole.Quotation, 1);
        AddCompleteDocument(run, ReconciliationDocumentRole.PurchaseOrder, 4,
            quantity: "2");
        AddCompleteDocument(run, ReconciliationDocumentRole.Invoice, 8,
            quantity: "2");
        AddLine(run, ReconciliationDocumentRole.Invoice, 9, "EXTRA", "Extra service",
            "1", "25.00", "1.25", "26.25");

        new ReconciliationRuleEngine().Evaluate(run, Guid.NewGuid, Now);

        var quantities = run.Findings.Where(item => item.RuleCode == "LINE-002").ToArray();
        Assert.Single(quantities);
        Assert.All(quantities, finding => Assert.Equal(1m, finding.Difference));
        var missing = Assert.Single(run.Findings.Where(item => item.RuleCode == "LINE-001"));
        Assert.Equal("EXTRA", missing.RightValue);
        Assert.Equal(ReconciliationDocumentRole.Invoice, missing.RightRole);
    }

    [Fact]
    public void Missing_page_evidence_yields_data_quality_finding_instead_of_value_mismatch()
    {
        var (_, run) = CreateRun();
        AddCompleteDocument(run, ReconciliationDocumentRole.Quotation, 1);
        AddCompleteDocument(run, ReconciliationDocumentRole.PurchaseOrder, 2);
        AddCompleteDocument(run, ReconciliationDocumentRole.Invoice, 3,
            supplierEvidencePage: null, supplierTrn: "999999999999999");

        new ReconciliationRuleEngine().Evaluate(run, Guid.NewGuid, Now);

        Assert.Contains(run.Findings, finding => finding.RuleCode == "DATA-001" &&
            finding.FieldKey == "supplier_trn" &&
            finding.RightRole == ReconciliationDocumentRole.Invoice);
        Assert.DoesNotContain(run.Findings, finding => finding.RuleCode == "SUP-001" &&
            finding.RightRole == ReconciliationDocumentRole.Invoice);
    }

    private static (ReconciliationCase Case, ReconciliationRun Run) CreateRun(
        decimal documentTotalTolerance = 0.01m)
    {
        var item = CreateCase();
        Link(item, ReconciliationDocumentRole.Quotation, 'a');
        Link(item, ReconciliationDocumentRole.PurchaseOrder, 'b');
        Link(item, ReconciliationDocumentRole.Invoice, 'c');
        var policy = new ReconciliationTolerancePolicy(0, 0.01m, 0.01m, 0.01m,
            documentTotalTolerance);
        return (item, Begin(item, policy));
    }

    private static ReconciliationCase CreateCase() => new(Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), "Rule case", Now);

    private static void Link(ReconciliationCase item, ReconciliationDocumentRole role, char hash) =>
        item.LinkReviewedDocument(Guid.NewGuid(), role, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), new string(hash, 64), Now);

    private static ReconciliationRun Begin(ReconciliationCase item,
        ReconciliationTolerancePolicy? policy = null) => item.BeginEvaluation(Guid.NewGuid(),
        item.DocumentLinks.ToDictionary(link => link.Role, _ => Guid.NewGuid()), "1.0",
        policy ?? ReconciliationTolerancePolicy.Default, Now);

    private static void AddCompleteDocument(ReconciliationRun run,
        ReconciliationDocumentRole role, int lineOccurrence, string total = "10000.00",
        string quantity = "1", int? supplierEvidencePage = 1,
        string supplierTrn = "100000000000003")
    {
        AddField(run, role, "supplier_trn", 0, FieldValueType.Identifier, supplierTrn,
            supplierEvidencePage);
        AddField(run, role, "currency", 0, FieldValueType.Currency, "AED");
        AddField(run, role, "total", 0, FieldValueType.Decimal, total);
        AddField(run, role, "vat", 0, FieldValueType.Decimal, "500.00");
        AddLine(run, role, lineOccurrence, "CONSULT", "Consulting service", quantity,
            "9500.00", "500.00", "10000.00");
    }

    private static void AddLine(ReconciliationRun run, ReconciliationDocumentRole role,
        int occurrence, string code, string description, string quantity,
        string unitPrice, string tax, string total)
    {
        AddField(run, role, "line_item_code", occurrence, FieldValueType.Identifier, code);
        AddField(run, role, "line_description", occurrence, FieldValueType.String, description);
        AddField(run, role, "line_quantity", occurrence, FieldValueType.Decimal, quantity);
        AddField(run, role, "line_unit_price", occurrence, FieldValueType.Decimal, unitPrice);
        AddField(run, role, "line_tax", occurrence, FieldValueType.Decimal, tax);
        AddField(run, role, "line_total", occurrence, FieldValueType.Decimal, total);
    }

    private static ReconciliationFieldSnapshot AddField(ReconciliationRun run,
        ReconciliationDocumentRole role, string key, int occurrence,
        FieldValueType type, string value, int? page = 1)
    {
        var document = run.DocumentSnapshots.Single(snapshot => snapshot.Role == role);
        return run.AddFieldSnapshot(Guid.NewGuid(), document.Id, key, occurrence, type,
            value, Guid.NewGuid(), null, page, page.HasValue ? value : null, Now);
    }
}
