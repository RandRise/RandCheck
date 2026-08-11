using UaeDocumentIntelligence.Domain.Common;

namespace UaeDocumentIntelligence.Domain.Model;

public sealed class ExtractionJob
{
    public const int MaxAttempts = 3;
    private ExtractionJob() { }

    public ExtractionJob(Guid id, Guid organizationId, Guid documentId, Guid extractionRunId,
        DateTimeOffset now)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || documentId == Guid.Empty ||
            extractionRunId == Guid.Empty)
            throw new ArgumentException("Job, organization, document, and extraction run IDs are required.");
        Id = id;
        OrganizationId = organizationId;
        DocumentId = documentId;
        ExtractionRunId = extractionRunId;
        Status = ExtractionJobStatus.Pending;
        AvailableAtUtc = CreatedAtUtc = UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid DocumentId { get; private set; }
    public Guid ExtractionRunId { get; private set; }
    public ExtractionJobStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset AvailableAtUtc { get; private set; }
    public DateTimeOffset? LastClaimedAtUtc { get; private set; }
    public Guid? LeaseToken { get; private set; }
    public string? LeaseOwner { get; private set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; private set; }
    public DateTimeOffset? LastHeartbeatAtUtc { get; private set; }
    public string? LastErrorCode { get; private set; }
    public string? LastSafeErrorSummary { get; private set; }
    public ExtractionOutcomeValue? ResultOutcome { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public void Lease(string owner, Guid token, DateTimeOffset databaseNow, TimeSpan duration)
    {
        if (token == Guid.Empty) throw new ArgumentException("Lease token is required.", nameof(token));
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        var due = Status is ExtractionJobStatus.Pending or ExtractionJobStatus.RetryScheduled &&
            AvailableAtUtc <= databaseNow;
        var expired = Status == ExtractionJobStatus.Leased && LeaseExpiresAtUtc <= databaseNow;
        if ((!due && !expired) || AttemptCount >= MaxAttempts)
            throw new InvalidOperationException("Job is not claimable.");
        Status = ExtractionJobStatus.Leased;
        AttemptCount++;
        LeaseOwner = Guard.Required(owner, nameof(owner), 100);
        LeaseToken = token;
        LastClaimedAtUtc = LastHeartbeatAtUtc = databaseNow;
        LeaseExpiresAtUtc = databaseNow.Add(duration);
        UpdatedAtUtc = databaseNow;
    }

    public void Heartbeat(string owner, Guid token, DateTimeOffset databaseNow, TimeSpan duration)
    {
        RequireAuthority(owner, token, databaseNow);
        LastHeartbeatAtUtc = databaseNow;
        LeaseExpiresAtUtc = databaseNow.Add(duration);
        UpdatedAtUtc = databaseNow;
    }

    public void ScheduleRetry(string owner, Guid token, string code, string safeSummary,
        DateTimeOffset databaseNow, TimeSpan delay)
    {
        RequireAuthority(owner, token, databaseNow);
        if (AttemptCount >= MaxAttempts) throw new InvalidOperationException("Job has exhausted its claims.");
        Status = ExtractionJobStatus.RetryScheduled;
        AvailableAtUtc = databaseNow.Add(delay);
        SetError(code, safeSummary);
        ClearLease();
        UpdatedAtUtc = databaseNow;
    }

    public void Succeed(string owner, Guid token, ExtractionOutcomeValue outcome,
        DateTimeOffset databaseNow)
    {
        RequireAuthority(owner, token, databaseNow);
        Status = ExtractionJobStatus.Succeeded;
        ResultOutcome = outcome;
        CompletedAtUtc = UpdatedAtUtc = databaseNow;
        ClearLease();
    }

    public void Fail(string owner, Guid token, string code, string safeSummary,
        DateTimeOffset databaseNow)
    {
        RequireAuthority(owner, token, databaseNow);
        FailUnfenced(code, safeSummary, databaseNow);
    }

    public void FailExpired(string code, string safeSummary, DateTimeOffset databaseNow)
    {
        if (Status != ExtractionJobStatus.Leased || LeaseExpiresAtUtc > databaseNow ||
            AttemptCount < MaxAttempts)
            throw new InvalidOperationException("Only an exhausted expired lease can be terminalized.");
        FailUnfenced(code, safeSummary, databaseNow);
    }

    public void Cancel(string code, string safeSummary, DateTimeOffset databaseNow)
    {
        if (Status is ExtractionJobStatus.Succeeded or ExtractionJobStatus.Failed or ExtractionJobStatus.Cancelled)
            throw new InvalidOperationException("A terminal job cannot be cancelled.");
        Status = ExtractionJobStatus.Cancelled;
        SetError(code, safeSummary);
        ResultOutcome = null;
        CompletedAtUtc = UpdatedAtUtc = databaseNow;
        ClearLease();
    }

    private void RequireAuthority(string owner, Guid token, DateTimeOffset databaseNow)
    {
        if (Status != ExtractionJobStatus.Leased || LeaseToken != token ||
            !string.Equals(LeaseOwner, owner, StringComparison.Ordinal) ||
            LeaseExpiresAtUtc <= databaseNow)
            throw new InvalidOperationException("Worker no longer owns this lease.");
    }

    private void FailUnfenced(string code, string safeSummary, DateTimeOffset databaseNow)
    {
        Status = ExtractionJobStatus.Failed;
        SetError(code, safeSummary);
        ResultOutcome = null;
        CompletedAtUtc = UpdatedAtUtc = databaseNow;
        ClearLease();
    }

    private void SetError(string code, string safeSummary)
    {
        LastErrorCode = Guard.Required(code, nameof(code), 100);
        LastSafeErrorSummary = Guard.Required(safeSummary, nameof(safeSummary), 500);
    }

    private void ClearLease()
    {
        LeaseToken = null;
        LeaseOwner = null;
        LeaseExpiresAtUtc = null;
        LastHeartbeatAtUtc = null;
    }
}

public enum ExtractionOutcomeValue { Ready, Partial }
