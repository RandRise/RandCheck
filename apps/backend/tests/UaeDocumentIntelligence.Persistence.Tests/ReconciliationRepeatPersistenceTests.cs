using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.Reconciliation;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public async Task Case_repeat_creation_is_durable_concurrent_actor_tenant_scoped_and_audited_once()
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allMissed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var misses = 0;
        var tasks = Enumerable.Range(0, 6).Select(async _ => {
            await using var scope = services.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<SecurityAuditContext>().Bind(seed.OrganizationA, seed.ActorA, "System", "repeat-create");
            await gate.Task;
            var repository = new CreationBarrierRepository(scope.ServiceProvider.GetRequiredService<IReconciliationRepository>(), async () => {
                if (Interlocked.Increment(ref misses) == 6) allMissed.SetResult();
                await allMissed.Task.WaitAsync(TimeSpan.FromSeconds(20));
            });
            return await new CreateReconciliationCaseHandler(repository, new CitedExecution(seed.OrganizationA, seed.ActorA),
                    scope.ServiceProvider.GetRequiredService<IClock>())
                .HandleAsync("Original case", default, "Equipment", "durable-intent");
        }).ToArray();
        gate.SetResult(); var results = await Task.WhenAll(tasks);
        Assert.Equal(6, misses);
        Assert.Single(results.Select(x => x.Id).Distinct()); Assert.Single(results.Select(x => x.Reference).Distinct());
        var saved = results[0];
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<UpdateReconciliationCaseMetadataHandler>()
                .HandleAsync(saved.Id, saved.ConcurrencyToken, "Renamed", "Services", default);
        }
        // A fresh provider/context models a later process/request, not an in-memory replay cache.
        await using var fresh = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        await using (var scope = fresh.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<CreateReconciliationCaseHandler>();
            var replay = await handler.HandleAsync(" Original case ", default, "Equipment", "durable-intent");
            Assert.Equal(saved.Id, replay.Id); Assert.Equal(saved.Reference, replay.Reference);
            Assert.Equal("Renamed", replay.Name); Assert.Equal("Services", replay.Category);
            var conflict = await Assert.ThrowsAsync<ReconciliationConflictException>(() => handler.HandleAsync("Different", default, "Equipment", "durable-intent"));
            Assert.Equal("request_key_reused", conflict.Code);
        }
        var otherActor = Guid.NewGuid();
        await ExecuteAsync("INSERT INTO actors(id,organization_id,kind,display_name,is_active,created_at_utc) VALUES(@id,@org,'System','Other actor',true,now())", ("id", otherActor), ("org", seed.OrganizationA));
        foreach (var identity in new[] { (seed.OrganizationA, otherActor), (seed.OrganizationB, seed.ActorB) })
        {
            await using var other = CitedServices(identity.Item1, identity.Item2, "http://127.0.0.1:1");
            await using var scope = other.CreateAsyncScope();
            var distinct = await scope.ServiceProvider.GetRequiredService<CreateReconciliationCaseHandler>()
                .HandleAsync("Original case", default, "Equipment", "durable-intent");
            Assert.NotEqual(saved.Id, distinct.Id);
        }
        await using var db = _database.CreateContext();
        Assert.Equal(3, await db.ReconciliationCases.CountAsync());
        Assert.Equal(1, await db.SecurityAuditEvents.CountAsync(x => x.TargetId == saved.Id && x.Action == "reconciliation.case-changed"));
        await AssertSqlStateAsync("55000", "UPDATE reconciliation_cases SET creation_request_key='changed' WHERE id=@id", ("id", saved.Id));
        await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20260930123528_AddReconciliationCaseMetadata"));
    }

    [Fact]
    public async Task Case_repeat_creation_works_under_restricted_api_role_and_hides_immutable_request_identity()
    {
        var seed = await SeedGraphAsync(); await using var role = await ApiRoleAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:Database"] = role.Connection, ["AiService:BaseUrl"] = "http://127.0.0.1:1",
            ["AiService:WorkerToken"] = "test-only-token" }).Build();
        await using var services = new ServiceCollection().AddInfrastructure(config)
            .AddSingleton<IExecutionContext>(new CitedExecution(seed.OrganizationA, seed.ActorA)).BuildServiceProvider();
        ReconciliationCaseView first;
        await using (var scope = services.CreateAsyncScope())
            first = await scope.ServiceProvider.GetRequiredService<CreateReconciliationCaseHandler>().HandleAsync("Case", default, null, "restricted-intent");
        await using (var scope = services.CreateAsyncScope())
        {
            var retry = await scope.ServiceProvider.GetRequiredService<CreateReconciliationCaseHandler>().HandleAsync("Case", default, null, "restricted-intent");
            Assert.Equal(first.Id, retry.Id); Assert.Equal(first.Reference, retry.Reference);
        }
        await using var restricted = role.Db();
        foreach (var sql in new[] { "UPDATE reconciliation_cases SET creation_request_key='changed'",
            "UPDATE reconciliation_cases SET creation_payload_hash=repeat('a',64)", "UPDATE reconciliation_cases SET created_by_actor_id=gen_random_uuid()" })
            Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => restricted.Database.ExecuteSqlRawAsync(sql))).SqlState);
        await ApiDatabasePolicy.VerifyAsync(restricted, role.Role, true, default);
    }

    [Fact]
    public async Task Case_repeat_evaluation_concurrency_commits_one_run_and_noops_preserve_history_and_token()
    {
        var seed = await SeedGraphAsync(); var now = DateTimeOffset.UtcNow;
        await ExecuteAsync("UPDATE review_sessions SET status='Completed',completed_at_utc=@now WHERE id=@id", ("now", now), ("id", seed.SessionA));
        var item = new ReconciliationCase(Guid.NewGuid(), seed.OrganizationA, seed.ActorA, "Case", now);
        item.LinkReviewedDocument(Guid.NewGuid(), ReconciliationDocumentRole.Invoice, seed.DocumentA, seed.SessionA, seed.RunA, ValidSha, now);
        await using (var db = _database.CreateContext()) { db.ReconciliationCases.Add(item); await db.SaveChangesAsync(); }
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        await using var one = services.CreateAsyncScope(); await using var two = services.CreateAsyncScope();
        foreach (var scope in new[] { one, two })
            await scope.ServiceProvider.GetRequiredService<IReconciliationRepository>().GetAsync(seed.OrganizationA, item.Id, default);
        async Task<bool> Evaluate(AsyncServiceScope scope)
        {
            try { await scope.ServiceProvider.GetRequiredService<EvaluateReconciliationCaseHandler>().HandleAsync(item.Id, item.ConcurrencyToken, new(), default); return true; }
            catch (ReconciliationPreconditionException) { return false; }
        }
        var results = await Task.WhenAll(Evaluate(one), Evaluate(two)); Assert.Single(results.Where(x => x));
        await using var check = services.CreateAsyncScope();
        var repo = check.ServiceProvider.GetRequiredService<IReconciliationRepository>();
        var saved = (await repo.GetAsync(seed.OrganizationA, item.Id, default))!;
        var run = Assert.Single(saved.Runs); var finding = run.Findings.First(); var token = saved.ConcurrencyToken;
        var pinned = await check.ServiceProvider.GetRequiredService<LinkReconciliationDocumentHandler>().HandleAsync(item.Id, token,
            new("Invoice", seed.DocumentA, seed.SessionA), default);
        Assert.Equal(token, pinned.ConcurrencyToken); Assert.Equal(item.DocumentLinks.Single().Id, pinned.Documents.Single().Id);
        var decide = check.ServiceProvider.GetRequiredService<ResolveReconciliationFindingHandler>();
        var decided = await decide.HandleAsync(item.Id, run.Id, finding.Id, token, new("Accepted", "Reviewed"), default);
        var repeat = await decide.HandleAsync(item.Id, run.Id, finding.Id, decided.ConcurrencyToken, new("Accepted", " Reviewed "), default);
        Assert.Equal(decided.ConcurrencyToken, repeat.ConcurrencyToken);
        await using var final = _database.CreateContext();
        Assert.Equal(1, await final.ReconciliationRuns.CountAsync()); Assert.Equal(1, await final.FindingResolutions.CountAsync());
        Assert.Equal(1, await final.ReconciliationDocumentLinks.CountAsync());
    }

    [Fact]
    public async Task Case_repeat_pin_and_decision_concurrency_returns_stale_without_duplicate_history()
    {
        var seed = await SeedGraphAsync(); var now = DateTimeOffset.UtcNow;
        await ExecuteAsync("UPDATE review_sessions SET status='Completed',completed_at_utc=@now WHERE id=@id", ("now", now), ("id", seed.SessionA));
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        ReconciliationCaseView created;
        await using (var create = services.CreateAsyncScope())
            created = await create.ServiceProvider.GetRequiredService<CreateReconciliationCaseHandler>().HandleAsync("Case", default);
        async Task Race(Func<IServiceProvider, IReconciliationRepository, Task> action)
        {
            await using var first = services.CreateAsyncScope(); await using var second = services.CreateAsyncScope();
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var waiting = 0;
            foreach (var scope in new[] { first, second })
                await scope.ServiceProvider.GetRequiredService<IReconciliationRepository>().GetAsync(seed.OrganizationA, created.Id, default);
            async Task<bool> Run(AsyncServiceScope scope)
            {
                var repository = new CreationBarrierRepository(scope.ServiceProvider.GetRequiredService<IReconciliationRepository>(),
                    () => Task.CompletedTask, async () => {
                        if (Interlocked.Increment(ref waiting) == 2) ready.SetResult();
                        await ready.Task.WaitAsync(TimeSpan.FromSeconds(20));
                    });
                try { await action(scope.ServiceProvider, repository); return true; }
                catch (ReconciliationPreconditionException) { return false; }
            }
            var outcomes = await Task.WhenAll(Run(first), Run(second)); Assert.Single(outcomes.Where(x => x));
        }
        await Race((provider, repository) => new LinkReconciliationDocumentHandler(repository,
            provider.GetRequiredService<IDocumentRepository>(), new CitedExecution(seed.OrganizationA, seed.ActorA),
            provider.GetRequiredService<IClock>()).HandleAsync(created.Id,
            created.ConcurrencyToken, new("Invoice", seed.DocumentA, seed.SessionA), default));
        ReconciliationCaseView evaluated;
        await using (var evaluate = services.CreateAsyncScope())
        {
            var saved = await evaluate.ServiceProvider.GetRequiredService<GetReconciliationCaseHandler>().HandleAsync(created.Id, default);
            evaluated = await evaluate.ServiceProvider.GetRequiredService<EvaluateReconciliationCaseHandler>()
                .HandleAsync(created.Id, saved.ConcurrencyToken, new(), default);
        }
        var run = Assert.Single(evaluated.Runs); var finding = run.Findings.First();
        await Race((provider, repository) => new ResolveReconciliationFindingHandler(repository,
            new CitedExecution(seed.OrganizationA, seed.ActorA), provider.GetRequiredService<IClock>()).HandleAsync(created.Id,
            run.Id, finding.Id, evaluated.ConcurrencyToken, new("Accepted", "Checked"), default));
        await using var db = _database.CreateContext();
        Assert.Equal(1, await db.ReconciliationDocumentLinks.CountAsync()); Assert.Equal(1, await db.FindingResolutions.CountAsync());
    }

    private sealed class CreationBarrierRepository(IReconciliationRepository inner, Func<Task> wait, Func<Task>? beforeSave = null) : IReconciliationRepository
    {
        private bool _firstLookup = true;
        public async Task<ReconciliationCase?> GetByCreationRequestAsync(Guid org, Guid actor, string key, CancellationToken ct)
        {
            var value = await inner.GetByCreationRequestAsync(org, actor, key, ct);
            if (_firstLookup) { _firstLookup = false; Assert.Null(value); await wait(); }
            return value;
        }
        public Task AddAsync(ReconciliationCase item, CancellationToken ct) => inner.AddAsync(item, ct);
        public Task<ReconciliationCase?> GetAsync(Guid org, Guid id, CancellationToken ct) => inner.GetAsync(org, id, ct);
        public async Task<ReconciliationSaveOutcome> SaveChangesAsync(CancellationToken ct)
        { if (beforeSave is not null) await beforeSave(); return await inner.SaveChangesAsync(ct); }
        public Task<IReadOnlyList<ReconciliationCaseSummary>> ListAsync(Guid org, int offset, int limit, CancellationToken ct,
            ReconciliationCaseFilter? filter = null) => inner.ListAsync(org, offset, limit, ct, filter);
    }
}
