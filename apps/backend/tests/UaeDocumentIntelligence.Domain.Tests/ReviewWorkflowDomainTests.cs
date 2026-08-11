using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Domain.Tests;

public sealed class ReviewWorkflowDomainTests
{
    private const string ValidSha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OpenReview_pins_selected_run_starts_session_and_assigns_actor()
    {
        var (document, run) = CreateReadyDocument();
        var actor = Guid.NewGuid();

        var session = document.OpenReview(Guid.NewGuid(), actor, Now.AddMinutes(1));

        Assert.Equal(ReviewStatus.InProgress, session.Status);
        Assert.Equal(run.Id, session.ExtractionRunId);
        Assert.Equal(actor, session.AssignedActorId);
        Assert.Equal(1, session.RevisionNumber);
        Assert.Null(session.SupersedesReviewSessionId);
        Assert.Same(session, Assert.Single(document.ReviewSessions));
    }

    [Fact]
    public void OpenReview_creates_next_revision_and_supersedes_latest_completed_review()
    {
        var (document, firstRun) = CreateReadyDocument();
        var first = document.OpenReview(Guid.NewGuid(), Guid.NewGuid(), Now.AddMinutes(1));
        ConfirmEveryField(first, firstRun);
        first.Complete(firstRun.Fields.ToList(), Now.AddMinutes(2));

        var secondRun = AddSuccessfulRun(document, 2, Now.AddMinutes(3), "new-total");
        var second = document.OpenReview(Guid.NewGuid(), Guid.NewGuid(), Now.AddMinutes(4));

        Assert.Equal(2, second.RevisionNumber);
        Assert.Equal(first.Id, second.SupersedesReviewSessionId);
        Assert.Equal(secondRun.Id, second.ExtractionRunId);
        Assert.Equal(ReviewStatus.Completed, first.Status);
    }

    [Fact]
    public void OpenReview_rejects_active_review_and_already_reviewed_selected_run()
    {
        var (document, run) = CreateReadyDocument();
        var session = document.OpenReview(Guid.NewGuid(), Guid.NewGuid(), Now);

        Assert.Throws<InvalidOperationException>(() =>
            document.OpenReview(Guid.NewGuid(), Guid.NewGuid(), Now.AddMinutes(1)));

        ConfirmEveryField(session, run);
        session.Complete(run.Fields.ToList(), Now.AddMinutes(2));
        Assert.Throws<InvalidOperationException>(() =>
            document.OpenReview(Guid.NewGuid(), Guid.NewGuid(), Now.AddMinutes(3)));
    }

    [Fact]
    public void Completion_reports_undecided_machine_fields_without_changing_session_state()
    {
        var (document, run) = CreateReadyDocument(fieldCount: 2);
        var session = document.OpenReview(Guid.NewGuid(), Guid.NewGuid(), Now);
        var first = run.Fields.OrderBy(x => x.FieldKey).First();
        session.AddAction(Guid.NewGuid(), Guid.NewGuid(), 1, ReviewActionType.Confirmed,
            first.FieldKey, first.OccurrenceIndex, first.Id, null, null, null, Now);
        var token = session.ConcurrencyToken;

        var exception = Assert.Throws<IncompleteReviewException>(() =>
            session.Complete(run.Fields.ToList(), Now.AddMinutes(1)));

        Assert.Single(exception.MissingMachineFieldIds);
        Assert.Equal(ReviewStatus.InProgress, session.Status);
        Assert.Null(session.CompletedAtUtc);
        Assert.Equal(token, session.ConcurrencyToken);
    }

    [Fact]
    public void Completion_accepts_latest_decisions_for_every_machine_field()
    {
        var (document, run) = CreateReadyDocument(fieldCount: 2);
        var session = document.OpenReview(Guid.NewGuid(), Guid.NewGuid(), Now);
        var fields = run.Fields.OrderBy(x => x.FieldKey).ToArray();
        session.AddAction(Guid.NewGuid(), Guid.NewGuid(), 1, ReviewActionType.Confirmed,
            fields[0].FieldKey, 0, fields[0].Id, null, null, null, Now);
        session.AddAction(Guid.NewGuid(), Guid.NewGuid(), 2, ReviewActionType.Corrected,
            fields[0].FieldKey, 0, fields[0].Id, FieldValueType.Decimal, "200.00", "Correction", Now);
        session.AddAction(Guid.NewGuid(), Guid.NewGuid(), 3, ReviewActionType.Cleared,
            fields[1].FieldKey, 0, fields[1].Id, null, null, "Not present", Now);

        session.Complete(run.Fields.ToList(), Now.AddMinutes(1));

        Assert.Equal(ReviewStatus.Completed, session.Status);
        Assert.Equal(Now.AddMinutes(1), session.CompletedAtUtc);
    }

    [Fact]
    public void Completion_rejects_required_fields_from_another_extraction_run()
    {
        var (document, firstRun) = CreateReadyDocument();
        var session = document.OpenReview(Guid.NewGuid(), Guid.NewGuid(), Now);
        var secondRun = AddSuccessfulRun(document, 2, Now.AddMinutes(1), "other");

        Assert.Throws<InvalidOperationException>(() =>
            session.Complete(secondRun.Fields.ToList(), Now.AddMinutes(2)));
        Assert.Equal(ReviewStatus.InProgress, session.Status);
        Assert.NotEqual(firstRun.Id, secondRun.Id);
    }

    private static (Document Document, ExtractionRun Run) CreateReadyDocument(int fieldCount = 1)
    {
        var document = new Document(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "invoice.pdf",
            "opaque.pdf", "application/pdf", 100, ValidSha, Now);
        var run = AddSuccessfulRun(document, 1, Now, "initial", fieldCount);
        return (document, run);
    }

    private static ExtractionRun AddSuccessfulRun(Document document, int number, DateTimeOffset now,
        string correlation, int fieldCount = 1)
    {
        var run = document.QueueExtraction(Guid.NewGuid(), number, "mock", "1", "invoice-v1",
            correlation, now);
        run.Start(now);
        run.AddPage(Guid.NewGuid(), 1, "Synthetic invoice", 0);
        for (var index = 0; index < fieldCount; index++)
        {
            var field = run.AddField(Guid.NewGuid(), $"field_{index}", 0, FieldValueType.Decimal,
                $"{100 + index}.00", 0.9m, now);
            run.AddEvidence(field, Guid.NewGuid(), 1);
        }
        run.RecordProviderMetrics(25, 1, null, null, null);
        run.Succeed("invoice", "en", now, "mock-model", "v1");
        document.RecordExtractionSuccess(run, now);
        return run;
    }

    private static void ConfirmEveryField(ReviewSession session, ExtractionRun run)
    {
        var sequence = 1;
        foreach (var field in run.Fields)
            session.AddAction(Guid.NewGuid(), Guid.NewGuid(), sequence++, ReviewActionType.Confirmed,
                field.FieldKey, field.OccurrenceIndex, field.Id, null, null, null, Now);
    }
}
