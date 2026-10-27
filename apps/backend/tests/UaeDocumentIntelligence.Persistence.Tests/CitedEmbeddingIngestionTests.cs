using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Tests.Fixtures;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    private static CitedGenerationRate IngestionRate => new("openai", CitedEmbeddingStorage.Model, CitedEmbeddingStorage.Model,
        "test-only-mocked-ingestion", DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1), .02m, 0m);
    private static EmbeddingOperation IngestionOperation(CitedSeed g) => new(Guid.NewGuid(), "chunk", [g.Index.Id], [g.Chunk.Id]);
    private static CitedEmbeddingCoordinator Coordinator(IServiceProvider services, ICitedEmbeddingClient client, bool enabled = true) =>
        new(services.GetRequiredService<ICitedEmbeddingSources>(), client, services.GetRequiredService<ICitedGenerationLedger>(),
            services.GetRequiredService<ICitedGenerationJournal>(), services.GetRequiredService<ICitedEmbeddingStore>(), new(enabled, IngestionRate));

    [Fact]
    public async Task Embedding_ingestion_dispatches_once_stores_and_replays_with_provider_offline()
    {
        var g = await SeedCitedGraphAsync();
        var op = IngestionOperation(g);
        await using var services = CitedPilotServices(g.Seed.OrganizationA, g.Seed.ActorA, "http://127.0.0.1:1");
        var client = new EmbeddingClientFixture();
        var first = await Coordinator(services, client).RunAsync(op, default);
        Assert.Equal(new EmbeddingStorageResult(1, 0), first.Storage);
        Assert.False(first.Replayed);
        Assert.Equal(1, client.Calls);
        await using var restarted = CitedPilotServices(g.Seed.OrganizationA, g.Seed.ActorA, "http://127.0.0.1:1");
        var offline = new EmbeddingClientFixture("transport");
        var replay = await Coordinator(restarted, offline).RunAsync(op, default);
        Assert.True(replay.Replayed);
        Assert.Equal(new EmbeddingStorageResult(0,1), replay.Storage);
        // A different operation ID cannot buy already-stored chunk/profile vectors again.
        var duplicate = await Coordinator(restarted, offline).RunAsync(op with { OperationId = Guid.NewGuid() }, default);
        Assert.True(duplicate.Replayed);
        Assert.Null(duplicate.ReservationId);
        Assert.Equal(0, offline.Preparations);
        Assert.Equal(0, offline.Calls);
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_reservations"));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_chunk_embeddings"));
    }

    [Theory]
    [InlineData("invalid", "Settled", false)]
    [InlineData("mismatch", "Settled", true)]
    [InlineData("unknown", "Uncertain", true)]
    [InlineData("transport", "Uncertain", false)]
    public async Task Embedding_ingestion_failures_keep_spend_or_hold_and_never_redispatch(string mode, string state, bool freeze)
    {
        var g = await SeedCitedGraphAsync();
        var op = IngestionOperation(g);
        await using var services = CitedPilotServices(g.Seed.OrganizationA, g.Seed.ActorA, "http://127.0.0.1:1");
        var client = new EmbeddingClientFixture(mode);
        await Assert.ThrowsAsync<CitedWorkflowException>(() => Coordinator(services, client).RunAsync(op, default));
        Assert.Equal(state, await ScalarAsync<string>("SELECT state FROM cited_spend_reservations"));
        Assert.Equal(freeze, await ScalarAsync<bool>("SELECT requires_reconciliation FROM cited_pilot_budgets"));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_chunk_embeddings"));
        Assert.Equal(mode == "transport" ? 1 : 2, await ScalarAsync<long>("SELECT count(*) FROM cited_generation_artifacts"));
        await Assert.ThrowsAsync<CitedWorkflowException>(() => Coordinator(services, client).RunAsync(op, default));
        Assert.Equal(1, client.Calls);
        if (state == "Settled") Assert.True(await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets") > 0);
    }

    [Fact]
    public async Task Embedding_ingestion_source_retirement_during_preparation_prevents_reservation_and_dispatch()
    {
        var g = await SeedCitedGraphAsync();
        await using var services = CitedPilotServices(g.Seed.OrganizationA, g.Seed.ActorA, "http://127.0.0.1:1");
        var client = new EmbeddingClientFixture { AfterPrepare = () => ExecuteAsync(
            "UPDATE cited_document_indexes SET status='Quarantined' WHERE id=@id", ("id",g.Index.Id)) };
        await SpendDenied("embedding_source_ineligible", () => Coordinator(services, client).RunAsync(IngestionOperation(g), default));
        Assert.Equal(0, client.Calls);
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_reservations"));
    }

    [Fact]
    public async Task Embedding_ingestion_serializes_different_operation_ids_before_duplicate_paid_work()
    {
        var g = await SeedCitedGraphAsync();
        await using var a = CitedPilotServices(g.Seed.OrganizationA, g.Seed.ActorA, "http://127.0.0.1:1");
        await using var b = CitedPilotServices(g.Seed.OrganizationA, g.Seed.ActorA, "http://127.0.0.1:1");
        var ca = new EmbeddingClientFixture();
        var cb = new EmbeddingClientFixture();
        var result = await Task.WhenAll(Coordinator(a, ca).RunAsync(IngestionOperation(g), default),
            Coordinator(b, cb).RunAsync(IngestionOperation(g), default));
        Assert.Equal(1, ca.Calls + cb.Calls);
        Assert.Equal(1, result.Sum(x => x.Storage!.Inserted));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_reservations"));
    }

    [Fact]
    public async Task Embedding_ingestion_scope_and_default_disable_fail_before_private_source_transmission()
    {
        var g = await SeedCitedGraphAsync();
        var client = new EmbeddingClientFixture();
        await using var owner = CitedPilotServices(g.Seed.OrganizationA, g.Seed.ActorA, "http://127.0.0.1:1");
        await SpendDenied("embedding_disabled", () => Coordinator(owner, client, false).RunAsync(IngestionOperation(g), default));
        await using var foreign = CitedPilotServices(g.Seed.OrganizationB, g.Seed.ActorB, "http://127.0.0.1:1");
        await SpendDenied("embedding_source_ineligible", () => Coordinator(foreign, client).RunAsync(IngestionOperation(g), default));
        Assert.Equal(0, client.Preparations);
        Assert.Equal(0, client.Calls);
    }

    [Fact]
    public async Task Embedding_ingestion_query_requires_coverage_then_searches_without_indexing_question()
    {
        var g = await SeedCitedGraphAsync();
        await using var services = CitedPilotServices(g.Seed.OrganizationA, g.Seed.ActorA, "http://127.0.0.1:1");
        var client = new EmbeddingClientFixture();
        var query = new EmbeddingOperation(Guid.NewGuid(), "query", [g.Index.Id], [], "What is the invoice total?");
        await SpendDenied("embedding_corpus_incomplete", () => Coordinator(services, client).RunAsync(query, default));
        Assert.Equal(0, client.Preparations);
        await Coordinator(services, client).RunAsync(IngestionOperation(g), default);
        var result = await Coordinator(services, client).RunAsync(query, default);
        Assert.Equal(g.Chunk.Id, Assert.Single(result.Hits!).ChunkId);
        Assert.Null(result.Storage);
        Assert.True((await Coordinator(services, new EmbeddingClientFixture("transport")).RunAsync(query, default)).Replayed);
        Assert.Equal(2, client.Calls);
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_chunk_embeddings"));
    }

    private sealed class EmbeddingClientFixture(string mode = "vectors") : ICitedEmbeddingClient
    {
        private EmbeddingFixture? fixture;
        public int Preparations { get; private set; }
        public int Calls { get; private set; }
        public Func<Task>? AfterPrepare { get; init; }
        public async Task<byte[]> PrepareAsync(byte[] source, CancellationToken ct)
        {
            Preparations++;
            using var parsed = JsonDocument.Parse(source);
            var s = parsed.RootElement;
            var c = s.GetProperty("context");
            fixture = new(c.GetProperty("organizationId").GetGuid(), c.GetProperty("actorId").GetGuid(),
                c.GetProperty("indexIds").EnumerateArray().Select(x => x.GetGuid()).ToArray(),
                s.GetProperty("inputs").EnumerateArray().Select(x => (x.GetProperty("inputId").GetGuid(), x.GetProperty("text").GetString()!)).ToArray(),
                c.GetProperty("purpose").GetString()!, c.GetProperty("operationId").GetGuid());
            fixture.Dispatch["requestUtf8"] = Convert.ToBase64String(source);
            fixture.Dispatch["preparation"]!["sourceSha256"] = CitedEmbeddingStorage.Digest(source);
            fixture.Outcome["sourceSha256"] = CitedEmbeddingStorage.Digest(source);
            if (AfterPrepare is not null) await AfterPrepare();
            return Encoding.UTF8.GetBytes(fixture.Dispatch["preparation"]!.ToJsonString());
        }
        public Task<byte[]> ProposeAsync(byte[] dispatch, CancellationToken ct)
        {
            Calls++;
            if (mode == "transport") throw new CitedWorkflowException("test_transport_lost", "Synthetic transport lost.");
            var f = fixture!;
            if (mode == "invalid") { f.Raw["data"]!.AsArray().Clear(); f.Outcome["outcome"] = "invalid_vectors"; }
            if (mode == "mismatch")
            {
                f.Raw["usage"]!["prompt_tokens"] = f.Tokens+1;
                f.Raw["usage"]!["total_tokens"] = f.Tokens+1;
                f.Outcome["inputTokens"] = f.Tokens+1;
                f.Outcome["requiresReconciliation"] = true;
                f.Outcome["outcome"] = "usage_mismatch";
            }
            if (mode == "unknown") { f.Raw.Remove("usage"); f.Outcome["accountingKnown"] = false; f.Outcome["inputTokens"] = null; f.Outcome["requiresReconciliation"] = true; }
            f.SyncRaw();
            return Task.FromResult(f.OutcomeBytes);
        }
    }
}
