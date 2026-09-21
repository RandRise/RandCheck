using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    private static byte[] PreparationArtifact() => JsonSerializer.SerializeToUtf8Bytes(new
        { schemaVersion = "test-journal-v1", request = "Synthetic invoice total 100.00", counter = "test-only" });
    private static byte[] OutcomeArtifact() => JsonSerializer.SerializeToUtf8Bytes(new
        { schemaVersion = "test-journal-v1", status = "completed", responseId = "test-response", output = "Synthetic answer" });
    private static ReserveCitedGeneration ArtifactCommand(byte[] bytes) => SpendCommand() with
        { RequestSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() };

    [Fact]
    public async Task Journal_preparation_and_reservation_commit_together_and_restart_replays_exact_bytes()
    {
        var seed = await SeedGraphAsync();
        var bytes = PreparationArtifact();
        var command = ArtifactCommand(bytes);
        CitedSpendView spend;
        await using (var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1"))
        {
            var journal = services.GetRequiredService<ICitedGenerationJournal>();
            spend = await journal.ReservePreparedAsync(command, bytes, true, default);
            Assert.Equal(bytes, (await journal.GetSavedAsync(command.OperationId, default))!.PreparationUtf8);
        }
        await using var restarted = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var replay = await restarted.GetRequiredService<ICitedGenerationJournal>().ReservePreparedAsync(command, bytes, true, default);
        Assert.Equal(spend.Id, replay.Id);
        Assert.True(replay.Replayed);
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_generation_artifacts"));
        await SpendDenied("reservation_key_conflict", () => restarted.GetRequiredService<ICitedGenerationJournal>()
            .ReservePreparedAsync(command with { MaximumInputTokens = command.MaximumInputTokens - 1 }, bytes, true, default));
        await Expected_schema_constraints_and_indexes_exist();
    }

    [Theory]
    [InlineData("zero", false)]
    [InlineData("normal", false)]
    [InlineData("overrun", false)]
    [InlineData("normal", true)]
    public async Task Journal_outcome_settlement_is_atomic_idempotent_and_preserves_freezes(string amountKind, bool freeze)
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var journal = services.GetRequiredService<ICitedGenerationJournal>();
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        var bytes = PreparationArtifact();
        var command = ArtifactCommand(bytes);
        var spend = await journal.ReservePreparedAsync(command, bytes, true, default);
        Assert.True(await ledger.TryMarkDispatchedAsync(spend.Id, true, default));
        var cost = amountKind == "zero" ? 0m : amountKind == "overrun" ? .01m : .001m;
        var outcome = OutcomeArtifact();
        await journal.RecordOutcomeAsync(spend.Id, outcome, cost, "test-usage", freeze, default);
        await journal.RecordOutcomeAsync(spend.Id, outcome, cost, "test-usage", freeze, default);
        Assert.Equal(cost, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
        Assert.Equal(freeze || cost > spend.ReservedUsd,
            await ScalarAsync<bool>("SELECT requires_reconciliation FROM cited_pilot_budgets"));
        Assert.Equal(3, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_events"));
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM cited_generation_artifacts"));
        Assert.False(await ledger.TryMarkDispatchedAsync(spend.Id, true, default));
        Assert.Equal(outcome, (await journal.GetSavedAsync(command.OperationId, default))!.OutcomeUtf8);
        await SpendDenied("generation_outcome_conflict", () => journal.RecordOutcomeAsync(spend.Id, outcome,
            cost + .000001m, "test-usage", freeze, default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Journal_unknown_usage_is_saved_without_releasing_hold(bool alreadyUncertain)
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var journal = services.GetRequiredService<ICitedGenerationJournal>();
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        var bytes = PreparationArtifact();
        var command = ArtifactCommand(bytes);
        var spend = await journal.ReservePreparedAsync(command, bytes, true, default);
        await ledger.TryMarkDispatchedAsync(spend.Id, true, default);
        if (alreadyUncertain) await ledger.MarkUncertainAsync(spend.Id, default);
        await journal.RecordOutcomeAsync(spend.Id, "{\"status\":\"unknown_usage\"}"u8.ToArray(), null, null, true, default);
        var saved = (await journal.GetSavedAsync(command.OperationId, default))!;
        Assert.Equal(CitedSpendState.Uncertain, saved.Spend.State);
        Assert.NotNull(saved.OutcomeUtf8);
        Assert.Equal(0m, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
        Assert.True(await ScalarAsync<bool>("SELECT requires_reconciliation FROM cited_pilot_budgets"));
        await SpendDenied("budget_reconciliation_required", () => ledger.ReserveAsync(SpendCommand(), true, default));
    }

    [Fact]
    public async Task Journal_failed_artifact_insert_rolls_back_reservation_and_outcome_accounting()
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var journal = services.GetRequiredService<ICitedGenerationJournal>();
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        var bytes = PreparationArtifact();
        var command = ArtifactCommand(bytes);
        await ExecuteAsync("ALTER TABLE cited_generation_artifacts ADD CONSTRAINT reject_artifact CHECK (false)");
        await SpendDenied("spend_store_unavailable", () => journal.ReservePreparedAsync(command, bytes, true, default));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_reservations"));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_events"));
        await ExecuteAsync("ALTER TABLE cited_generation_artifacts DROP CONSTRAINT reject_artifact");
        var spend = await journal.ReservePreparedAsync(command, bytes, true, default);
        await ledger.TryMarkDispatchedAsync(spend.Id, true, default);
        await ExecuteAsync("ALTER TABLE cited_generation_artifacts ADD CONSTRAINT reject_artifact CHECK (kind='Preparation')");
        await SpendDenied("spend_store_unavailable", () => journal.RecordOutcomeAsync(spend.Id, OutcomeArtifact(), .001m, "test-usage", false, default));
        Assert.Equal(CitedSpendState.Dispatched, (await ledger.GetAsync(spend.Id, default)).State);
        Assert.Equal(0m, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_events"));
        await ExecuteAsync("ALTER TABLE cited_generation_artifacts DROP CONSTRAINT reject_artifact");
        await journal.RecordOutcomeAsync(spend.Id, OutcomeArtifact(), .001m, "test-usage", false, default);
    }

    [Fact]
    public async Task Journal_foreign_and_inactive_actors_cannot_read_or_mutate_saved_content()
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var journal = services.GetRequiredService<ICitedGenerationJournal>();
        var bytes = PreparationArtifact();
        var command = ArtifactCommand(bytes);
        var spend = await journal.ReservePreparedAsync(command, bytes, true, default);
        await using var foreign = CitedServices(seed.OrganizationB, seed.ActorB, "http://127.0.0.1:1");
        var other = foreign.GetRequiredService<ICitedGenerationJournal>();
        Assert.Null(await other.GetSavedAsync(command.OperationId, default));
        await SpendDenied("reservation_not_found", () => other.RecordOutcomeAsync(spend.Id, OutcomeArtifact(), .001m, "test", false, default));
        // Direct inserts must not attach another actor/organization's payload to this reservation.
        var error = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            "INSERT INTO cited_generation_artifacts (id,organization_id,actor_id,reservation_id,kind,payload_utf8,payload_sha256,command_sha256,created_at_utc) " +
            "SELECT @id,@organization,@actor,reservation_id,'Outcome',payload_utf8,payload_sha256,command_sha256,created_at_utc FROM cited_generation_artifacts",
            ("id", Guid.NewGuid()), ("organization", seed.OrganizationB), ("actor", seed.ActorB)));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        await ExecuteAsync("UPDATE actors SET is_active=false WHERE id=@id", ("id", seed.ActorA));
        await SpendDenied("reservation_not_found", () => journal.GetSavedAsync(command.OperationId, default));
    }

    [Fact]
    public async Task Journal_database_and_ef_reject_artifact_edits_and_reapply_preserves_saved_bytes()
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var journal = services.GetRequiredService<ICitedGenerationJournal>();
        var bytes = PreparationArtifact();
        var command = ArtifactCommand(bytes);
        await journal.ReservePreparedAsync(command, bytes, true, default);
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync("UPDATE cited_generation_artifacts SET payload_utf8=payload_utf8"));
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync("DELETE FROM cited_generation_artifacts"));
        await using (var db = _database.CreateContext())
        {
            db.Remove(await db.CitedGenerationArtifacts.SingleAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        await using (var db = _database.CreateContext()) await db.Database.MigrateAsync();
        Assert.Equal(bytes, (await journal.GetSavedAsync(command.OperationId, default))!.PreparationUtf8);
    }

    [Fact]
    public async Task Journal_disabled_malformed_oversized_and_unbound_inputs_have_no_effect()
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var journal = services.GetRequiredService<ICitedGenerationJournal>();
        await SpendDenied("generation_disabled", () => journal.ReservePreparedAsync(ArtifactCommand(PreparationArtifact()), PreparationArtifact(), false, default));
        foreach (var bytes in new[] { Array.Empty<byte>(), "[]"u8.ToArray(), "{\"a\":1,\"a\":2}"u8.ToArray(), new byte[98305] })
            await SpendDenied("invalid_generation_artifact", () => journal.ReservePreparedAsync(ArtifactCommand(bytes), bytes, true, default));
        await SpendDenied("invalid_generation_artifact", () => journal.ReservePreparedAsync(SpendCommand(), PreparationArtifact(), true, default));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_reservations"));
    }

    [Fact]
    public async Task Journal_saved_coordinator_result_rechecks_source_and_never_calls_offline_client()
    {
        var graph = await SeedCitedGraphAsync();
        var runId = await GenerationRun(graph);
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        var client = new AnswerReply();
        await Coordinator(services, client).ProposeAsync(runId, default);
        client.ChangePreparation = _ => throw new InvalidOperationException("Should not remeasure");
        Assert.True((await Coordinator(services, client).ProposeAsync(runId, default)).Replayed);
        Assert.Equal(1, client.Calls);
        Assert.Equal(1, client.Preparations);
        await ExecuteAsync("UPDATE cited_document_indexes SET status='Quarantined' WHERE id=@id", ("id", graph.Index.Id));
        await SpendDenied("evidence_unavailable", () => Coordinator(services, client).ProposeAsync(runId, default));
        Assert.Equal(1, client.Calls);
    }
}
