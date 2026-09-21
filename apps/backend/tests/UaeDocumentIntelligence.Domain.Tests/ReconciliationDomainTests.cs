using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Domain.Tests;

public sealed class ReconciliationDomainTests
{
    private const string ShaA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string ShaB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Case_pins_one_reviewed_document_per_role_and_allows_duplicate_source_for_rule_detection()
    {
        var item = CreateCase();
        var quotation = Link(item, ReconciliationDocumentRole.Quotation, ShaA);
        var invoice = Link(item, ReconciliationDocumentRole.Invoice, ShaA);

        Assert.Equal(new[] { ReconciliationDocumentRole.Quotation, ReconciliationDocumentRole.Invoice },
            item.DocumentLinks.Select(link => link.Role));
        Assert.Equal(quotation.Sha256, invoice.Sha256);
        Assert.Throws<InvalidOperationException>(() =>
            Link(item, ReconciliationDocumentRole.Invoice, ShaB));
    }

    [Fact]
    public void Evaluation_freezes_links_and_tolerances_and_preserves_prior_runs()
    {
        var item = CreateCase();
        Link(item, ReconciliationDocumentRole.Quotation, ShaA);
        Link(item, ReconciliationDocumentRole.PurchaseOrder, ShaB);
        var policy = new ReconciliationTolerancePolicy(0m, 0.02m, 0.03m, 0.04m, 1m);

        var first = Begin(item, policy);

        Assert.Equal(1, first.RunNumber);
        Assert.Equal(2, first.DocumentSnapshots.Count);
        Assert.Equal(1m, first.DocumentTotalTolerance);
        item.CompleteEvaluation(first, Now.AddMinutes(1));
        var second = Begin(item, ReconciliationTolerancePolicy.Default, Now.AddMinutes(2));

        Assert.Equal(2, second.RunNumber);
        Assert.Equal(2, item.Runs.Count);
        Assert.Equal(ReconciliationRunStatus.Completed, first.Status);
        Assert.Equal(0.01m, second.DocumentTotalTolerance);
    }

    [Fact]
    public void Evaluation_rejects_missing_snapshot_identity_and_negative_tolerance()
    {
        var item = CreateCase();
        Link(item, ReconciliationDocumentRole.Invoice, ShaA);

        Assert.Throws<ArgumentException>(() => item.BeginEvaluation(Guid.NewGuid(),
            new Dictionary<ReconciliationDocumentRole, Guid>(), "1.0",
            ReconciliationTolerancePolicy.Default, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => item.BeginEvaluation(Guid.NewGuid(),
            SnapshotIds(item), "1.0", new ReconciliationTolerancePolicy(0, 0, 0, 0, -1), Now));
    }

    [Fact]
    public void Completed_run_is_immutable_while_resolutions_remain_append_only()
    {
        var item = CreateCase();
        Link(item, ReconciliationDocumentRole.PurchaseOrder, ShaA);
        Link(item, ReconciliationDocumentRole.Invoice, ShaB);
        var run = Begin(item, ReconciliationTolerancePolicy.Default);
        var snapshots = run.DocumentSnapshots.OrderBy(snapshot => snapshot.Role).ToArray();
        var left = run.AddFieldSnapshot(Guid.NewGuid(), snapshots[0].Id, "total", 0,
            FieldValueType.Decimal, "100.00", Guid.NewGuid(), null, 1, "100.00", Now);
        var right = run.AddFieldSnapshot(Guid.NewGuid(), snapshots[1].Id, "total", 0,
            FieldValueType.Decimal, "115.00", Guid.NewGuid(), Guid.NewGuid(), 2, "115.00", Now);
        var finding = run.AddFinding(Guid.NewGuid(), "AMT-002", "1.0",
            ReconciliationFindingSeverity.Error, ReconciliationDocumentRole.PurchaseOrder,
            ReconciliationDocumentRole.Invoice, left.Id, right.Id, "total", 0,
            left.ValueText, right.ValueText, 15m, run.DocumentTotalTolerance,
            "Invoice total exceeds the purchase order by AED 15.00.", Now);
        item.CompleteEvaluation(run, Now.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() => run.AddFinding(Guid.NewGuid(),
            "AMT-002", "1.0", ReconciliationFindingSeverity.Error,
            ReconciliationDocumentRole.PurchaseOrder, ReconciliationDocumentRole.Invoice,
            left.Id, right.Id, "total", 0, "100", "120", 20, 0.01m, "late", Now));
        var first = run.AddResolution(finding.Id, Guid.NewGuid(), Guid.NewGuid(),
            FindingResolutionAction.Accepted, "Approved commercial variance", Now.AddMinutes(2));
        var second = run.AddResolution(finding.Id, Guid.NewGuid(), Guid.NewGuid(),
            FindingResolutionAction.Resolved, "Supplier issued a credit note", Now.AddMinutes(3));

        Assert.Equal(1, first.SequenceNumber);
        Assert.Equal(2, second.SequenceNumber);
        Assert.Equal(2, finding.Resolutions.Count);
        Assert.Equal("Accepted", first.Action.ToString());
    }

    [Fact]
    public void Field_snapshot_requires_review_source_and_valid_optional_page_evidence()
    {
        var item = CreateCase();
        Link(item, ReconciliationDocumentRole.Invoice, ShaA);
        var run = Begin(item, ReconciliationTolerancePolicy.Default);
        var snapshot = Assert.Single(run.DocumentSnapshots);

        Assert.Throws<ArgumentException>(() => run.AddFieldSnapshot(Guid.NewGuid(), snapshot.Id,
            "total", 0, FieldValueType.Decimal, "100", null, null, 1, "100", Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => run.AddFieldSnapshot(Guid.NewGuid(), snapshot.Id,
            "total", 0, FieldValueType.Decimal, "100", Guid.NewGuid(), null, 0, "100", Now));
        var withoutPage = run.AddFieldSnapshot(Guid.NewGuid(), snapshot.Id, "currency", 0,
            FieldValueType.Currency, "AED", null, Guid.NewGuid(), null, null, Now);

        Assert.Null(withoutPage.PageNumber);
    }

    [Fact]
    public void Closed_case_rejects_link_and_evaluation_changes()
    {
        var item = CreateCase();
        Link(item, ReconciliationDocumentRole.Invoice, ShaA);
        item.Close(Now.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() =>
            Link(item, ReconciliationDocumentRole.PurchaseOrder, ShaB));
        Assert.Throws<InvalidOperationException>(() =>
            Begin(item, ReconciliationTolerancePolicy.Default));
    }

    private static ReconciliationCase CreateCase() => new(Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), "August procurement case", Now);

    private static ReconciliationDocumentLink Link(ReconciliationCase item,
        ReconciliationDocumentRole role, string sha) => item.LinkReviewedDocument(
            Guid.NewGuid(), role, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), sha, Now);

    private static ReconciliationRun Begin(ReconciliationCase item,
        ReconciliationTolerancePolicy policy, DateTimeOffset? now = null) => item.BeginEvaluation(
            Guid.NewGuid(), SnapshotIds(item), "1.0", policy, now ?? Now);

    private static Dictionary<ReconciliationDocumentRole, Guid> SnapshotIds(ReconciliationCase item) =>
        item.DocumentLinks.ToDictionary(link => link.Role, _ => Guid.NewGuid());
}
