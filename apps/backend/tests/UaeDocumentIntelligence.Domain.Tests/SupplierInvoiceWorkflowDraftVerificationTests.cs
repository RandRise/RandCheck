using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Domain.Tests;

public sealed class SupplierInvoiceWorkflowDraftVerificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
    private static ReconciliationCase Pair() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Draft pair", Now,
        workflowKind: ReconciliationWorkflowKind.InvoicePurchaseOrder);
    private static ReconciliationDocumentLink Pin(ReconciliationCase pair, ReconciliationDocumentRole role, Guid doc) =>
        pair.LinkReviewedDocument(Guid.NewGuid(), role, doc, Guid.NewGuid(), Guid.NewGuid(), new string('a', 64), Now);

    [Fact]
    public void Legacy_default_remains_without_draft_roles()
    {
        var item = new ReconciliationCase(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Old case", Now);
        Assert.Equal(ReconciliationWorkflowKind.Legacy, item.WorkflowKind);
        Assert.Throws<InvalidOperationException>(() => item.SetDraftDocument(ReconciliationDocumentRole.Invoice, Guid.NewGuid(), Now));
        Assert.Null(item.DraftInvoiceDocumentId);
    }

    [Fact]
    public void Saving_drafts_does_not_pin_reviews_or_create_runs()
    {
        var item = Pair(); var invoice = Guid.NewGuid(); var po = Guid.NewGuid();
        item.SetDraftDocument(ReconciliationDocumentRole.Invoice, invoice, Now);
        item.SetDraftDocument(ReconciliationDocumentRole.PurchaseOrder, po, Now);
        Assert.Equal(invoice, item.DraftInvoiceDocumentId); Assert.Equal(po, item.DraftPurchaseOrderDocumentId);
        Assert.NotNull(item.DraftInvoiceVersion); Assert.NotNull(item.DraftPurchaseOrderVersion);
        Assert.Empty(item.DocumentLinks); Assert.Empty(item.Runs);
    }

    [Fact]
    public void Replacement_clears_only_its_live_pin_and_preserves_completed_run_sources()
    {
        var item = Pair(); var invoice = Guid.NewGuid(); var po = Guid.NewGuid();
        item.SetDraftDocument(ReconciliationDocumentRole.Invoice, invoice, Now);
        item.SetDraftDocument(ReconciliationDocumentRole.PurchaseOrder, po, Now);
        var originalInvoice = Pin(item, ReconciliationDocumentRole.Invoice, invoice);
        var originalPo = Pin(item, ReconciliationDocumentRole.PurchaseOrder, po);
        var run = item.BeginEvaluation(Guid.NewGuid(), new Dictionary<ReconciliationDocumentRole, Guid> {
            [ReconciliationDocumentRole.Invoice] = Guid.NewGuid(), [ReconciliationDocumentRole.PurchaseOrder] = Guid.NewGuid()
        }, InvoicePurchaseOrderRuleEngine.RuleSetVersion, new(.01m, .01m, .01m, .01m, .01m), Now);
        run.SetPairContext(item.CreatedByActorId, InvoiceOrderScope.Unknown); item.CompleteEvaluation(run, Now);
        var snapshots = run.DocumentSnapshots.Select(s => (s.Id, s.DocumentId, s.ReviewSessionId, s.ExtractionRunId, s.Sha256)).ToArray();
        item.SetDraftDocument(ReconciliationDocumentRole.Invoice, Guid.NewGuid(), Now.AddMinutes(1));
        Assert.DoesNotContain(item.DocumentLinks, link => link.Id == originalInvoice.Id);
        Assert.Equal(originalPo.Id, Assert.Single(item.DocumentLinks).Id);
        Assert.Equal(snapshots, run.DocumentSnapshots.Select(s => (s.Id, s.DocumentId, s.ReviewSessionId, s.ExtractionRunId, s.Sha256)).ToArray());
        Assert.Equal(ReconciliationRunStatus.Completed, run.Status);
    }

    [Fact]
    public void A_to_B_to_A_advances_role_version_even_when_document_returns()
    {
        var item = Pair(); var a = Guid.NewGuid();
        item.SetDraftDocument(ReconciliationDocumentRole.Invoice, a, Now); var first = item.DraftInvoiceVersion;
        item.SetDraftDocument(ReconciliationDocumentRole.Invoice, Guid.NewGuid(), Now);
        item.SetDraftDocument(ReconciliationDocumentRole.Invoice, a, Now);
        Assert.Equal(a, item.DraftInvoiceDocumentId); Assert.NotEqual(first, item.DraftInvoiceVersion);
    }

    [Fact]
    public void No_op_selection_preserves_role_version_and_live_pin_but_new_intent_rotates_case_token()
    {
        var item = Pair(); var a = Guid.NewGuid();
        item.SetDraftDocument(ReconciliationDocumentRole.Invoice, a, Now); var pin = Pin(item, ReconciliationDocumentRole.Invoice, a);
        var roleVersion = item.DraftInvoiceVersion; var token = item.ConcurrencyToken;
        item.SetDraftDocument(ReconciliationDocumentRole.Invoice, a, Now.AddMinutes(1));
        Assert.Equal(roleVersion, item.DraftInvoiceVersion); Assert.Equal(token, item.ConcurrencyToken);
        Assert.Equal(pin.Id, Assert.Single(item.DocumentLinks).Id);
        item.RecordDraftIntent(Now.AddMinutes(1)); Assert.NotEqual(token, item.ConcurrencyToken);
        Assert.Equal(roleVersion, item.DraftInvoiceVersion); Assert.Equal(pin.Id, Assert.Single(item.DocumentLinks).Id);
    }

    [Theory]
    [InlineData(ReconciliationDocumentRole.Quotation)]
    [InlineData((ReconciliationDocumentRole)99)]
    public void Non_pair_role_cannot_change_draft(ReconciliationDocumentRole role)
    {
        var item = Pair(); var token = item.ConcurrencyToken;
        Assert.Throws<ArgumentOutOfRangeException>(() => item.SetDraftDocument(role, Guid.NewGuid(), Now));
        Assert.Equal(token, item.ConcurrencyToken); Assert.Empty(item.DocumentLinks);
    }

    [Fact]
    public void Duplicate_document_roles_are_rejected_without_changing_existing_selection()
    {
        var item = Pair(); var doc = Guid.NewGuid(); item.SetDraftDocument(ReconciliationDocumentRole.Invoice, doc, Now);
        Assert.Throws<InvalidOperationException>(() => item.SetDraftDocument(ReconciliationDocumentRole.PurchaseOrder, doc, Now));
        Assert.Null(item.DraftPurchaseOrderDocumentId); Assert.Equal(doc, item.DraftInvoiceDocumentId);
    }

    [Fact]
    public void Clear_retains_supersession_version_and_clears_its_pin()
    {
        var item = Pair(); var doc = Guid.NewGuid(); item.SetDraftDocument(ReconciliationDocumentRole.Invoice, doc, Now); Pin(item, ReconciliationDocumentRole.Invoice, doc);
        var version = item.DraftInvoiceVersion; item.SetDraftDocument(ReconciliationDocumentRole.Invoice, null, Now);
        Assert.Null(item.DraftInvoiceDocumentId); Assert.NotNull(item.DraftInvoiceVersion); Assert.NotEqual(version, item.DraftInvoiceVersion);
        Assert.Empty(item.DocumentLinks);
    }

    [Fact]
    public void Pin_requires_saved_exact_draft_and_completed_comparison_requires_pair_policy()
    {
        var item = Pair(); var doc = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => Pin(item, ReconciliationDocumentRole.Invoice, doc));
        item.SetDraftDocument(ReconciliationDocumentRole.Invoice, doc, Now); Pin(item, ReconciliationDocumentRole.Invoice, doc);
        Assert.Throws<InvalidOperationException>(() => item.BeginEvaluation(Guid.NewGuid(), new Dictionary<ReconciliationDocumentRole, Guid> {
            [ReconciliationDocumentRole.Invoice] = Guid.NewGuid()
        }, "1.0", new(.01m, .01m, .01m, .01m, .01m), Now));
        Assert.Empty(item.Runs);
    }

    [Fact]
    public void Closed_pair_cannot_clear_draft_or_admit_intent()
    {
        var item = Pair(); var doc = Guid.NewGuid(); item.SetDraftDocument(ReconciliationDocumentRole.Invoice, doc, Now); item.Close(Now);
        Assert.Throws<InvalidOperationException>(() => item.SetDraftDocument(ReconciliationDocumentRole.Invoice, null, Now));
        Assert.Throws<InvalidOperationException>(() => item.RecordDraftIntent(Now)); Assert.Equal(doc, item.DraftInvoiceDocumentId);
    }

    [Fact]
    public void Rejected_replacement_does_not_remove_existing_pin()
    {
        var item=Pair();var doc=Guid.NewGuid();item.SetDraftDocument(ReconciliationDocumentRole.Invoice,doc,Now);var saved=Pin(item,ReconciliationDocumentRole.Invoice,doc);var token=item.ConcurrencyToken;
        Assert.Throws<InvalidOperationException>(()=>item.ReplaceReviewedDocument(Guid.NewGuid(),ReconciliationDocumentRole.Invoice,Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),new string('a',64),Now));
        Assert.Equal(saved.Id,Assert.Single(item.DocumentLinks).Id);Assert.Equal(token,item.ConcurrencyToken);
    }
}
