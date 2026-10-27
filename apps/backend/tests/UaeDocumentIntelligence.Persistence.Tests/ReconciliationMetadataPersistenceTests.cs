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
    public async Task Case_metadata_migration_backfills_legacy_titles_by_utc_year_and_preserves_ids()
    {
        await using var db = _database.CreateContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260914125645_AddOrganizationCapacity");
        var seed = await SeedGraphAsync();
        var first = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var third = Guid.NewGuid(); var foreign = Guid.NewGuid();
        var yearEnd = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero);
        foreach (var row in new[] { (second, seed.OrganizationA, seed.ActorA, yearEnd),
                     (first, seed.OrganizationA, seed.ActorA, yearEnd),
                     (third, seed.OrganizationA, seed.ActorA, yearEnd.AddSeconds(1)),
                     (foreign, seed.OrganizationB, seed.ActorB, yearEnd) })
            await ExecuteAsync("""
                INSERT INTO reconciliation_cases(id,organization_id,created_by_actor_id,name,status,concurrency_token,created_at_utc,updated_at_utc)
                VALUES(@id,@org,@actor,'عنوان قديم · unchanged','Draft',@token,@now,@now)
                """, ("id", row.Item1), ("org", row.Item2), ("actor", row.Item3), ("token", Guid.NewGuid()), ("now", row.Item4));
        await migrator.MigrateAsync();
        var rows = await db.ReconciliationCases.AsNoTracking().ToDictionaryAsync(x => x.Id);
        Assert.Equal("REC-2026-000001", rows[first].Reference);
        Assert.Equal("REC-2026-000002", rows[second].Reference);
        Assert.Equal("REC-2027-000001", rows[third].Reference);
        Assert.Equal("REC-2026-000001", rows[foreign].Reference);
        Assert.All(rows.Values, x => { Assert.Equal("عنوان قديم · unchanged", x.Name); Assert.Equal(ReconciliationCaseCategory.Uncategorized, x.Category); });
        var next = new ReconciliationCase(Guid.NewGuid(), seed.OrganizationA, seed.ActorA, "Next", yearEnd);
        db.ReconciliationCases.Add(next); await db.SaveChangesAsync();
        Assert.Equal("REC-2026-000003", next.Reference);
        await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync("20260914125645_AddOrganizationCapacity"));
        Assert.Equal(5, await db.ReconciliationCases.CountAsync());
    }

    [Fact]
    public async Task Case_metadata_reference_allocation_is_concurrent_tenant_scoped_and_expands_without_truncation()
    {
        var seed = await SeedGraphAsync();
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 8).Select(async index =>
        {
            await using var db = _database.CreateContext();
            var item = new ReconciliationCase(Guid.NewGuid(), seed.OrganizationA, seed.ActorA, $"Concurrent {index}", now);
            db.ReconciliationCases.Add(item); await gate.Task; await db.SaveChangesAsync(); return item.Reference;
        }).ToArray();
        gate.SetResult(); var references = await Task.WhenAll(tasks);
        Assert.Equal(8, references.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 8).Select(x => $"REC-2026-{x:000000}"), references.Order());
        await using var other = _database.CreateContext();
        var foreign = new ReconciliationCase(Guid.NewGuid(), seed.OrganizationB, seed.ActorB, "Other", now);
        var later = new ReconciliationCase(Guid.NewGuid(), seed.OrganizationA, seed.ActorA, "New year", now.AddYears(1));
        other.ReconciliationCases.AddRange(foreign, later); await other.SaveChangesAsync();
        Assert.Equal("REC-2026-000001", foreign.Reference); Assert.Equal("REC-2027-000001", later.Reference);
        await ExecuteAsync("UPDATE reconciliation_reference_counters SET last_number=999999 WHERE organization_id=@org AND reference_year=2026", ("org", seed.OrganizationA));
        var large = new ReconciliationCase(Guid.NewGuid(), seed.OrganizationA, seed.ActorA, "Large reference", now);
        other.ReconciliationCases.Add(large); await other.SaveChangesAsync();
        Assert.Equal("REC-2026-1000000", large.Reference);
        await AssertSqlStateAsync("55000", "UPDATE reconciliation_cases SET reference='REC-2026-000999' WHERE id=@id", ("id", large.Id));
    }

    [Fact]
    public async Task Case_metadata_filters_search_references_literal_text_categories_status_and_inclusive_utc_dates()
    {
        var seed = await SeedGraphAsync(); var day = new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);
        var expected = new ReconciliationCase(Guid.NewGuid(), seed.OrganizationA, seed.ActorA, "Office معدات 10%_discount", day.AddDays(1).AddMilliseconds(-1), ReconciliationCaseCategory.Equipment);
        var closed = new ReconciliationCase(Guid.NewGuid(), seed.OrganizationA, seed.ActorA, "Office archived", day, ReconciliationCaseCategory.Services); closed.Close(day);
        await using (var db = _database.CreateContext())
        {
            db.ReconciliationCases.AddRange(expected, closed,
                new ReconciliationCase(Guid.NewGuid(), seed.OrganizationA, seed.ActorA, "Office before", day.AddTicks(-10), ReconciliationCaseCategory.Equipment),
                new ReconciliationCase(Guid.NewGuid(), seed.OrganizationA, seed.ActorA, "Office after", day.AddDays(1), ReconciliationCaseCategory.Equipment),
                new ReconciliationCase(Guid.NewGuid(), seed.OrganizationB, seed.ActorB, "Office معدات 10%_discount", day, ReconciliationCaseCategory.Equipment));
            await db.SaveChangesAsync();
        }
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        await using var scope = services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ListReconciliationCasesHandler>();
        var filtered = await handler.HandleAsync(0, default, new("office", "Equipment", "Draft", "2026-09-30", "2026-09-30"));
        Assert.Equal(expected.Id, Assert.Single(filtered.Cases).Id);
        Assert.Equal(expected.Reference, Assert.Single(filtered.Cases).Reference);
        Assert.Equal("Equipment", Assert.Single(filtered.Cases).Category);
        Assert.Null(filtered.NextOffset);
        Assert.Equal(expected.Id, Assert.Single((await handler.HandleAsync(0, default, new(expected.Reference.ToLowerInvariant()))).Cases).Id);
        Assert.Equal(expected.Id, Assert.Single((await handler.HandleAsync(0, default, new("%_"))).Cases).Id);
        Assert.Equal(expected.Id, Assert.Single((await handler.HandleAsync(0, default, new("معدات"))).Cases).Id);
        Assert.Equal(closed.Id, Assert.Single((await handler.HandleAsync(0, default, new(Status: "Closed"))).Cases).Id);
    }

    [Fact]
    public async Task Case_metadata_restricted_role_creates_updates_audits_and_cannot_edit_reference_or_counter()
    {
        var seed = await SeedGraphAsync(); await using var role = await ApiRoleAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:Database"] = role.Connection, ["AiService:BaseUrl"] = "http://127.0.0.1:1",
            ["AiService:WorkerToken"] = "test-only-token" }).Build();
        await using var services = new ServiceCollection().AddInfrastructure(config)
            .AddSingleton<IExecutionContext>(new CitedExecution(seed.OrganizationA, seed.ActorA)).BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SecurityAuditContext>().Bind(seed.OrganizationA, seed.ActorA, "System", "case-metadata-test");
        var created = await scope.ServiceProvider.GetRequiredService<CreateReconciliationCaseHandler>().HandleAsync("Sensitive title", default, "Equipment");
        Assert.Matches("^REC-[0-9]{4}-000001$", created.Reference);
        var handler = scope.ServiceProvider.GetRequiredService<UpdateReconciliationCaseMetadataHandler>();
        var changed = await handler.HandleAsync(created.Id, created.ConcurrencyToken, "عنوان سري جديد", "Services", default);
        Assert.Equal(created.Reference, changed.Reference); Assert.NotEqual(created.ConcurrencyToken, changed.ConcurrencyToken);
        Assert.Equal(created.Documents, changed.Documents); Assert.Equal(created.Runs, changed.Runs);
        var unchanged = await handler.HandleAsync(created.Id, changed.ConcurrencyToken, changed.Name, changed.Category, default);
        Assert.Equal(changed.ConcurrencyToken, unchanged.ConcurrencyToken);
        await using var owner = _database.CreateContext();
        var audit = Assert.Single(await owner.SecurityAuditEvents.Where(x => x.Action == "reconciliation.metadata-changed").ToArrayAsync());
        Assert.Equal(created.Id, audit.TargetId); Assert.Equal(seed.ActorA, audit.ActorId);
        Assert.Contains("category=Equipment", audit.BeforeState); Assert.Contains("category=Services", audit.AfterState);
        Assert.Contains("titleChanged=True", audit.AfterState); Assert.DoesNotContain("Sensitive", audit.BeforeState); Assert.DoesNotContain("سري", audit.AfterState);
        await using var restricted = role.Db();
        foreach (var sql in new[] { "SELECT * FROM reconciliation_reference_counters", "UPDATE reconciliation_reference_counters SET last_number=1",
            "UPDATE reconciliation_cases SET reference='REC-2026-000001'", "SELECT public.allocate_reconciliation_reference()" })
            Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => restricted.Database.ExecuteSqlRawAsync(sql))).SqlState);
        await ApiDatabasePolicy.VerifyAsync(restricted, role.Role, true, default);
    }
}
