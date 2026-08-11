using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Domain.Tests;

public sealed class ExtractionJobTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void New_job_is_pending_due_and_has_no_lease_or_result()
    {
        var job = CreateJob();

        Assert.Equal(ExtractionJobStatus.Pending, job.Status);
        Assert.Equal(0, job.AttemptCount);
        Assert.Equal(Now, job.AvailableAtUtc);
        Assert.Null(job.LeaseToken);
        Assert.Null(job.LeaseOwner);
        Assert.Null(job.CompletedAtUtc);
        Assert.Null(job.ResultOutcome);
    }

    [Fact]
    public void Lease_uses_supplied_database_time_and_increments_attempt()
    {
        var job = CreateJob();
        var token = Guid.NewGuid();
        var databaseNow = Now.AddSeconds(7);

        job.Lease("worker-a", token, databaseNow, TimeSpan.FromSeconds(180));

        Assert.Equal(ExtractionJobStatus.Leased, job.Status);
        Assert.Equal(1, job.AttemptCount);
        Assert.Equal(token, job.LeaseToken);
        Assert.Equal("worker-a", job.LeaseOwner);
        Assert.Equal(databaseNow, job.LastClaimedAtUtc);
        Assert.Equal(databaseNow, job.LastHeartbeatAtUtc);
        Assert.Equal(databaseNow.AddSeconds(180), job.LeaseExpiresAtUtc);
    }

    [Fact]
    public void Heartbeat_requires_current_owner_token_and_unexpired_lease()
    {
        var job = CreateJob();
        var token = Guid.NewGuid();
        job.Lease("worker-a", token, Now, TimeSpan.FromSeconds(180));

        Assert.Throws<InvalidOperationException>(() =>
            job.Heartbeat("worker-b", token, Now.AddSeconds(30), TimeSpan.FromSeconds(180)));
        Assert.Throws<InvalidOperationException>(() =>
            job.Heartbeat("worker-a", Guid.NewGuid(), Now.AddSeconds(30), TimeSpan.FromSeconds(180)));
        Assert.Throws<InvalidOperationException>(() =>
            job.Heartbeat("worker-a", token, Now.AddSeconds(181), TimeSpan.FromSeconds(180)));

        job.Heartbeat("worker-a", token, Now.AddSeconds(30), TimeSpan.FromSeconds(180));
        Assert.Equal(Now.AddSeconds(210), job.LeaseExpiresAtUtc);
    }

    [Fact]
    public void Retry_clears_lease_and_uses_exact_fixed_availability_time()
    {
        var job = CreateJob();
        var token = Guid.NewGuid();
        job.Lease("worker-a", token, Now, TimeSpan.FromSeconds(180));

        job.ScheduleRetry("worker-a", token, "provider_timeout", "Timed out.",
            Now.AddSeconds(1), TimeSpan.FromSeconds(5));

        Assert.Equal(ExtractionJobStatus.RetryScheduled, job.Status);
        Assert.Equal(Now.AddSeconds(6), job.AvailableAtUtc);
        Assert.Equal("provider_timeout", job.LastErrorCode);
        Assert.Null(job.LeaseToken);
        Assert.Null(job.LeaseOwner);
        Assert.Null(job.LeaseExpiresAtUtc);
    }

    [Fact]
    public void Reclaim_replaces_token_and_old_worker_cannot_mutate_job()
    {
        var job = CreateJob();
        var oldToken = Guid.NewGuid();
        job.Lease("worker-old", oldToken, Now, TimeSpan.FromSeconds(10));
        var newToken = Guid.NewGuid();
        job.Lease("worker-new", newToken, Now.AddSeconds(11), TimeSpan.FromSeconds(180));

        Assert.Equal(2, job.AttemptCount);
        Assert.Equal(newToken, job.LeaseToken);
        Assert.Throws<InvalidOperationException>(() => job.Succeed("worker-old", oldToken,
            ExtractionOutcomeValue.Ready, Now.AddSeconds(12)));
        Assert.Throws<InvalidOperationException>(() => job.Fail("worker-old", oldToken,
            "provider_timeout", "Timed out.", Now.AddSeconds(12)));
    }

    [Fact]
    public void Third_expired_claim_terminalizes_and_cannot_be_claimed_again()
    {
        var job = CreateJob();
        var first = Guid.NewGuid();
        job.Lease("worker-1", first, Now, TimeSpan.FromSeconds(1));
        job.ScheduleRetry("worker-1", first, "provider_timeout", "Timed out.",
            Now, TimeSpan.FromSeconds(5));
        var second = Guid.NewGuid();
        job.Lease("worker-2", second, Now.AddSeconds(5), TimeSpan.FromSeconds(1));
        job.ScheduleRetry("worker-2", second, "provider_timeout", "Timed out.",
            Now.AddSeconds(5), TimeSpan.FromSeconds(30));
        var third = Guid.NewGuid();
        job.Lease("worker-3", third, Now.AddSeconds(35), TimeSpan.FromSeconds(1));

        Assert.Throws<InvalidOperationException>(() => job.ScheduleRetry("worker-3", third,
            "provider_timeout", "Timed out.", Now.AddSeconds(35), TimeSpan.FromSeconds(30)));
        job.FailExpired("worker_lease_expired", "The final worker lease expired.",
            Now.AddSeconds(37));

        Assert.Equal(ExtractionJobStatus.Failed, job.Status);
        Assert.Equal(3, job.AttemptCount);
        Assert.Equal("worker_lease_expired", job.LastErrorCode);
        Assert.Throws<InvalidOperationException>(() => job.Lease("worker-4", Guid.NewGuid(),
            Now.AddSeconds(38), TimeSpan.FromSeconds(180)));
    }

    [Theory]
    [InlineData(ExtractionOutcomeValue.Ready)]
    [InlineData(ExtractionOutcomeValue.Partial)]
    public void Success_is_terminal_and_records_outcome(ExtractionOutcomeValue outcome)
    {
        var job = CreateJob();
        var token = Guid.NewGuid();
        job.Lease("worker", token, Now, TimeSpan.FromSeconds(180));

        job.Succeed("worker", token, outcome, Now.AddSeconds(2));

        Assert.Equal(ExtractionJobStatus.Succeeded, job.Status);
        Assert.Equal(outcome, job.ResultOutcome);
        Assert.Equal(Now.AddSeconds(2), job.CompletedAtUtc);
        Assert.Null(job.LeaseToken);
        Assert.Throws<InvalidOperationException>(() =>
            job.Cancel("request_cancelled", "Cancelled.", Now.AddSeconds(3)));
    }

    [Fact]
    public void Cancellation_invalidates_active_lease_and_stale_completion_is_rejected()
    {
        var job = CreateJob();
        var token = Guid.NewGuid();
        job.Lease("worker", token, Now, TimeSpan.FromSeconds(180));

        job.Cancel("request_cancelled", "Cancelled.", Now.AddSeconds(1));

        Assert.Equal(ExtractionJobStatus.Cancelled, job.Status);
        Assert.Null(job.LeaseToken);
        Assert.Equal("request_cancelled", job.LastErrorCode);
        Assert.Throws<InvalidOperationException>(() => job.Succeed("worker", token,
            ExtractionOutcomeValue.Ready, Now.AddSeconds(2)));
    }

    private static ExtractionJob CreateJob() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), Now);
}
