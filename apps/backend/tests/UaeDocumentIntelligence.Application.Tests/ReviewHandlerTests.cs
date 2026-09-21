using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.Documents.Review;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Tests;

public sealed class ReviewHandlerTests
{
    private const string ValidSha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Start_creates_active_review_pinned_to_selected_run_and_is_idempotent()
    {
        var (document, run) = CreateReadyDocument();
        var execution = new FakeExecutionContext(document.OrganizationId, Guid.NewGuid());
        var repository = new FakeRepository(document);
        var handler = new StartReviewHandler(repository, execution, new FakeClock(Now));

        var created = await handler.HandleAsync(document.Id, CancellationToken.None);
        var existing = await handler.HandleAsync(document.Id, CancellationToken.None);

        Assert.True(created.Created);
        Assert.False(existing.Created);
        Assert.Equal(1, repository.ReviewSaveCount);
        var session = Assert.Single(document.ReviewSessions);
        Assert.Equal(run.Id, session.ExtractionRunId);
        Assert.Equal(execution.ActorId, session.AssignedActorId);
        Assert.Equal("InProgress", created.Workspace.Session?.Status);
    }

    [Fact]
    public async Task Start_reloads_and_resumes_concurrently_created_review_after_persistence_race()
    {
        var documentId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var (document, _) = CreateReadyDocument(documentId: documentId, runId: runId,
            organizationId: organizationId);
        var (reloaded, _) = CreateReadyDocument(documentId: documentId, runId: runId,
            organizationId: organizationId);
        var concurrentSession = reloaded.OpenReview(Guid.NewGuid(), Guid.NewGuid(), Now);
        var repository = new FakeRepository(document) { ReviewOutcome = ReviewSaveOutcome.ConstraintConflict };
        repository.ReviewReads.Enqueue(document);
        repository.ReviewReads.Enqueue(reloaded);
        var handler = new StartReviewHandler(repository,
            new FakeExecutionContext(document.OrganizationId, Guid.NewGuid()), new FakeClock(Now));

        var result = await handler.HandleAsync(document.Id, CancellationToken.None);

        Assert.False(result.Created);
        Assert.Equal(concurrentSession.Id, result.Workspace.Session?.Id);
        Assert.Equal(2, repository.ReviewReadCount);
    }

    [Fact]
    public void Workspace_exposes_missing_and_low_confidence_cues_without_hiding_machine_values()
    {
        var (document, run) = CreateReadyDocument(confidence: 0.42m);

        var workspace = ReviewWorkspaceView.From(document);

        var machine = workspace.Fields.Single(x => x.Machine?.Id == Assert.Single(run.Fields).Id);
        Assert.Equal("100.00", machine.Machine?.Value);
        Assert.Contains("LowConfidence", machine.Cues);
        var missing = workspace.Fields.Single(x => x.FieldKey == "invoice_number");
        Assert.Equal("Missing", missing.DecisionStatus);
        Assert.Contains("MissingMachineValue", missing.Cues);
        Assert.Null(missing.Machine);
        var due = workspace.Fields.Single(x => x.FieldKey == "due_date");
        Assert.Equal("Missing", due.DecisionStatus);
        Assert.False(due.Effective.IsPresent);
        Assert.Contains("MissingMachineValue", due.Cues);
    }

    [Fact]
    public async Task Reviewer_can_add_an_explicit_missing_due_date_as_a_date()
    {
        var (document, _) = CreateReadyDocument();
        var actor = Guid.NewGuid();
        var session = document.OpenReview(Guid.NewGuid(), actor, Now);
        var handler = new RecordReviewActionHandler(new FakeRepository(document),
            new FakeExecutionContext(document.OrganizationId, actor), new FakeClock(Now));
        var workspace = await handler.HandleAsync(document.Id, session.Id, session.ConcurrencyToken,
            new RecordReviewActionCommand("Added", null, "due_date", 0, "Date", "2026-09-15",
                "Explicit date checked against source"), CancellationToken.None);
        var due = workspace.Fields.Single(x => x.FieldKey == "due_date");
        Assert.Equal("Reviewer", due.Effective.Source);
        Assert.Equal("Date", due.Effective.ValueType);
        Assert.Equal("2026-09-15", due.Effective.Value);
        Assert.Null(due.Machine);
    }

    [Fact]
    public async Task History_export_preserves_every_action_in_reconstructable_sequence()
    {
        var (document, run) = CreateReadyDocument();
        var execution = new FakeExecutionContext(document.OrganizationId, Guid.NewGuid());
        var repository = new FakeRepository(document);
        var session = document.OpenReview(Guid.NewGuid(), execution.ActorId, Now);
        var field = Assert.Single(run.Fields);
        var actionHandler = new RecordReviewActionHandler(repository, execution, new FakeClock(Now));
        await actionHandler.HandleAsync(document.Id, session.Id, session.ConcurrencyToken,
            new RecordReviewActionCommand("Corrected", field.Id, null, null, "Decimal",
                "125.00", "Invoice evidence"), CancellationToken.None);

        var export = await new GetReviewHistoryHandler(repository, execution)
            .HandleAsync(document.Id, CancellationToken.None);

        var exportedSession = Assert.Single(export.Sessions);
        var action = Assert.Single(exportedSession.Actions);
        Assert.Equal(session.ExtractionRunId, exportedSession.ExtractionRunId);
        Assert.Equal(1, action.SequenceNumber);
        Assert.Equal("Corrected", action.Action);
        Assert.Equal(field.Id, action.MachineFieldValueId);
        Assert.Equal("Invoice evidence", action.Reason);
    }

    [Fact]
    public async Task Actions_project_latest_effective_values_for_all_action_types()
    {
        var (document, run) = CreateReadyDocument(fieldCount: 2);
        var execution = new FakeExecutionContext(document.OrganizationId, Guid.NewGuid());
        var repository = new FakeRepository(document);
        var session = document.OpenReview(Guid.NewGuid(), execution.ActorId, Now);
        var handler = new RecordReviewActionHandler(repository, execution, new FakeClock(Now));
        var fields = run.Fields.OrderBy(x => x.FieldKey).ToArray();

        var workspace = await Act("Confirmed", fields[0].Id, null, null, null, null, null);
        workspace = await Act("Corrected", fields[0].Id, null, null, "Decimal", "250.00", "Correction");
        workspace = await Act("Added", null, "currency", 0, "Currency", "AED", "Missing field");
        workspace = await Act("Cleared", null, "currency", 0, null, null, "Remove addition");
        workspace = await Act("Cleared", fields[1].Id, null, null, null, null, "Not present");

        Assert.Equal(5, session.Actions.Count);
        Assert.Equal(Enumerable.Range(1, 5), session.Actions.Select(x => x.SequenceNumber));
        var corrected = workspace.Fields.Single(x => x.Machine?.Id == fields[0].Id);
        Assert.Equal("Corrected", corrected.DecisionStatus);
        Assert.Equal("250.00", corrected.Effective.Value);
        Assert.Equal("Reviewer", corrected.Effective.Source);
        var clearedMachine = workspace.Fields.Single(x => x.Machine?.Id == fields[1].Id);
        Assert.False(clearedMachine.Effective.IsPresent);
        Assert.Equal("Cleared", clearedMachine.DecisionStatus);
        var clearedAdded = workspace.Fields.Single(x => x.Machine is null && x.FieldKey == "currency");
        Assert.False(clearedAdded.Effective.IsPresent);
        Assert.Equal("Cleared", clearedAdded.DecisionStatus);
        Assert.Contains("MissingMachineValue", clearedAdded.Cues);

        async Task<ReviewWorkspaceView> Act(string action, Guid? machineId, string? fieldKey,
            int? occurrence, string? valueType, string? value, string? reason)
        {
            var token = session.ConcurrencyToken;
            return await handler.HandleAsync(document.Id, session.Id, token,
                new RecordReviewActionCommand(action, machineId, fieldKey, occurrence,
                    valueType, value, reason), CancellationToken.None);
        }
    }

    [Fact]
    public async Task Action_rejects_stale_token_without_mutating_history()
    {
        var (document, run) = CreateReadyDocument();
        var execution = new FakeExecutionContext(document.OrganizationId, Guid.NewGuid());
        var repository = new FakeRepository(document);
        var session = document.OpenReview(Guid.NewGuid(), execution.ActorId, Now);
        var field = Assert.Single(run.Fields);
        var handler = new RecordReviewActionHandler(repository, execution, new FakeClock(Now));

        var exception = await Assert.ThrowsAsync<ReviewPreconditionException>(() => handler.HandleAsync(
            document.Id, session.Id, Guid.NewGuid(),
            new RecordReviewActionCommand("Confirmed", field.Id, null, null, null, null, null),
            CancellationToken.None));

        Assert.Equal("stale_review_version", exception.Code);
        Assert.Empty(session.Actions);
        Assert.Equal(0, repository.ReviewSaveCount);
    }

    [Theory]
    [InlineData("999", null, "invalid_review_action")]
    [InlineData("Corrected", "999", "invalid_value_type")]
    public async Task Action_rejects_undefined_numeric_enum_values(string action, string? valueType,
        string expectedCode)
    {
        var (document, run) = CreateReadyDocument();
        var execution = new FakeExecutionContext(document.OrganizationId, Guid.NewGuid());
        var repository = new FakeRepository(document);
        var session = document.OpenReview(Guid.NewGuid(), execution.ActorId, Now);
        var field = Assert.Single(run.Fields);
        var handler = new RecordReviewActionHandler(repository, execution, new FakeClock(Now));

        var exception = await Assert.ThrowsAsync<ReviewValidationException>(() => handler.HandleAsync(
            document.Id, session.Id, session.ConcurrencyToken,
            new RecordReviewActionCommand(action, field.Id, null, null, valueType,
                valueType is null ? null : "105.00", valueType is null ? null : "reason"),
            CancellationToken.None));

        Assert.Equal(expectedCode, exception.Code);
        Assert.Empty(session.Actions);
    }

    [Fact]
    public async Task Action_cannot_target_field_outside_pinned_extraction_run()
    {
        var (document, oldRun) = CreateReadyDocument();
        var selected = AddSuccessfulRun(document, 2, Now.AddMinutes(1), 1);
        var execution = new FakeExecutionContext(document.OrganizationId, Guid.NewGuid());
        var repository = new FakeRepository(document);
        var session = document.OpenReview(Guid.NewGuid(), execution.ActorId, Now.AddMinutes(2));
        var oldField = Assert.Single(oldRun.Fields);
        var handler = new RecordReviewActionHandler(repository, execution, new FakeClock(Now));

        await Assert.ThrowsAsync<ReviewNotFoundException>(() => handler.HandleAsync(document.Id,
            session.Id, session.ConcurrencyToken,
            new RecordReviewActionCommand("Confirmed", oldField.Id, null, null, null, null, null),
            CancellationToken.None));

        Assert.Equal(selected.Id, session.ExtractionRunId);
        Assert.Empty(session.Actions);
    }

    [Fact]
    public async Task Complete_requires_decision_for_every_machine_field_then_completes()
    {
        var (document, run) = CreateReadyDocument(fieldCount: 2);
        var execution = new FakeExecutionContext(document.OrganizationId, Guid.NewGuid());
        var repository = new FakeRepository(document);
        var session = document.OpenReview(Guid.NewGuid(), execution.ActorId, Now);
        var actionHandler = new RecordReviewActionHandler(repository, execution, new FakeClock(Now));
        var completeHandler = new CompleteReviewHandler(repository, execution, new FakeClock(Now.AddMinutes(1)));
        var fields = run.Fields.OrderBy(x => x.FieldKey).ToArray();
        await actionHandler.HandleAsync(document.Id, session.Id, session.ConcurrencyToken,
            new RecordReviewActionCommand("Confirmed", fields[0].Id, null, null, null, null, null),
            CancellationToken.None);

        var incomplete = await Assert.ThrowsAsync<ReviewConflictException>(() =>
            completeHandler.HandleAsync(document.Id, session.Id, session.ConcurrencyToken,
                CancellationToken.None));
        Assert.Equal("review_incomplete", incomplete.Code);
        Assert.Equal(new[] { "field_1[0]" }, incomplete.PendingFields);
        Assert.Equal(ReviewStatus.InProgress, session.Status);

        await actionHandler.HandleAsync(document.Id, session.Id, session.ConcurrencyToken,
            new RecordReviewActionCommand("Cleared", fields[1].Id, null, null, null, null, "Absent"),
            CancellationToken.None);
        var completed = await completeHandler.HandleAsync(document.Id, session.Id,
            session.ConcurrencyToken, CancellationToken.None);

        Assert.Equal("Completed", completed.Session?.Status);
        Assert.NotNull(completed.Session?.CompletedAt);
    }

    private static (Document Document, ExtractionRun Run) CreateReadyDocument(int fieldCount = 1,
        Guid? documentId = null, Guid? runId = null, Guid? organizationId = null)
        => CreateReadyDocument(fieldCount, documentId, runId, organizationId, 0.9m);

    private static (Document Document, ExtractionRun Run) CreateReadyDocument(int fieldCount,
        Guid? documentId, Guid? runId, Guid? organizationId, decimal confidence)
    {
        var document = new Document(documentId ?? Guid.NewGuid(), organizationId ?? Guid.NewGuid(), Guid.NewGuid(), "invoice.pdf",
            "storage.pdf", "application/pdf", 100, ValidSha, Now);
        var run = AddSuccessfulRun(document, 1, Now, fieldCount, runId, confidence);
        return (document, run);
    }

    private static (Document Document, ExtractionRun Run) CreateReadyDocument(decimal confidence)
        => CreateReadyDocument(1, null, null, null, confidence);

    private static ExtractionRun AddSuccessfulRun(Document document, int number, DateTimeOffset now,
        int fieldCount, Guid? runId = null, decimal confidence = 0.9m)
    {
        var run = document.QueueExtraction(runId ?? Guid.NewGuid(), number, "mock", "1", "invoice-v1",
            $"request-{number}", now);
        run.Start(now);
        run.AddPage(Guid.NewGuid(), 1, string.Empty, 0);
        for (var index = 0; index < fieldCount; index++)
        {
            var field = run.AddField(Guid.NewGuid(), $"field_{index}", 0, FieldValueType.Decimal,
                $"{100 + index}.00", confidence, now);
            run.AddEvidence(field, Guid.NewGuid(), 1);
        }
        run.RecordProviderMetrics(1, 1, null, null, null);
        run.Succeed("invoice", "en", now, "mock-model", "v1");
        document.RecordExtractionSuccess(run, now);
        return run;
    }

    private sealed class FakeRepository(Document? document) : IDocumentRepository
    {
        public ReviewSaveOutcome ReviewOutcome { get; init; } = ReviewSaveOutcome.Saved;
        public int ReviewSaveCount { get; private set; }
        public int ReviewReadCount { get; private set; }
        public Queue<Document?> ReviewReads { get; } = new();

        public Task<Document?> GetAsync(Guid organizationId, Guid documentId, CancellationToken token) =>
            Task.FromResult(document is not null && document.OrganizationId == organizationId &&
                document.Id == documentId ? document : null);
        public Task<Document?> GetForReviewAsync(Guid organizationId, Guid documentId, CancellationToken token)
        {
            ReviewReadCount++;
            if (ReviewReads.TryDequeue(out var next))
                return Task.FromResult(next is not null && next.OrganizationId == organizationId &&
                    next.Id == documentId ? next : null);
            return GetAsync(organizationId, documentId, token);
        }
        public Task<ReviewSaveOutcome> SaveReviewChangesAsync(CancellationToken token)
        {
            ReviewSaveCount++;
            return Task.FromResult(ReviewOutcome);
        }
        public Task AddAsync(Document value, CancellationToken token) => Task.CompletedTask;
        public Task<IReadOnlyList<Document>> ListAsync(Guid organizationId, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<Document>>([]);
        public Task<int> NextRunNumberAsync(Guid organizationId, Guid documentId, CancellationToken token) =>
            Task.FromResult(1);
        public Task<DocumentMetrics> GetMetricsAsync(Guid organizationId, CancellationToken token) =>
            Task.FromResult(new DocumentMetrics(0, 0, 0, null));
        public Task SaveChangesAsync(CancellationToken token) => Task.CompletedTask;
    }

    private sealed record FakeExecutionContext(Guid OrganizationId, Guid ActorId) : IExecutionContext;
    private sealed record FakeClock(DateTimeOffset UtcNow) : IClock;
}
