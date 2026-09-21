using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure;

namespace UaeDocumentIntelligence.Persistence.Tests;

// Share the existing fixture and reset lifecycle; a second fixture would race database recreation.
public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public async Task Cited_graph_round_trips_with_exact_citations_and_scoped_repository_reads()
    {
        var graph = await SeedCitedGraphAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = _database.TestConnectionString
        }).Build();
        await using var services = new ServiceCollection().AddInfrastructure(config).BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICitedQuestionRepository>();
        var actual = await repository.GetRunAsync(graph.Seed.OrganizationA, graph.Request.Id, default);
        Assert.NotNull(actual);
        Assert.Equal(CitedRetrievalStatus.Completed, actual.Status);
        Assert.Single(actual.Sources);
        Assert.Single(actual.Hits);
        Assert.Equal(1, actual.RetrievalDurationMs);
        Assert.Equal("The total is 100.00.", actual.Answer!.Claims.Single().Text);
        Assert.Equal(graph.Chunk.Spans.Single().Id, actual.Answer.Claims.Single().Citations.Single().SpanId);
        Assert.Null(await repository.GetRunAsync(graph.Seed.OrganizationB, graph.Request.Id, default));
        Assert.Null(await repository.GetIndexAsync(graph.Seed.OrganizationB, graph.Index.Id, default));
        var index = await repository.GetIndexAsync(graph.Seed.OrganizationA, graph.Index.Id, default);
        Assert.Equal("Total 100.00", index!.Chunks.Single().Spans.Single().Quote);
        Assert.Equal(graph.Seed.SessionA, index.ReviewSessionId);
        Assert.Equal(graph.Seed.RunA, index.ExtractionRunId);
    }

    [Fact]
    public async Task Cited_database_rejects_foreign_corpus_page_and_review_references()
    {
        var graph = await SeedCitedGraphAsync();
        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation,
            "UPDATE retrieval_sources SET organization_id=@org WHERE retrieval_run_id=@run",
            ("org", graph.Seed.OrganizationB), ("run", graph.Request.Id));
        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation,
            "UPDATE cited_document_indexes SET extraction_run_id=@run WHERE id=@id",
            ("run", graph.Seed.RunB), ("id", graph.Index.Id));
        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation,
            "UPDATE chunk_evidence_spans SET extraction_run_id=@run WHERE chunk_id=@chunk",
            ("run", graph.Seed.RunB), ("chunk", graph.Chunk.Id));
    }

    [Fact]
    public async Task Citation_cannot_be_reassigned_to_hit_or_span_from_another_run_or_chunk()
    {
        var graph = await SeedCitedGraphAsync();
        await using var db = _database.CreateContext();
        var other = new CitedRetrievalRun(Guid.NewGuid(), graph.Seed.OrganizationA, graph.Seed.ActorA,
            "Another question", Guid.NewGuid().ToString(), "lexical-1", "{}", [graph.Index], DateTimeOffset.UtcNow);
        var otherHit = other.AddHit(Guid.NewGuid(), graph.Chunk, 1, 1, null, null);
        db.CitedRetrievalRuns.Add(other);
        await db.SaveChangesAsync();
        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation,
            "UPDATE answer_citations SET hit_id=@hit WHERE retrieval_run_id=@run",
            ("hit", otherHit.Id), ("run", graph.Request.Id));
        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation,
            "UPDATE answer_citations SET span_id=@span WHERE retrieval_run_id=@run",
            ("span", Guid.NewGuid()), ("run", graph.Request.Id));
    }

    [Fact]
    public async Task Cited_database_rejects_invalid_rank_cost_reason_and_quote_length()
    {
        var graph = await SeedCitedGraphAsync();
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation,
            "UPDATE retrieval_hits SET rank=0 WHERE retrieval_run_id=@id", ("id", graph.Request.Id));
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation,
            "UPDATE retrieval_hits SET lexical_score='NaN'::float8 WHERE retrieval_run_id=@id", ("id", graph.Request.Id));
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation,
            "UPDATE cited_answers SET estimated_cost=-1, cost_currency='AED' WHERE retrieval_run_id=@id", ("id", graph.Request.Id));
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation,
            "UPDATE cited_answers SET status='InsufficientEvidence', reason_code=NULL WHERE retrieval_run_id=@id", ("id", graph.Request.Id));
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation,
            "UPDATE chunk_evidence_spans SET byte_end=byte_start WHERE chunk_id=@id", ("id", graph.Chunk.Id));
        // Equal byte length is not enough: the stored quote must match the actual page bytes.
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation,
            "UPDATE chunk_evidence_spans SET quote='Total 999.00' WHERE chunk_id=@id", ("id", graph.Chunk.Id));
    }

    [Fact]
    public async Task Cited_history_survives_index_retirement_and_rejects_tracked_edits()
    {
        var graph = await SeedCitedGraphAsync();
        await using (var db = _database.CreateContext())
        {
            var index = await db.CitedDocumentIndexes.SingleAsync(x => x.Id == graph.Index.Id);
            index.Retire();
            await db.SaveChangesAsync();
            Assert.Single(await db.CitedAnswers.ToListAsync());
            var chunk = await db.DocumentChunks.SingleAsync(x => x.Id == graph.Chunk.Id);
            db.Entry(chunk).Property(x => x.Content).CurrentValue = "Changed evidence";
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        await using (var db = _database.CreateContext())
        {
            var claim = await db.AnswerClaims.SingleAsync();
            db.AnswerClaims.Remove(claim);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Index_concurrency_and_request_idempotency_are_enforced()
    {
        var graph = await SeedCitedGraphAsync();
        await using var first = _database.CreateContext();
        await using var second = _database.CreateContext();
        var a = await first.CitedDocumentIndexes.SingleAsync(x => x.Id == graph.Index.Id);
        var b = await second.CitedDocumentIndexes.SingleAsync(x => x.Id == graph.Index.Id);
        a.Retire();
        b.Retire(true);
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        await AssertSqlStateAsync(PostgresErrorCodes.UniqueViolation,
            """
            INSERT INTO cited_retrieval_runs
            (id,organization_id,actor_id,question,request_key,policy_version,policy_json,status,concurrency_token,created_at_utc)
            SELECT @id,organization_id,actor_id,question,request_key,policy_version,policy_json,'Running',@token,created_at_utc
            FROM cited_retrieval_runs WHERE id=@original
            """, ("id", Guid.NewGuid()), ("token", Guid.NewGuid()), ("original", graph.Request.Id));
    }

    [Fact]
    public async Task Refusal_and_reviewed_fact_graphs_persist_without_invented_page_citations()
    {
        var graph = await SeedCitedGraphAsync();
        await using var db = _database.CreateContext();
        var doc = await db.Documents.SingleAsync(x => x.Id == graph.Seed.DocumentA);
        var review = await db.ReviewSessions.Include(x => x.Actions).SingleAsync(x => x.Id == graph.Seed.SessionA);
        var extraction = await db.ExtractionRuns.Include(x => x.Pages)
            .Include(x => x.Fields).ThenInclude(x => x.Evidence).SingleAsync(x => x.Id == graph.Seed.RunA);
        var index = new CitedDocumentIndex(Guid.NewGuid(), doc, review, "facts", "1", DateTimeOffset.UtcNow);
        var fact = index.AddReviewedFact(Guid.NewGuid(), 0, review, "total", 0, extraction);
        index.Publish();
        db.CitedDocumentIndexes.Add(index);
        var run = new CitedRetrievalRun(Guid.NewGuid(), doc.OrganizationId, graph.Seed.ActorA,
            "What did the reviewer confirm?", Guid.NewGuid().ToString(), "1", "{}", [index], DateTimeOffset.UtcNow);
        var hit = run.AddHit(Guid.NewGuid(), fact, 1, 1, null, null);
        run.Complete(Guid.NewGuid(), CitedAnswerStatus.Answered, null,
            [new("Reviewed total is 100.00", [new(hit.Id, null)])], [fact],
            "evidence-only", "none", "1", 0, 0, null, null, DateTimeOffset.UtcNow);
        db.CitedRetrievalRuns.Add(run);
        var refusal = new CitedRetrievalRun(Guid.NewGuid(), doc.OrganizationId, graph.Seed.ActorA,
            "Unknown question", Guid.NewGuid().ToString(), "1", "{}", [index], DateTimeOffset.UtcNow);
        refusal.Complete(Guid.NewGuid(), CitedAnswerStatus.InsufficientEvidence, "no_relevant_evidence",
            [], [], "evidence-only", "none", "1", 0, 0, null, null, DateTimeOffset.UtcNow);
        db.CitedRetrievalRuns.Add(refusal);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Null((await db.AnswerCitations.SingleAsync(x => x.RetrievalRunId == run.Id)).SpanId);
        Assert.Equal(graph.Seed.ActionA, (await db.DocumentChunks.SingleAsync(x => x.Id == fact.Id)).ReviewActionId);
        Assert.Equal("no_relevant_evidence", (await db.CitedAnswers.SingleAsync(x => x.RetrievalRunId == refusal.Id)).ReasonCode);
        Assert.Empty(await db.AnswerClaims.Where(x => x.RetrievalRunId == refusal.Id).ToListAsync());
    }

    private async Task<CitedSeed> SeedCitedGraphAsync(string pageSuffix = "")
    {
        var seed = await SeedGraphAsync();
        if (pageSuffix.Length > 0)
            await ExecuteAsync("UPDATE extraction_pages SET canonical_text=canonical_text || @suffix WHERE id=@id",
                ("suffix", pageSuffix), ("id", seed.PageA));
        await ExecuteAsync("UPDATE review_sessions SET status='Completed', completed_at_utc=@now WHERE id=@id",
            ("now", DateTimeOffset.UtcNow), ("id", seed.SessionA));
        await using var db = _database.CreateContext();
        var doc = await db.Documents.SingleAsync(x => x.Id == seed.DocumentA);
        var review = await db.ReviewSessions.SingleAsync(x => x.Id == seed.SessionA);
        var page = await db.ExtractionPages.SingleAsync(x => x.Id == seed.PageA);
        var now = DateTimeOffset.UtcNow;
        var index = new CitedDocumentIndex(Guid.NewGuid(), doc, review, "page", "1", now);
        var chunk = index.AddPageChunk(Guid.NewGuid(), 0, page, 0, Encoding.UTF8.GetByteCount(page.CanonicalText));
        index.Publish();
        var request = new CitedRetrievalRun(Guid.NewGuid(), seed.OrganizationA, seed.ActorA,
            "What is the total?", Guid.NewGuid().ToString(), "1", "{}", [index], now);
        var hit = request.AddHit(Guid.NewGuid(), chunk, 1, 1, null, null);
        request.Complete(Guid.NewGuid(), CitedAnswerStatus.Answered, null,
            [new("The total is 100.00.", [new(hit.Id, chunk.Spans.Single().Id)])], [chunk],
            "evidence-only", "none", "1", 1, 0, null, null, now);
        db.CitedDocumentIndexes.Add(index);
        db.CitedRetrievalRuns.Add(request);
        await db.SaveChangesAsync();
        return new(seed, index, chunk, request);
    }

    private sealed record CitedSeed(SeedData Seed, CitedDocumentIndex Index, DocumentChunk Chunk,
        CitedRetrievalRun Request);
}
