using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Tests.Fixtures;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    private static LexicalPlan SemanticPlan(string question = "total") => new("1.0", LexicalPlan.CurrentVersion,
        CitedEmbeddingStorage.Digest(Encoding.UTF8.GetBytes(question)), "english", "\"total\"", 20);

    private static async Task<EmbeddingFixture> SemanticSeed(IServiceProvider services, CitedSeed g, bool store = true)
    {
        var chunks = StoredFixture(g);
        await SaveEmbeddingJournal(services, chunks);
        if (store) await services.GetRequiredService<ICitedEmbeddingStore>().StoreChunkOutcomeAsync(chunks.Operation, default);
        var query = StoredFixture(g, "query", text: "total", input: Guid.NewGuid());
        await SaveEmbeddingJournal(services, query);
        return query;
    }

    [Theory]
    [InlineData("vector")]
    [InlineData("hybrid")]
    [InlineData("hybrid-nonredundant")]
    public async Task Semantic_retrieval_persists_scores_policy_evidence_and_replays_without_spend(string mode)
    {
        var g = await SeedCitedGraphAsync();
        await using var services = CitedServices(g.Seed.OrganizationA, g.Seed.ActorA, "http://127.0.0.1:1");
        var q = await SemanticSeed(services, g);
        var store = services.GetRequiredService<ICitedSemanticRetrievalStore>();
        var first = await store.RetrieveAsync(q.Operation, "semantic", mode, 5, mode != "vector" ? SemanticPlan() : null, default);
        Assert.False(first.Replayed);
        var hit = Assert.Single(first.Hits);
        Assert.Equal(g.Chunk.Id, hit.ChunkId);
        Assert.Equal(mode == "vector" ? 1d : 2d / 61, hit.Score);
        Assert.NotEmpty(hit.Spans);
        await using var db = _database.CreateContext();
        var saved = await db.CitedRetrievalRuns.Include(x => x.Hits).SingleAsync(x => x.Id == first.Id);
        var raw = Assert.Single(saved.Hits);
        Assert.Equal(1, raw.VectorScore);
        Assert.Equal(mode != "vector", raw.LexicalScore.HasValue);
        Assert.Equal(mode != "vector", raw.FusedScore.HasValue);
        using var policy = JsonDocument.Parse(saved.PolicyJson);
        Assert.Equal(q.Operation, policy.RootElement.GetProperty("queryOperationId").GetGuid());
        Assert.Equal(1536, policy.RootElement.GetProperty("dimension").GetInt32());
        await using var restarted = CitedServices(g.Seed.OrganizationA, g.Seed.ActorA, "http://127.0.0.1:1");
        var replay = await restarted.GetRequiredService<ICitedSemanticRetrievalStore>()
            .RetrieveAsync(q.Operation, "semantic", mode, 5, mode != "vector" ? SemanticPlan() : null, default);
        Assert.True(replay.Replayed);
        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(hit.HitId, Assert.Single(replay.Hits).HitId);
        Assert.Equal(first.RetrievalDurationMs, replay.RetrievalDurationMs);
        var history = await restarted.GetRequiredService<RetrieveEvidenceHandler>().GetAsync(first.Id, default);
        Assert.Equal(hit.Score, Assert.Single(history.Hits).Score);
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_reservations"));
        Assert.Equal(4, await ScalarAsync<long>("SELECT count(*) FROM cited_generation_artifacts"));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_answers")); // Original seeded answer only.
    }

    [Fact]
    public async Task Semantic_retrieval_same_key_race_has_one_run_and_rejects_changed_inputs()
    {
        var g = await SeedCitedGraphAsync();
        await using var a = CitedServices(g.Seed.OrganizationA, g.Seed.ActorA, "http://127.0.0.1:1");
        await using var b = CitedServices(g.Seed.OrganizationA, g.Seed.ActorA, "http://127.0.0.1:1");
        var q = await SemanticSeed(a, g);
        var ca = a.GetRequiredService<ICitedSemanticRetrievalStore>();
        var cb = b.GetRequiredService<ICitedSemanticRetrievalStore>();
        var results = await Task.WhenAll(ca.RetrieveAsync(q.Operation, "race", "vector", 5, null, default),
            cb.RetrieveAsync(q.Operation, "race", "vector", 5, null, default));
        Assert.Equal(results[0].Id, results[1].Id);
        Assert.Single(results.Where(x => !x.Replayed));
        await SpendDenied("request_key_reused", () => ca.RetrieveAsync(q.Operation, "race", "hybrid", 5, SemanticPlan(), default));
        await SpendDenied("request_key_reused", () => ca.RetrieveAsync(q.Operation, "race", "vector", 1, null, default));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_retrieval_runs WHERE request_key='race'"));
    }

    [Fact]
    public async Task Semantic_retrieval_rejects_wrong_question_missing_coverage_and_foreign_caller_without_run()
    {
        var g = await SeedCitedGraphAsync();
        await using var a = CitedServices(g.Seed.OrganizationA, g.Seed.ActorA, "http://127.0.0.1:1");
        var q = await SemanticSeed(a, g, false);
        var store = a.GetRequiredService<ICitedSemanticRetrievalStore>();
        await SpendDenied("invalid_search_plan", () => store.RetrieveAsync(q.Operation, "bad", "hybrid", 5, SemanticPlan("different"), default));
        await SpendDenied("embedding_corpus_incomplete", () => store.RetrieveAsync(q.Operation, "bad", "vector", 5, null, default));
        await using var b = CitedServices(g.Seed.OrganizationB, g.Seed.ActorB, "http://127.0.0.1:1");
        await SpendDenied("embedding_operation_not_found", () => b.GetRequiredService<ICitedSemanticRetrievalStore>()
            .RetrieveAsync(q.Operation, "bad", "vector", 5, null, default));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_retrieval_runs WHERE request_key='bad'"));
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("retired")]
    public async Task Semantic_retrieval_rechecks_eligibility_on_replay(string change)
    {
        var g = await SeedCitedGraphAsync();
        await using var a = CitedServices(g.Seed.OrganizationA, g.Seed.ActorA, "http://127.0.0.1:1");
        var q = await SemanticSeed(a, g);
        var store = a.GetRequiredService<ICitedSemanticRetrievalStore>();
        await store.RetrieveAsync(q.Operation, "retire", "vector", 5, null, default);
        if (change == "actor") await ExecuteAsync("UPDATE actors SET is_active=false WHERE id=@id", ("id", g.Seed.ActorA));
        else await ExecuteAsync("UPDATE cited_document_indexes SET status='Superseded' WHERE id=@id", ("id", g.Index.Id));
        await SpendDenied(change == "actor" ? "embedding_operation_not_found" : "embedding_source_ineligible",
            () => store.RetrieveAsync(q.Operation, "retire", "vector", 5, null, default));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_retrieval_runs WHERE request_key='retire'"));
    }

    [Fact]
    public async Task Semantic_retrieval_failure_rolls_back_run_sources_and_hits()
    {
        var g = await SeedCitedGraphAsync();
        await using var a = CitedServices(g.Seed.OrganizationA, g.Seed.ActorA, "http://127.0.0.1:1");
        var q = await SemanticSeed(a, g);
        var runs = await ScalarAsync<long>("SELECT count(*) FROM cited_retrieval_runs");
        var sources = await ScalarAsync<long>("SELECT count(*) FROM retrieval_sources");
        var hits = await ScalarAsync<long>("SELECT count(*) FROM retrieval_hits");
        await ExecuteAsync("""
            CREATE FUNCTION reject_semantic_hit() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'synthetic write failure'; END $$;
            CREATE TRIGGER reject_semantic_hit BEFORE INSERT ON retrieval_hits FOR EACH ROW EXECUTE FUNCTION reject_semantic_hit();
            """);
        await SpendDenied("embedding_store_unavailable", () => a.GetRequiredService<ICitedSemanticRetrievalStore>()
            .RetrieveAsync(q.Operation, "rollback", "vector", 5, null, default));
        Assert.Equal(runs, await ScalarAsync<long>("SELECT count(*) FROM cited_retrieval_runs"));
        Assert.Equal(sources, await ScalarAsync<long>("SELECT count(*) FROM retrieval_sources"));
        Assert.Equal(hits, await ScalarAsync<long>("SELECT count(*) FROM retrieval_hits"));
    }
}
