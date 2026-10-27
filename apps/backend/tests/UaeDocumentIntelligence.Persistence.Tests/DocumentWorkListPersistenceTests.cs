using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Api;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.Documents;
using UaeDocumentIntelligence.Application.Documents.Queries;
using UaeDocumentIntelligence.Infrastructure;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public async Task Document_work_list_projects_one_detached_query_without_values_history_or_foreign_tenant()
    {
        var seed = await SeedGraphAsync(); var own = new List<Guid> { seed.DocumentA };
        for (var i = 0; i < 5; i++)
        {
            var id = Guid.NewGuid(); own.Add(id);
            await ExecuteAsync(InsertDocumentSql, DocumentParameters(seed.OrganizationA, seed.ActorA, id, $"summary-{i}.pdf", ValidSha));
            await ExecuteAsync(InsertRunSql, RunParameters(seed.OrganizationA, id, Guid.NewGuid(), 1, $"summary-{i}"));
        }
        await ExecuteAsync("UPDATE documents SET created_at_utc='2026-09-01T00:00:00Z',updated_at_utc=now() WHERE organization_id=@org", ("org", seed.OrganizationA));
        await ExecuteAsync("UPDATE documents SET original_filename='فاتورة — invoice.pdf' WHERE id=@id", ("id", seed.DocumentA));
        var capture = new WorkListQueryCapture();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:Database"] = _database.TestConnectionString,
            ["AiService:BaseUrl"] = "http://127.0.0.1:1", ["AiService:WorkerToken"] = "synthetic-only" }).Build();
        await using var services = new ServiceCollection().AddInfrastructure(config)
            .AddSingleton(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_database.TestConnectionString).AddInterceptors(capture).Options)
            .AddSingleton<IExecutionContext>(new CitedExecution(seed.OrganizationA, seed.ActorA)).BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var list = await scope.ServiceProvider.GetRequiredService<DocumentQueries>().WorkListAsync(default);
        Assert.Equal(own.Order(), list.Select(x => x.Id));
        Assert.DoesNotContain(list, x => x.Id == seed.DocumentB);
        Assert.Equal("فاتورة — invoice.pdf", list.Single(x => x.Id == seed.DocumentA).OriginalFilename);
        Assert.Equal("InProgress", list.Single(x => x.Id == seed.DocumentA).ReviewState);
        Assert.All(list.Where(x => x.Id != seed.DocumentA), x => Assert.Equal("NeedsReview", x.ReviewState));
        Assert.Empty(scope.ServiceProvider.GetRequiredService<AppDbContext>().ChangeTracker.Entries());
        var sql = Assert.Single(capture.Reads);
        foreach (var forbidden in new[] { "machine_field_values", "field_evidence", "extraction_pages", "review_actions" })
            Assert.DoesNotContain(forbidden, sql, StringComparison.OrdinalIgnoreCase);
        await using var foreign = CitedServices(seed.OrganizationB, seed.ActorB, "http://127.0.0.1:1");
        await using var foreignScope = foreign.CreateAsyncScope();
        Assert.Equal(seed.DocumentB, Assert.Single(await foreignScope.ServiceProvider.GetRequiredService<DocumentQueries>().WorkListAsync(default)).Id);
    }

    [Fact]
    public async Task Document_work_list_keeps_completed_and_active_review_context_separate_from_latest_processing()
    {
        var seed = await SeedGraphAsync();
        async Task<DocumentWorkItem> Read()
        {
            await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
            await using var scope = services.CreateAsyncScope();
            return Assert.Single(await scope.ServiceProvider.GetRequiredService<DocumentQueries>().WorkListAsync(default));
        }
        var initial = await Read(); Assert.Equal("InProgress", initial.ReviewState); Assert.False(initial.ReviewUsesOlderExtraction);
        await ExecuteAsync("UPDATE review_sessions SET status='Completed',completed_at_utc=now() WHERE id=@id", ("id", seed.SessionA));
        var reviewed = await Read(); Assert.Equal("Reviewed", reviewed.ReviewState); Assert.Equal(seed.SessionA, reviewed.LatestCompletedReviewId);
        var queuedRun = Guid.NewGuid();
        await ExecuteAsync(InsertRunSql, RunParameters(seed.OrganizationA, seed.DocumentA, queuedRun, 2, "summary-queued", "Queued"));
        // Aggregate ReadyForReview intentionally retains the older usable output.
        var processing = await Read(); Assert.Equal("Ready", processing.Status); Assert.Equal("Queued", processing.LatestExtractionStatus);
        Assert.Equal("Reviewed", processing.ReviewState);
        Assert.Equal(seed.RunA, processing.SelectedExtractionRunId); Assert.Equal(seed.SessionA, processing.LatestCompletedReviewId);
        await ExecuteAsync("UPDATE extraction_runs SET status='Failed',failure_code='provider_failure',safe_failure_summary='Safe failure.' WHERE id=@id", ("id", queuedRun));
        var failed = await Read(); Assert.Equal("Reviewed", failed.ReviewState); Assert.Equal(seed.RunA, failed.SelectedExtractionRunId);
        Assert.Equal("Ready", failed.Status); Assert.Equal("Failed", failed.LatestExtractionStatus);
        Assert.Equal("Safe failure.", failed.ErrorMessage);
        var activeId = Guid.NewGuid();
        var reviewRun = Guid.NewGuid();
        await ExecuteAsync(InsertRunSql, RunParameters(seed.OrganizationA, seed.DocumentA, reviewRun, 3, "summary-review-run"));
        // A later successful extraction can arrive while an already started review
        // is still editing its older successful output.
        await ExecuteAsync(InsertReviewSessionSql, ReviewSessionParameters(seed.OrganizationA, seed.DocumentA, reviewRun, activeId, 2));
        var freshRun = Guid.NewGuid();
        await ExecuteAsync(InsertRunSql, RunParameters(seed.OrganizationA, seed.DocumentA, freshRun, 4, "summary-fresh"));
        var older = await Read(); Assert.Equal("InProgress", older.ReviewState); Assert.True(older.ReviewUsesOlderExtraction);
        Assert.Equal("Ready", older.LatestExtractionStatus);
        Assert.Equal(freshRun, older.SelectedExtractionRunId); Assert.Equal(reviewRun, older.ReviewExtractionRunId);
        await ExecuteAsync("UPDATE review_sessions SET status='Cancelled' WHERE id=@id", ("id", activeId));
        var needsReview = await Read(); Assert.Equal("NeedsReview", needsReview.ReviewState); Assert.False(needsReview.ReviewUsesOlderExtraction);
        Assert.Equal(freshRun, needsReview.ReviewExtractionRunId); Assert.Equal(seed.SessionA, needsReview.LatestCompletedReviewId);
    }

    [Fact]
    public async Task Document_work_list_real_api_is_no_store_scoped_and_follows_each_document_role_independent_of_admin()
    {
        using var own = await SourceFixtureAsync("Staff"); using var foreign = await SourceFixtureAsync("Staff");
        await using var role = await ApiRoleAsync(); using var host = role.Host(own.Host);
        using var user = WithAuthority(host, own.User); var member = own.Member;
        foreach (var access in new[] { ("Admin", true, true), ("Staff", false, true), ("Staff", false, false), ("Admin", true, false) })
        {
            var change = await SecurityChange(own.Admin, member, access.Item1, access.Item2, access.Item3);
            Assert.Equal(HttpStatusCode.OK, change.StatusCode); member = (await change.Content.ReadFromJsonAsync<MembershipView>())!;
            var result = await user.GetAsync("/api/documents/work-list");
            if (!access.Item3) { Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode); continue; }
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Contains("no-store", result.Headers.CacheControl!.ToString());
            var item = Assert.Single((await result.Content.ReadFromJsonAsync<DocumentWorkItem[]>())!);
            Assert.Equal(own.DocumentId, item.Id); Assert.NotEqual(foreign.DocumentId, item.Id);
            Assert.Null(item.ReviewState); Assert.Null(item.SelectedExtractionRunId);
            var body = await result.Content.ReadAsStringAsync(); Assert.DoesNotContain("fields", body, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM documents"));
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM extraction_jobs"));
    }

    private sealed class WorkListQueryCapture : DbCommandInterceptor
    {
        public List<string> Reads { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Reads.Add(command.CommandText); return ValueTask.FromResult(result);
        }
    }
}
