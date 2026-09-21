using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Documents.Review;

public sealed record ReviewHistoryActionView(Guid Id, Guid ActorId, int SequenceNumber,
    string Action, string FieldKey, int OccurrenceIndex, Guid? MachineFieldValueId,
    string? ValueType, string? Value, string? Reason, DateTimeOffset CreatedAt);

public sealed record ReviewHistorySessionView(Guid Id, Guid ExtractionRunId, int RevisionNumber,
    string Status, Guid? AssignedActorId, Guid? SupersedesReviewSessionId,
    DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt,
    IReadOnlyList<ReviewHistoryActionView> Actions);

public sealed record ReviewHistoryExportView(Guid DocumentId,
    IReadOnlyList<ReviewHistorySessionView> Sessions)
{
    public static ReviewHistoryExportView From(Document document) => new(document.Id,
        document.ReviewSessions.OrderBy(x => x.RevisionNumber).Select(session =>
            new ReviewHistorySessionView(session.Id, session.ExtractionRunId,
                session.RevisionNumber, session.Status.ToString(), session.AssignedActorId,
                session.SupersedesReviewSessionId, session.CreatedAtUtc, session.StartedAtUtc,
                session.CompletedAtUtc, session.Actions.OrderBy(x => x.SequenceNumber)
                    .Select(action => new ReviewHistoryActionView(action.Id, action.ActorId,
                        action.SequenceNumber, action.Action.ToString(), action.FieldKey,
                        action.OccurrenceIndex, action.MachineFieldValueId,
                        action.CorrectedValueType?.ToString(), action.CorrectedValueText,
                        action.Reason, action.CreatedAtUtc)).ToList())).ToList());
}

public sealed class GetReviewHistoryHandler(IDocumentRepository documents, IExecutionContext execution)
{
    public async Task<ReviewHistoryExportView> HandleAsync(Guid documentId,
        CancellationToken cancellationToken)
    {
        var document = await documents.GetForReviewAsync(execution.OrganizationId, documentId,
            cancellationToken) ?? throw new ReviewNotFoundException();
        return ReviewHistoryExportView.From(document);
    }
}
