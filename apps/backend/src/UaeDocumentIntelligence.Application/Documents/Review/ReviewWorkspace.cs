using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Documents.Review;

public sealed record ReviewEvidenceView(int Page, string? QuotedText);
public sealed record ReviewMachineValueView(Guid Id, string Value, string ValueType,
    decimal? Confidence, IReadOnlyList<ReviewEvidenceView> Evidence);
public sealed record ReviewDecisionView(Guid Id, string Action, string? ValueType, string? Value,
    string? Reason, int SequenceNumber, DateTimeOffset CreatedAt);
public sealed record ReviewEffectiveValueView(bool IsPresent, string? Value, string? ValueType, string Source);
public sealed record ReviewFieldView(string FieldKey, int OccurrenceIndex,
    ReviewMachineValueView? Machine, ReviewDecisionView? LatestAction,
    ReviewEffectiveValueView Effective, string DecisionStatus, IReadOnlyList<string> Cues);
public sealed record ReviewSessionView(Guid Id, int RevisionNumber, string Status, Guid ConcurrencyToken,
    DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt);
public sealed record ReviewWorkspaceView(Guid DocumentId, Guid SelectedExtractionRunId,
    Guid ExtractionRunId, ReviewSessionView? Session, IReadOnlyList<ReviewFieldView> Fields)
{
    private static readonly string[] ExpectedInvoiceFields =
    [
        "invoice_number", "invoice_date", "due_date", "supplier_trn", "currency",
        "subtotal", "vat", "total", "payment_terms"
    ];
    private const decimal LowConfidenceThreshold = 0.80m;

    public static ReviewWorkspaceView From(Document document)
    {
        var selected = document.SelectedExtractionRun()
            ?? throw new ReviewNotAvailableException("review_requires_successful_extraction",
                "A successful extraction is required before review.");
        var active = document.ReviewSessions
            .Where(x => x.Status is ReviewStatus.Pending or ReviewStatus.InProgress)
            .OrderByDescending(x => x.RevisionNumber).FirstOrDefault();
        var session = active ?? document.ReviewSessions
            .Where(x => x.Status == ReviewStatus.Completed && x.ExtractionRunId == selected.Id)
            .OrderByDescending(x => x.RevisionNumber).FirstOrDefault();
        var run = session is null
            ? selected
            : document.ExtractionRuns.Single(x => x.Id == session.ExtractionRunId);

        var fields = run.Fields.OrderBy(x => x.FieldKey).ThenBy(x => x.OccurrenceIndex)
            .Select(field => ProjectMachineField(field, session)).ToList();
        if (session is not null)
        {
            var machineIds = run.Fields.Select(x => x.Id).ToHashSet();
            var added = session.Actions.Where(x => !x.MachineFieldValueId.HasValue)
                .GroupBy(x => new { x.FieldKey, x.OccurrenceIndex })
                .Select(x => x.OrderByDescending(action => action.SequenceNumber).First())
                .Where(x => !machineIds.Contains(x.MachineFieldValueId ?? Guid.Empty))
                .Select(ProjectAddedField);
            fields.AddRange(added);
        }

        var identities = fields.Select(x => (x.FieldKey, x.OccurrenceIndex)).ToHashSet();
        fields.AddRange(ExpectedInvoiceFields
            .Where(fieldKey => !identities.Contains((fieldKey, 0)))
            .Select(ProjectMissingField));

        var sessionView = session is null ? null : new ReviewSessionView(session.Id,
            session.RevisionNumber, session.Status.ToString(), session.ConcurrencyToken,
            session.CreatedAtUtc, session.StartedAtUtc, session.CompletedAtUtc);
        return new ReviewWorkspaceView(document.Id, selected.Id, run.Id, sessionView,
            fields.OrderBy(x => x.FieldKey).ThenBy(x => x.OccurrenceIndex).ToList());
    }

    private static ReviewFieldView ProjectMachineField(MachineFieldValue field, ReviewSession? session)
    {
        var action = session?.Actions.Where(x => x.MachineFieldValueId == field.Id)
            .OrderByDescending(x => x.SequenceNumber).FirstOrDefault();
        var machine = new ReviewMachineValueView(field.Id, field.ValueText, field.ValueType.ToString(),
            field.Confidence, field.Evidence.OrderBy(x => x.PageNumber)
                .Select(x => new ReviewEvidenceView(x.PageNumber, x.QuotedText)).ToList());
        return new ReviewFieldView(field.FieldKey, field.OccurrenceIndex, machine, ToView(action),
            Effective(machine, action), action?.Action.ToString() ?? "Unreviewed",
            AttentionCues(field.Confidence));
    }

    private static ReviewFieldView ProjectAddedField(ReviewAction action) => new(action.FieldKey,
        action.OccurrenceIndex, null, ToView(action), Effective(null, action), action.Action.ToString(),
        action.Action == ReviewActionType.Cleared ? ["MissingMachineValue"] : []);

    private static ReviewFieldView ProjectMissingField(string fieldKey) => new(fieldKey, 0,
        null, null, new ReviewEffectiveValueView(false, null, null, "MissingMachineValue"),
        "Missing", ["MissingMachineValue"]);

    private static IReadOnlyList<string> AttentionCues(decimal? confidence) => confidence switch
    {
        null => ["ConfidenceUnavailable"],
        < LowConfidenceThreshold => ["LowConfidence"],
        _ => []
    };

    private static ReviewDecisionView? ToView(ReviewAction? action) => action is null ? null :
        new ReviewDecisionView(action.Id, action.Action.ToString(), action.CorrectedValueType?.ToString(),
            action.CorrectedValueText, action.Reason, action.SequenceNumber, action.CreatedAtUtc);

    private static ReviewEffectiveValueView Effective(ReviewMachineValueView? machine, ReviewAction? action) =>
        action?.Action switch
        {
            ReviewActionType.Corrected or ReviewActionType.Added => new(true,
                action.CorrectedValueText, action.CorrectedValueType?.ToString(), "Reviewer"),
            ReviewActionType.Cleared => new(false, null, null, "Reviewer"),
            ReviewActionType.Confirmed => new(true, machine?.Value, machine?.ValueType, "MachineConfirmed"),
            _ when machine is not null => new(true, machine.Value, machine.ValueType, "MachinePending"),
            _ => new(false, null, null, "Reviewer")
        };
}

public abstract class ReviewWorkflowException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ReviewNotFoundException()
    : ReviewWorkflowException("review_not_found", "The document or review was not found.");
public sealed class ReviewNotAvailableException(string code, string message)
    : ReviewWorkflowException(code, message);
public sealed class ReviewConflictException(string code, string message,
    IReadOnlyList<string>? pendingFields = null) : ReviewWorkflowException(code, message)
{
    public IReadOnlyList<string>? PendingFields { get; } = pendingFields;
}
public sealed class ReviewPreconditionException(string code, string message)
    : ReviewWorkflowException(code, message);
public sealed class ReviewValidationException(string code, string message)
    : ReviewWorkflowException(code, message);
