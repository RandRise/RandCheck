using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.Reconciliation;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public async Task Case_list_projects_counts_and_pages_deterministically_without_crossing_tenants()
    {
        var seed = await SeedGraphAsync();
        var now = new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
        await ExecuteAsync("UPDATE review_sessions SET status='Completed', completed_at_utc=@now WHERE id=@id",
            ("now", now), ("id", seed.SessionA));
        // Equal creation times exercise the ID tie-breaker at the page boundary.
        var own = Enumerable.Range(0, 51).Select(i => new ReconciliationCase(Guid.NewGuid(),
            seed.OrganizationA, seed.ActorA, $"Case {i}", now)).ToList();
        var evaluated = own[0];
        evaluated.LinkReviewedDocument(Guid.NewGuid(), ReconciliationDocumentRole.Invoice,
            seed.DocumentA, seed.SessionA, seed.RunA, ValidSha, now);
        var run = evaluated.BeginEvaluation(Guid.NewGuid(),
            new Dictionary<ReconciliationDocumentRole, Guid> { [ReconciliationDocumentRole.Invoice] = Guid.NewGuid() },
            "1.0", ReconciliationTolerancePolicy.Default, now);
        evaluated.CompleteEvaluation(run, now.AddMinutes(1));
        var foreign = new ReconciliationCase(Guid.NewGuid(), seed.OrganizationB, seed.ActorB,
            "Other tenant private case", now.AddDays(1));
        await using (var write = _database.CreateContext())
        {
            write.ReconciliationCases.AddRange([.. own, foreign]);
            await write.SaveChangesAsync();
        }

        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        await using var scope = services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ListReconciliationCasesHandler>();
        var first = await handler.HandleAsync(0, default);
        var last = await handler.HandleAsync(first.NextOffset!.Value, default);
        var listed = first.Cases.Concat(last.Cases).ToArray();
        Assert.Equal(50, first.Cases.Count);
        Assert.Single(last.Cases);
        Assert.Null(last.NextOffset);
        Assert.Equal(own.OrderByDescending(item => item.Id).Select(item => item.Id), listed.Select(item => item.Id));
        Assert.DoesNotContain(listed, item => item.Id == foreign.Id);
        var summary = Assert.Single(listed.Where(item => item.Id == evaluated.Id));
        Assert.Equal("Evaluated", summary.Status);
        Assert.Equal(1, summary.DocumentCount);
        Assert.Equal(1, summary.RunCount);
        Assert.Equal(now.AddMinutes(1), summary.UpdatedAt);
        Assert.Empty(scope.ServiceProvider.GetRequiredService<UaeDocumentIntelligence.Infrastructure.Persistence.AppDbContext>()
            .ChangeTracker.Entries());

        await using var otherServices = CitedServices(seed.OrganizationB, seed.ActorB, "http://127.0.0.1:1");
        await using var otherScope = otherServices.CreateAsyncScope();
        var other = await otherScope.ServiceProvider.GetRequiredService<ListReconciliationCasesHandler>().HandleAsync(0, default);
        Assert.Equal(foreign.Id, Assert.Single(other.Cases).Id);
        Assert.Null(other.NextOffset);
    }
}
