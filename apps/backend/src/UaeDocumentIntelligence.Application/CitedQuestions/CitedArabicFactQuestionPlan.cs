using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace UaeDocumentIntelligence.Application.CitedQuestions;

/// <summary>Whole-question Arabic grammar; translations never become source evidence.</summary>
internal static class CitedArabicFactQuestionPlan
{
    public static (CitedFactPlan? Plan, string? Refusal) Parse(string question)
    {
        var text = new StringBuilder();
        foreach (var c in question.Normalize(NormalizationForm.FormKD))
        {
            if (c == '\u0640' || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            text.Append(c == '\u0671' ? 'ا' : c);
        }
        var q = Regex.Replace(text.ToString().Trim(), @"\s+", " ").TrimEnd('?', '؟').TrimEnd();
        var match = Regex.Match(q,
            @"\A(?:ما(?: هو| هي)?|كم|اعرض) (?<field>اجمالي|المجموع الفرعي|ضريبة القيمة المضافة|رقم|تاريخ|تاريخ استحقاق|شروط دفع|الرقم الضريبي للمورد)(?: (?<role>الفاتورة|عرض السعر|امر الشراء))?\z",
            RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100));
        if (!match.Success) return (null, "unsupported_scope");
        var field = match.Groups["field"].Value switch
        {
            "اجمالي" => "total",
            "المجموع الفرعي" => "subtotal",
            "ضريبة القيمة المضافة" => "vat",
            "رقم" => "document_number",
            "تاريخ" => "invoice_date",
            "تاريخ استحقاق" => "due_date",
            "شروط دفع" => "payment_terms",
            "الرقم الضريبي للمورد" => "supplier_trn",
            _ => throw new InvalidOperationException("Unmapped Arabic intent")
        };
        CitedFactRole[] roles = match.Groups["role"].Value switch
        {
            "الفاتورة" => [CitedFactRole.Invoice],
            "عرض السعر" => [CitedFactRole.Quotation],
            "امر الشراء" => [CitedFactRole.PurchaseOrder],
            _ => []
        };
        if (field == "invoice_date" && (roles.Length != 1 || roles[0] != CitedFactRole.Invoice))
            return (null, "unsupported_scope");
        return (new(CitedFactOperation.Values, field, roles), null);
    }
}
