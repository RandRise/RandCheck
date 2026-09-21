namespace UaeDocumentIntelligence.Domain.Model;

public enum CitedRecoveryAction { CancelReserved, RecordVerifiedCost }

public sealed class CitedRecoveryAudit
{
    private CitedRecoveryAudit() { }
    public CitedRecoveryAudit(Guid organizationId, Guid requestId, Guid reservationId, Guid originalActorId,
        Guid operatorActorId, Guid spendEventId, string commandSha256, CitedRecoveryAction action,
        CitedSpendState beforeState, CitedSpendState afterState, int beforeSequence, decimal? actualUsd,
        Guid? evidenceId, string? evidenceReference, string? evidenceSha256, DateTimeOffset now)
    {
        Id = Guid.NewGuid(); OrganizationId = organizationId; RequestId = requestId; ReservationId = reservationId;
        OriginalActorId = originalActorId; OperatorActorId = operatorActorId; SpendEventId = spendEventId;
        CommandSha256 = commandSha256; Action = action; BeforeState = beforeState; AfterState = afterState;
        BeforeEventSequence = beforeSequence; AfterEventSequence = checked(beforeSequence + 1); ActualUsd = actualUsd;
        EvidenceId = evidenceId; EvidenceReference = evidenceReference; EvidenceSha256 = evidenceSha256;
        ReasonCode = action == CitedRecoveryAction.CancelReserved ? "not_dispatched" : "verified_final_cost";
        CreatedAtUtc = now;
    }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid ReservationId { get; private set; }
    public Guid OriginalActorId { get; private set; }
    public Guid OperatorActorId { get; private set; }
    public Guid SpendEventId { get; private set; }
    public string PolicyVersion { get; private set; } = "cited-recovery-execution-v1";
    public string CommandSha256 { get; private set; } = "";
    public CitedRecoveryAction Action { get; private set; }
    public string ReasonCode { get; private set; } = "";
    public CitedSpendState BeforeState { get; private set; }
    public CitedSpendState AfterState { get; private set; }
    public int BeforeEventSequence { get; private set; }
    public int AfterEventSequence { get; private set; }
    public decimal? ActualUsd { get; private set; }
    public Guid? EvidenceId { get; private set; }
    public string? EvidenceReference { get; private set; }
    public string? EvidenceSha256 { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
