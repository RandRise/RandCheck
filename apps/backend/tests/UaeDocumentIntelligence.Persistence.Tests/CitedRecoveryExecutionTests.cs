using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public async Task Recovery_execute_default_denies_active_requester_without_changing_the_ledger()
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var r = await services.GetRequiredService<ICitedGenerationLedger>().ReserveAsync(SpendCommand(), true, default);
        await SpendDenied("recovery_forbidden", () => services.GetRequiredService<ICitedRecoveryExecutor>().ExecuteAsync(CancelRecovery(r.Id), default));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_recovery_audits"));
        Assert.Equal("Reserved", await ScalarAsync<string>("SELECT state FROM cited_spend_reservations"));
    }

    [Fact]
    public async Task Recovery_execute_preserves_original_actor_and_attributes_operator_with_stable_replay()
    {
        var seed = await SeedGraphAsync();
        await using var owner = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var r = await owner.GetRequiredService<ICitedGenerationLedger>().ReserveAsync(SpendCommand(), true, default);
        var operatorId = await RecoveryOperator(seed.OrganizationA);
        await ExecuteAsync("UPDATE actors SET is_active=false WHERE id=@id", ("id", seed.ActorA));
        await using var services = ExecutionServices(seed.OrganizationA, operatorId);
        var executor = services.GetRequiredService<ICitedRecoveryExecutor>();
        var command = CancelRecovery(r.Id);
        var result = await executor.ExecuteAsync(command, default);
        Assert.False(result.Replayed);
        Assert.Equal(CitedSpendState.Cancelled, result.State);
        Assert.Equal(operatorId, result.OperatorActorId);
        Assert.Equal(seed.ActorA, await ScalarAsync<Guid>("SELECT actor_id FROM cited_spend_reservations"));
        Assert.Equal(seed.ActorA, await ScalarAsync<Guid>("SELECT original_actor_id FROM cited_recovery_audits"));
        Assert.Equal(operatorId, await ScalarAsync<Guid>("SELECT actor_id FROM cited_spend_events WHERE sequence=2"));
        Assert.Equal(result.AuditId, (await executor.ExecuteAsync(command, default)).AuditId);
        await SpendDenied("recovery_key_conflict", () => executor.ExecuteAsync(command with { ExpectedEventSequence = 2 }, default));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_recovery_audits"));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(.001, false)]
    [InlineData(.003, true)]
    public async Task Recovery_execute_final_cost_is_exact_terminal_and_never_clears_existing_freeze(double cost, bool overrun)
    {
        var seed = await SeedGraphAsync();
        await using var services = ExecutionServices(seed.OrganizationA, seed.ActorA, new ExecutionVerifier((decimal)cost));
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        var r = await ledger.ReserveAsync(SpendCommand(), true, default);
        Assert.True(await ledger.TryMarkDispatchedAsync(r.Id, true, default));
        await ledger.MarkUncertainAsync(r.Id, default);
        if (cost == 0) await ExecuteAsync("UPDATE cited_pilot_budgets SET requires_reconciliation=true");
        var command = SettleRecovery(r.Id) with { ExpectedState = CitedSpendState.Uncertain, ExpectedEventSequence = 3 };
        var result = await services.GetRequiredService<ICitedRecoveryExecutor>().ExecuteAsync(command, default);
        Assert.Equal((decimal)cost, result.ActualUsd);
        Assert.Equal((decimal)cost, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
        Assert.Equal(overrun || cost == 0, await ScalarAsync<bool>("SELECT requires_reconciliation FROM cited_pilot_budgets"));
        Assert.False(await ledger.TryMarkDispatchedAsync(r.Id, true, default));
        // A completed request is replayed even if the proof adapter is now unavailable.
        await using var restarted = ExecutionServices(seed.OrganizationA, seed.ActorA);
        var replay = await restarted.GetRequiredService<ICitedRecoveryExecutor>().ExecuteAsync(command, default);
        Assert.True(replay.Replayed);
        Assert.Equal(result.AuditId, replay.AuditId);
        Assert.Equal(4, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_events"));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_recovery_audits"));
    }

    [Fact]
    public async Task Recovery_execute_duplicate_operators_share_one_committed_outcome()
    {
        var seed = await SeedGraphAsync();
        await using var owner = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var r = await owner.GetRequiredService<ICitedGenerationLedger>().ReserveAsync(SpendCommand(), true, default);
        var other = await RecoveryOperator(seed.OrganizationA);
        var command = CancelRecovery(r.Id);
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(async i =>
        {
            await using var services = ExecutionServices(seed.OrganizationA, i % 2 == 0 ? seed.ActorA : other);
            return await services.GetRequiredService<ICitedRecoveryExecutor>().ExecuteAsync(command, default);
        }));
        Assert.Equal(1, results.Count(x => !x.Replayed));
        Assert.Single(results.Select(x => x.AuditId).Distinct());
        Assert.Single(results.Select(x => x.OperatorActorId).Distinct());
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_events"));
    }

    [Fact]
    public async Task Recovery_execute_dispatch_and_cancellation_have_only_one_winner()
    {
        var seed = await SeedGraphAsync();
        await using var services = ExecutionServices(seed.OrganizationA, seed.ActorA);
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        var r = await ledger.ReserveAsync(SpendCommand(), true, default);
        var executor = services.GetRequiredService<ICitedRecoveryExecutor>();
        var dispatch = ledger.TryMarkDispatchedAsync(r.Id, true, default);
        var cancel = Attempt();
        await Task.WhenAll(dispatch, cancel);
        Assert.NotEqual(await dispatch, await cancel);
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_events"));
        Assert.Equal(await cancel ? 1 : 0, await ScalarAsync<long>("SELECT count(*) FROM cited_recovery_audits"));
        async Task<bool> Attempt()
        {
            try { await executor.ExecuteAsync(CancelRecovery(r.Id), default); return true; }
            catch (CitedWorkflowException e) when (e.Code == "recovery_stale") { return false; }
        }
    }

    [Theory]
    [InlineData("unavailable")]
    [InlineData("expired")]
    [InlineData("partial")]
    [InlineData("swapped")]
    [InlineData("fault")]
    public async Task Recovery_execute_untrusted_or_failed_evidence_never_releases_hold(string defect)
    {
        var seed = await SeedGraphAsync();
        var verifier = new ExecutionVerifier(.001m) { Defect = defect };
        await using var services = ExecutionServices(seed.OrganizationA, seed.ActorA, verifier);
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        var r = await ledger.ReserveAsync(SpendCommand(), true, default);
        Assert.True(await ledger.TryMarkDispatchedAsync(r.Id, true, default));
        var error = await Assert.ThrowsAsync<CitedWorkflowException>(() => services.GetRequiredService<ICitedRecoveryExecutor>()
            .ExecuteAsync(SettleRecovery(r.Id), default));
        Assert.DoesNotContain("private", error.ToString());
        Assert.Null(error.InnerException);
        Assert.Equal("Dispatched", await ScalarAsync<string>("SELECT state FROM cited_spend_reservations"));
        Assert.Equal(0m, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_recovery_audits"));
    }

    [Fact]
    public async Task Recovery_execute_foreign_inactive_and_revoked_operators_cannot_mutate()
    {
        var seed = await SeedGraphAsync();
        await using var owner = ExecutionServices(seed.OrganizationA, seed.ActorA);
        var r = await owner.GetRequiredService<ICitedGenerationLedger>().ReserveAsync(SpendCommand(), true, default);
        foreach (var identity in new[] { (seed.OrganizationB, seed.ActorB), (seed.OrganizationA, seed.ActorB) })
        {
            await using var foreign = ExecutionServices(identity.Item1, identity.Item2);
            await SpendDenied("recovery_not_found", () => foreign.GetRequiredService<ICitedRecoveryExecutor>().ExecuteAsync(CancelRecovery(r.Id), default));
        }
        await using var revoked = ExecutionServices(seed.OrganizationA, seed.ActorA, authorization: new ExecutionAuthorization { RevokeOnCall = 3 });
        await SpendDenied("recovery_forbidden", () => revoked.GetRequiredService<ICitedRecoveryExecutor>().ExecuteAsync(CancelRecovery(r.Id), default));
        await ExecuteAsync("UPDATE actors SET is_active=false WHERE id=@id", ("id", seed.ActorA));
        await SpendDenied("recovery_not_found", () => owner.GetRequiredService<ICitedRecoveryExecutor>().ExecuteAsync(CancelRecovery(r.Id), default));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_recovery_audits"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Recovery_execute_evidence_id_or_reference_cannot_be_consumed_again(bool sameId)
    {
        var seed = await SeedGraphAsync();
        var verifier = new ExecutionVerifier(.001m);
        await using var services = ExecutionServices(seed.OrganizationA, seed.ActorA, verifier);
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        var first = await ledger.ReserveAsync(SpendCommand(), true, default);
        Assert.True(await ledger.TryMarkDispatchedAsync(first.Id, true, default));
        var executor = services.GetRequiredService<ICitedRecoveryExecutor>();
        await executor.ExecuteAsync(SettleRecovery(first.Id), default);
        var second = await ledger.ReserveAsync(SpendCommand(), true, default);
        Assert.True(await ledger.TryMarkDispatchedAsync(second.Id, true, default));
        if (!sameId) verifier.EvidenceId = Guid.NewGuid();
        await SpendDenied("recovery_evidence_conflict", () => executor.ExecuteAsync(SettleRecovery(second.Id) with
        { EvidenceReference = sameId ? "test:other-alias" : "test:final-usage" }, default));
        Assert.Equal(.001m, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_recovery_audits"));
    }

    [Fact]
    public async Task Recovery_execute_audit_failure_rolls_back_every_mutation_and_safe_retry_succeeds()
    {
        var seed = await SeedGraphAsync();
        await using var services = ExecutionServices(seed.OrganizationA, seed.ActorA, new ExecutionVerifier(.001m));
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        var r = await ledger.ReserveAsync(SpendCommand(), true, default);
        Assert.True(await ledger.TryMarkDispatchedAsync(r.Id, true, default));
        var command = SettleRecovery(r.Id);
        var executor = services.GetRequiredService<ICitedRecoveryExecutor>();
        await ExecuteAsync("ALTER TABLE cited_recovery_audits ADD CONSTRAINT test_recovery_audit_failure CHECK (false)");
        await SpendDenied("recovery_store_unavailable", () => executor.ExecuteAsync(command, default));
        Assert.Equal("Dispatched", await ScalarAsync<string>("SELECT state FROM cited_spend_reservations"));
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_events"));
        Assert.Equal(0m, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
        await ExecuteAsync("ALTER TABLE cited_recovery_audits DROP CONSTRAINT test_recovery_audit_failure");
        await executor.ExecuteAsync(command, default);
        Assert.Equal(.001m, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
    }

    [Fact]
    public async Task Recovery_execute_audit_is_immutable_and_migration_reapply_preserves_replay()
    {
        var seed = await SeedGraphAsync();
        await using var services = ExecutionServices(seed.OrganizationA, seed.ActorA);
        var r = await services.GetRequiredService<ICitedGenerationLedger>().ReserveAsync(SpendCommand(), true, default);
        var command = CancelRecovery(r.Id);
        var executor = services.GetRequiredService<ICitedRecoveryExecutor>();
        var result = await executor.ExecuteAsync(command, default);
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync("UPDATE cited_recovery_audits SET reason_code='changed'"));
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync("DELETE FROM cited_recovery_audits"));
        await using var db = _database.CreateContext();
        var audit = await db.CitedRecoveryAudits.SingleAsync();
        db.CitedRecoveryAudits.Remove(audit);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        await db.Database.MigrateAsync();
        Assert.Equal(result.AuditId, (await executor.ExecuteAsync(command, default)).AuditId);
        await Expected_schema_constraints_and_indexes_exist();
    }

    [Fact]
    public async Task Recovery_execute_stale_malformed_cancelled_and_live_commands_are_denied()
    {
        var seed = await SeedGraphAsync();
        await using var services = ExecutionServices(seed.OrganizationA, seed.ActorA);
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        var r = await ledger.ReserveAsync(SpendCommand(), true, default);
        var executor = services.GetRequiredService<ICitedRecoveryExecutor>();
        var command = CancelRecovery(r.Id);
        foreach (var bad in new[] { command with { RequestId = Guid.Empty }, command with { EvidenceReference = "https://private.invalid" },
            command with { Action = (CitedRecoveryAction)99 }, command with { ExpectedEventSequence = 0 } })
            await SpendDenied("invalid_recovery_request", () => executor.ExecuteAsync(bad, default));
        await SpendDenied("recovery_stale", () => executor.ExecuteAsync(command with { ExpectedEventSequence = 2 }, default));
        using var ct = new CancellationTokenSource();
        ct.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteAsync(command, ct.Token));
        await ledger.CancelBeforeDispatchAsync(r.Id, default);
        await SpendDenied("recovery_stale", () => executor.ExecuteAsync(command, default));
        var liveCommand = SpendCommand();
        var live = await ledger.ReserveAsync(liveCommand with { Rate = liveCommand.Rate with { Provider = "not-test" } }, true, default);
        await SpendDenied("recovery_live_not_ready", () => executor.ExecuteAsync(CancelRecovery(live.Id), default));
    }

    private static ExecuteCitedRecovery CancelRecovery(Guid id) => new(Guid.NewGuid(), id, CitedSpendState.Reserved, 1, CitedRecoveryAction.CancelReserved, null);
    private static ExecuteCitedRecovery SettleRecovery(Guid id) => new(Guid.NewGuid(), id, CitedSpendState.Dispatched, 2, CitedRecoveryAction.RecordVerifiedCost, "test:final-usage");
    private async Task<Guid> RecoveryOperator(Guid organizationId)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync("INSERT INTO actors (id,organization_id,kind,display_name,is_active,created_at_utc) VALUES (@id,@org,'System','Synthetic Recovery Operator',true,@now)",
            ("id", id), ("org", organizationId), ("now", DateTimeOffset.UtcNow));
        return id;
    }
    private ServiceProvider ExecutionServices(Guid organizationId, Guid actorId, ICitedRecoveryEvidenceVerifier? verifier = null,
        ICitedRecoveryExecutionAuthorization? authorization = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:Database"] = _database.TestConnectionString }).Build();
        var services = new ServiceCollection().AddInfrastructure(config);
        services.AddSingleton<IExecutionContext>(new CitedExecution(organizationId, actorId));
        services.AddSingleton(authorization ?? new ExecutionAuthorization());
        if (verifier is not null) services.AddSingleton(verifier);
        return services.BuildServiceProvider();
    }
    private sealed class ExecutionAuthorization : ICitedRecoveryExecutionAuthorization
    {
        public int RevokeOnCall = int.MaxValue;
        private int _calls;
        public Task<bool> CanExecuteAsync(Guid organizationId, Guid operatorActorId, CancellationToken ct) =>
            Task.FromResult(Interlocked.Increment(ref _calls) < RevokeOnCall);
    }
    private sealed class ExecutionVerifier(decimal cost) : ICitedRecoveryEvidenceVerifier
    {
        public Guid EvidenceId = Guid.NewGuid();
        public string? Defect;
        public Task<CitedRecoveryEvidence?> VerifyAsync(CitedRecoverySnapshot s, string reference, CancellationToken ct)
        {
            if (Defect == "fault") throw new CitedWorkflowException("private", "private provider details");
            if (Defect == "unavailable") return Task.FromResult<CitedRecoveryEvidence?>(null);
            var now = DateTimeOffset.UtcNow;
            var proof = new CitedRecoveryEvidence(EvidenceId, reference, s.OrganizationId, s.ReservationId, s.OperationId,
                s.RequestSha256, s.Provider, s.Model, s.ModelVersion, cost == 0 ? CitedRecoveryEvidenceKind.ConfirmedNoCharge : CitedRecoveryEvidenceKind.FinalCost,
                "USD", cost, true, now, now.AddHours(1));
            proof = Defect switch { "expired" => proof with { ValidUntilUtc = now.AddSeconds(-1) },
                "partial" => proof with { IsFinal = false }, "swapped" => proof with { RequestSha256 = new string('b', 64) }, _ => proof };
            return Task.FromResult<CitedRecoveryEvidence?>(proof);
        }
    }
}
