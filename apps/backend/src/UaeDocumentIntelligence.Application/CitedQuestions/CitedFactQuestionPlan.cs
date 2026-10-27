using System.Text;
using System.Text.RegularExpressions;

namespace UaeDocumentIntelligence.Application.CitedQuestions;

/// <summary>Conservative, versioned English intents. Unknown or compound instructions refuse; no model authority.</summary>
public static class CitedFactQuestionPlan
{
    private static bool Has(string question, string pattern) => Regex.IsMatch(question, pattern,
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100));

    public static (CitedFactPlan? Plan, string? Refusal) Parse(string question)
    {
        if (string.IsNullOrWhiteSpace(question) || question.Length > 4000) return (null, "unsupported_scope");
        if (question.Any(c => c is >= '\u0600' and <= '\u06ff' or >= '\ufb50' and <= '\ufeff'))
            return CitedArabicFactQuestionPlan.Parse(question);
        var q = question.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        if (CitedScopeClaimPolicy.RequiresRefusal(q) || Has(q,
            @"\b(ignore|instructions?|system|prompt|pretend|override|execute|scripts?|legal|compliant|compliance|eligible|eligibility|deduct|reclaim)\b"))
            return (null, "unsupported_scope");
        if (CitedPaymentClaimPolicy.RequiresVerifiedEvent(q) || Has(q, @"\b(iban|exchange|conversion)\b"))
            return (null, "incomplete_evidence");
        // Conjunctions introducing unrelated tasks and unsupported arithmetic are not silently ignored.
        if (Has(q, @"[;\r\n]|\b(then|also|plus|minus|multiply|divide|excluding|including|usd|dollars?|eur)\b"))
            return (null, "unsupported_scope");
        var roles = new List<CitedFactRole>();
        if (Has(q, @"\b(quote|quotation)\b")) roles.Add(CitedFactRole.Quotation);
        if (Has(q, @"\b(order|po)\b")) roles.Add(CitedFactRole.PurchaseOrder);
        if (Has(q, @"\binvoice\b")) roles.Add(CitedFactRole.Invoice);
        var expected = Has(q, @"\b(all three|three documents)\b") ? 3 : Has(q, @"\bboth\b") ? (int?)2 : null;
        var line = Has(q, @"\b(training|onboarding|workshop)\b");
        var selector = line ? string.Join(' ', Regex.Matches(q, @"\b(training|onboarding|workshop)\b")
            .Select(m => m.Value).Distinct(StringComparer.Ordinal)) : null;
        var fields = new HashSet<string>();
        var buyer = Has(q, @"\b(buyer|customer)\b");
        var supplier = Has(q, @"\b(supplier|seller)\b");
        if (buyer && (supplier || Has(q, @"\btin\b"))) return (null, "unsupported_scope");
        if (Has(q, @"\btrn\b|tax[ -]registration")) fields.Add(Has(q, @"\b(buyer|customer)\b") ? "buyer_trn" : "supplier_trn");
        if (Has(q, @"\btin\b")) fields.Add("supplier_tin");
        if (Has(q, @"\bvat\b|value[ -]added tax")) fields.Add(line ? "line_tax" : "vat");
        if (Has(q, @"\bsubtotal\b")) fields.Add("subtotal");
        if (Has(q, @"\bunit price\b|\bprice per (unit|item)\b")) fields.Add("line_unit_price");
        if (Has(q, @"\bquantity\b|\bhow many\b")) fields.Add("line_quantity");
        if (Has(q, @"\bpayment terms\b|\bterms\b")) fields.Add("payment_terms");
        if (Has(q, @"\b(invoice date|issued|issue date)\b")) fields.Add("invoice_date");
        if (Has(q, @"\bdue\b") && !Has(q, @"\b(amount|total|how much)\b")) fields.Add("due_date");
        if (Has(q, @"\b(total|totals|grand amount|owe|owes|payable|amount due)\b")) fields.Add(line ? "line_total" : "total");
        if (Has(q, @"\b(number|reference code|reference|identifier)\b") && !fields.Any(f => f is "supplier_trn" or "buyer_trn" or "supplier_tin"))
            fields.Add("document_number");
        var breakdown = line && Has(q, @"\bbilled\b|\bbreakdown\b");
        var maximum = Has(q, @"\b(highest|most expensive)\b") || line && Has(q, @"\bcharges? more\b");
        if (breakdown)
        {
            if (fields.Count > 0) return (null, "unsupported_scope");
            fields.Add("line_breakdown");
        }
        else if (maximum)
        {
            if (fields.Count == 0 && line) fields.Add("line_total");
            else if (fields.Count != 1 || !CitedTypedFactProjection.Monetary(fields.Single()))
                return (null, "unsupported_scope");
        }
        if (fields.Count != 1) return (null, "unsupported_scope");
        var field = fields.Single();
        if (field.StartsWith("line_", StringComparison.Ordinal) && !line) return (null, "unsupported_scope");
        // A numeric token is never allowed to specify an answer value or an unparsed filter.
        if (q.Any(char.IsDigit)) return (null, "unsupported_scope");
        // All words must belong to this bounded intent vocabulary. This is intentionally not general English QA.
        const string vocabulary = "what whats is are was were the a an on in of to for from by at as does do how much many when which " +
            "show shown use uses line have has list give me tell please according stated appears appear identify identifies identifies identify belongs belong " +
            "same all three both across between documents document quote quotation purchase order po invoice " +
            "number reference code identifier supplier seller buyer customer tax registration trn tin vat value added " +
            "total totals subtotal grand amount authorized due owe owes payable date issued issue payment terms " +
            "unit price per item items quantity training reviewer onboarding service workshop billed breakdown charges charge " +
            "more highest most expensive differ differs difference compare comparison agree match each and with s";
        var allowed = vocabulary.Split(' ').ToHashSet(StringComparer.Ordinal);
        if (Regex.Matches(q, @"[\p{L}]+").Any(m => !allowed.Contains(m.Value))) return (null, "unsupported_scope");
        var difference = Has(q, @"\bdifference\b");
        if (difference && !CitedTypedFactProjection.Numeric(field)) return (null, "unsupported_scope");
        return (new(breakdown ? CitedFactOperation.LineBreakdown : maximum ? CitedFactOperation.Maximum :
            difference ? CitedFactOperation.Difference : CitedFactOperation.Values,
            field, roles.AsReadOnly(), selector, expected, Has(q, @"\b(compare|comparison|differ|differs|difference|agree|match|same|highest|more|across)\b")), null);
    }
}
