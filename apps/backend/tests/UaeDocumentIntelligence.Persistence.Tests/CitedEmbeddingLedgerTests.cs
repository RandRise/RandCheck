using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    private static ReserveCitedGeneration EmbeddingSpendCommand(byte[]? bytes = null) =>
        (bytes is null ? SpendCommand() : ArtifactCommand(bytes)) with
        {
            Rate = new("test-only", "fake-embedding", "1", "test-input-rate",
                DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1), .02m, 0m),
            MaximumOutputTokens = 0, Workload = CitedSpendWorkload.Embedding
        };

    [Fact]
    public async Task Embedding_ledger_record_cannot_be_replayed_as_an_answer()
    {
        var graph = await SeedCitedGraphAsync();
        var runId = await GenerationRun(graph);
        await using var services = CitedPilotServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        var bytes = PreparationArtifact();
        var command = EmbeddingSpendCommand(bytes) with { OperationId = runId };
        await services.GetRequiredService<ICitedGenerationJournal>().ReservePreparedAsync(command, bytes, true, default);
        var client = new AnswerReply();
        await SpendDenied("reservation_key_conflict", () => Coordinator(services, client).ProposeAsync(runId, default));
        Assert.Equal(0, client.Preparations);
        Assert.Equal(0, client.Calls);
        Assert.Equal("Reserved", await ScalarAsync<string>("SELECT state FROM cited_spend_reservations"));
    }

    [Fact]
    public async Task Embedding_ledger_settles_in_same_budget_and_restarts_with_exact_journal_replay()
    {
        var seed = await SeedGraphAsync();
        var bytes = PreparationArtifact();
        var outcome = "{\"status\":\"invalid_vectors\",\"prompt_tokens\":100}"u8.ToArray();
        var command = EmbeddingSpendCommand(bytes);
        CitedSpendView embedding;
        await using (var services = CitedPilotServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1"))
        {
            var ledger = services.GetRequiredService<ICitedGenerationLedger>();
            var journal = services.GetRequiredService<ICitedGenerationJournal>();
            var generation = await ledger.ReserveAsync(SpendCommand(), true, default);
            Assert.True(await ledger.TryMarkDispatchedAsync(generation.Id, true, default));
            await ledger.SettleAsync(generation.Id, .001m, "test-generation-usage", default);
            embedding = await journal.ReservePreparedAsync(command, bytes, true, default);
            Assert.Equal(.000160m, embedding.ReservedUsd);
            Assert.Equal(CitedSpendWorkload.Embedding, embedding.Workload);
            Assert.True(await ledger.TryMarkDispatchedAsync(embedding.Id, true, default));
            Assert.False(await ledger.TryMarkDispatchedAsync(embedding.Id, true, default));
            // Vector usability is separate from trusted usage settlement.
            await journal.RecordOutcomeAsync(embedding.Id, outcome, .000002m, "test-embedding-usage", false, default);
        }
        await using var restarted = CitedPilotServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var replayJournal = restarted.GetRequiredService<ICitedGenerationJournal>();
        var replayLedger = restarted.GetRequiredService<ICitedGenerationLedger>();
        var replay = await replayJournal.ReservePreparedAsync(command, bytes, true, default);
        Assert.True(replay.Replayed);
        Assert.Equal(embedding.Id, replay.Id);
        Assert.Equal(CitedSpendWorkload.Embedding, replay.Workload);
        Assert.Equal(CitedSpendState.Settled, replay.State);
        await replayJournal.RecordOutcomeAsync(embedding.Id, outcome, .000002m, "test-embedding-usage", false, default);
        var saved = (await replayJournal.GetSavedAsync(command.OperationId, default))!;
        Assert.Equal(bytes, saved.PreparationUtf8);
        Assert.Equal(outcome, saved.OutcomeUtf8);
        Assert.False(await replayLedger.TryMarkDispatchedAsync(embedding.Id, true, default));
        Assert.Equal(.001002m, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_pilot_budgets"));
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM cited_generation_artifacts"));
        await SpendDenied("reservation_key_conflict", () => replayLedger.ReserveAsync(command with
            { Workload = CitedSpendWorkload.Generation, MaximumOutputTokens = 4000,
                Rate = command.Rate with { MaximumOutputUsdPerMillion = .4m } }, true, default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Embedding_ledger_unknown_or_overrun_usage_blocks_both_workloads(bool overrun)
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedPilotServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var journal = services.GetRequiredService<ICitedGenerationJournal>();
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        var bytes = PreparationArtifact();
        var command = EmbeddingSpendCommand(bytes);
        var spend = await journal.ReservePreparedAsync(command, bytes, true, default);
        await ledger.TryMarkDispatchedAsync(spend.Id, true, default);
        await journal.RecordOutcomeAsync(spend.Id, OutcomeArtifact(), overrun ? .000161m : null,
            overrun ? "test-overrun" : null, !overrun, default);
        Assert.Equal(overrun ? CitedSpendState.Settled : CitedSpendState.Uncertain,
            (await ledger.GetAsync(spend.Id, default)).State);
        Assert.True(await ScalarAsync<bool>("SELECT requires_reconciliation FROM cited_pilot_budgets"));
        Assert.Equal(overrun ? .000161m : 0m, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
        await SpendDenied("budget_reconciliation_required", () => ledger.ReserveAsync(SpendCommand(), true, default));
        await SpendDenied("budget_reconciliation_required", () => ledger.ReserveAsync(EmbeddingSpendCommand(), true, default));
        Assert.False(await ledger.TryMarkDispatchedAsync(spend.Id, true, default));
    }

    [Fact]
    public async Task Embedding_ledger_and_generation_concurrently_share_one_slot_across_organizations()
    {
        var seed = await SeedGraphAsync();
        await using var a = CitedPilotServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        await using var b = CitedPilotServices(seed.OrganizationB, seed.ActorB, "http://127.0.0.1:1");
        async Task<bool> Attempt(IServiceProvider services, ReserveCitedGeneration command)
        {
            try { await services.GetRequiredService<ICitedGenerationLedger>().ReserveAsync(command, true, default); return true; }
            catch (CitedWorkflowException error) { Assert.Equal("budget_reconciliation_required", error.Code); return false; }
        }
        var results = await Task.WhenAll(Attempt(a, EmbeddingSpendCommand()), Attempt(b, SpendCommand()));
        Assert.Single(results.Where(x => x));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_reservations"));
    }

    [Fact]
    public async Task Embedding_ledger_rejects_wrong_token_shape_unknown_workload_and_SQL_workload_changes()
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedPilotServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        await SpendDenied("invalid_reservation_request", () => ledger.ReserveAsync(EmbeddingSpendCommand() with
            { Workload = (CitedSpendWorkload)99 }, true, default));
        await SpendDenied("embedding_token_limit", () => ledger.ReserveAsync(EmbeddingSpendCommand() with
            { MaximumOutputTokens = 1 }, true, default));
        await SpendDenied("invalid_budget_configuration", () => ledger.ReserveAsync(EmbeddingSpendCommand() with
            { Rate = SpendCommand().Rate }, true, default));
        var spend = await ledger.ReserveAsync(EmbeddingSpendCommand(), true, default);
        Assert.Equal(0, await ScalarAsync<int>("SELECT maximum_output_tokens FROM cited_spend_reservations"));
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation,
            "UPDATE cited_spend_reservations SET maximum_output_tokens=1");
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation,
            "UPDATE cited_spend_reservations SET workload='Generation', maximum_output_tokens=4000");
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation,
            "UPDATE cited_spend_reservations SET workload='Other'");
        Assert.Equal(CitedSpendWorkload.Embedding, (await ledger.GetAsync(spend.Id, default)).Workload);
        // Downgrading cannot erase even a cancelled embedding accounting record.
        await ledger.CancelBeforeDispatchAsync(spend.Id, default);
        await using var db = _database.CreateContext();
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            db.GetService<IMigrator>().MigrateAsync("20260909123847_AddCitedGenerationArtifacts"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(13, (await db.Database.GetAppliedMigrationsAsync()).Count());
        Assert.Equal("Embedding", await ScalarAsync<string>("SELECT workload FROM cited_spend_reservations"));
    }

    [Fact]
    public async Task Embedding_ledger_scope_and_rate_expiry_remain_enforced()
    {
        var seed = await SeedGraphAsync();
        await using var a = CitedPilotServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        await using var b = CitedPilotServices(seed.OrganizationB, seed.ActorB, "http://127.0.0.1:1");
        var ledger = a.GetRequiredService<ICitedGenerationLedger>();
        var bytes = PreparationArtifact();
        var command = EmbeddingSpendCommand(bytes);
        var spend = await a.GetRequiredService<ICitedGenerationJournal>().ReservePreparedAsync(command, bytes, true, default);
        Assert.Null(await b.GetRequiredService<ICitedGenerationJournal>().GetSavedAsync(command.OperationId, default));
        await SpendDenied("reservation_not_found", () => b.GetRequiredService<ICitedGenerationLedger>().GetAsync(spend.Id, default));
        await ExecuteAsync("UPDATE cited_spend_reservations SET rate_valid_until_utc=clock_timestamp()-interval '1 second'");
        await SpendDenied("rate_or_model_expired", () => ledger.TryMarkDispatchedAsync(spend.Id, true, default));
        await ExecuteAsync("UPDATE actors SET is_active=false WHERE id=@id", ("id", seed.ActorA));
        await SpendDenied("reservation_not_found", () => ledger.GetAsync(spend.Id, default));
        Assert.Equal("Reserved", await ScalarAsync<string>("SELECT state FROM cited_spend_reservations"));
    }

    [Fact]
    public async Task Embedding_migration_preserves_legacy_generation_fingerprint_and_replay()
    {
        var seed = await SeedGraphAsync();
        await using var db = _database.CreateContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260909123847_AddCitedGenerationArtifacts");
        var command = SpendCommand();
        var legacyBytes = JsonSerializer.SerializeToUtf8Bytes(new { command.OperationId, command.RequestSha256,
            command.Rate, command.MaximumInputTokens, command.MaximumOutputTokens });
        var fingerprint = Convert.ToHexString(SHA256.HashData(legacyBytes)).ToLowerInvariant();
        var id = Guid.NewGuid();
        await ExecuteAsync("""
            INSERT INTO cited_spend_reservations (id,budget_id,organization_id,actor_id,operation_id,
                fingerprint,request_sha256,provider,model,model_version,rate_version,rate_snapshot_json,
                rate_valid_until_utc,maximum_input_tokens,maximum_output_tokens,reserved_usd,state,created_at_utc)
            VALUES (@id,1,@org,@actor,@op,@fp,@sha,@provider,@model,@version,@rate,CAST(@json AS jsonb),
                @until,8000,4000,.002,'Reserved',clock_timestamp())
            """, ("id", id), ("org", seed.OrganizationA), ("actor", seed.ActorA), ("op", command.OperationId),
            ("fp", fingerprint), ("sha", command.RequestSha256), ("provider", command.Rate.Provider),
            ("model", command.Rate.Model), ("version", command.Rate.ModelVersion), ("rate", command.Rate.RateVersion),
            ("json", JsonSerializer.Serialize(command.Rate)), ("until", command.Rate.ValidUntilUtc));
        await migrator.MigrateAsync();
        Assert.Equal(fingerprint, await ScalarAsync<string>("SELECT fingerprint FROM cited_spend_reservations"));
        Assert.Equal("Generation", await ScalarAsync<string>("SELECT workload FROM cited_spend_reservations"));
        await using var services = CitedPilotServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var replay = await services.GetRequiredService<ICitedGenerationLedger>().ReserveAsync(command, true, default);
        Assert.True(replay.Replayed);
        Assert.Equal(id, replay.Id);
        Assert.Equal(CitedSpendWorkload.Generation, replay.Workload);
        Assert.Equal(.002m, replay.ReservedUsd);
    }
}
