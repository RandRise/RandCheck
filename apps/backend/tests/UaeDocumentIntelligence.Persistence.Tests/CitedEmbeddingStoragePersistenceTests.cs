using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Tests.Fixtures;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    private static EmbeddingFixture StoredFixture(CitedSeed graph, string purpose = "chunk", Guid[]? indexes = null,
        string? text = null, Guid? input = null) => new(graph.Seed.OrganizationA, graph.Seed.ActorA,
            indexes ?? [graph.Index.Id], [(input ?? graph.Chunk.Id, text ?? graph.Chunk.Content)], purpose);

    private static async Task<CitedSpendView> SaveEmbeddingJournal(IServiceProvider services, EmbeddingFixture f,
        bool settle = true, decimal? cost = null)
    {
        var journal = services.GetRequiredService<ICitedGenerationJournal>();
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        var rate = new CitedGenerationRate("openai", CitedEmbeddingStorage.Model, CitedEmbeddingStorage.Model,
            "test-only-mocked-provider", DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1), .02m, 0m);
        var command = new ReserveCitedGeneration(f.Operation, CitedEmbeddingStorage.Digest(f.PreparationBytes),
            rate, f.Tokens, 0, CitedSpendWorkload.Embedding);
        var reserved = await journal.ReservePreparedAsync(command, f.PreparationBytes, true, default);
        if (settle)
        {
            Assert.True(await ledger.TryMarkDispatchedAsync(reserved.Id, true, default));
            await journal.RecordOutcomeAsync(reserved.Id, f.OutcomeBytes, cost ?? f.Cost,
                "test-mocked-embedding-response", false, default);
        }
        return reserved;
    }

    private async Task<(CitedDocumentIndex Index, DocumentChunk[] Chunks)> AdditionalEmbeddingIndex(CitedSeed g, int count = 1)
    {
        await using var db = _database.CreateContext();
        var doc = await db.Documents.SingleAsync(x => x.Id == g.Seed.DocumentA);
        var review = await db.ReviewSessions.SingleAsync(x => x.Id == g.Seed.SessionA);
        var page = await db.ExtractionPages.SingleAsync(x => x.Id == g.Seed.PageA);
        var index = new CitedDocumentIndex(Guid.NewGuid(), doc, review, "embedding-test", Guid.NewGuid().ToString(), DateTimeOffset.UtcNow);
        var chunks = Enumerable.Range(0, count).Select(i => index.AddPageChunk(Guid.NewGuid(), i, page, 0,
            System.Text.Encoding.UTF8.GetByteCount(page.CanonicalText))).ToArray();
        index.Publish();
        db.CitedDocumentIndexes.Add(index);
        await db.SaveChangesAsync();
        return (index, chunks);
    }

    [Fact]
    public async Task Embedding_storage_persists_pgvector_and_replays_without_spending_again()
    {
        var g = await SeedCitedGraphAsync();
        var f = StoredFixture(g);
        await using (var services = CitedPilotServices(g.Seed.OrganizationA, g.Seed.ActorA, "http://127.0.0.1:1"))
        {
            await SaveEmbeddingJournal(services, f);
            Assert.Equal(new EmbeddingStorageResult(1, 0), await services.GetRequiredService<ICitedEmbeddingStore>()
                .StoreChunkOutcomeAsync(f.Operation, default));
        }
        await using var restarted = CitedPilotServices(g.Seed.OrganizationA, g.Seed.ActorA, "http://127.0.0.1:1");
        Assert.Equal(new EmbeddingStorageResult(0, 1), await restarted.GetRequiredService<ICitedEmbeddingStore>()
            .StoreChunkOutcomeAsync(f.Operation, default));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_chunk_embeddings"));
        Assert.Equal("vector", await ScalarAsync<string>("SELECT pg_typeof(embedding)::text FROM cited_chunk_embeddings"));
        Assert.Equal(1536, await ScalarAsync<int>("SELECT vector_dims(embedding) FROM cited_chunk_embeddings"));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_reservations"));
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM cited_generation_artifacts"));
        Assert.Equal(f.Cost, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
    }

    [Fact]
    public async Task Embedding_storage_concurrent_replay_has_one_canonical_row()
    {
        var g = await SeedCitedGraphAsync();
        var f = StoredFixture(g);
        await using var a = CitedPilotServices(g.Seed.OrganizationA, g.Seed.ActorA, "http://127.0.0.1:1");
        await using var b = CitedPilotServices(g.Seed.OrganizationA, g.Seed.ActorA, "http://127.0.0.1:1");
        await SaveEmbeddingJournal(a, f);
        var results = await Task.WhenAll(a.GetRequiredService<ICitedEmbeddingStore>().StoreChunkOutcomeAsync(f.Operation, default),
            b.GetRequiredService<ICitedEmbeddingStore>().StoreChunkOutcomeAsync(f.Operation, default));
        Assert.Equal(1, results.Sum(x => x.Inserted));
        Assert.Equal(1, results.Sum(x => x.Reused));
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("actor")]
    [InlineData("retired")]
    [InlineData("source")]
    [InlineData("query")]
    [InlineData("unsettled")]
    [InlineData("wrong_cost")]
    [InlineData("raw_usage")]
    public async Task Embedding_storage_denies_ineligible_or_unverified_outcomes(string kind)
    {
        var g = await SeedCitedGraphAsync();
        var f = StoredFixture(g, kind == "query" ? "query" : "chunk", text: kind == "source" ? "Different source" : null);
        if (kind == "raw_usage") { f.Raw["usage"]!["prompt_tokens"] = 1; f.SyncRaw(); }
        await using var owner = CitedPilotServices(g.Seed.OrganizationA, g.Seed.ActorA, "http://127.0.0.1:1");
        await SaveEmbeddingJournal(owner, f, kind != "unsettled", kind == "wrong_cost" ? 0m : null);
        if (kind == "actor") await ExecuteAsync("UPDATE actors SET is_active=false WHERE id=@id", ("id", g.Seed.ActorA));
        if (kind == "retired") await ExecuteAsync("UPDATE cited_document_indexes SET status='Superseded' WHERE id=@id", ("id", g.Index.Id));
        await using var caller = CitedPilotServices(kind == "foreign" ? g.Seed.OrganizationB : g.Seed.OrganizationA,
            kind == "foreign" ? g.Seed.ActorB : g.Seed.ActorA, "http://127.0.0.1:1");
        var e = await Assert.ThrowsAsync<CitedWorkflowException>(() => caller.GetRequiredService<ICitedEmbeddingStore>()
            .StoreChunkOutcomeAsync(f.Operation, default));
        Assert.NotEqual("embedding_store_unavailable", e.Code); // Must fail an explicit domain gate.
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_chunk_embeddings"));
    }

    [Fact]
    public async Task Embedding_storage_conflicting_vector_cannot_overwrite_existing_values()
    {
        var g = await SeedCitedGraphAsync();
        await using var services = CitedPilotServices(g.Seed.OrganizationA, g.Seed.ActorA, "http://127.0.0.1:1");
        var store = services.GetRequiredService<ICitedEmbeddingStore>();
        var first = StoredFixture(g);
        await SaveEmbeddingJournal(services, first);
        await store.StoreChunkOutcomeAsync(first.Operation, default);
        var second = StoredFixture(g);
        second.Raw["data"]![0]!["embedding"]![0] = -1;
        second.SyncRaw();
        await SaveEmbeddingJournal(services, second);
        await SpendDenied("embedding_storage_conflict", () => store.StoreChunkOutcomeAsync(second.Operation, default));
        Assert.Equal(1f, await ScalarAsync<float>("SELECT (embedding::real[])[1] FROM cited_chunk_embeddings"));
        Assert.Equal(2 * first.Cost, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
    }

    [Fact]
    public async Task Embedding_storage_SQL_guards_preserve_vectors_and_refuse_destructive_downgrade()
    {
        var g = await SeedCitedGraphAsync();
        var f = StoredFixture(g);
        await using var services = CitedPilotServices(g.Seed.OrganizationA, g.Seed.ActorA, "http://127.0.0.1:1");
        await SaveEmbeddingJournal(services, f);
        await services.GetRequiredService<ICitedEmbeddingStore>().StoreChunkOutcomeAsync(f.Operation, default);
        await AssertSqlStateAsync("23514", "UPDATE cited_chunk_embeddings SET input_sha256=repeat('0',64)");
        await AssertSqlStateAsync("23514", "DELETE FROM cited_chunk_embeddings");
        await using var db = _database.CreateContext();
        var e = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.GetService<IMigrator>()
            .MigrateAsync("20260909213640_AddEmbeddingSpendWorkload"));
        Assert.Equal("23514", e.SqlState);
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_chunk_embeddings"));
    }

    [Fact]
    public async Task Embedding_search_uses_saved_query_and_excludes_unselected_sources_and_rechecks_retirement()
    {
        var g = await SeedCitedGraphAsync();
        await using var services = CitedPilotServices(g.Seed.OrganizationA, g.Seed.ActorA, "http://127.0.0.1:1");
        var store = services.GetRequiredService<ICitedEmbeddingStore>();
        var chunk = StoredFixture(g);
        await SaveEmbeddingJournal(services, chunk);
        await store.StoreChunkOutcomeAsync(chunk.Operation, default);
        var other = await AdditionalEmbeddingIndex(g);
        var unselected = new EmbeddingFixture(g.Seed.OrganizationA, g.Seed.ActorA, [other.Index.Id],
            [(other.Chunks[0].Id, other.Chunks[0].Content)]);
        await SaveEmbeddingJournal(services, unselected);
        await store.StoreChunkOutcomeAsync(unselected.Operation, default);
        var query = StoredFixture(g, "query", text: "What is the total?", input: Guid.NewGuid());
        await SaveEmbeddingJournal(services, query);
        var hit = Assert.Single(await store.SearchQueryOutcomeAsync(query.Operation, 5, default));
        Assert.Equal(g.Chunk.Id, hit.ChunkId);
        Assert.Equal(g.Index.Id, hit.IndexId);
        Assert.Equal(1d, hit.Similarity, 6);
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM cited_chunk_embeddings")); // Query is never indexed.
        await using var foreign = CitedPilotServices(g.Seed.OrganizationB, g.Seed.ActorB, "http://127.0.0.1:1");
        await SpendDenied("embedding_operation_not_found", () => foreign.GetRequiredService<ICitedEmbeddingStore>()
            .SearchQueryOutcomeAsync(query.Operation, 5, default));
        await ExecuteAsync("UPDATE cited_document_indexes SET status='Quarantined' WHERE id=@id", ("id", g.Index.Id));
        await SpendDenied("embedding_source_ineligible", () => store.SearchQueryOutcomeAsync(query.Operation, 5, default));
    }

    [Fact]
    public async Task Embedding_storage_batch_failure_rolls_back_earlier_insertions()
    {
        var g = await SeedCitedGraphAsync();
        var extra = await AdditionalEmbeddingIndex(g);
        await using var services = CitedPilotServices(g.Seed.OrganizationA, g.Seed.ActorA, "http://127.0.0.1:1");
        var store = services.GetRequiredService<ICitedEmbeddingStore>();
        var first = StoredFixture(g);
        await SaveEmbeddingJournal(services, first);
        await store.StoreChunkOutcomeAsync(first.Operation, default);
        var batch = new EmbeddingFixture(g.Seed.OrganizationA, g.Seed.ActorA, [g.Index.Id, extra.Index.Id],
            [(extra.Chunks[0].Id, extra.Chunks[0].Content), (g.Chunk.Id, g.Chunk.Content)]);
        await SaveEmbeddingJournal(services, batch);
        await SpendDenied("embedding_storage_conflict", () => store.StoreChunkOutcomeAsync(batch.Operation, default));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_chunk_embeddings"));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_chunk_embeddings WHERE index_id=@id", ("id", extra.Index.Id)));
    }

    [Fact]
    public async Task Embedding_search_ranks_all_selected_chunks_and_has_stable_ties()
    {
        var g = await SeedCitedGraphAsync();
        var extra = await AdditionalEmbeddingIndex(g, 3);
        await using var services = CitedPilotServices(g.Seed.OrganizationA, g.Seed.ActorA, "http://127.0.0.1:1");
        var store = services.GetRequiredService<ICitedEmbeddingStore>();
        var batch = new EmbeddingFixture(g.Seed.OrganizationA, g.Seed.ActorA, [extra.Index.Id],
            extra.Chunks.Select(x => (x.Id, x.Content)).ToArray());
        await SaveEmbeddingJournal(services, batch);
        await store.StoreChunkOutcomeAsync(batch.Operation, default);
        var query = StoredFixture(g, "query", indexes: [extra.Index.Id], text: "Synthetic question");
        await SaveEmbeddingJournal(services, query);
        var hits = await store.SearchQueryOutcomeAsync(query.Operation, 3, default);
        Assert.Equal(extra.Chunks[0].Id, hits[0].ChunkId);
        Assert.Equal(1d, hits[0].Similarity, 6);
        Assert.All(hits.Skip(1), x => Assert.Equal(0d, x.Similarity, 6));
        Assert.Equal(extra.Chunks.Skip(1).Select(x => x.Id).OrderBy(x => x.ToString(), StringComparer.Ordinal),
            hits.Skip(1).Select(x => x.ChunkId));
    }

    [Fact]
    public async Task Embedding_search_rejects_incomplete_corpus_and_foreign_selected_index()
    {
        var g = await SeedCitedGraphAsync();
        await using var services = CitedPilotServices(g.Seed.OrganizationA, g.Seed.ActorA, "http://127.0.0.1:1");
        var store = services.GetRequiredService<ICitedEmbeddingStore>();
        var query = StoredFixture(g, "query", text: "What is the total?");
        await SaveEmbeddingJournal(services, query);
        await SpendDenied("embedding_corpus_incomplete", () => store.SearchQueryOutcomeAsync(query.Operation, 5, default));
        var foreign = StoredFixture(g, "query", indexes: [Guid.NewGuid()]);
        await SaveEmbeddingJournal(services, foreign);
        await SpendDenied("embedding_source_ineligible", () => store.SearchQueryOutcomeAsync(foreign.Operation, 5, default));
    }
}
