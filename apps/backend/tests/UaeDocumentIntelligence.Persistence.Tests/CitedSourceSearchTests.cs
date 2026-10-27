using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.CitedQuestions;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public async Task Reviewed_source_search_pages_ten_and_keeps_scope_literal_text_and_completed_revisions()
    {
        var seed = await SeedGraphAsync();
        await ExecuteAsync("UPDATE review_sessions SET status='Completed',completed_at_utc=now() WHERE id=@id", ("id", seed.SessionA));
        for (var i = 0; i < 23; i++)
        {
            var document = Guid.NewGuid();
            var run = Guid.NewGuid();
            var review = Guid.NewGuid();
            var filename = i == 21 ? "فاتورة 21_%~.pdf" : $"review-item-{i:D2}.pdf";
            await ExecuteAsync(InsertDocumentSql, DocumentParameters(seed.OrganizationA, seed.ActorA, document, filename, ValidSha));
            await ExecuteAsync("UPDATE documents SET original_filename=@name WHERE id=@id", ("name", filename), ("id", document));
            await ExecuteAsync(InsertRunSql, RunParameters(seed.OrganizationA, document, run, 1, $"review-run-{i}"));
            await ExecuteAsync(InsertReviewSessionSql, ReviewSessionParameters(seed.OrganizationA, document, run, review, 1));
            if (i < 22)
                await ExecuteAsync("UPDATE review_sessions SET status='Completed',completed_at_utc=now() WHERE id=@id", ("id", review));
        }
        var foreignReview = Guid.NewGuid();
        await ExecuteAsync("UPDATE documents SET original_filename='org-b-document.pdf' WHERE id=@id", ("id", seed.DocumentB));
        await ExecuteAsync(InsertReviewSessionSql, ReviewSessionParameters(seed.OrganizationB, seed.DocumentB, seed.RunB, foreignReview, 1));
        await ExecuteAsync("UPDATE review_sessions SET status='Completed',completed_at_utc=now() WHERE id=@id", ("id", foreignReview));
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        await using var scope = services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<CitedWorkspaceHandler>();
        var first = await handler.SourcesAsync(0, default);
        var second = await handler.SourcesAsync(first.NextOffset!.Value, default);
        var last = await handler.SourcesAsync(second.NextOffset!.Value, default);
        Assert.Equal(10, first.Sources.Count);
        Assert.Equal(10, second.Sources.Count);
        Assert.Equal(3, last.Sources.Count);
        Assert.Null(last.NextOffset);
        Assert.Equal(23, first.Sources.Concat(second.Sources).Concat(last.Sources).Select(x => x.ReviewSessionId).Distinct().Count());
        var match = Assert.Single((await handler.SourcesAsync(0, default, last.Sources[0].Filename.ToUpperInvariant())).Sources);
        Assert.Equal(last.Sources[0].ReviewSessionId, match.ReviewSessionId);
        Assert.Empty((await handler.SourcesAsync(0, default, "org-b-document")).Sources);
        Assert.Empty((await handler.SourcesAsync(0, default, "review-item-22")).Sources);
        Assert.Single((await handler.SourcesAsync(0, default, "  فاتورة 21_%~  ")).Sources);
        Assert.Empty((await handler.SourcesAsync(0, default, "nonexistent%_")).Sources);
        await Assert.ThrowsAsync<ArgumentException>(() => handler.SourcesAsync(0, default, new string('a', 201)));
        await Assert.ThrowsAsync<ArgumentException>(() => handler.SourcesAsync(-1, default));
    }
}
