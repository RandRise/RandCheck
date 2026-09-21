using System.Globalization;
using System.Text.Json;
using UaeDocumentIntelligence.Application.CitedQuestions;

namespace UaeDocumentIntelligence.Application.Tests;

public sealed partial class CitedTypedFactsTests
{
    public static IEnumerable<object[]> DiverseQuestions()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "samples/phase-5/reviewed-diverse-v0.1.0")))
            root = root.Parent;
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(root!.FullName,
            "samples/phase-5/reviewed-diverse-v0.1.0/cases.json")));
        foreach (var c in data.RootElement.GetProperty("cases").EnumerateArray())
            yield return [c.GetProperty("id").GetString()!, c.GetRawText()];
    }

    [Theory]
    [MemberData(nameof(DiverseQuestions))]
    public void Varied_reviewed_corpus_matches_frozen_question_expectations(string id, string json)
    {
        using var item = JsonDocument.Parse(json);
        var expected = item.RootElement;
        var corpus = new[] {
            Diverse(CitedFactRole.Quotation, 7, "40.00", "2.00", "42.00", "20.00"),
            Diverse(CitedFactRole.PurchaseOrder, 3, "42.00", "2.10", "44.10", "21.00"),
            Diverse(CitedFactRole.Invoice, 9, "38.00", "1.90", "39.90", "19.00") };
        var answer = CitedFactAnswerEngine.Answer(expected.GetProperty("question").GetString()!, corpus);
        Assert.True(expected.GetProperty("status").GetString() == answer.Status, id + ": " + answer.Status);
        if (answer.Status == "InsufficientEvidence")
        {
            Assert.Empty(answer.Claims);
            Assert.Empty(answer.Operands);
            return;
        }
        Assert.Equal(expected.GetProperty("field").GetString(), answer.Plan!.FieldKey);
        Assert.Equal(expected.GetProperty("operation").GetString(), answer.Plan.Operation.ToString());
        Assert.Equal(expected.GetProperty("operandCount").GetInt32(), answer.Operands.Count);
        if (expected.GetProperty("value").GetString() is string value)
            Assert.Equal(value, Assert.Single(answer.Operands).Value);
        if (expected.GetProperty("calculation").GetString() is string calculation)
        {
            Assert.Equal(decimal.Parse(calculation, CultureInfo.InvariantCulture), answer.Calculation!.Value);
            Assert.Equal(answer.Operands.Select(o => o.IndexId).Order(), answer.Calculation.OperandIndexIds.Order());
        }
        foreach (var claim in answer.Claims)
        {
            Assert.NotEmpty(claim.Citations);
            foreach (var citation in claim.Citations)
                Assert.Contains(corpus.SelectMany(d => d.Facts), f => f.Citation == citation);
        }
        Assert.False(answer.ModelInvoked);
    }

    private static CitedTypedDocument Diverse(CitedFactRole role, int occurrence, string subtotal, string vat, string total, string unit)
    {
        var fields = new List<(string, int, string)> {
            ("supplier_trn",0,"00000701"), ("supplier_tin",0,"000099"), ("buyer_trn",0,"00000802"),
            ("currency",0,"AED"), ("subtotal",0,subtotal), ("vat",0,vat), ("total",0,total),
            ("line_description",occurrence,"Onboarding training"), ("line_item_code",occurrence,"TRAINING"),
            ("line_quantity",occurrence,"2"), ("line_unit_price",occurrence,unit),
            ("line_tax",occurrence,vat), ("line_total",occurrence,total) };
        if (role == CitedFactRole.Invoice)
            fields.AddRange([("invoice_date",0,"2026-08-20"),("due_date",0,"2026-09-15"),("payment_terms",0,"Net 26")]);
        return Project(role, fields.ToArray());
    }
}
