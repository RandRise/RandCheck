using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.Reconciliation;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public async Task Report_snapshot_does_not_mix_a_decision_committed_between_split_queries()
    {
        var seed = await SeedGraphAsync();
        var item = await SupplierPairStoredFixture(seed.OrganizationA, seed.ActorA, "1260", "1261");
        await using var writers = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        ReconciliationCaseView compared;
        await using (var scope = writers.CreateAsyncScope())
            compared = await scope.ServiceProvider.GetRequiredService<EvaluateReconciliationCaseHandler>()
                .HandleAsync(item.Id, item.ConcurrencyToken, new(RuleSetVersion: "invoice-po-2.0"), default);
        var run = Assert.Single(compared.Runs);
        var finding = run.Findings.First();
        ReconciliationCaseView? changed = null;
        var barrier = new ReportReadBarrier(async () =>
        {
            await using var scope = writers.CreateAsyncScope();
            changed = await scope.ServiceProvider.GetRequiredService<ResolveReconciliationFindingHandler>()
                .HandleAsync(item.Id, run.Id, finding.Id, compared.ConcurrencyToken,
                    new("Accepted", "Decision committed during export"), default);
        });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:Database"] = _database.TestConnectionString }).Build();
        var registrations = new ServiceCollection().AddInfrastructure(configuration);
        registrations.AddSingleton<IExecutionContext>(new CitedExecution(seed.OrganizationA, seed.ActorA));
        registrations.AddSingleton(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_database.TestConnectionString).AddInterceptors(barrier).Options);
        await using var readers = registrations.BuildServiceProvider();
        ReconciliationReportView captured;
        await using (var scope = readers.CreateAsyncScope())
            captured = await scope.ServiceProvider.GetRequiredService<GetReconciliationReportHandler>()
                .HandleAsync(item.Id, run.Id, compared.ConcurrencyToken, default);

        Assert.True(barrier.Triggered);
        Assert.Equal(IsolationLevel.RepeatableRead, barrier.Isolation);
        Assert.NotNull(changed);
        Assert.Equal(compared.ConcurrencyToken, captured.CaseVersion);
        Assert.Empty(captured.Run.Findings.SelectMany(value => value.Resolutions));
        Assert.Equal(run.Checks!.OrderBy(value => value.Id), captured.Run.Checks!.OrderBy(value => value.Id));
        await using var after = writers.CreateAsyncScope();
        var handler = after.ServiceProvider.GetRequiredService<GetReconciliationReportHandler>();
        await Assert.ThrowsAsync<ReconciliationPreconditionException>(() => handler.HandleAsync(item.Id, run.Id, compared.ConcurrencyToken, default));
        var current = await handler.HandleAsync(item.Id, run.Id, changed.ConcurrencyToken, default);
        Assert.Single(current.Run.Findings.SelectMany(value => value.Resolutions));
        Assert.NotEqual(captured.CaseVersion, current.CaseVersion);
        Assert.Equal(captured.Run.Coverage, current.Run.Coverage);
    }

    [Fact]
    public async Task Report_old_run_keeps_original_review_sources_after_repin_and_rejects_foreign_scope()
    {
        var seed = await SeedGraphAsync();
        var item = await SupplierPairStoredFixture(seed.OrganizationA, seed.ActorA, "1260", "1261");
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        ReconciliationCaseView first;
        await using (var scope = services.CreateAsyncScope())
            first = await scope.ServiceProvider.GetRequiredService<EvaluateReconciliationCaseHandler>()
                .HandleAsync(item.Id, item.ConcurrencyToken, new(RuleSetVersion: "invoice-po-2.0"), default);
        var original = Assert.Single(first.Runs);
        var replacement = await SupplierPairStoredFixture(seed.OrganizationA, seed.ActorA, "1260", "1400");
        var invoice = replacement.DocumentLinks.Single(link => link.Role == ReconciliationDocumentRole.Invoice);
        ReconciliationCaseView newer;
        await using (var scope = services.CreateAsyncScope())
        {
            var pinned = await scope.ServiceProvider.GetRequiredService<LinkReconciliationDocumentHandler>()
                .HandleAsync(item.Id, first.ConcurrencyToken, new("Invoice", invoice.DocumentId, invoice.ReviewSessionId), default);
            newer = await scope.ServiceProvider.GetRequiredService<EvaluateReconciliationCaseHandler>()
                .HandleAsync(item.Id, pinned.ConcurrencyToken, new(RuleSetVersion: "invoice-po-2.0"), default);
        }
        await using (var scope = services.CreateAsyncScope())
        {
            var report = await scope.ServiceProvider.GetRequiredService<GetReconciliationReportHandler>()
                .HandleAsync(item.Id, original.Id, newer.ConcurrencyToken, default);
            Assert.Equal(original.Id, report.Run.Id);
            Assert.Equal(original.Sources!.OrderBy(value => value.Id), report.Run.Sources!.OrderBy(value => value.Id));
            Assert.Equal(original.Fields.OrderBy(value => value.Id), report.Run.Fields.OrderBy(value => value.Id));
            Assert.Equal(original.Checks!.OrderBy(value => value.Id), report.Run.Checks!.OrderBy(value => value.Id));
            Assert.DoesNotContain(report.Run.Sources!, source => source.DocumentId == invoice.DocumentId);
        }
        await using var foreign = CitedServices(seed.OrganizationB, seed.ActorB, "http://127.0.0.1:1");
        await using var other = foreign.CreateAsyncScope();
        await Assert.ThrowsAsync<ReconciliationNotFoundException>(() => other.ServiceProvider.GetRequiredService<GetReconciliationReportHandler>()
            .HandleAsync(item.Id, original.Id, newer.ConcurrencyToken, default));
    }

    private sealed class ReportReadBarrier(Func<Task> betweenQueries) : DbCommandInterceptor
    {
        private int reads;
        public bool Triggered { get; private set; }
        public IsolationLevel? Isolation { get; private set; }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref reads) == 2)
            {
                Isolation = command.Transaction?.IsolationLevel;
                await betweenQueries();
                Triggered = true;
            }
            return result;
        }
    }
}
