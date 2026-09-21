using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.CitedQuestions;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public async Task Workspace_lists_only_completed_scoped_reviews_and_opens_exact_pinned_page()
    {
        var graph = await SeedCitedGraphAsync();
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        await using var scope = services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<CitedWorkspaceHandler>();
        var source = Assert.Single((await handler.SourcesAsync(0, default)).Sources);
        Assert.Equal(graph.Seed.DocumentA, source.DocumentId);
        Assert.Equal(graph.Seed.SessionA, source.ReviewSessionId);
        Assert.Empty((await handler.SourcesAsync(50, default)).Sources);
        var hit = graph.Request.Hits.Single();
        var span = graph.Chunk.Spans.Single();
        var opened = await handler.EvidenceAsync(graph.Request.Id, hit.Id, span.Id, default);
        Assert.Equal(graph.Index.Filename, opened.Filename);
        Assert.Equal(graph.Seed.RunA, opened.ExtractionRunId);
        Assert.Equal("Total 100.00", opened.Page!.Before + opened.Page.Quote + opened.Page.After);
        Assert.Equal(span.Quote, opened.Page.Quote);
        Assert.Equal(span.ByteStart, opened.Page.ByteStart);
        Assert.Null(opened.Review);
        Assert.Null(opened.ReviewedFact);
    }

    [Fact]
    public async Task Evidence_open_rejects_foreign_actor_unknown_swapped_and_retired_sources()
    {
        var graph = await SeedCitedGraphAsync();
        var hit = graph.Request.Hits.Single();
        var span = graph.Chunk.Spans.Single();
        foreach (var identity in new[] { (graph.Seed.OrganizationB, graph.Seed.ActorB), (graph.Seed.OrganizationA, Guid.NewGuid()) })
        {
            await using var other = CitedServices(identity.Item1, identity.Item2, "http://127.0.0.1:1");
            await using var otherScope = other.CreateAsyncScope();
            var foreign = otherScope.ServiceProvider.GetRequiredService<CitedWorkspaceHandler>();
            Assert.Equal(404, (await Assert.ThrowsAsync<CitedWorkflowException>(() => foreign.EvidenceAsync(graph.Request.Id, hit.Id, span.Id, default))).Status);
        }
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        await using var scope = services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<CitedWorkspaceHandler>();
        foreach (var ids in new[] { (Guid.NewGuid(), span.Id), (hit.Id, Guid.NewGuid()), (hit.Id, Guid.Empty) })
            Assert.Equal(404, (await Assert.ThrowsAsync<CitedWorkflowException>(() => handler.EvidenceAsync(graph.Request.Id, ids.Item1, ids.Item2, default))).Status);
        Assert.Equal(404, (await Assert.ThrowsAsync<CitedWorkflowException>(() => handler.EvidenceAsync(graph.Request.Id, hit.Id, null, default))).Status);
        await using (var db = _database.CreateContext())
        {
            var index = await db.CitedDocumentIndexes.SingleAsync(x => x.Id == graph.Index.Id);
            index.Retire(true);
            await db.SaveChangesAsync();
        }
        Assert.Equal("evidence_unavailable", (await Assert.ThrowsAsync<CitedWorkflowException>(() => handler.EvidenceAsync(graph.Request.Id, hit.Id, span.Id, default))).Code);
    }

    [Fact]
    public async Task Reviewed_provenance_opens_without_inventing_page_and_corrupt_page_quotes_fail_closed()
    {
        var graph = await SeedCitedGraphAsync();
        await using var python = await PrivateCitedServer.StartAsync();
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, python.Url);
        await using var scope = services.CreateAsyncScope();
        var index = await scope.ServiceProvider.GetRequiredService<IndexReviewedDocumentHandler>()
            .HandleAsync(graph.Seed.DocumentA, graph.Seed.SessionA, default);
        var result = await scope.ServiceProvider.GetRequiredService<RetrieveEvidenceHandler>()
            .HandleAsync("total", "workspace-fact", [index.Id], 5, default);
        var fact = Assert.Single(result.Hits.Where(x => x.Kind == "ReviewedFact"));
        var handler = scope.ServiceProvider.GetRequiredService<CitedWorkspaceHandler>();
        var review = await handler.EvidenceAsync(result.Id, fact.HitId, null, default);
        Assert.Null(review.Page);
        Assert.Equal("total[0]: 100.00", review.ReviewedFact);
        Assert.Equal(graph.Seed.ActionA, review.Review!.ActionId);
        await ExecuteAsync("UPDATE extraction_pages SET canonical_text='Total 999.00' WHERE id=@id", ("id", graph.Seed.PageA));
        var pageHit = Assert.Single(result.Hits.Where(x => x.Kind == "PageText"));
        Assert.Equal("invalid_evidence_span", (await Assert.ThrowsAsync<CitedWorkflowException>(() =>
            handler.EvidenceAsync(result.Id, pageHit.HitId, pageHit.Spans.Single().Id, default))).Code);
    }
}
