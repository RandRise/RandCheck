using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    private static ReserveCitedGeneration SpendCommand() => new(Guid.NewGuid(), new string('a', 64),
        new("test-only", "fake-answer", "1", "test-rate-1", DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1), 0.05m, 0.40m), 8000, 4000);

    [Fact]
    public async Task Ledger_disabled_calls_and_invalid_requests_do_not_write()
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedPilotServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        await SpendDenied("generation_disabled", () => ledger.ReserveAsync(SpendCommand(), false, default));
        await SpendDenied("generation_disabled", () => ledger.TryMarkDispatchedAsync(Guid.NewGuid(), false, default));
        await SpendDenied("invalid_reservation_request", () => ledger.ReserveAsync(SpendCommand() with { RequestSha256 = "private-value" }, true, default));
        await SpendDenied("generation_token_limit", () => ledger.ReserveAsync(SpendCommand() with { MaximumInputTokens = 8001 }, true, default));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_reservations"));
    }

    [Fact]
    public async Task Ledger_reservation_survives_new_service_provider_and_replay_never_revives_it()
    {
        var seed = await SeedGraphAsync();
        var command = SpendCommand();
        CitedSpendView first;
        await using (var original = CitedPilotServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1"))
            first = await original.GetRequiredService<ICitedGenerationLedger>().ReserveAsync(command, true, default);
        await using var restarted = CitedPilotServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var ledger = restarted.GetRequiredService<ICitedGenerationLedger>();
        var replay = await ledger.ReserveAsync(command, true, default);
        Assert.Equal(first.Id, replay.Id);
        Assert.True(replay.Replayed);
        Assert.Equal(0.002m, replay.ReservedUsd);
        await SpendDenied("reservation_key_conflict", () => ledger.ReserveAsync(command with { RequestSha256 = new string('b', 64) }, true, default));
        await ledger.CancelBeforeDispatchAsync(first.Id, default);
        Assert.Equal(CitedSpendState.Cancelled, (await ledger.ReserveAsync(command, true, default)).State);
        Assert.False(await ledger.TryMarkDispatchedAsync(first.Id, true, default));
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_events"));
    }

    [Fact]
    public async Task Ledger_concurrent_identical_reservations_share_one_durable_record()
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedPilotServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        var command = SpendCommand();
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => ledger.ReserveAsync(command, true, default)));
        Assert.Single(results.Select(x => x.Id).Distinct());
        Assert.Single(results.Where(x => !x.Replayed));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_events"));
    }

    [Fact]
    public async Task Ledger_concurrent_organizations_share_one_pending_slot_and_allowance()
    {
        var seed = await SeedGraphAsync();
        await using var a = CitedPilotServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        await using var b = CitedPilotServices(seed.OrganizationB, seed.ActorB, "http://127.0.0.1:1");
        async Task<bool> Attempt(IServiceProvider services)
        {
            try { await services.GetRequiredService<ICitedGenerationLedger>().ReserveAsync(SpendCommand(), true, default); return true; }
            catch (CitedWorkflowException error) { Assert.Equal("budget_reconciliation_required", error.Code); return false; }
        }
        var outcomes = await Task.WhenAll(Attempt(a), Attempt(b));
        Assert.Single(outcomes.Where(x => x));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_pilot_budgets"));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_reservations"));
    }

    [Fact]
    public async Task Ledger_dispatch_has_one_winner_and_uncertain_cost_remains_reserved()
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedPilotServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        var reservation = await ledger.ReserveAsync(SpendCommand(), true, default);
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => ledger.TryMarkDispatchedAsync(reservation.Id, true, default)));
        Assert.Single(outcomes.Where(x => x));
        await ledger.MarkUncertainAsync(reservation.Id, default);
        await ledger.MarkUncertainAsync(reservation.Id, default);
        await SpendDenied("invalid_spend_transition", () => ledger.CancelBeforeDispatchAsync(reservation.Id, default));
        await SpendDenied("budget_reconciliation_required", () => ledger.ReserveAsync(SpendCommand(), true, default));
        await using var restarted = CitedPilotServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var fresh = restarted.GetRequiredService<ICitedGenerationLedger>();
        Assert.Equal(CitedSpendState.Uncertain, (await fresh.GetAsync(reservation.Id, default)).State);
        await fresh.SettleAsync(reservation.Id, 0.001m, "verified-test-usage", default);
        await fresh.SettleAsync(reservation.Id, 0.001m, "verified-test-usage", default);
        await using (var migrated = _database.CreateContext())
            await migrated.Database.MigrateAsync(); // Reapplying migrations cannot reset spending.
        await SpendDenied("settlement_conflict", () => fresh.SettleAsync(reservation.Id, 0m, "verified-test-usage", default));
        Assert.Equal(0.001m, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
        Assert.Equal(4, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_events"));
        Assert.False(await fresh.TryMarkDispatchedAsync(reservation.Id, true, default));
        Assert.False((await fresh.ReserveAsync(SpendCommand(), true, default)).Replayed);
    }

    [Fact]
    public async Task Ledger_exact_ceiling_is_shared_after_settlement_across_organizations()
    {
        var seed = await SeedGraphAsync();
        // Controlled near-limit fixture; this is not a public balance-adjustment API.
        await ExecuteAsync("UPDATE cited_pilot_budgets SET settled_usd=7.998 WHERE id=1");
        await using var a = CitedPilotServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var ledger = a.GetRequiredService<ICitedGenerationLedger>();
        var reservation = await ledger.ReserveAsync(SpendCommand(), true, default);
        Assert.True(await ledger.TryMarkDispatchedAsync(reservation.Id, true, default));
        await ledger.SettleAsync(reservation.Id, 0.002m, "verified-test-usage", default);
        await using var b = CitedPilotServices(seed.OrganizationB, seed.ActorB, "http://127.0.0.1:1");
        await SpendDenied("pilot_budget_exhausted", () => b.GetRequiredService<ICitedGenerationLedger>().ReserveAsync(SpendCommand(), true, default));
        Assert.Equal(8m, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
    }

    [Fact]
    public async Task Ledger_foreign_organization_actor_and_inactive_actor_cannot_read_or_mutate()
    {
        var seed = await SeedGraphAsync();
        await using var a = CitedPilotServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var owner = a.GetRequiredService<ICitedGenerationLedger>();
        var reservation = await owner.ReserveAsync(SpendCommand(), true, default);
        var otherActor = Guid.NewGuid();
        await ExecuteAsync("INSERT INTO actors (id,organization_id,kind,display_name,is_active,created_at_utc) VALUES (@id,@org,'System','Other Actor',true,@now)",
            ("id", otherActor), ("org", seed.OrganizationA), ("now", DateTimeOffset.UtcNow));
        foreach (var identity in new[] { (seed.OrganizationB, seed.ActorB), (seed.OrganizationA, seed.ActorB), (seed.OrganizationA, otherActor) })
        {
            await using var foreign = CitedPilotServices(identity.Item1, identity.Item2, "http://127.0.0.1:1");
            var ledger = foreign.GetRequiredService<ICitedGenerationLedger>();
            await SpendDenied("reservation_not_found", () => ledger.GetAsync(reservation.Id, default));
            await SpendDenied("reservation_not_found", () => ledger.TryMarkDispatchedAsync(reservation.Id, true, default));
            await SpendDenied("reservation_not_found", () => ledger.CancelBeforeDispatchAsync(reservation.Id, default));
            await SpendDenied("reservation_not_found", () => ledger.SettleAsync(reservation.Id, 0m, "test-proof", default));
        }
        await ExecuteAsync("UPDATE actors SET is_active=false WHERE id=@id", ("id", seed.ActorA));
        await SpendDenied("reservation_not_found", () => owner.GetAsync(reservation.Id, default));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_events"));
    }

    [Fact]
    public async Task Ledger_actual_cost_above_quote_is_recorded_and_freezes_further_admission()
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedPilotServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        var reservation = await ledger.ReserveAsync(SpendCommand(), true, default);
        await ledger.TryMarkDispatchedAsync(reservation.Id, true, default);
        await ledger.SettleAsync(reservation.Id, 0.003m, "verified-overrun", default);
        Assert.Equal(0.003m, (await ledger.GetAsync(reservation.Id, default)).ActualUsd);
        Assert.True(await ScalarAsync<bool>("SELECT requires_reconciliation FROM cited_pilot_budgets"));
        await SpendDenied("budget_reconciliation_required", () => ledger.ReserveAsync(SpendCommand(), true, default));
    }

    [Fact]
    public async Task Ledger_expired_rates_fail_before_reservation_and_before_dispatch()
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedPilotServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        var command = SpendCommand();
        await SpendDenied("rate_or_model_expired", () => ledger.ReserveAsync(command with
            { Rate = command.Rate with { ValidUntilUtc = DateTimeOffset.UtcNow.AddMinutes(-1) } }, true, default));
        var reservation = await ledger.ReserveAsync(command, true, default);
        await ExecuteAsync("UPDATE cited_spend_reservations SET rate_valid_until_utc=clock_timestamp()-interval '1 second' WHERE id=@id", ("id", reservation.Id));
        await SpendDenied("rate_or_model_expired", () => ledger.TryMarkDispatchedAsync(reservation.Id, true, default));
        await ledger.CancelBeforeDispatchAsync(reservation.Id, default);
        Assert.Equal(CitedSpendState.Cancelled, (await ledger.GetAsync(reservation.Id, default)).State);
    }

    [Fact]
    public async Task Ledger_database_enforces_singleton_pending_uniqueness_and_audit_immutability()
    {
        var seed = await SeedGraphAsync();
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation,
            "INSERT INTO cited_pilot_budgets (id,ceiling_usd,settled_usd,requires_reconciliation) VALUES (2,8,0,false)");
        await using var services = CitedPilotServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var first = await services.GetRequiredService<ICitedGenerationLedger>().ReserveAsync(SpendCommand(), true, default);
        await using (var db = _database.CreateContext())
        {
            var r = await db.CitedSpendReservations.SingleAsync();
            db.CitedSpendReservations.Add(new(Guid.NewGuid(), seed.OrganizationB, seed.ActorB, Guid.NewGuid(),
                r.Fingerprint, r.RequestSha256, r.Provider, r.Model, r.ModelVersion, r.RateVersion,
                r.RateSnapshotJson, r.RateValidUntilUtc, r.MaximumInputTokens, r.MaximumOutputTokens, r.ReservedUsd, DateTimeOffset.UtcNow));
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
        }
        await using (var db = _database.CreateContext())
        {
            db.Remove(await db.CitedSpendEvents.SingleAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        Assert.Equal(first.Id, await ScalarAsync<Guid>("SELECT id FROM cited_spend_reservations"));
    }

    [Fact]
    public async Task Ledger_failed_event_write_rolls_back_dispatch_and_settlement_together()
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedPilotServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        var reservation = await ledger.ReserveAsync(SpendCommand(), true, default);
        await ExecuteAsync("ALTER TABLE cited_spend_events ADD CONSTRAINT test_reject_event CHECK (sequence=1)");
        await SpendDenied("spend_store_unavailable", () => ledger.TryMarkDispatchedAsync(reservation.Id, true, default));
        Assert.Equal(CitedSpendState.Reserved, (await ledger.GetAsync(reservation.Id, default)).State);
        await ExecuteAsync("ALTER TABLE cited_spend_events DROP CONSTRAINT test_reject_event");
        Assert.True(await ledger.TryMarkDispatchedAsync(reservation.Id, true, default));
        await ExecuteAsync("ALTER TABLE cited_spend_events ADD CONSTRAINT test_reject_event CHECK (sequence<=2)");
        await SpendDenied("spend_store_unavailable", () => ledger.SettleAsync(reservation.Id, 0.001m, "test-proof", default));
        Assert.Equal(CitedSpendState.Dispatched, (await ledger.GetAsync(reservation.Id, default)).State);
        Assert.Equal(0m, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
        await ExecuteAsync("ALTER TABLE cited_spend_events DROP CONSTRAINT test_reject_event");
    }

    [Fact]
    public async Task Ledger_invalid_settlement_and_precancelled_requests_do_not_release_holds()
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedPilotServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ledger.ReserveAsync(SpendCommand(), true, cancelled.Token));
        var reservation = await ledger.ReserveAsync(SpendCommand(), true, default);
        await SpendDenied("invalid_spend_transition", () => ledger.SettleAsync(reservation.Id, 0m, "test-proof", default));
        foreach (var amount in new[] { -1m, 0.0000001m, decimal.MaxValue })
            await SpendDenied("invalid_settlement", () => ledger.SettleAsync(reservation.Id, amount, "test-proof", default));
        await SpendDenied("invalid_settlement", () => ledger.SettleAsync(reservation.Id, 0m, "", default));
        Assert.Equal(CitedSpendState.Reserved, (await ledger.GetAsync(reservation.Id, default)).State);
    }

    private static async Task SpendDenied(string code, Func<Task> action)
    {
        var error = await Assert.ThrowsAsync<CitedWorkflowException>(action);
        Assert.Equal(code, error.Code);
        Assert.Null(error.InnerException);
    }
}
