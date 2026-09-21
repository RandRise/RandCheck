using System.Text.Json;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Domain.Tests;

public sealed class Phase4TruthSetTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Versioned_synthetic_chain_produces_exact_expected_finding_identities()
    {
        using var source = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "phase4_procurement_source_v0.1.0.json")));
        var root = source.RootElement;
        Assert.Equal("0.1.0", root.GetProperty("dataset_version").GetString());
        Assert.False(root.GetProperty("claim_eligible").GetBoolean());

        var item = new ReconciliationCase(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Synthetic truth case", Now);
        var documents = root.GetProperty("documents").EnumerateArray().ToArray();
        foreach (var document in documents)
        {
            var role = Enum.Parse<ReconciliationDocumentRole>(
                document.GetProperty("role").GetString()!);
            var hashCharacter = role switch
            {
                ReconciliationDocumentRole.Quotation => 'a',
                ReconciliationDocumentRole.PurchaseOrder => 'b',
                _ => 'c'
            };
            item.LinkReviewedDocument(Guid.NewGuid(), role, Guid.NewGuid(), Guid.NewGuid(),
                Guid.NewGuid(), new string(hashCharacter, 64), Now);
        }
        var run = item.BeginEvaluation(Guid.NewGuid(), item.DocumentLinks.ToDictionary(
            link => link.Role, _ => Guid.NewGuid()), ReconciliationRuleEngine.RuleSetVersion,
            ReconciliationTolerancePolicy.Default, Now);

        foreach (var document in documents)
            AddDocument(run, document, root);

        new ReconciliationRuleEngine().Evaluate(run, Guid.NewGuid, Now);

        var expected = root.GetProperty("expected_findings").EnumerateArray()
            .Select(FindingIdentity).Order(StringComparer.Ordinal).ToArray();
        var actual = run.Findings.Select(finding => string.Join("|", finding.RuleCode,
                finding.LeftRole, finding.RightRole, finding.FieldKey,
                finding.OccurrenceIndex ?? 0))
            .Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, actual);
        Assert.Equal(9, actual.Length);
        Assert.All(run.Findings, finding =>
        {
            Assert.NotNull(finding.LeftFieldSnapshotId);
            Assert.NotNull(finding.RightFieldSnapshotId);
        });
    }

    private static string FindingIdentity(JsonElement finding) => string.Join("|",
        finding.GetProperty("rule_code").GetString(),
        finding.GetProperty("left_role").GetString(),
        finding.GetProperty("right_role").GetString(),
        finding.GetProperty("field_key").GetString(),
        finding.GetProperty("occurrence").GetInt32());

    private static void AddDocument(ReconciliationRun run, JsonElement document, JsonElement root)
    {
        var role = Enum.Parse<ReconciliationDocumentRole>(document.GetProperty("role").GetString()!);
        var page = role == ReconciliationDocumentRole.Invoice ? 2 : 1;
        Add(run, role, "document_number", 0, FieldValueType.String,
            document.GetProperty("document_number").GetString()!, page);
        Add(run, role, "supplier_trn", 0, FieldValueType.Identifier,
            root.GetProperty("supplier").GetProperty("trn").GetString()!, page);
        Add(run, role, "currency", 0, FieldValueType.Currency,
            root.GetProperty("currency").GetString()!, page);
        foreach (var key in new[] { "subtotal", "vat", "total" })
            Add(run, role, key, 0, FieldValueType.Decimal,
                document.GetProperty(key).GetString()!, page);
        foreach (var line in document.GetProperty("lines").EnumerateArray())
        {
            var occurrence = line.GetProperty("occurrence").GetInt32();
            Add(run, role, "line_item_code", occurrence, FieldValueType.Identifier,
                line.GetProperty("item_code").GetString()!, page);
            Add(run, role, "line_description", occurrence, FieldValueType.String,
                line.GetProperty("description").GetString()!, page);
            foreach (var pair in new[] {
                ("line_quantity", "quantity"), ("line_unit_price", "unit_price"),
                ("line_tax", "tax"), ("line_total", "total") })
                Add(run, role, pair.Item1, occurrence, FieldValueType.Decimal,
                    line.GetProperty(pair.Item2).GetString()!, page);
        }
    }

    private static void Add(ReconciliationRun run, ReconciliationDocumentRole role,
        string key, int occurrence, FieldValueType type, string value, int page)
    {
        var document = run.DocumentSnapshots.Single(snapshot => snapshot.Role == role);
        run.AddFieldSnapshot(Guid.NewGuid(), document.Id, key, occurrence, type, value,
            Guid.NewGuid(), null, page, value, Now);
    }
}
