using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public async Task Recovery_default_registration_denies_even_the_active_original_requester()
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var reservation = await services.GetRequiredService<ICitedGenerationLedger>().ReserveAsync(SpendCommand(), true, default);
        await SpendDenied("recovery_forbidden", () => services.GetRequiredService<AssessCitedRecovery>()
            .AssessAsync(reservation.Id, null, default));
        Assert.Equal("Reserved", await ScalarAsync<string>("SELECT state FROM cited_spend_reservations"));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_events"));
    }

    [Fact]
    public async Task Recovery_operator_is_separate_from_inactive_requester_and_reader_filters_foreign_scope()
    {
        var seed = await SeedGraphAsync();
        await using var owner = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var reservation = await owner.GetRequiredService<ICitedGenerationLedger>().ReserveAsync(SpendCommand(), true, default);
        var operatorId = Guid.NewGuid();
        await ExecuteAsync("INSERT INTO actors (id,organization_id,kind,display_name,is_active,created_at_utc) VALUES (@id,@org,'System','Test Recovery Operator',true,@now)",
            ("id", operatorId), ("org", seed.OrganizationA), ("now", DateTimeOffset.UtcNow));
        await ExecuteAsync("UPDATE actors SET is_active=false WHERE id=@id", ("id", seed.ActorA));
        await using var services = CitedServices(seed.OrganizationA, operatorId, "http://127.0.0.1:1");
        var reader = services.GetRequiredService<ICitedRecoveryReader>();
        var result = await RecoveryAssessor(services).AssessAsync(reservation.Id, null, default);
        Assert.Equal(operatorId, result.OperatorActorId);
        Assert.Equal(seed.ActorA, result.OriginalActorId);
        Assert.False(result.Executable);
        Assert.Null(await reader.ReadAsync(seed.OrganizationB, seed.ActorB, reservation.Id, default));
        Assert.Null(await reader.ReadAsync(seed.OrganizationA, seed.ActorB, reservation.Id, default));
        Assert.Null(await reader.ReadAsync(seed.OrganizationA, seed.ActorA, reservation.Id, default));
        Assert.Null(await reader.ReadAsync(seed.OrganizationA, operatorId, Guid.NewGuid(), default));
        await ExecuteAsync("UPDATE actors SET is_active=false WHERE id=@id", ("id", operatorId));
        await SpendDenied("recovery_not_found", () => RecoveryAssessor(services).AssessAsync(reservation.Id, null, default));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_events"));
    }

    [Fact]
    public async Task Recovery_verified_cost_preview_never_settles_or_clears_a_shared_freeze()
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        var reservation = await ledger.ReserveAsync(SpendCommand(), true, default);
        Assert.True(await ledger.TryMarkDispatchedAsync(reservation.Id, true, default));
        await ledger.MarkUncertainAsync(reservation.Id, default);
        await ExecuteAsync("UPDATE cited_pilot_budgets SET requires_reconciliation=true");
        var snapshot = (await services.GetRequiredService<ICitedRecoveryReader>().ReadAsync(seed.OrganizationA, seed.ActorA, reservation.Id, default))!;
        var now = DateTimeOffset.UtcNow;
        var evidence = new CitedRecoveryEvidence(Guid.NewGuid(), "test:recovery-proof", seed.OrganizationA,
            reservation.Id, snapshot.OperationId, snapshot.RequestSha256, snapshot.Provider, snapshot.Model, snapshot.ModelVersion,
            CitedRecoveryEvidenceKind.FinalCost, "USD", 9m, true, now, now.AddHours(1));
        var result = await RecoveryAssessor(services, new RecoveryTestVerifier(evidence)).AssessAsync(reservation.Id, evidence.Reference, default);
        Assert.Equal(CitedRecoveryRecommendation.RecordVerifiedCost, result.Recommendation);
        Assert.Equal(CitedSpendState.Uncertain, result.ExpectedState);
        Assert.Equal(3, result.ExpectedEventSequence);
        Assert.True(result.RequiresOverrunFreeze);
        Assert.False(result.MayClearSharedFreeze);
        Assert.Equal("Uncertain", await ScalarAsync<string>("SELECT state FROM cited_spend_reservations"));
        Assert.Equal(0m, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
        Assert.True(await ScalarAsync<bool>("SELECT requires_reconciliation FROM cited_pilot_budgets"));
        Assert.Equal(3, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_events"));
        await Expected_schema_constraints_and_indexes_exist();
    }

    [Fact]
    public async Task Recovery_unknown_outcome_keeps_hold_and_default_verifier_cannot_invent_zero_cost()
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        var reservation = await ledger.ReserveAsync(SpendCommand(), true, default);
        Assert.True(await ledger.TryMarkDispatchedAsync(reservation.Id, true, default));
        var assessor = RecoveryAssessor(services);
        Assert.Equal(CitedRecoveryRecommendation.KeepHold, (await assessor.AssessAsync(reservation.Id, null, default)).Recommendation);
        await SpendDenied("recovery_evidence_unavailable", () => assessor.AssessAsync(reservation.Id, "missing:receipt", default));
        await SpendDenied("budget_reconciliation_required", () => ledger.ReserveAsync(SpendCommand(), true, default));
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_events"));
    }

    [Fact]
    public async Task Recovery_reserved_preview_is_not_authority_to_cancel_after_a_concurrent_dispatch()
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        var reservation = await ledger.ReserveAsync(SpendCommand(), true, default);
        var assessor = RecoveryAssessor(services);
        var before = await assessor.AssessAsync(reservation.Id, null, default);
        Assert.Equal(CitedRecoveryRecommendation.CancelReserved, before.Recommendation);
        Assert.True(await ledger.TryMarkDispatchedAsync(reservation.Id, true, default));
        await SpendDenied("invalid_spend_transition", () => ledger.CancelBeforeDispatchAsync(reservation.Id, default));
        var after = await assessor.AssessAsync(reservation.Id, null, default);
        Assert.Equal(CitedRecoveryRecommendation.KeepHold, after.Recommendation);
        Assert.NotEqual(before.ExpectedEventSequence, after.ExpectedEventSequence);
        Assert.False(before.Executable);
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_events"));
    }

    private static AssessCitedRecovery RecoveryAssessor(IServiceProvider services, ICitedRecoveryEvidenceVerifier? verifier = null) =>
        new(new RecoveryTestAuthorization(), services.GetRequiredService<ICitedRecoveryReader>(),
            verifier ?? services.GetRequiredService<ICitedRecoveryEvidenceVerifier>(),
            services.GetRequiredService<IExecutionContext>(), services.GetRequiredService<IClock>());
    private sealed class RecoveryTestAuthorization : ICitedRecoveryAuthorization
    {
        public Task<bool> CanAssessAsync(Guid organizationId, Guid operatorActorId, CancellationToken ct) => Task.FromResult(true);
    }
    private sealed class RecoveryTestVerifier(CitedRecoveryEvidence evidence) : ICitedRecoveryEvidenceVerifier
    {
        public Task<CitedRecoveryEvidence?> VerifyAsync(CitedRecoverySnapshot snapshot, string reference, CancellationToken ct) =>
            Task.FromResult<CitedRecoveryEvidence?>(evidence);
    }
}
