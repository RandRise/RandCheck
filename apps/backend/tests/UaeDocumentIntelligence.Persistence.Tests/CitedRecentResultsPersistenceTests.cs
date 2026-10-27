using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public async Task Questions_exact_source_restores_old_completed_revision_beyond_first_page_and_fails_closed()
    {
        var graph = await SeedCitedGraphAsync();
        await ExecuteAsync("UPDATE review_sessions SET completed_at_utc='2026-01-01T00:00:00Z' WHERE id=@id", ("id", graph.Seed.SessionA));
        for (var revision = 2; revision <= 56; revision++)
        {
            var id = Guid.NewGuid();
            await ExecuteAsync(InsertReviewSessionSql, ReviewSessionParameters(graph.Seed.OrganizationA, graph.Seed.DocumentA, graph.Seed.RunA, id, revision));
            await ExecuteAsync("UPDATE review_sessions SET status='Completed',completed_at_utc=now() WHERE id=@id", ("id", id));
        }
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        await using var scope = services.CreateAsyncScope(); var handler = scope.ServiceProvider.GetRequiredService<CitedWorkspaceHandler>();
        Assert.DoesNotContain((await handler.SourcesAsync(0, default)).Sources, s => s.ReviewSessionId == graph.Seed.SessionA);
        var exact = await handler.SourceAsync(graph.Seed.DocumentA, graph.Seed.SessionA, default);
        Assert.Equal(graph.Seed.SessionA, exact.ReviewSessionId); Assert.Equal(graph.Seed.RunA, exact.ExtractionRunId);
        Assert.Equal(1, exact.RevisionNumber); Assert.Equal(graph.Index.Filename, exact.Filename);
        Assert.Equal(2026, exact.CompletedAtUtc!.Value.Year);
        foreach (var ids in new[] { (graph.Seed.DocumentB, graph.Seed.SessionA), (graph.Seed.DocumentA, Guid.NewGuid()) })
            Assert.Equal(404, (await Assert.ThrowsAsync<CitedWorkflowException>(() => handler.SourceAsync(ids.Item1, ids.Item2, default))).Status);
        await using var foreign = CitedServices(graph.Seed.OrganizationB, graph.Seed.ActorB, "http://127.0.0.1:1");
        await using var foreignScope = foreign.CreateAsyncScope();
        Assert.Equal(404, (await Assert.ThrowsAsync<CitedWorkflowException>(() => foreignScope.ServiceProvider.GetRequiredService<CitedWorkspaceHandler>()
            .SourceAsync(graph.Seed.DocumentA, graph.Seed.SessionA, default))).Status);
        await ExecuteAsync("UPDATE extraction_runs SET status='Partial' WHERE id=@id", ("id", graph.Seed.RunA));
        Assert.Equal(exact, await handler.SourceAsync(graph.Seed.DocumentA, graph.Seed.SessionA, default));
        await ExecuteAsync("UPDATE extraction_runs SET status='Failed' WHERE id=@id", ("id", graph.Seed.RunA));
        Assert.Equal(404, (await Assert.ThrowsAsync<CitedWorkflowException>(() => handler.SourceAsync(graph.Seed.DocumentA, graph.Seed.SessionA, default))).Status);
        await ExecuteAsync("UPDATE extraction_runs SET status='Succeeded' WHERE id=@id", ("id", graph.Seed.RunA));
        await ExecuteAsync("UPDATE review_sessions SET status='InProgress',completed_at_utc=NULL WHERE id=@id", ("id", graph.Seed.SessionA));
        Assert.Equal(404, (await Assert.ThrowsAsync<CitedWorkflowException>(() => handler.SourceAsync(graph.Seed.DocumentA, graph.Seed.SessionA, default))).Status);
    }

    [Fact]
    public async Task Questions_recent_results_are_bounded_stable_actor_scoped_and_include_zero_hit_terminal_modes()
    {
        var graph = await SeedCitedGraphAsync(); var now = DateTimeOffset.Parse("2026-10-02T12:00:00Z");
        var eligible = new List<CitedRetrievalRun>();
        await using (var db = _database.CreateContext())
        {
            for (var i = 0; i < 25; i++)
            {
                var run = RecentRun(graph, graph.Seed.ActorA, $"Saved question {i}", now, i % 2 == 0);
                db.CitedRetrievalRuns.Add(run); eligible.Add(run);
            }
            var running = RecentRun(graph, graph.Seed.ActorA, "Still running", now.AddMinutes(1), false, terminal: false);
            var failed = RecentRun(graph, graph.Seed.ActorA, "Failed request", now.AddMinutes(1), false, terminal: false);
            failed.Fail("synthetic_failure", now.AddMinutes(1));
            var peer = new Actor(Guid.NewGuid(), graph.Seed.OrganizationA, ActorKind.System, "Other test actor", now);
            db.Actors.Add(peer); db.CitedRetrievalRuns.Add(RecentRun(graph, peer.Id, "Other actor private question", now.AddMinutes(1), false));
            db.CitedRetrievalRuns.AddRange(running, failed); await db.SaveChangesAsync();
            // A deterministic no-hit refusal can be reopened; generated historical
            // answers and nearly matching malformed refusals cannot be offered.
            var refusal = RecentCompleted(graph, "Valid deterministic refusal", now.AddMinutes(2), "evidence-only", "none", CitedQuestionHandler.NoEvidencePolicy);
            eligible.Add(refusal); db.CitedRetrievalRuns.Add(refusal);
            db.CitedRetrievalRuns.Add(RecentCompleted(graph, "Unsupported generated pilot", now.AddMinutes(3), "openai", "pilot-model", "pilot-v1"));
            db.CitedRetrievalRuns.Add(RecentCompleted(graph, "Wrong refusal prompt", now.AddMinutes(3), "evidence-only", "none", "wrong-policy"));
            db.CitedRetrievalRuns.Add(RecentCompleted(graph, "Wrong refusal model", now.AddMinutes(3), "evidence-only", "wrong-model", CitedQuestionHandler.NoEvidencePolicy));
            await db.SaveChangesAsync();
        }
        var capture = new WorkListQueryCapture();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:Database"] = _database.TestConnectionString,
            ["AiService:BaseUrl"] = "http://127.0.0.1:1", ["AiService:WorkerToken"] = "synthetic-only" }).Build();
        await using var services = new ServiceCollection().AddInfrastructure(config)
            .AddSingleton(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_database.TestConnectionString).AddInterceptors(capture).Options)
            .AddSingleton<IExecutionContext>(new CitedExecution(graph.Seed.OrganizationA, graph.Seed.ActorA)).BuildServiceProvider();
        await using var scope = services.CreateAsyncScope(); var handler = scope.ServiceProvider.GetRequiredService<CitedWorkspaceHandler>();
        var before = await RecentBusinessCounts(); var result = await handler.RecentQuestionsAsync(default);
        Assert.Equal(eligible.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Take(20).Select(x => x.Id), result.Questions.Select(x => x.Id));
        Assert.All(result.Questions, item => {
            var expected = eligible.Single(x => x.Id == item.Id);
            Assert.Equal(expected.Question, item.Question); Assert.Equal(1, item.SourceCount); Assert.Equal(expected.CreatedAtUtc, item.CreatedAt);
            Assert.Equal(expected.PolicyVersion == CitedReviewedQuestionHandler.Policy ? "reviewed" : "evidence", item.Mode);
        });
        Assert.Contains(result.Questions, x => x.Mode == "reviewed"); Assert.Contains(result.Questions, x => x.Mode == "evidence");
        Assert.Equal(before, await RecentBusinessCounts());
        Assert.Empty(scope.ServiceProvider.GetRequiredService<AppDbContext>().ChangeTracker.Entries());
        var sql = Assert.Single(capture.Reads);
        foreach (var forbidden in new[] { "machine_field_values", "field_evidence", "extraction_pages", "review_actions" })
            Assert.DoesNotContain(forbidden, sql, StringComparison.OrdinalIgnoreCase);
        // Independently prove the offered shapes are accepted by the unchanged
        // saved-result reader. This is test verification, not list implementation.
        foreach (var item in result.Questions)
        {
            await using var readScope = services.CreateAsyncScope();
            var reopened = await readScope.ServiceProvider.GetRequiredService<CitedQuestionHandler>().GetAsync(item.Id, default);
            Assert.Equal(item.Question, reopened.Question);
        }
        Assert.Equal(before, await RecentBusinessCounts());
        await using var foreign = CitedServices(graph.Seed.OrganizationB, graph.Seed.ActorB, "http://127.0.0.1:1");
        await using var foreignScope = foreign.CreateAsyncScope();
        Assert.Empty((await foreignScope.ServiceProvider.GetRequiredService<CitedWorkspaceHandler>().RecentQuestionsAsync(default)).Questions);
    }

    [Fact]
    public async Task Questions_recent_results_require_every_frozen_source_and_active_actor_even_without_hits()
    {
        var graph = await SeedCitedGraphAsync(); var second = await RecentSecondIndex(graph.Seed);
        var multi = new CitedRetrievalRun(Guid.NewGuid(), graph.Seed.OrganizationA, graph.Seed.ActorA,
            "Two source question", "recent-two", "lexical-1", "{}", [graph.Index, second], DateTimeOffset.UtcNow);
        multi.MarkRetrieved(0);
        var baseline = RecentRun(graph, graph.Seed.ActorA, "One eligible source", DateTimeOffset.UtcNow, true);
        await using (var db = _database.CreateContext()) { db.CitedRetrievalRuns.AddRange(multi, baseline); await db.SaveChangesAsync(); }
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        await using var scope = services.CreateAsyncScope(); var handler = scope.ServiceProvider.GetRequiredService<CitedWorkspaceHandler>();
        async Task<RecentQuestionView[]> Read() => (await handler.RecentQuestionsAsync(default)).Questions.ToArray();
        Assert.Equal(2, (await Read()).Single(x => x.Id == multi.Id).SourceCount);
        foreach (var status in new[] { "Superseded", "Quarantined" })
        {
            await ExecuteAsync("UPDATE cited_document_indexes SET status=@status WHERE id=@id", ("status", status), ("id", second.Id));
            Assert.Equal(baseline.Id, Assert.Single(await Read()).Id);
        }
        await ExecuteAsync("UPDATE cited_document_indexes SET status='Ready' WHERE id=@id", ("id", second.Id));
        await ExecuteAsync("UPDATE review_sessions SET status='Cancelled' WHERE id=@id", ("id", second.ReviewSessionId));
        Assert.Equal(baseline.Id, Assert.Single(await Read()).Id);
        await ExecuteAsync("UPDATE review_sessions SET status='Completed' WHERE id=@id", ("id", second.ReviewSessionId));
        await ExecuteAsync("UPDATE extraction_runs SET status='Failed' WHERE id=@id", ("id", second.ExtractionRunId));
        Assert.Equal(baseline.Id, Assert.Single(await Read()).Id);
        await ExecuteAsync("UPDATE extraction_runs SET status='Partial' WHERE id=@id", ("id", second.ExtractionRunId));
        Assert.Equal(2, (await Read()).Length);
        await ExecuteAsync("UPDATE actors SET is_active=false WHERE id=@id", ("id", graph.Seed.ActorA));
        Assert.Empty(await Read());
        Assert.Equal(3, await ScalarAsync<long>("SELECT count(*) FROM cited_retrieval_runs"));
    }

    [Fact]
    public async Task Questions_discovery_real_api_reads_are_no_store_and_follow_current_roles_without_business_mutation()
    {
        var graph = await SeedCitedGraphAsync(); var subject = await SecurityAttachFixtureHuman(graph.Seed.OrganizationA);
        var peer = await SecurityAttachFixtureHuman(graph.Seed.OrganizationA); var foreign = await SecurityAttachFixtureHuman(graph.Seed.OrganizationB);
        Guid actor;
        await using (var db = _database.CreateContext())
        {
            actor = await (from m in db.OrganizationMemberships join i in db.EnterpriseIdentities on m.IdentityId equals i.Id where i.ObjectId == subject select m.ActorId).SingleAsync();
            db.CitedRetrievalRuns.Add(RecentRun(graph, actor, "Human saved question", DateTimeOffset.UtcNow, false)); await db.SaveChangesAsync();
        }
        var before = await RecentBusinessCounts(); await using var role = await ApiRoleAsync();
        using var original = SecurityHost(); using var host = role.Host(original);
        using var owner = SecurityHttp(host, subject); using var other = SecurityHttp(host, peer); using var otherOrg = SecurityHttp(host, foreign);
        var sourcePath = $"/api/cited-sources/{graph.Seed.DocumentA}/reviews/{graph.Seed.SessionA}";
        foreach (var access in new[] { ("Staff", true), ("Admin", true), ("Staff", false), ("Admin", false) })
        {
            // Only disposable synthetic fixture memberships are changed; no external identity calls.
            await ExecuteAsync("UPDATE organization_memberships SET role=@role,is_active=@active,version=@version WHERE actor_id=@actor",
                ("role", access.Item1), ("active", access.Item2), ("version", Guid.NewGuid()), ("actor", actor));
            foreach (var path in new[] { sourcePath, "/api/cited-questions" })
            {
                var response = await owner.GetAsync(path);
                if (!access.Item2) { Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); continue; }
                Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
                if (path == sourcePath) Assert.Equal(graph.Seed.SessionA, (await response.Content.ReadFromJsonAsync<ReviewedSourceView>())!.ReviewSessionId);
                else Assert.Equal("Human saved question", Assert.Single((await response.Content.ReadFromJsonAsync<RecentQuestionsView>())!.Questions).Question);
            }
        }
        Assert.Empty((await other.GetFromJsonAsync<RecentQuestionsView>("/api/cited-questions"))!.Questions);
        Assert.Empty((await otherOrg.GetFromJsonAsync<RecentQuestionsView>("/api/cited-questions"))!.Questions);
        Assert.Equal(HttpStatusCode.NotFound, (await otherOrg.GetAsync(sourcePath)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/cited-sources/{graph.Seed.DocumentB}/reviews/{graph.Seed.SessionA}")).StatusCode);
        Assert.Equal(before, await RecentBusinessCounts());
    }

    private static CitedRetrievalRun RecentRun(CitedSeed graph, Guid actor, string question, DateTimeOffset now, bool reviewed, bool terminal = true)
    {
        var run = new CitedRetrievalRun(Guid.NewGuid(), graph.Seed.OrganizationA, actor, question,
            Guid.NewGuid().ToString(), reviewed ? CitedReviewedQuestionHandler.Policy : "lexical-1",
            reviewed ? "{\"missingParts\":[]}" : "{}", [graph.Index], now);
        if (!terminal) return run;
        if (reviewed) run.Complete(Guid.NewGuid(), CitedAnswerStatus.InsufficientEvidence, "no_relevant_evidence", [], [],
            "reviewed-facts", "none", "1", 0, 0, null, null, now);
        else run.MarkRetrieved(0);
        return run;
    }

    private static CitedRetrievalRun RecentCompleted(CitedSeed graph, string question, DateTimeOffset now,
        string provider, string model, string prompt)
    {
        var run = RecentRun(graph, graph.Seed.ActorA, question, now, false, terminal: false);
        if (provider == "openai")
        {
            var hit = run.AddHit(Guid.NewGuid(), graph.Chunk, 1, 1, null, null);
            run.Complete(Guid.NewGuid(), CitedAnswerStatus.Answered, null,
                [new("The total is 100.00.", [new(hit.Id, graph.Chunk.Spans.Single().Id)])], [graph.Chunk],
                provider, model, prompt, 0, 0, null, null, now);
            return run;
        }
        run.Complete(Guid.NewGuid(), CitedAnswerStatus.InsufficientEvidence, "no_relevant_evidence", [], [],
            provider, model, prompt, 0, 0, null, null, now);
        return run;
    }

    private async Task<CitedDocumentIndex> RecentSecondIndex(SeedData seed)
    {
        var docId = Guid.NewGuid(); var runId = Guid.NewGuid(); var pageId = Guid.NewGuid(); var reviewId = Guid.NewGuid();
        await ExecuteAsync(InsertDocumentSql, DocumentParameters(seed.OrganizationA, seed.ActorA, docId, "Second source — فاتورة.pdf", ValidSha));
        await ExecuteAsync(InsertRunSql, RunParameters(seed.OrganizationA, docId, runId, 1, "recent-second"));
        await ExecuteAsync(InsertPageSql, PageParameters(seed.OrganizationA, runId, pageId));
        await ExecuteAsync(InsertReviewSessionSql, ReviewSessionParameters(seed.OrganizationA, docId, runId, reviewId, 1));
        await ExecuteAsync("UPDATE review_sessions SET status='Completed',completed_at_utc=now() WHERE id=@id", ("id", reviewId));
        await using var db = _database.CreateContext();
        var index = new CitedDocumentIndex(Guid.NewGuid(), await db.Documents.SingleAsync(x => x.Id == docId),
            await db.ReviewSessions.SingleAsync(x => x.Id == reviewId), "page", "1", DateTimeOffset.UtcNow);
        var page = await db.ExtractionPages.SingleAsync(x => x.Id == pageId);
        index.AddPageChunk(Guid.NewGuid(), 0, page, 0, Encoding.UTF8.GetByteCount(page.CanonicalText)); index.Publish();
        db.CitedDocumentIndexes.Add(index); await db.SaveChangesAsync(); return index;
    }

    private async Task<string> RecentBusinessCounts() => await ScalarAsync<string>("""
        SELECT concat((SELECT count(*) FROM cited_retrieval_runs),':',(SELECT count(*) FROM retrieval_sources),':',
            (SELECT count(*) FROM retrieval_hits),':',(SELECT count(*) FROM cited_answers),':',
            (SELECT count(*) FROM cited_document_indexes),':',(SELECT count(*) FROM review_sessions),':',
            (SELECT count(*) FROM documents),':',(SELECT count(*) FROM cited_spend_events))
        """);
}
