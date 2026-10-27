using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Documents.Review;

public sealed class GetReviewWorkspaceHandler(IDocumentRepository documents, IExecutionContext execution)
{
    public async Task<ReviewWorkspaceView> HandleAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await documents.GetForReviewAsync(execution.OrganizationId, documentId, cancellationToken)
            ?? throw new ReviewNotFoundException();
        return ReviewWorkspaceView.From(document);
    }
}

public sealed record StartReviewResult(ReviewWorkspaceView Workspace, bool Created);

public sealed class StartReviewHandler(IDocumentRepository documents, IExecutionContext execution, IClock clock)
{
    public async Task<StartReviewResult> HandleAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await documents.GetForReviewAsync(execution.OrganizationId, documentId, cancellationToken)
            ?? throw new ReviewNotFoundException();
        var selected = document.SelectedExtractionRun()
            ?? throw new ReviewNotAvailableException("review_requires_successful_extraction",
                "A successful extraction is required before review.");
        var active = document.ReviewSessions
            .Where(x => x.Status is ReviewStatus.Pending or ReviewStatus.InProgress)
            .OrderByDescending(x => x.RevisionNumber).FirstOrDefault();
        if (active is not null)
        {
            if (active.ExtractionRunId != selected.Id)
                throw new ReviewConflictException("active_review_uses_different_extraction",
                    "Complete or cancel the active review before reviewing a newer extraction.");
            return new StartReviewResult(ReviewWorkspaceView.From(document), false);
        }
        if (document.ReviewSessions.Any(x =>
                x.Status == ReviewStatus.Completed && x.ExtractionRunId == selected.Id))
            throw new ReviewConflictException("extraction_already_reviewed",
                "The selected extraction run already has a completed review.");

        document.OpenReview(Guid.NewGuid(), execution.ActorId, clock.UtcNow);
        var outcome = await documents.SaveReviewChangesAsync(cancellationToken);
        if (outcome != ReviewSaveOutcome.Saved)
        {
            var reloaded = await documents.GetForReviewAsync(execution.OrganizationId, documentId, cancellationToken);
            var concurrent = reloaded?.ReviewSessions
                .Where(x => x.Status is ReviewStatus.Pending or ReviewStatus.InProgress)
                .OrderByDescending(x => x.RevisionNumber).FirstOrDefault();
            if (reloaded is not null && concurrent?.ExtractionRunId == selected.Id)
                return new StartReviewResult(ReviewWorkspaceView.From(reloaded), false);
            throw new ReviewConflictException("review_start_conflict",
                "Another review operation changed this document. Reload and try again.");
        }
        return new StartReviewResult(ReviewWorkspaceView.From(document), true);
    }
}

public sealed record RecordReviewActionCommand(string Action, Guid? MachineFieldValueId,
    string? FieldKey, int? OccurrenceIndex, string? ValueType, string? Value, string? Reason);

public sealed class RecordReviewActionHandler(
    IDocumentRepository documents, IExecutionContext execution, IClock clock)
{
    public async Task<ReviewWorkspaceView> HandleAsync(Guid documentId, Guid reviewId,
        Guid expectedConcurrencyToken, RecordReviewActionCommand command, CancellationToken cancellationToken)
    {
        var document = await documents.GetForReviewAsync(execution.OrganizationId, documentId, cancellationToken)
            ?? throw new ReviewNotFoundException();
        var session = document.ReviewSessions.SingleOrDefault(x => x.Id == reviewId)
            ?? throw new ReviewNotFoundException();
        if (session.ConcurrencyToken != expectedConcurrencyToken)
            throw new ReviewPreconditionException("stale_review_version",
                "The review changed since it was loaded. Reload before saving.");
        if (session.Status != ReviewStatus.InProgress)
            throw new ReviewConflictException("review_not_active", "Only an active review can be changed.");
        if (!Enum.TryParse<ReviewActionType>(command.Action, true, out var action) || !Enum.IsDefined(action))
            throw new ReviewValidationException("invalid_review_action", "Unknown review action.");

        var run = document.ExtractionRuns.Single(x => x.Id == session.ExtractionRunId);
        var target = command.MachineFieldValueId.HasValue
            ? run.Fields.SingleOrDefault(x => x.Id == command.MachineFieldValueId.Value)
            : null;
        if (command.MachineFieldValueId.HasValue && target is null) throw new ReviewNotFoundException();

        string fieldKey;
        int occurrenceIndex;
        if (target is not null)
        {
            if (action == ReviewActionType.Added)
                throw new ReviewValidationException("added_field_cannot_reference_machine_value",
                    "An added field cannot reference a machine value.");
            fieldKey = target.FieldKey;
            occurrenceIndex = target.OccurrenceIndex;
        }
        else
        {
            fieldKey = command.FieldKey?.Trim() ?? string.Empty;
            occurrenceIndex = command.OccurrenceIndex ?? 0;
            if (action is ReviewActionType.Confirmed or ReviewActionType.Corrected)
                throw new ReviewValidationException("machine_field_required",
                    "This action requires a machine field.");
            if (string.IsNullOrWhiteSpace(fieldKey) || occurrenceIndex < 0)
                throw new ReviewValidationException("invalid_field_identity",
                    "A field key and non-negative occurrence are required.");
            if (run.Fields.Any(x => x.FieldKey == fieldKey && x.OccurrenceIndex == occurrenceIndex))
                throw new ReviewValidationException("machine_field_identity_exists",
                    "Use the machine field identifier for an extracted field.");
        }

        FieldValueType? valueType = null;
        if (command.ValueType is not null)
        {
            if (!Enum.TryParse<FieldValueType>(command.ValueType, true, out var parsed) || !Enum.IsDefined(parsed))
                throw new ReviewValidationException("invalid_value_type", "Unknown field value type.");
            valueType = parsed;
        }
        if (action is ReviewActionType.Corrected or ReviewActionType.Added &&
            string.IsNullOrWhiteSpace(command.Value))
            throw new ReviewValidationException("review_value_required", "A non-empty reviewed value is required.");

        var sequence = session.Actions.Select(x => x.SequenceNumber).DefaultIfEmpty(0).Max() + 1;
        try
        {
            if (session.IsIdenticalLatestAction(execution.ActorId, action, fieldKey, occurrenceIndex,
                target?.Id, valueType, command.Value, command.Reason, clock.UtcNow))
                return ReviewWorkspaceView.From(document);
            if (action == ReviewActionType.Cleared && target is null && session.Actions
                .Where(x => !x.MachineFieldValueId.HasValue && x.FieldKey == fieldKey && x.OccurrenceIndex == occurrenceIndex)
                .OrderByDescending(x => x.SequenceNumber).FirstOrDefault()?.Action != ReviewActionType.Added)
                throw new ReviewValidationException("added_field_not_active",
                    "Only an active reviewer-added field can be cleared without a machine value.");
            session.AddAction(Guid.NewGuid(), execution.ActorId, sequence, action, fieldKey,
                occurrenceIndex, target?.Id, valueType, command.Value, command.Reason, clock.UtcNow);
        }
        catch (ArgumentException exception)
        {
            throw new ReviewValidationException("invalid_review_action", exception.Message);
        }

        var outcome = await documents.SaveReviewChangesAsync(cancellationToken);
        if (outcome != ReviewSaveOutcome.Saved)
            throw new ReviewPreconditionException("stale_review_version",
                "The review changed since it was loaded. Reload before saving.");
        return ReviewWorkspaceView.From(document);
    }
}

public sealed class CompleteReviewHandler(IDocumentRepository documents, IExecutionContext execution, IClock clock)
{
    public async Task<ReviewWorkspaceView> HandleAsync(Guid documentId, Guid reviewId,
        Guid expectedConcurrencyToken, CancellationToken cancellationToken)
    {
        var document = await documents.GetForReviewAsync(execution.OrganizationId, documentId, cancellationToken)
            ?? throw new ReviewNotFoundException();
        var session = document.ReviewSessions.SingleOrDefault(x => x.Id == reviewId)
            ?? throw new ReviewNotFoundException();
        if (session.ConcurrencyToken != expectedConcurrencyToken)
            throw new ReviewPreconditionException("stale_review_version",
                "The review changed since it was loaded. Reload before completing it.");
        if (session.Status != ReviewStatus.InProgress)
            throw new ReviewConflictException("review_not_active", "Only an active review can be completed.");
        var run = document.ExtractionRuns.Single(x => x.Id == session.ExtractionRunId);
        try
        {
            session.Complete(run.Fields.ToList(), clock.UtcNow);
        }
        catch (IncompleteReviewException exception)
        {
            var pending = run.Fields.Where(x => exception.MissingMachineFieldIds.Contains(x.Id))
                .OrderBy(x => x.FieldKey).ThenBy(x => x.OccurrenceIndex)
                .Select(x => $"{x.FieldKey}[{x.OccurrenceIndex}]").ToList();
            throw new ReviewConflictException("review_incomplete",
                "Every machine field requires a decision before completion.", pending);
        }

        var outcome = await documents.SaveReviewChangesAsync(cancellationToken);
        if (outcome != ReviewSaveOutcome.Saved)
            throw new ReviewPreconditionException("stale_review_version",
                "The review changed since it was loaded. Reload before completing it.");
        return ReviewWorkspaceView.From(document);
    }
}
