using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Tests;

public sealed class CitedRecoveryAssessmentTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-09T00:00:00Z");
    private static readonly Guid Operator = Guid.NewGuid();
    private static readonly CitedRecoverySnapshot Snapshot = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), new string('a', 64), "test-only", "fake-answer", "1", CitedSpendState.Dispatched, 2, .002m, null, Now.AddHours(-1));
    private static readonly CitedRecoveryEvidence Evidence = new(Guid.NewGuid(), "test:receipt-1", Snapshot.OrganizationId,
        Snapshot.ReservationId, Snapshot.OperationId, Snapshot.RequestSha256, Snapshot.Provider, Snapshot.Model,
        Snapshot.ModelVersion, CitedRecoveryEvidenceKind.FinalCost, "USD", .001m, true, Now, Now.AddHours(1));

    [Theory]
    [InlineData(CitedSpendState.Reserved, CitedRecoveryRecommendation.CancelReserved)]
    [InlineData(CitedSpendState.Dispatched, CitedRecoveryRecommendation.KeepHold)]
    [InlineData(CitedSpendState.Uncertain, CitedRecoveryRecommendation.KeepHold)]
    [InlineData(CitedSpendState.Cancelled, CitedRecoveryRecommendation.NoChange)]
    [InlineData(CitedSpendState.Settled, CitedRecoveryRecommendation.NoChange)]
    [InlineData(CitedSpendState.HeldUnknown, CitedRecoveryRecommendation.NoChange)]
    public void State_assessments_are_never_execution_or_redispatch_permissions(CitedSpendState state, CitedRecoveryRecommendation expected)
    {
        var snapshot = Snapshot with { State = state, ActualUsd = state == CitedSpendState.Settled ? .001m : null };
        var result = CitedRecoveryPolicy.Assess(snapshot, Operator, null, Now);
        Assert.Equal(expected, result.Recommendation);
        Assert.Equal(Operator, result.OperatorActorId);
        Assert.Equal(Snapshot.OriginalActorId, result.OriginalActorId);
        Assert.Equal(Snapshot.EventSequence, result.ExpectedEventSequence);
        Assert.False(result.Executable);
        Assert.False(result.MayClearSharedFreeze);
        Assert.False(result.MayRedispatch);
        Assert.Null(result.VerifiedActualUsd);
    }

    [Theory]
    [InlineData(CitedSpendState.Dispatched)]
    [InlineData(CitedSpendState.Uncertain)]
    public void Verified_cost_and_zero_charge_recommend_terminal_accounting_not_cancellation(CitedSpendState state)
    {
        var snapshot = Snapshot with { State = state };
        foreach (var evidence in new[] { Evidence, Evidence with { ActualUsd = 0, Kind = CitedRecoveryEvidenceKind.ConfirmedNoCharge } })
        {
            var result = CitedRecoveryPolicy.Assess(snapshot, Operator, evidence, Now);
            Assert.Equal(CitedRecoveryRecommendation.RecordVerifiedCost, result.Recommendation);
            Assert.Equal(evidence.ActualUsd, result.VerifiedActualUsd);
            Assert.Equal(evidence.EvidenceId, result.EvidenceId);
            Assert.False(result.Executable);
            Assert.False(result.MayRedispatch);
        }
    }

    [Fact]
    public void Overrun_is_not_clamped_or_authorized_to_clear_the_global_freeze()
    {
        var result = CitedRecoveryPolicy.Assess(Snapshot, Operator, Evidence with { ActualUsd = 9m }, Now);
        Assert.Equal(9m, result.VerifiedActualUsd);
        Assert.True(result.RequiresOverrunFreeze);
        Assert.False(result.MayClearSharedFreeze);
    }

    [Theory]
    [InlineData("organization")]
    [InlineData("reservation")]
    [InlineData("operation")]
    [InlineData("hash")]
    [InlineData("provider")]
    [InlineData("model")]
    [InlineData("version")]
    [InlineData("id")]
    [InlineData("reference")]
    [InlineData("currency")]
    [InlineData("negative")]
    [InlineData("precision")]
    [InlineData("overflow")]
    [InlineData("partial")]
    [InlineData("future")]
    [InlineData("before-reservation")]
    [InlineData("expired")]
    [InlineData("kind")]
    [InlineData("false-zero")]
    public void Mismatched_incomplete_expired_or_invalid_evidence_is_denied(string defect)
    {
        var evidence = defect switch
        {
            "organization" => Evidence with { OrganizationId = Guid.NewGuid() },
            "reservation" => Evidence with { ReservationId = Guid.NewGuid() },
            "operation" => Evidence with { OperationId = Guid.NewGuid() },
            "hash" => Evidence with { RequestSha256 = new string('b', 64) },
            "provider" => Evidence with { Provider = "other" }, "model" => Evidence with { Model = "other" },
            "version" => Evidence with { ModelVersion = "2" }, "id" => Evidence with { EvidenceId = Guid.Empty },
            "reference" => Evidence with { Reference = null! }, "currency" => Evidence with { Currency = "AED" },
            "negative" => Evidence with { ActualUsd = -1 }, "precision" => Evidence with { ActualUsd = .0000001m },
            "overflow" => Evidence with { ActualUsd = decimal.MaxValue }, "partial" => Evidence with { IsFinal = false },
            "future" => Evidence with { VerifiedAtUtc = Now.AddTicks(1) },
            "before-reservation" => Evidence with { VerifiedAtUtc = Snapshot.CreatedAtUtc.AddTicks(-1) },
            "expired" => Evidence with { ValidUntilUtc = Now }, "kind" => Evidence with { Kind = (CitedRecoveryEvidenceKind)999 },
            _ => Evidence with { Kind = CitedRecoveryEvidenceKind.ConfirmedNoCharge }
        };
        Assert.Equal("invalid_recovery_evidence", Assert.Throws<CitedWorkflowException>(() =>
            CitedRecoveryPolicy.Assess(Snapshot, Operator, evidence, Now)).Code);
    }

    [Fact]
    public void Terminal_or_never_dispatched_operations_do_not_accept_cost_evidence()
    {
        foreach (var state in new[] { CitedSpendState.Reserved, CitedSpendState.Settled, CitedSpendState.Cancelled })
            Assert.Throws<CitedWorkflowException>(() => CitedRecoveryPolicy.Assess(Snapshot with
            { State = state, ActualUsd = state == CitedSpendState.Settled ? .001m : null }, Operator, Evidence, Now));
    }

    [Fact]
    public void Malformed_snapshots_fail_closed()
    {
        foreach (var snapshot in new[] { Snapshot with { EventSequence = 0 }, Snapshot with { State = (CitedSpendState)99 },
            Snapshot with { ActualUsd = 0 }, Snapshot with { State = CitedSpendState.Settled }, Snapshot with { ReservedUsd = 0 },
            Snapshot with { RequestSha256 = "bad" }, Snapshot with { CreatedAtUtc = Now.AddTicks(1) } })
            Assert.Equal("invalid_recovery_snapshot", Assert.Throws<CitedWorkflowException>(() =>
                CitedRecoveryPolicy.Assess(snapshot, Operator, null, Now)).Code);
    }

    [Fact]
    public async Task Default_authorization_denies_before_any_reader_or_verifier_call()
    {
        var ports = new FakePorts();
        await Denied("recovery_forbidden", Handler(ports, new DenyCitedRecoveryAuthorization()).AssessAsync(Snapshot.ReservationId, null, default));
        Assert.Equal(0, ports.Reads);
        Assert.Equal(0, ports.Verifications);
    }

    [Fact]
    public async Task Evidence_is_resolved_by_opaque_reference_and_privilege_rechecked()
    {
        var ports = new FakePorts();
        var result = await Handler(ports).AssessAsync(Snapshot.ReservationId, Evidence.Reference, default);
        Assert.Equal(2, ports.Authorizations);
        Assert.Equal(1, ports.Reads);
        Assert.Equal(1, ports.Verifications);
        Assert.Equal(Evidence.ActualUsd, result.VerifiedActualUsd);
        ports = new FakePorts { Revoke = true };
        await Denied("recovery_forbidden", Handler(ports).AssessAsync(Snapshot.ReservationId, Evidence.Reference, default));
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://private.invalid/receipt")]
    [InlineData("private\nsecret")]
    public async Task Bad_references_never_reach_evidence_lookup(string reference)
    {
        var ports = new FakePorts();
        await Denied("invalid_recovery_request", Handler(ports).AssessAsync(Snapshot.ReservationId, reference, default));
        Assert.Equal(0, ports.Reads);
        Assert.Equal(0, ports.Verifications);
    }

    [Fact]
    public async Task Foreign_snapshot_and_unavailable_or_wrong_reference_evidence_fail_closed()
    {
        var ports = new FakePorts { ReturnedSnapshot = Snapshot with { OrganizationId = Guid.NewGuid() } };
        await Denied("recovery_not_found", Handler(ports).AssessAsync(Snapshot.ReservationId, Evidence.Reference, default));
        Assert.Equal(0, ports.Verifications);
        foreach (var evidence in new[] { null, Evidence with { Reference = "different" } })
        {
            ports = new FakePorts { ReturnedEvidence = evidence };
            await Denied("recovery_evidence_unavailable", Handler(ports).AssessAsync(Snapshot.ReservationId, Evidence.Reference, default));
        }
    }

    [Theory]
    [InlineData("authorization")]
    [InlineData("reader")]
    [InlineData("verifier")]
    public async Task Dependency_errors_never_echo_private_messages_even_workflow_exceptions(string fault)
    {
        var ports = new FakePorts { Fault = fault };
        var error = await Denied("recovery_unavailable", Handler(ports).AssessAsync(Snapshot.ReservationId, Evidence.Reference, default));
        Assert.DoesNotContain("private", error.ToString());
        Assert.Null(error.InnerException);
    }

    [Fact]
    public async Task Caller_cancellation_does_not_start_assessment()
    {
        var ports = new FakePorts();
        using var ct = new CancellationTokenSource();
        ct.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Handler(ports).AssessAsync(Snapshot.ReservationId, null, ct.Token));
        Assert.Equal(0, ports.Authorizations);
    }

    [Fact]
    public async Task Deadline_bounds_a_verifier_that_ignores_cancellation()
    {
        var ports = new FakePorts { StallVerifier = true };
        try
        {
            await Denied("recovery_unavailable", Handler(ports).AssessAsync(Snapshot.ReservationId,
                Evidence.Reference, default).WaitAsync(TimeSpan.FromSeconds(15)));
            Assert.Equal(1, ports.Authorizations);
        }
        finally { ports.Pending.TrySetResult(null); }
    }

    [Fact]
    public async Task Caller_cancellation_during_evidence_lookup_does_not_return_an_assessment()
    {
        var ports = new FakePorts { StallVerifier = true };
        using var ct = new CancellationTokenSource();
        var task = Handler(ports).AssessAsync(Snapshot.ReservationId, Evidence.Reference, ct.Token);
        Assert.Equal(1, ports.Verifications);
        ct.Cancel();
        try { await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(TimeSpan.FromSeconds(2))); }
        finally { ports.Pending.TrySetResult(null); }
    }

    private static AssessCitedRecovery Handler(FakePorts ports, ICitedRecoveryAuthorization? authorization = null) =>
        new(authorization ?? ports, ports, ports, new Context(Snapshot.OrganizationId, Operator), new TestClock());
    private static async Task<CitedWorkflowException> Denied(string code, Task<CitedRecoveryAssessment> task)
    {
        var error = await Assert.ThrowsAsync<CitedWorkflowException>(() => task);
        Assert.Equal(code, error.Code);
        return error;
    }
    private sealed record Context(Guid OrganizationId, Guid ActorId) : IExecutionContext;
    private sealed class TestClock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class FakePorts : ICitedRecoveryAuthorization, ICitedRecoveryReader, ICitedRecoveryEvidenceVerifier
    {
        public int Authorizations, Reads, Verifications;
        public bool Revoke;
        public bool StallVerifier;
        public TaskCompletionSource<CitedRecoveryEvidence?> Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? Fault;
        public CitedRecoverySnapshot? ReturnedSnapshot = Snapshot;
        public CitedRecoveryEvidence? ReturnedEvidence = Evidence;
        public Task<bool> CanAssessAsync(Guid organizationId, Guid operatorActorId, CancellationToken ct)
        {
            Authorizations++;
            Fail("authorization");
            Assert.Equal(Snapshot.OrganizationId, organizationId);
            Assert.Equal(Operator, operatorActorId);
            return Task.FromResult(!Revoke || Authorizations == 1);
        }
        public Task<CitedRecoverySnapshot?> ReadAsync(Guid organizationId, Guid operatorActorId, Guid reservationId, CancellationToken ct)
        { Reads++; Fail("reader"); return Task.FromResult(ReturnedSnapshot); }
        public Task<CitedRecoveryEvidence?> VerifyAsync(CitedRecoverySnapshot snapshot, string reference, CancellationToken ct)
        { Verifications++; Fail("verifier"); return StallVerifier ? Pending.Task : Task.FromResult(ReturnedEvidence); }
        private void Fail(string stage) { if (Fault == stage) throw new CitedWorkflowException("private", "private downstream details"); }
    }
}
