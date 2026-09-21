using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public async Task Fresh_question_uses_live_private_planning_then_refuses_only_zero_hits()
    {
        var graph = await SeedCitedGraphAsync();
        await using var python = await PrivateCitedServer.StartAsync();
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, python.Url);
        await using var scope = services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<CitedQuestionHandler>();
        var missing = await handler.HandleAsync("xyzunmatchedtoken", "fresh-empty", [graph.Index.Id], 5, default);
        Assert.Equal("InsufficientEvidence", missing.Stage);
        Assert.False(missing.Retrieval.Replayed);
        Assert.Equal("no_relevant_evidence", missing.Answer!.ReasonCode);
        var found = await handler.HandleAsync("total", "fresh-hits", [graph.Index.Id], 5, default);
        Assert.Equal("EvidenceReady", found.Stage);
        Assert.Single(found.Retrieval.Hits);
        Assert.Null(found.Answer);
        var replay = await handler.HandleAsync("xyzunmatchedtoken", "fresh-empty", [graph.Index.Id], 5, default);
        Assert.True(replay.Retrieval.Replayed);
        Assert.Equal(missing.Answer.Id, replay.Answer!.Id);
        Assert.Equal(missing.Answer, (await handler.GetAsync(missing.Retrieval.Id, default)).Answer);
    }

    [Fact]
    public async Task No_hit_question_refusal_is_atomic_replayable_actor_scoped_and_does_not_call_provider()
    {
        var graph = await SeedCitedGraphAsync();
        Guid emptyId;
        await using (var db = _database.CreateContext())
        {
            var empty = StoredQuestion(graph, "unknown", "no-evidence");
            empty.MarkRetrieved(7);
            emptyId = empty.Id;
            db.CitedRetrievalRuns.Add(empty);
            await db.SaveChangesAsync();
        }
        // Unreachable private URL: persisted request replay and refusal need no model or Python call.
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        await using (var scope = services.CreateAsyncScope())
        {
            var before = await scope.ServiceProvider.GetRequiredService<CitedQuestionHandler>().GetAsync(emptyId, default);
            Assert.Equal("EvidenceReady", before.Stage);
            Assert.Null(before.Answer); // GET must remain read-only.
        }
        async Task<CitedQuestionView> Finish()
        {
            await using var scope = services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<CitedQuestionHandler>()
                .HandleAsync("unknown", "no-evidence", [graph.Index.Id], 5, default);
        }
        var racing = await Task.WhenAll(Finish(), Finish());
        Assert.Equal(racing[0].Answer!.Id, racing[1].Answer!.Id);
        Assert.All(racing, result =>
        {
            Assert.Equal("InsufficientEvidence", result.Stage);
            Assert.False(result.AnswerGenerationEnabled);
            Assert.Equal("Completed", result.Retrieval.Status);
            Assert.Empty(result.Retrieval.Hits);
        });
        Assert.Equal(racing[0].Answer, (await Finish()).Answer);
        await using (var db = _database.CreateContext())
        {
            var persisted = await db.CitedRetrievalRuns.Include(x => x.Answer).ThenInclude(x => x!.Claims)
                .SingleAsync(x => x.Id == emptyId);
            Assert.Equal(7, persisted.RetrievalDurationMs);
            Assert.NotNull(persisted.CompletedAtUtc);
            Assert.Empty(persisted.Answer!.Claims);
            Assert.Equal("no_relevant_evidence", persisted.Answer.ReasonCode);
            Assert.Equal(CitedQuestionHandler.NoEvidencePolicy, persisted.Answer.PromptVersion);
            Assert.Null(persisted.Answer.EstimatedCost);
            Assert.Equal(1, await db.CitedAnswers.CountAsync(x => x.RetrievalRunId == emptyId));
        }
        foreach (var identity in new[] { (graph.Seed.OrganizationB, graph.Seed.ActorB),
                     (graph.Seed.OrganizationA, Guid.NewGuid()) })
        {
            await using var foreign = CitedServices(identity.Item1, identity.Item2, "http://127.0.0.1:1");
            await using var scope = foreign.CreateAsyncScope();
            Assert.Equal(404, (await Assert.ThrowsAsync<CitedWorkflowException>(() =>
                scope.ServiceProvider.GetRequiredService<CitedQuestionHandler>().GetAsync(emptyId, default))).Status);
        }
    }

    [Fact]
    public async Task Retrieved_hits_remain_unanswered_and_structural_validation_uses_persisted_citations()
    {
        var graph = await SeedCitedGraphAsync();
        Guid runId;
        await using (var db = _database.CreateContext())
        {
            var run = StoredQuestion(graph, "total", "with-evidence");
            run.AddHit(Guid.NewGuid(), graph.Chunk, 1, 1, null, null);
            run.MarkRetrieved(5);
            runId = run.Id;
            db.CitedRetrievalRuns.Add(run);
            await db.SaveChangesAsync();
        }
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        await using var scope = services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<CitedQuestionHandler>();
        var result = await handler.HandleAsync("total", "with-evidence", [graph.Index.Id], 5, default);
        Assert.Equal("EvidenceReady", result.Stage);
        Assert.Null(result.Answer);
        Assert.False(result.AnswerGenerationEnabled);
        var repository = scope.ServiceProvider.GetRequiredService<ICitedQuestionRepository>();
        var persisted = (await repository.GetRunAsync(graph.Seed.OrganizationA, runId, default))!;
        var chunks = await scope.ServiceProvider.GetRequiredService<ICitedRetrievalStore>().GetHitChunksAsync(persisted, default);
        var proposal = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = "1.0", status = "Answered",
            reasonCode = (string?)null, claims = new[] { new { text = "Total is 100.00", citations = new[] {
                new { hitId = persisted.Hits.Single().Id, spanId = graph.Chunk.Spans.Single().Id } } } },
            unansweredParts = Array.Empty<string>() });
        var validated = CitedAnswerValidator.Validate(proposal, persisted, chunks);
        Assert.Equal("Total 100.00", validated.Claims.Single().Citations.Single().OriginalPageEvidence!.Quote);
        Assert.False(validated.SemanticSupportEstablished);
        Assert.Null(persisted.Answer);
        Assert.Equal(CitedRetrievalStatus.Retrieved, persisted.Status);
    }

    private static CitedRetrievalRun StoredQuestion(CitedSeed graph, string question, string key) =>
        new(Guid.NewGuid(), graph.Seed.OrganizationA, graph.Seed.ActorA, question, key,
            LexicalPlan.CurrentVersion, JsonSerializer.Serialize(Plan(question, question), new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            [graph.Index], DateTimeOffset.UtcNow);
}
