using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.Reconciliation;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Tests;

public sealed partial class ReconciliationHandlerTests
{
    private sealed class DraftReceiptMemory : IReconciliationDraftRequestStore
    {
        public List<ReconciliationDraftRequest> Items { get; } = [];
        public int Finds { get; private set; }
        public Task<ReconciliationDraftRequest?> FindAsync(Guid org, Guid actor, string key, CancellationToken ct)
        { Finds++; return Task.FromResult(Items.SingleOrDefault(x => x.OrganizationId == org && x.ActorId == actor && x.RequestKey == key)); }
        public Task AddAsync(ReconciliationDraftRequest receipt, CancellationToken ct) { Items.Add(receipt); return Task.CompletedTask; }
    }
    private sealed class WorkflowFixture
    {
        public Guid Org { get; } = Guid.NewGuid(); public Guid Actor { get; } = Guid.NewGuid();
        public Document Invoice { get; } public Document Po { get; } public Document Replacement { get; }
        public ReconciliationCase Item { get; } public FakeReconciliationRepository Repository { get; }
        public DraftReceiptMemory Receipts { get; } = new();
        public WorkflowFixture(bool legacy = false)
        {
            Invoice = CreateReviewedDocument(Org, Actor, "invoice", complete: false).Document;
            Po = CreateReviewedDocument(Org, Actor, "purchase_order").Document;
            Replacement = CreateReviewedDocument(Org, Actor, "invoice").Document;
            Item = new(Guid.NewGuid(), Org, Actor, "Saved pair", Now, workflowKind: legacy ? ReconciliationWorkflowKind.Legacy : ReconciliationWorkflowKind.InvoicePurchaseOrder);
            Repository = new(Item);
        }
        public SetReconciliationDraftDocumentHandler Handler(Guid? org = null, Guid? actor = null) => new(Repository, Receipts,
            new FakeDocumentRepository(Invoice, Po, Replacement), new FakeExecutionContext(org ?? Org, actor ?? Actor), new FakeClock(Now));
        public Task<ReconciliationCaseView> Save(string role, Guid? doc, Guid version, string key) => Handler().HandleAsync(Item.Id, role, version, new(doc, key), CancellationToken.None);
    }

    [Fact]
    public async Task Workflow_draft_accepts_uncompleted_review_without_pin_or_evaluation()
    {
        var f = new WorkflowFixture(); var version = f.Item.ConcurrencyToken;
        var result = await f.Save("Invoice", f.Invoice.Id, version, "draft-one");
        Assert.Equal("InvoicePurchaseOrder", result.WorkflowKind); Assert.Equal(f.Invoice.Id, Assert.Single(result.DraftDocuments!).DocumentId);
        Assert.Empty(result.Documents); Assert.Empty(result.Runs); Assert.Equal(1, f.Repository.SaveCount);
        var receipt = Assert.Single(f.Receipts.Items); Assert.Equal(version, receipt.ExpectedVersion); Assert.Equal(f.Item.DraftInvoiceVersion, receipt.DraftVersion);
    }

    [Fact]
    public async Task Workflow_replay_uses_original_etag_is_read_only_and_preserves_other_role_and_new_pin()
    {
        var f = new WorkflowFixture(); var original = f.Item.ConcurrencyToken;
        await f.Save("Invoice", f.Replacement.Id, original, "same-key");
        await f.Save("PurchaseOrder", f.Po.Id, f.Item.ConcurrencyToken, "other-role");
        Link(f.Item, ReconciliationDocumentRole.Invoice, f.Replacement); var pin = Assert.Single(f.Item.DocumentLinks); var token = f.Item.ConcurrencyToken;
        var replay = await f.Save("Invoice", f.Replacement.Id, original, "same-key");
        Assert.Equal(token, f.Item.ConcurrencyToken); Assert.Equal(2, f.Repository.SaveCount); Assert.Equal(2, f.Receipts.Items.Count);
        Assert.Equal(pin.ReviewSessionId, Assert.Single(replay.Documents).ReviewSessionId); Assert.Equal(2, replay.DraftDocuments!.Count);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Workflow_changed_payload_or_original_etag_under_same_key_is_conflict(bool changedDoc)
    {
        var f = new WorkflowFixture(); var original = f.Item.ConcurrencyToken;
        await f.Save("Invoice", f.Invoice.Id, original, "same-key");
        var error = await Assert.ThrowsAsync<ReconciliationConflictException>(() => f.Save("Invoice", changedDoc ? f.Replacement.Id : f.Invoice.Id, changedDoc ? original : f.Item.ConcurrencyToken, "same-key"));
        Assert.Equal("request_key_reused", error.Code); Assert.Equal(1, f.Repository.SaveCount); Assert.Single(f.Receipts.Items);
    }

    [Fact]
    public async Task Workflow_A_B_A_cannot_replay_the_first_intent()
    {
        var f = new WorkflowFixture(); var original = f.Item.ConcurrencyToken;
        await f.Save("Invoice", f.Invoice.Id, original, "A1");
        await f.Save("Invoice", f.Replacement.Id, f.Item.ConcurrencyToken, "B");
        await f.Save("Invoice", f.Invoice.Id, f.Item.ConcurrencyToken, "A2");
        var error = await Assert.ThrowsAsync<ReconciliationConflictException>(() => f.Save("Invoice", f.Invoice.Id, original, "A1"));
        Assert.Equal("draft_intent_superseded", error.Code); Assert.Equal(3, f.Repository.SaveCount); Assert.Equal(f.Invoice.Id, f.Item.DraftInvoiceDocumentId);
    }

    [Fact]
    public async Task Workflow_initial_clear_replay_cannot_clear_a_later_selection()
    {
        var f = new WorkflowFixture(); var original = f.Item.ConcurrencyToken;
        await f.Save("Invoice", null, original, "clear-empty"); await f.Save("Invoice", f.Invoice.Id, f.Item.ConcurrencyToken, "later");
        var error = await Assert.ThrowsAsync<ReconciliationConflictException>(() => f.Save("Invoice", null, original, "clear-empty"));
        Assert.Equal("draft_intent_superseded", error.Code); Assert.Equal(f.Invoice.Id, f.Item.DraftInvoiceDocumentId); Assert.Equal(2, f.Repository.SaveCount);
    }

    [Fact]
    public async Task Workflow_receipt_key_is_actor_scoped()
    {
        var f = new WorkflowFixture(); await f.Save("Invoice", f.Invoice.Id, f.Item.ConcurrencyToken, "shared-key");
        var secondActor = Guid.NewGuid(); await f.Handler(actor: secondActor).HandleAsync(f.Item.Id, "PurchaseOrder", f.Item.ConcurrencyToken, new(f.Po.Id, "shared-key"), CancellationToken.None);
        Assert.Equal(2, f.Receipts.Items.Count); Assert.Contains(f.Receipts.Items, x => x.ActorId == secondActor);
    }

    [Fact]
    public async Task Workflow_wrong_org_or_source_never_records_receipt()
    {
        var f = new WorkflowFixture();
        await Assert.ThrowsAsync<ReconciliationNotFoundException>(() => f.Handler(org: Guid.NewGuid()).HandleAsync(f.Item.Id, "Invoice", f.Item.ConcurrencyToken, new(f.Invoice.Id, "private"), CancellationToken.None));
        await Assert.ThrowsAsync<ReconciliationNotFoundException>(() => f.Save("Invoice", Guid.NewGuid(), f.Item.ConcurrencyToken, "wrong-source"));
        Assert.Empty(f.Receipts.Items); Assert.Equal(0, f.Repository.SaveCount);
    }

    [Fact]
    public async Task Workflow_same_document_for_both_roles_is_rejected_before_receipt()
    {
        var f = new WorkflowFixture(); await f.Save("Invoice", f.Invoice.Id, f.Item.ConcurrencyToken, "invoice");
        var error = await Assert.ThrowsAsync<ReconciliationConflictException>(() => f.Save("PurchaseOrder", f.Invoice.Id, f.Item.ConcurrencyToken, "duplicate"));
        Assert.Equal("draft_document_conflict", error.Code); Assert.Null(f.Item.DraftPurchaseOrderDocumentId); Assert.Single(f.Receipts.Items);
    }

    [Theory]
    [InlineData("Quotation")] [InlineData("invoice")] [InlineData("0")]
    public async Task Workflow_role_must_be_exact_supported_name(string role)
    {
        var f = new WorkflowFixture(); var error = await Assert.ThrowsAsync<ReconciliationValidationException>(() => f.Save(role, f.Invoice.Id, f.Item.ConcurrencyToken, "role"));
        Assert.Equal("invalid_document_role", error.Code); Assert.Empty(f.Receipts.Items);
    }

    [Fact]
    public async Task Workflow_stale_new_key_does_not_admit_intent()
    {
        var f = new WorkflowFixture(); var error = await Assert.ThrowsAsync<ReconciliationPreconditionException>(() => f.Save("Invoice", f.Invoice.Id, Guid.NewGuid(), "stale"));
        Assert.Equal("stale_reconciliation_version", error.Code); Assert.Empty(f.Receipts.Items); Assert.Equal(0, f.Repository.SaveCount);
    }

    [Theory]
    [InlineData("", "invalid_request_key")] [InlineData(" padded ", "invalid_request_key")]
    public async Task Workflow_invalid_key_cannot_mutate(string key, string code)
    {
        var f = new WorkflowFixture(); var error = await Assert.ThrowsAsync<ReconciliationValidationException>(() => f.Save("Invoice", f.Invoice.Id, f.Item.ConcurrencyToken, key));
        Assert.Equal(code, error.Code); Assert.Null(f.Item.DraftInvoiceDocumentId); Assert.Empty(f.Receipts.Items);
    }

    [Fact]
    public async Task Workflow_legacy_or_closed_case_cannot_admit_new_or_replayed_draft()
    {
        var legacy = new WorkflowFixture(legacy: true); var error = await Assert.ThrowsAsync<ReconciliationConflictException>(() => legacy.Save("Invoice", legacy.Invoice.Id, legacy.Item.ConcurrencyToken, "legacy"));
        Assert.Equal("draft_requires_pair_workflow", error.Code);
        var f = new WorkflowFixture(); var original = f.Item.ConcurrencyToken; await f.Save("Invoice", f.Invoice.Id, original, "saved"); f.Item.Close(Now);
        error = await Assert.ThrowsAsync<ReconciliationConflictException>(() => f.Save("Invoice", f.Invoice.Id, original, "saved"));
        Assert.Equal("case_closed", error.Code); Assert.Equal(1, f.Repository.SaveCount);
    }

    [Theory]
    [InlineData(null, "FullOrder", "pair_policy_required")]
    [InlineData("1.0", "FullOrder", "pair_policy_required")]
    [InlineData("invoice-po-1.0", null, "pair_scope_required")]
    public async Task Workflow_evaluation_cannot_silently_use_legacy_policy_or_default_scope(string? policy, string? scope, string code)
    {
        var f = new WorkflowFixture(); var handler = new EvaluateReconciliationCaseHandler(f.Repository, new FakeDocumentRepository(f.Invoice, f.Po), new FakeExecutionContext(f.Org, f.Actor), new FakeClock(Now));
        var error = await Assert.ThrowsAsync<ReconciliationValidationException>(() => handler.HandleAsync(f.Item.Id, f.Item.ConcurrencyToken, new(RuleSetVersion: policy, OrderScope: scope), CancellationToken.None));
        Assert.Equal(code, error.Code); Assert.Empty(f.Item.Runs); Assert.Equal(0, f.Repository.SaveCount);
    }
}
