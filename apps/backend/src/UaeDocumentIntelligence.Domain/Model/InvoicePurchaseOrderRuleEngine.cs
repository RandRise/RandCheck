using System.Globalization;
using System.Text.RegularExpressions;

namespace UaeDocumentIntelligence.Domain.Model;

/// <summary>An opt-in comparison of checked invoice and PO evidence, without inferred units or bases.</summary>
public sealed partial class InvoicePurchaseOrderRuleEngine
{
    public const string RuleSetVersion = "invoice-po-1.0";
    public const string FactualRuleSetVersion = "invoice-po-2.0";

    public static bool IsPairPolicy(string? version) => version is RuleSetVersion or FactualRuleSetVersion;
    private static readonly string[] LineKeys = ["line_item_code", "line_description", "line_quantity",
        "line_unit", "line_unit_price", "line_price_basis", "line_price_amount_basis", "line_tax", "line_total", "line_total_basis"];

    public void Evaluate(ReconciliationRun run, Func<Guid> nextId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(nextId);
        if (run.Status != ReconciliationRunStatus.Running || !IsPairPolicy(run.RuleSetVersion) ||
            run.OrderScope is null || run.EvaluationActorId is null)
            throw new InvalidOperationException("The pair policy requires a running run with frozen scope and actor.");
        var policy = new ReconciliationTolerancePolicy(run.QuantityTolerance, run.UnitPriceTolerance,
            run.TaxTolerance, run.LineTotalTolerance, run.DocumentTotalTolerance);
        ValidateTolerances(policy);
        var po = run.DocumentSnapshots.SingleOrDefault(source => source.Role == ReconciliationDocumentRole.PurchaseOrder);
        var invoice = run.DocumentSnapshots.SingleOrDefault(source => source.Role == ReconciliationDocumentRole.Invoice);
        var context = new Evaluation(run, nextId, now);
        if (po is null || invoice is null)
        {
            context.Record("PAIR-ROLES", "documents", null, null, null, null,
                ReconciliationCheckStatus.CouldNotCheck, "required_pair_missing",
                "A completed reviewed purchase order and invoice are required.");
            return;
        }
        if (po.DocumentId == invoice.DocumentId || po.Sha256 == invoice.Sha256)
        {
            context.Record("PAIR-SOURCE", "documents", null, null, null, null,
                ReconciliationCheckStatus.CouldNotCheck, "duplicate_source",
                "Invoice and purchase order use the same source. Independent pair checks were withheld.");
            return;
        }

        var factual = run.RuleSetVersion == FactualRuleSetVersion;
        var completeRows = RowCoverage(po) && RowCoverage(invoice);
        context.Record("PAIR-COVERAGE", "source_coverage", null, null, null, null,
            completeRows ? ReconciliationCheckStatus.NoDifference : ReconciliationCheckStatus.CouldNotCheck,
            completeRows ? "source_rows_verified" : "source_rows_unverified",
            completeRows ? "Both frozen extractions have verified, non-empty row coverage. Individual checks still require checked evidence."
                : "Extraction outcome, warnings, missing rows or legacy coverage prevent a claim of complete row coverage.");
        var leftLines = factual ? Lines(context, po) : null;
        var rightLines = factual ? Lines(context, invoice) : null;
        // In v2 equal, fully evidenced row quantities permit comparing these two amounts.
        // They never establish that this is a final invoice or a remaining balance.
        var fullOrder = factual
            ? completeRows && leftLines!.Count > 0 && leftLines.Count == rightLines!.Count &&
                leftLines.Count == po.ExtractedRowCount && rightLines.Count == invoice.ExtractedRowCount &&
                leftLines.All(entry => rightLines.TryGetValue(entry.Key, out var right) &&
                    EqualCheckedQuantity(po, invoice, entry.Value.Occurrence, right.Occurrence))
            : run.OrderScope == InvoiceOrderScope.FullOrder && completeRows;
        context.Record("PAIR-SCOPE", "order_scope", null, null, null, null,
            factual || fullOrder ? ReconciliationCheckStatus.NoDifference : ReconciliationCheckStatus.CouldNotCheck,
            factual ? "document_pair_only" : fullOrder ? "full_order_declared" : "full_order_scope_unavailable",
            factual ? "Only these two documents are compared. No final-invoice status or remaining balance is inferred."
                : fullOrder ? "The evaluator declared FullOrder; this declaration is frozen and is not inferred from matching amounts."
                : "Unknown or partial order scope, or unverified rows, withholds full-order quantity and aggregate amount comparisons.");

        RequiredIdentity(context, po, "purchase_order_number", ["purchase_order_number", "po_number", "document_number"]);
        RequiredIdentity(context, invoice, "invoice_number", ["invoice_number", "document_number"]);
        CompareIdentity(context, po, invoice, "purchase_order_reference",
            ["purchase_order_number", "po_number", "document_number"], NormalizeIdentifier,
            ["purchase_order_number", "po_number"]);
        CompareIdentity(context, po, invoice, "supplier_trn", ["supplier_trn"], NormalizeIdentifier);
        var currency = CompareIdentity(context, po, invoice, "currency", ["currency"], NormalizeCurrency);
        var currencyCode = currency ? NormalizeCurrency(Field(po, ["currency"], 0)?.ValueText ?? "") : null;
        var taxCurrency = CompatibleTaxCurrency(po, invoice, currencyCode);
        CompareNumber(context, po, invoice, "total", ["total", "gross_payable"], 0, 0,
            run.DocumentTotalTolerance, fullOrder && currency, "document_total_basis", null, currencyCode,
            !fullOrder ? factual ? "different_or_unverified_quantities" : "full_order_scope_unavailable" : "currency_unavailable");
        CompareNumber(context, po, invoice, "vat", ["vat", "tax", "vat_charged"], 0, 0,
            run.TaxTolerance, fullOrder && currency && taxCurrency, "tax_basis", null, currencyCode,
            !fullOrder ? factual ? "different_or_unverified_quantities" : "full_order_scope_unavailable" : "tax_currency_unavailable");

        // Keep v1 check ordering stable for older callers.
        leftLines ??= Lines(context, po);
        rightLines ??= Lines(context, invoice);
        foreach (var identity in leftLines.Keys.Union(rightLines.Keys).Order(StringComparer.Ordinal))
        {
            var left = leftLines.GetValueOrDefault(identity);
            var right = rightLines.GetValueOrDefault(identity);
            if (left is null || right is null)
            {
                context.Record("PAIR-LINE", "line_identity", left?.Occurrence ?? right?.Occurrence, identity,
                    left?.Identity, right?.Identity, ReconciliationCheckStatus.CouldNotCheck,
                    "line_match_unavailable", "A unique checked line has no unambiguous counterpart; this is not an overcharge conclusion.");
                continue;
            }
            var unit = CompatibleUnit(po, invoice, left.Occurrence, right.Occurrence);
            CompareNumber(context, po, invoice, "line_quantity", ["line_quantity"], left.Occurrence,
                right.Occurrence, run.QuantityTolerance, (factual || fullOrder) && unit is not null, null,
                identity, null, !fullOrder && !factual ? "full_order_scope_unavailable" : "unit_unavailable", unit);
            var priceBasis = CompatiblePriceBasis(po, invoice, left.Occurrence, right.Occurrence, unit);
            CompareNumber(context, po, invoice, "line_unit_price", ["line_unit_price"], left.Occurrence,
                right.Occurrence, run.UnitPriceTolerance, currency && unit is not null && priceBasis is not null,
                "line_price_amount_basis", identity, currencyCode, !currency ? "currency_unavailable" : "price_basis_unavailable", unit, priceBasis);
            CompareNumber(context, po, invoice, "line_total", ["line_total"], left.Occurrence,
                right.Occurrence, run.LineTotalTolerance, fullOrder && currency && unit is not null,
                "line_total_basis", identity, currencyCode,
                !fullOrder ? factual ? "different_or_unverified_quantities" : "full_order_scope_unavailable" : !currency ? "currency_unavailable" : "unit_unavailable", unit);
            CompareNumber(context, po, invoice, "line_tax", ["line_tax"], left.Occurrence,
                right.Occurrence, run.TaxTolerance, fullOrder && currency && taxCurrency && unit is not null,
                "tax_basis", identity, currencyCode,
                !fullOrder ? factual ? "different_or_unverified_quantities" : "full_order_scope_unavailable" : !currency || !taxCurrency ? "tax_currency_unavailable" : "unit_unavailable", unit);
        }
    }

    private static bool EqualCheckedQuantity(ReconciliationDocumentSnapshot po, ReconciliationDocumentSnapshot invoice,
        int leftOccurrence, int rightOccurrence)
    {
        var left = Field(po, ["line_quantity"], leftOccurrence);
        var right = Field(invoice, ["line_quantity"], rightOccurrence);
        return CompatibleUnit(po, invoice, leftOccurrence, rightOccurrence) is not null &&
            Typed(left, FieldValueType.Decimal) && Typed(right, FieldValueType.Decimal) &&
            SupplierInvoiceNumber.TryParse(left!.ValueText, out var a) &&
            SupplierInvoiceNumber.TryParse(right!.ValueText, out var b) && a == b;
    }

    public static void ValidateTolerances(ReconciliationTolerancePolicy policy)
    {
        if (new[] { policy.Quantity, policy.UnitPrice, policy.Tax, policy.LineTotal, policy.DocumentTotal }
            .Any(value => !SupplierInvoiceNumber.IsPersistableTolerance(value)))
            throw new ArgumentOutOfRangeException(nameof(policy), "Pair tolerances require non-negative values, at most 12 integer and 6 fractional digits, without rounding.");
    }

    private static bool RowCoverage(ReconciliationDocumentSnapshot source)
    {
        var occurrences = source.Fields.Where(field => LineKeys.Contains(field.FieldKey) && field.OccurrenceIndex > 0)
            .Select(field => field.OccurrenceIndex).Distinct().Count();
        return source.SourceOutcome == "Succeeded" && source.SourceProvider == "local" && source.SourceAdapterVersion == "local-1.6.0" &&
            source.RowCoverageStatus == "Complete" && source.ExtractedRowCount > 0 &&
            source.ExtractedRowCount == occurrences;
    }

    private static void RequiredIdentity(Evaluation context, ReconciliationDocumentSnapshot source,
        string key, string[] aliases)
    {
        var field = Field(source, aliases, 0);
        var valid = Typed(field, FieldValueType.String, FieldValueType.Identifier) && NormalizeIdentifier(field!.ValueText).Length > 0 &&
            !field.ValueText.Any(character => char.IsDigit(character) && character is not (>= '0' and <= '9'));
        context.Record("PAIR-ID", key, 0, null,
            source.Role == ReconciliationDocumentRole.PurchaseOrder ? field : null,
            source.Role == ReconciliationDocumentRole.Invoice ? field : null,
            valid ? ReconciliationCheckStatus.NoDifference : ReconciliationCheckStatus.CouldNotCheck,
            valid ? "document_identity_checked" : "document_identity_unavailable",
            valid ? "The role-specific document identity is checked; cross-role references are retained separately."
                : "A unique role-specific document identity with checked source evidence is unavailable.");
    }

    private static bool CompareIdentity(Evaluation context, ReconciliationDocumentSnapshot po,
        ReconciliationDocumentSnapshot invoice, string key, string[] aliases, Func<string, string> normalize,
        string[]? rightAliases = null)
    {
        var left = Field(po, aliases, 0);
        var right = Field(invoice, rightAliases ?? aliases, 0);
        var types = key == "currency" ? new[] { FieldValueType.Currency }
            : new[] { FieldValueType.String, FieldValueType.Identifier };
        if (!Typed(left, types) || !Typed(right, types) || normalize(left!.ValueText).Length == 0 || normalize(right!.ValueText).Length == 0)
        {
            context.Record("PAIR-IDENTITY", key, 0, null, left, right, ReconciliationCheckStatus.CouldNotCheck,
                "checked_identity_unavailable", "Unique checked identity values and page quotes are required.");
            return false;
        }
        var equal = normalize(left.ValueText) == normalize(right.ValueText);
        context.Record("PAIR-IDENTITY", key, 0, null, left, right,
            equal ? ReconciliationCheckStatus.NoDifference : ReconciliationCheckStatus.DifferenceFound,
            equal ? "identity_equal" : "identity_differs",
            key == "supplier_trn" ? equal ? "Checked TRN values agree; this does not verify supplier or buyer identity."
                : "Checked TRN values differ; party identity is not established by this field."
                : equal ? "Checked identity values agree." : "Checked identity values differ; affected numeric checks are withheld when currency differs.");
        return equal;
    }

    private static void CompareNumber(Evaluation context, ReconciliationDocumentSnapshot po,
        ReconciliationDocumentSnapshot invoice, string key, string[] aliases, int leftOccurrence,
        int rightOccurrence, decimal tolerance, bool permitted, string? basisKey,
        string? identity, string? currency, string blockedReason, string? unit = null, string? fixedBasis = null)
    {
        var left = Field(po, aliases, leftOccurrence);
        var right = Field(invoice, aliases, rightOccurrence);
        var basis = fixedBasis;
        if (basisKey is not null)
        {
            // Header tax basis can apply to a line only when the exact header evidence is checked.
            var basisOccurrenceLeft = basisKey == "tax_basis" ? 0 : leftOccurrence;
            var basisOccurrenceRight = basisKey == "tax_basis" ? 0 : rightOccurrence;
            var leftBasis = Field(po, [basisKey], basisOccurrenceLeft);
            var rightBasis = Field(invoice, [basisKey], basisOccurrenceRight);
            var normalizedLeft = Typed(leftBasis, FieldValueType.String) ? AmountBasis(leftBasis!.ValueText) : null;
            var normalizedRight = Typed(rightBasis, FieldValueType.String) ? AmountBasis(rightBasis!.ValueText) : null;
            basis = normalizedLeft is not null && normalizedLeft == normalizedRight
                ? fixedBasis is null ? normalizedLeft : fixedBasis + "; " + normalizedLeft : null;
        }
        var validNumber = Trusted(left) && Trusted(right) && left!.ValueType == FieldValueType.Decimal &&
            right!.ValueType == FieldValueType.Decimal && SupplierInvoiceNumber.TryParse(left.ValueText, out _) &&
            SupplierInvoiceNumber.TryParse(right.ValueText, out _);
        if (!permitted || basisKey is not null && basis is null || !validNumber)
        {
            var reason = !permitted ? blockedReason : basisKey is not null && basis is null
                ? "amount_basis_unavailable" : "checked_numeric_value_unavailable";
            context.Record("PAIR-NUMBER", key, leftOccurrence, identity, left, right,
                ReconciliationCheckStatus.CouldNotCheck, reason,
                "Arithmetic requires explicit compatible scope, currency, unit and amount/price basis where applicable, plus checked Decimal values with valid English-digit syntax and evidence.",
                null, null, unit, currency, basis);
            return;
        }
        SupplierInvoiceNumber.TryParse(left!.ValueText, out var leftNumber);
        SupplierInvoiceNumber.TryParse(right!.ValueText, out var rightNumber);
        var delta = rightNumber - leftNumber;
        var differs = Math.Abs(delta) > tolerance;
        context.Record("PAIR-NUMBER", key, leftOccurrence, identity, left, right,
            differs ? ReconciliationCheckStatus.DifferenceFound : ReconciliationCheckStatus.NoDifference,
            differs ? "absolute_tolerance_exceeded" : "within_absolute_tolerance",
            context.Factual && key == "line_quantity"
                ? string.Create(CultureInfo.InvariantCulture, $"The invoice lists {rightNumber:0.######} {unit}; this purchase order lists {leftNumber:0.######} {unit}. Difference: {delta:0.######}. Fewer invoiced items are not an error by themselves. This does not establish a remaining balance or a final invoice.")
                : string.Create(CultureInfo.InvariantCulture, $"Invoice minus purchase order is {delta:0.######}; absolute tolerance is {tolerance:0.######}. This check is independent and must not be summed with overlapping checks or called savings."),
            delta, tolerance, unit, currency, basis);
    }

    private static Dictionary<string, Line> Lines(Evaluation context, ReconciliationDocumentSnapshot source)
    {
        var candidates = new List<Line>();
        var unknownIdentity = false;
        foreach (var occurrence in source.Fields.Where(field => LineKeys.Contains(field.FieldKey) && field.OccurrenceIndex > 0)
            .Select(field => field.OccurrenceIndex).Distinct().Order())
        {
            var code = Field(source, ["line_item_code"], occurrence);
            var description = Field(source, ["line_description"], occurrence);
            var chosen = code ?? description;
            var key = code is null ? "D:" + Whitespace().Replace(description?.ValueText.Trim() ?? "", " ").ToUpperInvariant()
                : "C:" + code.ValueText.Trim().ToUpperInvariant();
            var identityTypes = code is null ? new[] { FieldValueType.String } : new[] { FieldValueType.String, FieldValueType.Identifier };
            var eligible = Typed(chosen, identityTypes) && key.Length > 2 &&
                !chosen!.ValueText.Any(c => char.IsDigit(c) && c is not (>= '0' and <= '9'));
            if (!eligible)
            {
                context.Record("PAIR-LINE", "line_identity", occurrence, null,
                    source.Role == ReconciliationDocumentRole.PurchaseOrder ? chosen : null,
                    source.Role == ReconciliationDocumentRole.Invoice ? chosen : null,
                    ReconciliationCheckStatus.CouldNotCheck, "line_identity_unavailable",
                    "A checked, evidenced code or description is required. Unchecked codes never fall back to a description.");
            }
            if (!eligible) unknownIdentity = true;
            if (chosen is not null && key.Length > 2) candidates.Add(new Line(key, occurrence, chosen, eligible));
        }
        var unique = new Dictionary<string, Line>(StringComparer.Ordinal);
        foreach (var group in candidates.GroupBy(line => line.Key).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            if (group.Count() == 1)
            {
                var candidate = group.Single();
                if (candidate.Eligible) unique.Add(group.Key, candidate);
                continue;
            }
            foreach (var line in group)
                context.Record("PAIR-LINE", "line_identity", line.Occurrence, line.Key,
                    source.Role == ReconciliationDocumentRole.PurchaseOrder ? line.Identity : null,
                    source.Role == ReconciliationDocumentRole.Invoice ? line.Identity : null,
                    ReconciliationCheckStatus.CouldNotCheck, "duplicate_line_identity",
                    "Repeated line identities are permanently excluded from matching; no arbitrary first or later row is selected.");
        }
        if (unknownIdentity || !RowCoverage(source))
        {
            context.Record("PAIR-LINE", "line_identity", null, null, null, null,
                ReconciliationCheckStatus.CouldNotCheck, unknownIdentity ? "unknown_line_identity" : "line_set_unverified",
                "An unusable line identity or unverified row coverage could conceal a duplicate; automatic matching for this source is withheld.");
            unique.Clear();
        }
        return unique;
    }

    private static string? CompatibleUnit(ReconciliationDocumentSnapshot po, ReconciliationDocumentSnapshot invoice,
        int leftOccurrence, int rightOccurrence)
    {
        var left = Field(po, ["line_unit"], leftOccurrence);
        var right = Field(invoice, ["line_unit"], rightOccurrence);
        var a = Typed(left, FieldValueType.String) ? Unit(left!.ValueText) : null;
        var b = Typed(right, FieldValueType.String) ? Unit(right!.ValueText) : null;
        return a is not null && a == b ? a : null;
    }

    private static bool CompatibleTaxCurrency(ReconciliationDocumentSnapshot po,
        ReconciliationDocumentSnapshot invoice, string? documentCurrency)
    {
        var left = Field(po, ["tax_currency"], 0);
        var right = Field(invoice, ["tax_currency"], 0);
        return documentCurrency is not null && Typed(left, FieldValueType.Currency) && Typed(right, FieldValueType.Currency) &&
            NormalizeCurrency(left!.ValueText) == documentCurrency && NormalizeCurrency(right!.ValueText) == documentCurrency;
    }

    private static string? CompatiblePriceBasis(ReconciliationDocumentSnapshot po, ReconciliationDocumentSnapshot invoice,
        int leftOccurrence, int rightOccurrence, string? unit)
    {
        var left = Field(po, ["line_price_basis"], leftOccurrence);
        var right = Field(invoice, ["line_price_basis"], rightOccurrence);
        if (unit is null || !Typed(left, FieldValueType.String) || !Typed(right, FieldValueType.String) ||
            !TryPriceBasis(left!.ValueText, unit, out var a) || !TryPriceBasis(right!.ValueText, unit, out var b) || a != b)
            return null;
        return string.Create(CultureInfo.InvariantCulture, $"{a:0.######} {unit}");
    }

    private static bool TryPriceBasis(string value, string unit, out decimal quantity)
    {
        quantity = 0;
        var parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && Unit(parts[1]) == unit && SupplierInvoiceNumber.TryParse(parts[0], out quantity) && quantity > 0;
    }
    private static string? Unit(string value) => value.Trim().ToUpperInvariant() switch
    { "EA" or "EACH" => "EA", "HOUR" or "HOURS" => "HOUR", _ => null };
    private static string? AmountBasis(string value) => value.Trim().ToLowerInvariant() switch
    { "net" => "net", "gross" => "gross", _ => null };
    private static string NormalizeCurrency(string value) => value.Trim().ToUpperInvariant() switch
    { "AED" => "AED", "USD" => "USD", "EUR" => "EUR", "GBP" => "GBP", _ => "" };
    private static string NormalizeIdentifier(string value) => value.Any(c => char.IsDigit(c) && c is not (>= '0' and <= '9'))
        ? "" : value.Trim().ToUpperInvariant();
    private static bool Trusted(ReconciliationFieldSnapshot? field) => field is not null &&
        field.SourceKind is ReviewedFieldSourceKind.Confirmed or ReviewedFieldSourceKind.Corrected &&
        field.ReviewActionId.HasValue && field.MachineFieldValueId.HasValue && field.PageNumber > 0 &&
        !string.IsNullOrWhiteSpace(field.QuotedText);
    private static bool Typed(ReconciliationFieldSnapshot? field, params FieldValueType[] types) =>
        Trusted(field) && types.Contains(field!.ValueType);
    private static ReconciliationFieldSnapshot? Field(ReconciliationDocumentSnapshot source, string[] aliases, int occurrence)
    {
        var candidates = source.Fields.Where(field => aliases.Contains(field.FieldKey) && field.OccurrenceIndex == occurrence).ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }
    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant)] private static partial Regex Whitespace();
    private sealed record Line(string Key, int Occurrence, ReconciliationFieldSnapshot Identity, bool Eligible);

    private sealed class Evaluation(ReconciliationRun run, Func<Guid> nextId, DateTimeOffset now)
    {
        public bool Factual => run.RuleSetVersion == FactualRuleSetVersion;
        public void Record(string code, string fieldKey, int? occurrence, string? identity,
            ReconciliationFieldSnapshot? left, ReconciliationFieldSnapshot? right,
            ReconciliationCheckStatus status, string reason, string explanation, decimal? delta = null,
            decimal? tolerance = null, string? unit = null, string? currency = null, string? basis = null)
        {
            Guid? findingId = null;
            if (status != ReconciliationCheckStatus.NoDifference)
                findingId = run.AddFinding(nextId(), code, run.RuleSetVersion,
                    status == ReconciliationCheckStatus.CouldNotCheck || run.RuleSetVersion == FactualRuleSetVersion && fieldKey == "line_quantity" ? ReconciliationFindingSeverity.Warning : ReconciliationFindingSeverity.Error,
                    ReconciliationDocumentRole.PurchaseOrder, ReconciliationDocumentRole.Invoice,
                    left?.Id, right?.Id, fieldKey, occurrence, left?.ValueText, right?.ValueText,
                    delta.HasValue ? Math.Abs(delta.Value) : null, delta.HasValue ? tolerance : null, explanation, now).Id;
            run.AddCheck(nextId(), code, fieldKey, occurrence, identity, status, reason, explanation,
                left?.Id, right?.Id, left?.ValueText, right?.ValueText, delta, tolerance, unit, currency, basis, findingId, now);
        }
    }
}
