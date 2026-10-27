using UaeDocumentIntelligence.Application.Reconciliation;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Tests;

public sealed partial class ReconciliationHandlerTests
{
    [Fact]
    public async Task Create_request_replays_current_case_after_metadata_edit_and_rejects_payload_change()
    {
        var execution = new FakeExecutionContext(Guid.NewGuid(), Guid.NewGuid());
        var repository = new FakeReconciliationRepository();
        var handler = new CreateReconciliationCaseHandler(repository, execution, new FakeClock(Now));
        var first = await handler.HandleAsync(" Case ", default, null, "intent-1");
        repository.Item!.UpdateMetadata("Renamed", ReconciliationCaseCategory.Services, Now.AddMinutes(1));
        var retry = await handler.HandleAsync("Case", default, "Uncategorized", "intent-1");
        Assert.Equal(first.Id, retry.Id);
        Assert.Equal("Renamed", retry.Name);
        Assert.Equal("Services", retry.Category);
        Assert.Equal(repository.Item.ConcurrencyToken, retry.ConcurrencyToken);
        Assert.Equal(1, repository.SaveCount);
        var conflict = await Assert.ThrowsAsync<ReconciliationConflictException>(() =>
            handler.HandleAsync("Case", default, "Equipment", "intent-1"));
        Assert.Equal("request_key_reused", conflict.Code);
    }

    [Fact]
    public async Task Same_pin_is_noop_but_changed_source_replaces_and_closed_case_rejects()
    {
        var org = Guid.NewGuid(); var actor = Guid.NewGuid();
        var first = CreateReviewedDocument(org, actor, "invoice");
        var second = CreateReviewedDocument(org, actor, "invoice");
        var item = new ReconciliationCase(Guid.NewGuid(), org, actor, "Case", Now);
        Link(item, ReconciliationDocumentRole.Invoice, first.Document);
        var original = Assert.Single(item.DocumentLinks);
        var token = item.ConcurrencyToken;
        var repo = new FakeReconciliationRepository(item);
        var handler = new LinkReconciliationDocumentHandler(repo, new FakeDocumentRepository(first.Document, second.Document),
            new FakeExecutionContext(org, actor), new FakeClock(Now));
        var command = new LinkReconciliationDocumentCommand("Invoice", first.Document.Id, first.Review.Id);
        var same = await handler.HandleAsync(item.Id, token, command, default);
        Assert.Equal(token, same.ConcurrencyToken); Assert.Equal(original.Id, Assert.Single(same.Documents).Id);
        Assert.Equal(0, repo.SaveCount);
        await handler.HandleAsync(item.Id, token, new("Invoice", second.Document.Id, second.Review.Id), default);
        Assert.Equal(second.Document.Id, Assert.Single(item.DocumentLinks).DocumentId);
        Assert.Equal(1, repo.SaveCount);
        await Assert.ThrowsAsync<ReconciliationPreconditionException>(() => handler.HandleAsync(item.Id, token, command, default));
        item.Close(Now);
        await Assert.ThrowsAsync<ReconciliationConflictException>(() => handler.HandleAsync(item.Id, item.ConcurrencyToken, command, default));
    }

    [Fact]
    public async Task Evaluation_replay_is_rejected_but_explicit_new_version_rerun_is_preserved()
    {
        var org = Guid.NewGuid(); var actor = Guid.NewGuid();
        var source = CreateReviewedDocument(org, actor, "invoice");
        var item = new ReconciliationCase(Guid.NewGuid(), org, actor, "Case", Now);
        Link(item, ReconciliationDocumentRole.Invoice, source.Document);
        var repo = new FakeReconciliationRepository(item);
        var handler = new EvaluateReconciliationCaseHandler(repo, new FakeDocumentRepository(source.Document),
            new FakeExecutionContext(org, actor), new FakeClock(Now));
        var token = item.ConcurrencyToken;
        await handler.HandleAsync(item.Id, token, new(), default);
        var first = Assert.Single(item.Runs);
        await Assert.ThrowsAsync<ReconciliationPreconditionException>(() => handler.HandleAsync(item.Id, token, new(), default));
        Assert.Single(item.Runs); Assert.Equal(1, repo.SaveCount);
        await handler.HandleAsync(item.Id, item.ConcurrencyToken, new(), default);
        Assert.Equal(2, item.Runs.Count); Assert.Contains(first, item.Runs);
    }

    [Fact]
    public async Task Identical_latest_decision_is_noop_and_changed_reason_keeps_history()
    {
        var org = Guid.NewGuid(); var actor = Guid.NewGuid();
        var source = CreateReviewedDocument(org, actor, "invoice");
        var item = new ReconciliationCase(Guid.NewGuid(), org, actor, "Case", Now);
        Link(item, ReconciliationDocumentRole.Invoice, source.Document);
        var repo = new FakeReconciliationRepository(item);
        await new EvaluateReconciliationCaseHandler(repo, new FakeDocumentRepository(source.Document),
            new FakeExecutionContext(org, actor), new FakeClock(Now)).HandleAsync(item.Id, item.ConcurrencyToken, new(), default);
        var run = Assert.Single(item.Runs); var finding = run.Findings.First();
        var handler = new ResolveReconciliationFindingHandler(repo, new FakeExecutionContext(org, actor), new FakeClock(Now));
        var stale = item.ConcurrencyToken;
        await handler.HandleAsync(item.Id, run.Id, finding.Id, stale, new("Accepted", " Checked "), default);
        var token = item.ConcurrencyToken;
        await handler.HandleAsync(item.Id, run.Id, finding.Id, token, new("Accepted", "Checked"), default);
        Assert.Single(finding.Resolutions); Assert.Equal(token, item.ConcurrencyToken); Assert.Equal(2, repo.SaveCount);
        await Assert.ThrowsAsync<ReconciliationPreconditionException>(() => handler.HandleAsync(item.Id, run.Id, finding.Id, stale, new("Accepted", "Checked"), default));
        await handler.HandleAsync(item.Id, run.Id, finding.Id, token, new("Accepted", "Further evidence"), default);
        Assert.Equal(2, finding.Resolutions.Count);
        item.Close(Now);
        await Assert.ThrowsAsync<ReconciliationConflictException>(() => handler.HandleAsync(item.Id, run.Id, finding.Id, item.ConcurrencyToken, new("Accepted", "Further evidence"), default));
    }
}
