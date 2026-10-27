using System.Text.Json;
using System.Text.Json.Serialization;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Domain.Tests;

/// <summary>Source-authored reviewed inputs; no extractor/OCR or browser acceptance.</summary>
public sealed class SupplierInvoicePairPolicyVerificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly ReconciliationTolerancePolicy Tolerances = new(.000001m, .000001m, .000001m, .000001m, .01m);
    private static readonly JsonSerializerOptions ReceiptOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void Complete_pair_matches_without_a_quotation_requirement()
    {
        var (_, run) = Pair();
        Evaluate(run);
        Assert.NotEmpty(run.Checks);
        Assert.All(run.Checks, check => Assert.Equal(ReconciliationCheckStatus.NoDifference, check.Status));
        Assert.Empty(run.Findings);
        Assert.DoesNotContain(run.Checks, check => check.Explanation.Contains("quotation", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("1260.01", ReconciliationCheckStatus.NoDifference, "0.01")]
    [InlineData("1260.010001", ReconciliationCheckStatus.DifferenceFound, "0.010001")]
    [InlineData("1259.989999", ReconciliationCheckStatus.DifferenceFound, "-0.010001")]
    public void Document_amount_boundary_and_signed_direction_are_exact(string invoice, ReconciliationCheckStatus expected, string signed)
    {
        var (_, run) = Pair(invoiceChanges: Change("total", 0, invoice));
        Evaluate(run);
        var check = Check(run, "total", 0);
        Assert.Equal(expected, check.Status);
        Assert.Equal(decimal.Parse(signed, System.Globalization.CultureInfo.InvariantCulture), check.SignedDifference);
        Assert.Equal(.01m, check.Tolerance);
        Assert.Equal("AED", check.Currency);
        Assert.NotNull(check.LeftFieldSnapshotId);
        Assert.NotNull(check.RightFieldSnapshotId);
        if (expected == ReconciliationCheckStatus.DifferenceFound)
        {
            var finding = Assert.Single(run.Findings.Where(finding => finding.FieldKey == "total"));
            Assert.Equal(Math.Abs(check.SignedDifference!.Value), finding.Difference);
            Assert.Equal(finding.Id, check.FindingId);
        }
    }

    [Theory]
    [InlineData("1,23")]
    [InlineData("١٬٠٥٠٫٥٠")]
    [InlineData("۱٬۰۵۰٫۵۰")]
    [InlineData("1٬050٫50")]
    [InlineData("1.234,56")]
    [InlineData("1.2345678")]
    [InlineData("-10.00")]
    [InlineData("+10")]
    [InlineData("1e3")]
    [InlineData("1000000000000000000")]
    public void Unsupported_reviewed_amount_is_unavailable_and_never_a_numeric_difference(string raw)
    {
        var (_, run) = Pair(invoiceChanges: Change("total", 0, raw));
        Evaluate(run);
        var check = Check(run, "total", 0);
        Assert.Equal(ReconciliationCheckStatus.CouldNotCheck, check.Status);
        Assert.Null(check.SignedDifference);
        Assert.NotNull(check.FindingId);
        Assert.Null(run.Findings.Single(finding => finding.Id == check.FindingId).Difference);
        Assert.False(string.IsNullOrWhiteSpace(check.ReasonCode));
        Assert.Equal(raw, run.DocumentSnapshots.Single(snapshot => snapshot.Role == ReconciliationDocumentRole.Invoice)
            .Fields.Single(field => field.FieldKey == "total").ValueText);
    }

    [Theory]
    [InlineData("supplier_trn", 0)]
    [InlineData("currency", 0)]
    [InlineData("total", 0)]
    [InlineData("vat", 0)]
    [InlineData("document_total_basis", 0)]
    [InlineData("tax_basis", 0)]
    [InlineData("tax_currency", 0)]
    [InlineData("line_unit", 1)]
    [InlineData("line_price_basis", 1)]
    [InlineData("line_price_amount_basis", 1)]
    [InlineData("line_total_basis", 1)]
    [InlineData("line_quantity", 1)]
    public void Missing_required_values_or_bases_are_explicitly_unavailable(string key, int occurrence)
    {
        var (_, run) = Pair(omitInvoice: [(key, occurrence)]);
        Evaluate(run);
        Assert.Contains(run.Checks, check => check.Status == ReconciliationCheckStatus.CouldNotCheck);
        Assert.All(run.Checks.Where(check => check.Status == ReconciliationCheckStatus.CouldNotCheck),
            check => Assert.False(string.IsNullOrWhiteSpace(check.ReasonCode)));
    }

    [Fact]
    public void Empty_line_sets_cannot_be_complete_agreement()
    {
        var (_, run) = Pair(includeRows: false);
        Evaluate(run);
        Assert.Contains(run.Checks, check => check.Status == ReconciliationCheckStatus.CouldNotCheck);
    }

    [Theory]
    [InlineData(InvoiceOrderScope.Unknown)]
    [InlineData(InvoiceOrderScope.Partial)]
    public void Unknown_or_partial_scope_withholds_full_order_quantity_and_document_amount(InvoiceOrderScope scope)
    {
        var (_, run) = Pair(scope: scope);
        Evaluate(run);
        Assert.Equal(ReconciliationCheckStatus.CouldNotCheck, Check(run, "total", 0).Status);
        Assert.All(run.Checks.Where(check => check.FieldKey == "line_quantity"),
            check => Assert.Equal(ReconciliationCheckStatus.CouldNotCheck, check.Status));
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("not-a-currency")]
    public void Currency_mismatch_or_invalid_currency_suppresses_monetary_subtraction(string currency)
    {
        var (_, run) = Pair(invoiceChanges: Change("currency", 0, currency));
        Evaluate(run);
        Assert.All(run.Checks.Where(check => check.FieldKey is "total" or "vat" or "line_unit_price" or "line_tax" or "line_total"), check =>
        {
            Assert.Equal(ReconciliationCheckStatus.CouldNotCheck, check.Status);
            Assert.Null(check.SignedDifference);
        });
    }

    [Theory]
    [InlineData("line_unit", "HOUR", "line_quantity")]
    [InlineData("line_unit", "BOX", "line_quantity")]
    [InlineData("line_price_basis", "2 EA", "line_unit_price")]
    [InlineData("line_price_basis", "1 HOUR", "line_unit_price")]
    [InlineData("line_price_basis", "per-pack", "line_unit_price")]
    [InlineData("line_price_amount_basis", "gross", "line_unit_price")]
    [InlineData("line_price_amount_basis", "unknown", "line_unit_price")]
    [InlineData("line_total_basis", "net", "line_total")]
    [InlineData("line_total_basis", "unknown", "line_total")]
    public void Incompatible_units_price_or_amount_bases_are_not_converted(string key, string value, string affected)
    {
        var (_, run) = Pair(invoiceChanges: Change(key, 1, value));
        Evaluate(run);
        var check = Check(run, affected, 1);
        Assert.Equal(ReconciliationCheckStatus.CouldNotCheck, check.Status);
        Assert.Null(check.SignedDifference);
    }

    [Theory]
    [InlineData("document_total_basis", "net", "total")]
    [InlineData("tax_basis", "gross", "vat")]
    public void Header_basis_mismatch_is_explicitly_unavailable(string key, string value, string affected)
    {
        var (_, run) = Pair(invoiceChanges: Change(key, 0, value));
        Evaluate(run);
        Assert.Equal(ReconciliationCheckStatus.CouldNotCheck, Check(run, affected, 0).Status);
    }

    [Fact]
    public void Reordered_unique_rows_match_by_identity_with_original_occurrences()
    {
        var (_, run) = Pair(reverseInvoiceRows: true);
        Evaluate(run);
        Assert.All(run.Checks, check => Assert.Equal(ReconciliationCheckStatus.NoDifference, check.Status));
        Assert.Empty(run.Findings);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void Duplicate_identity_is_permanently_ambiguous_after_three_or_more_occurrences(int count)
    {
        var (_, run) = Pair(extraInvoiceDuplicates: count - 1);
        Evaluate(run);
        Assert.Contains(run.Checks, check => check.Status == ReconciliationCheckStatus.CouldNotCheck);
        Assert.Equal(count, run.Checks.Count(check => check.ReasonCode == "duplicate_line_identity"));
        Assert.Equal(ReconciliationCheckStatus.NoDifference, Check(run, "source_coverage", 0).Status);
        Assert.Equal(count + 1, run.DocumentSnapshots.Single(source => source.Role == ReconciliationDocumentRole.Invoice).ExtractedRowCount);
        Assert.DoesNotContain(run.Checks, check => check.LineIdentity?.Contains("SRV-01", StringComparison.OrdinalIgnoreCase) == true &&
            check.FieldKey is "line_quantity" or "line_unit_price" or "line_total" && check.Status != ReconciliationCheckStatus.CouldNotCheck);
    }

    [Theory]
    [InlineData(ReviewedFieldSourceKind.Unchecked)]
    [InlineData(ReviewedFieldSourceKind.Added)]
    public void Unchecked_or_evidenceless_added_value_is_not_trusted(ReviewedFieldSourceKind kind)
    {
        var (_, run) = Pair(invoiceTotalSource: kind, omitTotalEvidence: true);
        Evaluate(run);
        Assert.Equal(ReconciliationCheckStatus.CouldNotCheck, Check(run, "total", 0).Status);
    }

    [Fact]
    public void Page_without_quote_is_not_usable_field_evidence()
    {
        var (_, run) = Pair(omitTotalQuote: true);
        Evaluate(run);
        Assert.Equal(ReconciliationCheckStatus.CouldNotCheck, Check(run, "total", 0).Status);
    }

    [Theory]
    [InlineData("Partial", "local-1.6.0", true)]
    [InlineData("Succeeded", "local-1.3.0", true)]
    [InlineData("Succeeded", "local-1.6.0", false)]
    public void Partial_legacy_or_unproven_row_coverage_is_frozen_as_unavailable(string outcome, string adapter, bool completeRows)
    {
        var (_, run) = Pair(sourceOutcome: outcome, adapter: adapter, completeRows: completeRows);
        Evaluate(run);
        Assert.Contains(run.Checks, check => check.Status == ReconciliationCheckStatus.CouldNotCheck);
    }

    [Fact]
    public void Large_valid_amount_difference_preserves_eighteen_integer_and_six_fractional_digits()
    {
        var (_, run) = Pair(poChanges: Change("total", 0, "0"), invoiceChanges: Change("total", 0, "999999999999999999.123456"));
        Evaluate(run);
        var check = Check(run, "total", 0);
        Assert.Equal(ReconciliationCheckStatus.DifferenceFound, check.Status);
        Assert.Equal(999999999999999999.123456m, check.SignedDifference);
        Assert.Equal(999999999999999999.123456m, Assert.Single(run.Findings.Where(finding => finding.FieldKey == "total")).Difference);
    }

    [Fact]
    public void Check_refuses_sub_micro_difference_before_database_rounding()
    {
        var (_, run) = Pair();
        Assert.Throws<ArgumentOutOfRangeException>(() => run.AddCheck(Guid.NewGuid(), "PRECISION", "total", 0,
            null, ReconciliationCheckStatus.NoDifference, "fixture", "Independent storage-boundary fixture",
            null, null, "0", "0.0000001", .0000001m, .01m, null, "AED", "gross", null, Now));
        Assert.Empty(run.Checks);
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("unknown")]
    public void Incompatible_tax_currency_withholds_header_and_line_tax(string currency)
    {
        var (_, run) = Pair(invoiceChanges: Change("tax_currency", 0, currency));
        Evaluate(run);
        Assert.All(run.Checks.Where(check => check.FieldKey is "vat" or "line_tax"),
            check => Assert.Equal(ReconciliationCheckStatus.CouldNotCheck, check.Status));
    }

    [Fact]
    public void Missing_invoice_order_reference_is_unavailable_despite_present_invoice_number()
    {
        var (_, run) = Pair(omitInvoice: [("purchase_order_number", 0)]);
        Evaluate(run);
        Assert.Equal(ReconciliationCheckStatus.CouldNotCheck, Check(run, "purchase_order_reference", 0).Status);
        Assert.Equal(ReconciliationCheckStatus.NoDifference, Check(run, "invoice_number", 0).Status);
    }

    [Theory]
    [InlineData("currency", "currency", 0)]
    [InlineData("line_price_amount_basis", "line_unit_price", 1)]
    [InlineData("document_total_basis", "total", 0)]
    public void Correct_text_with_wrong_reviewed_type_cannot_satisfy_currency_or_basis_prerequisite(string fieldKey, string checkKey, int occurrence)
    {
        var (_, run) = Pair(invoiceWrongTypeKey: fieldKey);
        Evaluate(run);
        Assert.Equal(ReconciliationCheckStatus.CouldNotCheck, Check(run, checkKey, occurrence).Status);
    }

    [Fact]
    public void Completed_checks_and_sources_stay_immutable_after_case_repin_and_resolution()
    {
        var (item, run) = Pair(invoiceChanges: Change("total", 0, "1261.00"));
        Evaluate(run);
        item.CompleteEvaluation(run, Now.AddSeconds(1));
        var frozen = JsonSerializer.Serialize(new { run.DocumentSnapshots, run.Checks, run.RuleSetVersion }, ReceiptOptions);
        item.ReplaceReviewedDocument(Guid.NewGuid(), ReconciliationDocumentRole.Invoice,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('f', 64), Now.AddMinutes(1));
        var finding = run.Findings.First();
        item.ResolveFinding(run.Id, finding.Id, Guid.NewGuid(), item.CreatedByActorId,
            FindingResolutionAction.Accepted, "Synthetic reviewed difference", Now.AddMinutes(2));
        Assert.Equal(frozen, JsonSerializer.Serialize(new { run.DocumentSnapshots, run.Checks, run.RuleSetVersion }, ReceiptOptions));
        Assert.Throws<InvalidOperationException>(() => run.SetPairContext(Guid.NewGuid(), InvoiceOrderScope.Partial));
    }

    [Fact]
    public void Legacy_policy_still_requires_quotation_and_has_no_new_pair_checks()
    {
        var (item, _) = Pair();
        var legacy = item.Runs.Single();
        // A separate independent case retains the old rule version and its explicit policy.
        var old = new ReconciliationCase(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Legacy pair", Now);
        foreach (var role in new[] { ReconciliationDocumentRole.PurchaseOrder, ReconciliationDocumentRole.Invoice })
            old.LinkReviewedDocument(Guid.NewGuid(), role, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string(role == ReconciliationDocumentRole.Invoice ? 'a' : 'b', 64), Now);
        var run = old.BeginEvaluation(Guid.NewGuid(), old.DocumentLinks.ToDictionary(link => link.Role, _ => Guid.NewGuid()), ReconciliationRuleEngine.RuleSetVersion, Tolerances, Now);
        AddFields(run, ReconciliationDocumentRole.PurchaseOrder, Fields("SPO-0001"));
        AddFields(run, ReconciliationDocumentRole.Invoice, Fields("SINV-0001"));
        new ReconciliationRuleEngine().Evaluate(run, Guid.NewGuid, Now);
        Assert.Equal("1.0", run.RuleSetVersion);
        Assert.Contains(run.Findings, finding => finding.RuleCode == "DOC-001" && finding.RightRole == ReconciliationDocumentRole.Quotation);
        Assert.Empty(run.Checks);
        Assert.Equal("invoice-po-1.0", legacy.RuleSetVersion);
    }

    [Fact]
    public void Manual_policy_checkpoint_writes_actual_engine_values_sources_and_unavailable_example()
    {
        var matched = Pair().Run;
        var difference = Pair(invoiceChanges: Change("total", 0, "1261.00")).Run;
        var unavailable = Pair(invoiceChanges: Change("currency", 0, "USD")).Run;
        foreach (var run in new[] { matched, difference, unavailable }) Evaluate(run);
        Assert.All(matched.Checks, check => Assert.Equal(ReconciliationCheckStatus.NoDifference, check.Status));
        Assert.Equal(1m, Check(difference, "total", 0).SignedDifference);
        Assert.Equal(ReconciliationCheckStatus.CouldNotCheck, Check(unavailable, "total", 0).Status);
        var root = RepositoryRoot();
        var output = Path.Combine(root, "outputs", "supplier-invoice-S2-20261006", "manual-" + DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffffff") + "-" + Guid.NewGuid().ToString("N")[..6] + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        using (var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write))
            JsonSerializer.Serialize(stream, new { Boundary = "Actual domain policy with independent synthetic reviewed inputs; no OCR/UI or lifecycle admission claim", Matched = matched, Difference = difference, UnavailableCurrency = unavailable }, ReceiptOptions);
        Console.WriteLine("invoice-po-1.0: matched pair 1260 AED; invoice 1261 vs PO 1260 => signed difference +1 AED; USD/AED amount => CouldNotCheck. Source field IDs, kinds, quotes and checks saved: " + output);
    }

    private static void Evaluate(ReconciliationRun run)
    {
        new InvoicePurchaseOrderRuleEngine().Evaluate(run, Guid.NewGuid, Now);
        foreach (var check in run.Checks.Where(check => check.Status != ReconciliationCheckStatus.NoDifference))
        {
            Assert.NotNull(check.FindingId);
            Assert.Contains(run.Findings, finding => finding.Id == check.FindingId);
        }
    }

    [Theory]
    [InlineData("1", "-1")]
    [InlineData("2", "0")]
    [InlineData("3", "1")]
    public void Factual_policy_compares_quantities_without_declaring_order_scope(string quantity, string delta)
    {
        var (_, run) = Pair(invoiceChanges: Change("line_quantity", 1, quantity),
            scope: InvoiceOrderScope.Unknown, version: InvoicePurchaseOrderRuleEngine.FactualRuleSetVersion);
        Evaluate(run);
        Assert.Equal(decimal.Parse(delta, System.Globalization.CultureInfo.InvariantCulture), Check(run, "line_quantity", 1).SignedDifference);
        Assert.Equal(ReconciliationCheckStatus.NoDifference, Check(run, "line_unit_price", 1).Status);
        Assert.Equal(quantity == "2" ? ReconciliationCheckStatus.NoDifference : ReconciliationCheckStatus.CouldNotCheck, Check(run, "total", 0).Status);
        Assert.All(run.Findings, finding => Assert.Equal(InvoicePurchaseOrderRuleEngine.FactualRuleSetVersion, finding.RuleVersion));
        Assert.Equal(InvoiceOrderScope.Unknown, run.OrderScope);
        Assert.Contains(run.Checks, check => check.ReasonCode == "document_pair_only");
    }

    [Theory]
    [InlineData("line_unit")]
    [InlineData("line_quantity")]
    [InlineData("line_item_code")]
    public void Factual_policy_does_not_invent_missing_quantity_evidence(string key)
    {
        var (_, run) = Pair(omitInvoice: [(key, 1)], scope: InvoiceOrderScope.Unknown,
            version: InvoicePurchaseOrderRuleEngine.FactualRuleSetVersion);
        Evaluate(run);
        Assert.Contains(run.Checks, check => check.Status == ReconciliationCheckStatus.CouldNotCheck);
        Assert.DoesNotContain(run.Checks, check => check.FieldKey == "total" && check.Status == ReconciliationCheckStatus.NoDifference);
    }

    [Fact]
    public void Factual_policy_withholds_duplicate_lines_and_incompatible_price_bases()
    {
        var (_, duplicate) = Pair(extraInvoiceDuplicates: 1, scope: InvoiceOrderScope.Unknown,
            version: InvoicePurchaseOrderRuleEngine.FactualRuleSetVersion);
        Evaluate(duplicate);
        Assert.DoesNotContain(duplicate.Checks, check => check.FieldKey == "line_quantity" && check.OccurrenceIndex == 1 && check.SignedDifference.HasValue);
        Assert.Contains(duplicate.Checks, check => check.ReasonCode == "duplicate_line_identity");
        Assert.Equal(ReconciliationCheckStatus.CouldNotCheck, Check(duplicate, "total", 0).Status);
        var (_, basis) = Pair(invoiceChanges: Change("line_price_basis", 1, "BOX"),
            scope: InvoiceOrderScope.Unknown, version: InvoicePurchaseOrderRuleEngine.FactualRuleSetVersion);
        Evaluate(basis);
        Assert.Equal(ReconciliationCheckStatus.CouldNotCheck, Check(basis, "line_unit_price", 1).Status);
    }

    private static ReconciliationCheck Check(ReconciliationRun run, string key, int occurrence) =>
        Assert.Single(run.Checks.Where(check => check.FieldKey == key && (check.OccurrenceIndex ?? 0) == occurrence));

    private static Dictionary<(string Key, int Occurrence), string> Change(string key, int occurrence, string value) => new() { [(key, occurrence)] = value };

    private static (ReconciliationCase Case, ReconciliationRun Run) Pair(
        Dictionary<(string Key, int Occurrence), string>? poChanges = null,
        Dictionary<(string Key, int Occurrence), string>? invoiceChanges = null,
        (string Key, int Occurrence)[]? omitInvoice = null, bool includeRows = true,
        InvoiceOrderScope scope = InvoiceOrderScope.FullOrder, bool reverseInvoiceRows = false,
        int extraInvoiceDuplicates = 0, ReviewedFieldSourceKind invoiceTotalSource = ReviewedFieldSourceKind.Confirmed,
        bool omitTotalEvidence = false, bool omitTotalQuote = false,
        string sourceOutcome = "Succeeded", string adapter = "local-1.6.0", bool completeRows = true,
        string? invoiceWrongTypeKey = null, string version = InvoicePurchaseOrderRuleEngine.RuleSetVersion)
    {
        var item = new ReconciliationCase(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Fictional invoice and complete PO", Now);
        foreach (var role in new[] { ReconciliationDocumentRole.PurchaseOrder, ReconciliationDocumentRole.Invoice })
            item.LinkReviewedDocument(Guid.NewGuid(), role, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string(role == ReconciliationDocumentRole.Invoice ? 'c' : 'd', 64), Now);
        var run = item.BeginEvaluation(Guid.NewGuid(), item.DocumentLinks.ToDictionary(link => link.Role, _ => Guid.NewGuid()), version, Tolerances, Now);
        run.SetPairContext(item.CreatedByActorId, scope);
        foreach (var role in new[] { ReconciliationDocumentRole.PurchaseOrder, ReconciliationDocumentRole.Invoice })
        {
            var values = Fields(role == ReconciliationDocumentRole.Invoice ? "SINV-0001" : "SPO-0001", includeRows);
            var changes = role == ReconciliationDocumentRole.Invoice ? invoiceChanges : poChanges;
            if (changes is not null)
                foreach (var entry in changes) values[entry.Key] = entry.Value;
            if (role == ReconciliationDocumentRole.Invoice)
            {
                foreach (var identity in omitInvoice ?? []) values.Remove(identity);
                if (reverseInvoiceRows)
                    values = values.ToDictionary(entry => (entry.Key.Key, entry.Key.Occurrence == 1 ? 2 : entry.Key.Occurrence == 2 ? 1 : entry.Key.Occurrence), entry => entry.Value);
                for (var duplicate = 0; duplicate < extraInvoiceDuplicates; duplicate++)
                    foreach (var entry in Fields("unused").Where(entry => entry.Key.Occurrence == 1))
                        values[(entry.Key.Key, duplicate + 3)] = entry.Value;
            }
            AddFields(run, role, values, role == ReconciliationDocumentRole.Invoice ? invoiceTotalSource : ReviewedFieldSourceKind.Confirmed,
                role == ReconciliationDocumentRole.Invoice && omitTotalEvidence, role == ReconciliationDocumentRole.Invoice && omitTotalQuote,
                role == ReconciliationDocumentRole.Invoice ? invoiceWrongTypeKey : null);
            var snapshot = run.DocumentSnapshots.Single(snapshot => snapshot.Role == role);
            var authoredRowCount = values.Keys.Where(identity => identity.Key.StartsWith("line_", StringComparison.Ordinal) && identity.Occurrence > 0)
                .Select(identity => identity.Occurrence).Distinct().Count();
            run.CaptureSourceState(snapshot.Id, sourceOutcome, "local", adapter, "pdfplumber", "synthetic reviewed fixture; no PDF proof", "[]", authoredRowCount, completeRows && includeRows);
        }
        return (item, run);
    }

    private static Dictionary<(string Key, int Occurrence), string> Fields(string number, bool rows = true)
    {
        var values = new Dictionary<(string Key, int Occurrence), string>
        {
            [("document_number", 0)] = number, [("supplier_trn", 0)] = "000000000000701",
            [("currency", 0)] = "AED", [("total", 0)] = "1260.00", [("vat", 0)] = "60.00",
            [("document_total_basis", 0)] = "gross", [("tax_basis", 0)] = "net", [("tax_currency", 0)] = "AED"
        };
        if (number.StartsWith("SINV", StringComparison.Ordinal)) values[("purchase_order_number", 0)] = "SPO-0001";
        if (!rows) return values;
        foreach (var row in new[] { (1, "SRV-01", "Document review training", "2", "800.00", "40.00", "840.00"), (2, "SRV-02", "Workflow configuration session", "1", "400.00", "20.00", "420.00") })
        {
            values[("line_item_code", row.Item1)] = row.Item2;
            values[("line_description", row.Item1)] = row.Item3;
            values[("line_quantity", row.Item1)] = row.Item4;
            values[("line_unit_price", row.Item1)] = "400.00";
            values[("line_tax", row.Item1)] = row.Item6;
            values[("line_total", row.Item1)] = row.Item7;
            values[("line_unit", row.Item1)] = "EA";
            values[("line_price_basis", row.Item1)] = "1 EA";
            values[("line_price_amount_basis", row.Item1)] = "net";
            values[("line_total_basis", row.Item1)] = "gross";
        }
        return values;
    }

    private static void AddFields(ReconciliationRun run, ReconciliationDocumentRole role,
        Dictionary<(string Key, int Occurrence), string> values,
        ReviewedFieldSourceKind totalSource = ReviewedFieldSourceKind.Confirmed, bool omitEvidence = false, bool omitQuote = false, string? wrongTypeKey = null)
    {
        var snapshot = run.DocumentSnapshots.Single(snapshot => snapshot.Role == role);
        foreach (var entry in values.OrderBy(entry => entry.Key.Occurrence).ThenBy(entry => entry.Key.Key, StringComparer.Ordinal))
        {
            var isTotal = entry.Key is ("total", 0);
            var numeric = entry.Key.Key is "total" or "vat" or "line_quantity" or "line_unit_price" or "line_tax" or "line_total";
            var type = numeric ? FieldValueType.Decimal : entry.Key.Key is "currency" or "tax_currency" ? FieldValueType.Currency :
                entry.Key.Key == "supplier_trn" ? FieldValueType.Identifier : FieldValueType.String;
            if (entry.Key.Key == wrongTypeKey) type = FieldValueType.Decimal;
            run.AddFieldSnapshot(Guid.NewGuid(), snapshot.Id, entry.Key.Key, entry.Key.Occurrence,
                type, entry.Value,
                isTotal && totalSource == ReviewedFieldSourceKind.Added ? null : Guid.NewGuid(), Guid.NewGuid(),
                isTotal && omitEvidence ? null : 1, isTotal && (omitEvidence || omitQuote) ? null : entry.Value, Now,
                isTotal ? totalSource : ReviewedFieldSourceKind.Confirmed);
        }
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "apps", "backend"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Repository root not found for owned test receipt output");
    }
}
