using System.Globalization;
using System.Text.RegularExpressions;

namespace UaeDocumentIntelligence.Application.CitedQuestions;

/// <summary>Provider-free answers from typed decisions. No model claims, copied excerpts, or floating-point arithmetic.</summary>
public static class CitedFactAnswerEngine
{
    public static CitedFactAnswer Answer(string question, IReadOnlyList<CitedTypedDocument> corpus)
    {
        var parsed = CitedFactQuestionPlan.Parse(question);
        return parsed.Plan is null ? Refuse(parsed.Refusal ?? "unsupported_scope") : Execute(parsed.Plan, corpus);
    }

    public static CitedFactAnswer Execute(CitedFactPlan plan, IReadOnlyList<CitedTypedDocument> corpus)
    {
        if (corpus.Count is < 1 or > 10 || corpus.Select(x => x.IndexId).Distinct().Count() != corpus.Count ||
            corpus.Select(x => x.DocumentId).Distinct().Count() != corpus.Count || corpus.Any(x => x.Role is null))
            return Refuse("incomplete_evidence", plan, ["Document roles or distinct reviewed revisions are ambiguous."]);
        var selected = corpus.Where(x => plan.Roles.Count == 0 || plan.Roles.Contains(x.Role!.Value))
            .OrderBy(x => x.Role).ThenBy(x => x.IndexId).ToArray();
        var missing = new List<string>();
        if ((plan.RequiresMultipleDocuments || plan.Operation == CitedFactOperation.Maximum) && selected.Length < 2)
            missing.Add("A comparison requires at least two identified documents.");
        foreach (var role in plan.Roles.Distinct())
            if (selected.All(x => x.Role != role)) missing.Add($"Missing selected document: {role}.");
        if (plan.ExpectedDocumentCount is int count && selected.Length != count)
            missing.Add($"The request requires {count} documents; {selected.Length} are available.");
        if (selected.GroupBy(x => x.Role).Any(g => g.Count() > 1))
            return Refuse("incomplete_evidence", plan, ["More than one document has the same role; select an unambiguous corpus."]);
        if (plan.Operation == CitedFactOperation.Difference && selected.Length != 2)
            return Refuse("incomplete_evidence", plan, ["A difference requires exactly two identified documents."]);
        var operands = new List<CitedFactOperand>();
        foreach (var document in selected)
        {
            var key = plan.FieldKey == "document_number" ? CitedTypedFactProjection.IdentityKey(document.Role!.Value) : plan.FieldKey;
            var occurrence = 0;
            CitedTypedFact? lineIdentity = null;
            if (key.StartsWith("line_", StringComparison.Ordinal))
            {
                var words = (plan.LineSelector ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var candidates = document.Facts.Where(f => f.Valid && f.FieldKey is "line_description" or "line_item_code")
                    .Where(f => words.Any(word => Regex.IsMatch(f.Value, @"\b" + Regex.Escape(word) + @"\b",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)))
                    .GroupBy(f => f.Occurrence).ToArray();
                if (candidates.Length != 1)
                {
                    missing.Add($"{document.Role}: requested line is missing or ambiguous.");
                    continue;
                }
                occurrence = candidates[0].Key;
                lineIdentity = candidates[0].OrderBy(f => f.FieldKey, StringComparer.Ordinal).First();
            }
            var keys = plan.Operation == CitedFactOperation.LineBreakdown
                ? new[] { "line_quantity", "line_unit_price", "line_tax", "line_total" } : new[] { key };
            var chosen = new List<CitedTypedFact>();
            foreach (var field in keys)
            {
                var matches = document.Facts.Where(f => f.FieldKey == field && f.Occurrence == occurrence).ToArray();
                if (matches.Length != 1 || !matches[0].Valid)
                    missing.Add($"{document.Role}: reviewed {field}[{occurrence}] is missing, invalid or ambiguous.");
                else chosen.Add(matches[0]);
            }
            if (chosen.Count != keys.Length) continue;
            CitedTypedFact? currency = null;
            if (chosen.Any(f => CitedTypedFactProjection.Monetary(f.FieldKey)))
            {
                var currencies = document.Facts.Where(f => f.FieldKey == "currency" && f.Occurrence == 0 && f.Valid).ToArray();
                if (currencies.Length != 1)
                {
                    missing.Add($"{document.Role}: reviewed currency is missing or ambiguous.");
                    continue;
                }
                currency = currencies[0];
            }
            if (plan.Operation == CitedFactOperation.LineBreakdown)
            {
                // A consistency check of the stated figures, not an inferred VAT rate or tax treatment.
                var amount = chosen.ToDictionary(f => f.FieldKey, f => f.Number!.Value);
                try
                {
                    if (checked(amount["line_quantity"] * amount["line_unit_price"] + amount["line_tax"]) != amount["line_total"])
                    {
                        missing.Add($"{document.Role}: reviewed quantity, unit price, tax and gross amount do not reconcile exactly.");
                        continue;
                    }
                }
                catch (OverflowException) { missing.Add($"{document.Role}: arithmetic is outside supported bounds."); continue; }
            }
            foreach (var fact in chosen)
            {
                var citations = new List<CitedFactCitation> { fact.Citation };
                var monetary = CitedTypedFactProjection.Monetary(fact.FieldKey);
                if (monetary && currency is not null) citations.Add(currency.Citation);
                if (lineIdentity is not null) citations.Add(lineIdentity.Citation);
                operands.Add(new(document.IndexId, document.Role!.Value, fact.FieldKey, occurrence, fact.Value,
                    fact.Number, monetary ? currency?.Value : null, citations.DistinctBy(c => c.ChunkId).ToArray()));
            }
        }
        if (operands.Count == 0) return Refuse("incomplete_evidence", plan,
            missing.Count > 0 ? missing : ["No reviewed facts match this request."]);
        var claims = operands.Select(o => new CitedFactClaim(
            $"{RoleLabel(o.Role)} {FieldLabel(o.FieldKey)}: {o.Value}{(o.Currency is null ? "" : " " + o.Currency)}.", o.Citations)).ToList();
        CitedFactCalculation? calculation = null;
        if (missing.Count == 0 && plan.Operation is CitedFactOperation.Difference or CitedFactOperation.Maximum)
        {
            if (operands.Any(o => o.Number is null) || operands.Select(o => o.Currency).Distinct().Count() != 1 ||
                operands[0].Currency is null)
                return Refuse("incomplete_evidence", plan, ["Numeric comparisons require verified amounts in the same currency."]);
            var value = plan.Operation == CitedFactOperation.Difference
                ? Math.Abs(operands[0].Number!.Value - operands[1].Number!.Value) : operands.Max(o => o.Number!.Value);
            var winners = plan.Operation == CitedFactOperation.Maximum
                ? operands.Where(o => o.Number == value).Select(o => o.IndexId).ToArray() : [];
            calculation = new(plan.Operation.ToString(), value, operands[0].Currency!, operands.Select(o => o.IndexId).ToArray(), winners);
            var label = plan.Operation == CitedFactOperation.Difference ? "Absolute difference" :
                "Highest amount (" + string.Join(", ", operands.Where(o => o.Number == value).Select(o => RoleLabel(o.Role))) + ")";
            claims.Add(new($"{label}: {value.ToString("0.######", CultureInfo.InvariantCulture)} {calculation.Currency}.",
                operands.SelectMany(o => o.Citations).DistinctBy(c => c.ChunkId).ToArray()));
        }
        // Listing values is safe for mixed currencies, but no equality/difference conclusion is made.
        if (missing.Count == 0 && plan.Operation == CitedFactOperation.Values && operands.Count > 1 &&
            operands.Select(o => o.FieldKey).Distinct().Count() == 1 && operands.Select(o => o.Currency).Distinct().Count() == 1)
        {
            var equal = operands.All(o => o.Number is not null)
                ? operands.Select(o => o.Number).Distinct().Count() == 1 : operands.Select(o => o.Value).Distinct(StringComparer.Ordinal).Count() == 1;
            claims.Add(new($"The selected documents' {FieldLabel(plan.FieldKey)} values {(equal ? "agree" : "differ")}.",
                operands.SelectMany(o => o.Citations).DistinctBy(c => c.ChunkId).ToArray()));
        }
        return new(missing.Count == 0 ? "Answered" : "PartiallyAnswered", missing.Count == 0 ? null : "incomplete_evidence",
            plan, operands.AsReadOnly(), claims.AsReadOnly(), missing.AsReadOnly(), calculation);
    }

    public static CitedFactAnswer Refuse(string reason, CitedFactPlan? plan = null, IReadOnlyList<string>? missing = null)
        => new("InsufficientEvidence", reason, plan, [], [], missing ?? [], null);

    private static string RoleLabel(CitedFactRole role) => role == CitedFactRole.PurchaseOrder ? "Purchase order" : role.ToString();
    private static string FieldLabel(string key) => key switch
    {
        "supplier_trn" => "supplier TRN", "supplier_tin" => "supplier TIN", "buyer_trn" => "buyer TRN",
        "quotation_number" or "purchase_order_number" or "invoice_number" => "number",
        "vat" => "VAT", "line_tax" => "line VAT", _ => key.Replace('_', ' ')
    };
}
