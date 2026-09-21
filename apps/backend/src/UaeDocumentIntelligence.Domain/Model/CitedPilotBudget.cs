namespace UaeDocumentIntelligence.Domain.Model;

// One shared development allowance, not a per-tenant subscription or billing system.
public sealed class CitedPilotBudget
{
    public int Id { get; private set; } = 1;
    public decimal CeilingUsd { get; private set; } = 8m;
    public decimal SettledUsd { get; private set; }
    public bool RequiresReconciliation { get; private set; }
}

public enum CitedSpendState { Reserved, Dispatched, Uncertain, Settled, Cancelled, HeldUnknown }
public enum CitedSpendWorkload { Generation, Embedding }

public sealed class CitedSpendReservation
{
    private CitedSpendReservation() { }
    public CitedSpendReservation(Guid id, Guid organizationId, Guid actorId, Guid operationId,
        string fingerprint, string requestSha256, string provider, string model, string modelVersion,
        string rateVersion, string rateSnapshotJson, DateTimeOffset rateValidUntilUtc,
        int inputTokens, int outputTokens, decimal reservedUsd, DateTimeOffset now,
        CitedSpendWorkload workload = CitedSpendWorkload.Generation)
    {
        Id = id; OrganizationId = organizationId; ActorId = actorId; OperationId = operationId;
        Fingerprint = fingerprint; RequestSha256 = requestSha256; Provider = provider; Model = model;
        ModelVersion = modelVersion; RateVersion = rateVersion; MaximumInputTokens = inputTokens;
        MaximumOutputTokens = outputTokens; ReservedUsd = reservedUsd; CreatedAtUtc = now;
        RateSnapshotJson = rateSnapshotJson; RateValidUntilUtc = rateValidUntilUtc;
        Workload = workload;
    }
    public Guid Id { get; private set; }
    public int BudgetId { get; private set; } = 1;
    public Guid OrganizationId { get; private set; }
    public Guid ActorId { get; private set; }
    public Guid OperationId { get; private set; }
    public string Fingerprint { get; private set; } = "";
    public string RequestSha256 { get; private set; } = "";
    public string Provider { get; private set; } = "";
    public string Model { get; private set; } = "";
    public string ModelVersion { get; private set; } = "";
    public string RateVersion { get; private set; } = "";
    public string RateSnapshotJson { get; private set; } = "";
    public DateTimeOffset RateValidUntilUtc { get; private set; }
    public int MaximumInputTokens { get; private set; }
    public int MaximumOutputTokens { get; private set; }
    public CitedSpendWorkload Workload { get; private set; }
    public decimal ReservedUsd { get; private set; }
    public decimal? ActualUsd { get; private set; }
    public CitedSpendState State { get; private set; } = CitedSpendState.Reserved;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
}

public sealed class CitedSpendEvent
{
    private CitedSpendEvent() { }
    public CitedSpendEvent(Guid organizationId, Guid reservationId, Guid actorId, int sequence,
        CitedSpendState state, decimal? actualUsd, string? evidenceReference, DateTimeOffset now)
    {
        Id = Guid.NewGuid(); OrganizationId = organizationId; ReservationId = reservationId;
        ActorId = actorId; Sequence = sequence; State = state; ActualUsd = actualUsd;
        EvidenceReference = evidenceReference; CreatedAtUtc = now;
    }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ReservationId { get; private set; }
    public Guid ActorId { get; private set; }
    public int Sequence { get; private set; }
    public CitedSpendState State { get; private set; }
    public decimal? ActualUsd { get; private set; }
    public string? EvidenceReference { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
