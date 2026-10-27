namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed record FindingResolutionView(Guid Id, int SequenceNumber, string Action,
    Guid ActorId, string Reason, DateTimeOffset CreatedAt);
