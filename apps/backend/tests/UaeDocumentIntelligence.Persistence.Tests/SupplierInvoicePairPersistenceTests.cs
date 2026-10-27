using System.Text;
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
    public async Task Supplier_pair_concurrent_evaluation_and_stale_scope_retry_commit_one_run_and_business_audit()
    {
        var seed = await SeedGraphAsync();
        var item = await SupplierPairStoredFixture(seed.OrganizationA, seed.ActorA, "1260", "1261");
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        await using var one = services.CreateAsyncScope();
        await using var two = services.CreateAsyncScope();
        foreach (var scope in new[] { one, two })
        {
            scope.ServiceProvider.GetRequiredService<SecurityAuditContext>().Bind(seed.OrganizationA, seed.ActorA, "System", "supplier-concurrent");
            await scope.ServiceProvider.GetRequiredService<IReconciliationRepository>().GetAsync(seed.OrganizationA, item.Id, default);
        }
        async Task<bool> Evaluate(AsyncServiceScope scope)
        {
            try {
                await scope.ServiceProvider.GetRequiredService<EvaluateReconciliationCaseHandler>().HandleAsync(item.Id, item.ConcurrencyToken,
                    new(RuleSetVersion: "invoice-po-1.0", OrderScope: "FullOrder"), default);
                return true;
            } catch (ReconciliationPreconditionException) { return false; }
        }
        Assert.Single((await Task.WhenAll(Evaluate(one), Evaluate(two))).Where(saved => saved));
        await using var fresh = services.CreateAsyncScope();
        var saved = (await fresh.ServiceProvider.GetRequiredService<IReconciliationRepository>().GetAsync(seed.OrganizationA, item.Id, default))!;
        var run = Assert.Single(saved.Runs);
        await Assert.ThrowsAsync<ReconciliationPreconditionException>(() => fresh.ServiceProvider.GetRequiredService<EvaluateReconciliationCaseHandler>()
            .HandleAsync(item.Id, item.ConcurrencyToken, new(RuleSetVersion: "invoice-po-1.0", OrderScope: "Partial"), default));
        Assert.Equal(InvoiceOrderScope.FullOrder, run.OrderScope);
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM reconciliation_runs WHERE reconciliation_case_id=@id", ("id", item.Id)));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM security_audit_events WHERE action='reconciliation.evaluated' AND target_id=@id", ("id", run.Id)));
    }

    [Theory]
    [InlineData("invoice-po-1.0")]
    [InlineData("invoice-po-2.0")]
    public async Task Supplier_pair_restricted_role_persists_exact_large_signed_difference_owned_findings_and_frozen_sources(string version)
    {
        var seed = await SeedGraphAsync();
        var item = await SupplierPairStoredFixture(seed.OrganizationA, seed.ActorA, "0", "999999999999999999.123456");
        await using var role = await ApiRoleAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:Database"] = role.Connection, ["AiService:BaseUrl"] = "http://127.0.0.1:1",
            ["AiService:WorkerToken"] = "test-only-token" }).Build();
        await using var services = new ServiceCollection().AddInfrastructure(config)
            .AddSingleton<IExecutionContext>(new CitedExecution(seed.OrganizationA, seed.ActorA)).BuildServiceProvider();
        ReconciliationRunView result;
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlRawAsync("SAVEPOINT supplier_pair_atomic_save");
            scope.ServiceProvider.GetRequiredService<SecurityAuditContext>().Bind(seed.OrganizationA, seed.ActorA, "System", "supplier-pair");
            var response = await scope.ServiceProvider.GetRequiredService<EvaluateReconciliationCaseHandler>()
                .HandleAsync(item.Id, item.ConcurrencyToken, new(RuleSetVersion: version, OrderScope: version == "invoice-po-1.0" ? "FullOrder" : null), default);
            result = Assert.Single(response.Runs);
            await transaction.CommitAsync();
        }
        await using (var read = services.CreateAsyncScope())
        {
            var repository = read.ServiceProvider.GetRequiredService<IReconciliationRepository>();
            var saved = (await repository.GetAsync(seed.OrganizationA, item.Id, default))!;
            var run = Assert.Single(saved.Runs);
            var total = Assert.Single(run.Checks.Where(check => check.FieldKey == "total"));
            Assert.Equal(999999999999999999.123456m, total.SignedDifference);
            Assert.Equal(999999999999999999.123456m, Assert.Single(run.Findings.Where(finding => finding.Id == total.FindingId)).Difference);
            Assert.Equal(seed.ActorA, run.EvaluationActorId);
            Assert.Equal(version == "invoice-po-1.0" ? InvoiceOrderScope.FullOrder : InvoiceOrderScope.Unknown, run.OrderScope);
            Assert.Equal(version, run.RuleSetVersion);
            Assert.All(run.DocumentSnapshots, source => {
                Assert.Equal("Succeeded", source.SourceOutcome);
                Assert.Equal("local-1.6.0", source.SourceAdapterVersion);
                Assert.Equal("Complete", source.RowCoverageStatus);
                Assert.Equal("[]", source.SourceWarningsJson);
            });
            Assert.All(run.DocumentSnapshots.SelectMany(source => source.Fields), field => Assert.Equal(ReviewedFieldSourceKind.Confirmed, field.SourceKind));
            Assert.Null(await repository.GetAsync(seed.OrganizationB, item.Id, default));
        }
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM security_audit_events WHERE action='reconciliation.evaluated' AND target_id=@id", ("id", result.Id)));
        await AssertSqlStateAsync("P0001", "UPDATE reconciliation_checks SET signed_difference=0 WHERE reconciliation_run_id=@id", ("id", result.Id));
        await AssertSqlStateAsync("P0001", "DELETE FROM reconciliation_checks WHERE reconciliation_run_id=@id", ("id", result.Id));
        await AssertSqlStateAsync("P0001", "UPDATE reconciliation_document_snapshots SET source_warnings_json='[\"changed\"]' WHERE reconciliation_run_id=@id", ("id", result.Id));
        await AssertSqlStateAsync("P0001", "UPDATE reconciliation_runs SET order_scope='Partial' WHERE id=@id", ("id", result.Id));
        foreach (var table in new[] { "reconciliation_checks", "reconciliation_document_snapshots", "reconciliation_field_snapshots", "reconciliation_findings" })
            await AssertSqlStateAsync("P0001", $"INSERT INTO {table} SELECT (jsonb_populate_record(NULL::{table}, to_jsonb(original) || jsonb_build_object('id',gen_random_uuid()))).* FROM {table} original WHERE reconciliation_run_id=@id LIMIT 1", ("id", result.Id));
        await AssertSqlStateAsync("P0001", "UPDATE reconciliation_runs SET creation_transaction_id=pg_current_xact_id()::text::numeric WHERE id=@id", ("id", result.Id));
        // Adversarial owner SQL proves the server replaces even a supplied fake marker;
        // this synthetic row is not a valid application-created comparison.
        var forged = Guid.NewGuid();
        var marker = await ScalarAsync<decimal>("INSERT INTO reconciliation_runs SELECT (jsonb_populate_record(NULL::reconciliation_runs, to_jsonb(original) || jsonb_build_object('id',@newId,'run_number',99,'creation_transaction_id',0))).* FROM reconciliation_runs original WHERE id=@id RETURNING creation_transaction_id", ("newId", forged), ("id", result.Id));
        Assert.True(marker > 0);
        await AssertSqlStateAsync("P0001", "INSERT INTO reconciliation_checks SELECT (jsonb_populate_record(NULL::reconciliation_checks, to_jsonb(original) || jsonb_build_object('id',gen_random_uuid(),'reconciliation_run_id',@newId))).* FROM reconciliation_checks original WHERE reconciliation_run_id=@id LIMIT 1", ("newId", forged), ("id", result.Id));
        // Verify current runtime grants before intentionally downgrading the disposable schema.
        await using var restricted = role.Db();
        await ApiDatabasePolicy.VerifyAsync(restricted, role.Role, true, default);
        await using (var owner = _database.CreateContext())
        {
            var failure = await Assert.ThrowsAsync<PostgresException>(() => owner.GetService<IMigrator>().MigrateAsync("20261002104454_AddDocumentProcessingRequests"));
            Assert.Equal("P0001", failure.SqlState);
            Assert.True(await TableExistsAsync("reconciliation_checks"));
            Assert.Equal(1, await owner.ReconciliationRuns.CountAsync(run => run.Id == result.Id));
        }
    }

    [Fact]
    public async Task Supplier_pair_confirmed_malformed_raw_value_cannot_persist_normalized_arithmetic()
    {
        var seed = await SeedGraphAsync();
        var item = await SupplierPairStoredFixture(seed.OrganizationA, seed.ActorA, "1260.00", "1,23", "123");
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        await using (var scope = services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<EvaluateReconciliationCaseHandler>()
                .HandleAsync(item.Id, item.ConcurrencyToken, new(RuleSetVersion: "invoice-po-1.0", OrderScope: "FullOrder"), default);
        await using var fresh = services.CreateAsyncScope();
        var saved = (await fresh.ServiceProvider.GetRequiredService<IReconciliationRepository>().GetAsync(seed.OrganizationA, item.Id, default))!;
        var run = Assert.Single(saved.Runs);
        var check = Assert.Single(run.Checks.Where(check => check.FieldKey == "total"));
        Assert.Equal(ReconciliationCheckStatus.CouldNotCheck, check.Status);
        Assert.Null(check.SignedDifference);
        var field = Assert.Single(run.DocumentSnapshots.Single(source => source.Role == ReconciliationDocumentRole.Invoice).Fields.Where(field => field.FieldKey == "total"));
        Assert.Equal("1,23", field.ValueText);
        Assert.Equal("1,23", field.QuotedText);
        Assert.Equal(ReviewedFieldSourceKind.Confirmed, field.SourceKind);
        Assert.NotNull(check.FindingId);
    }

    [Fact]
    public async Task Supplier_pair_source_checks_remain_frozen_across_resolution_scope_and_policy_evaluations()
    {
        var seed = await SeedGraphAsync();
        var item = await SupplierPairStoredFixture(seed.OrganizationA, seed.ActorA, "1260.00", "1261.00");
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        ReconciliationCaseView first;
        await using (var scope = services.CreateAsyncScope())
            first = await scope.ServiceProvider.GetRequiredService<EvaluateReconciliationCaseHandler>().HandleAsync(item.Id, item.ConcurrencyToken,
                new(RuleSetVersion: "invoice-po-1.0", OrderScope: "FullOrder"), default);
        // Compare persisted history to persisted history, including database timestamp precision.
        await using (var persisted = services.CreateAsyncScope())
            first = await persisted.ServiceProvider.GetRequiredService<GetReconciliationCaseHandler>().HandleAsync(item.Id, default);
        var initial = Assert.Single(first.Runs);
        ReconciliationCaseView second;
        await using (var scope = services.CreateAsyncScope())
        {
            var resolved = await scope.ServiceProvider.GetRequiredService<ResolveReconciliationFindingHandler>().HandleAsync(item.Id, initial.Id,
                initial.Findings.First().Id, first.ConcurrencyToken, new("Accepted", "Independent fixture decision"), default);
            second = await scope.ServiceProvider.GetRequiredService<EvaluateReconciliationCaseHandler>().HandleAsync(item.Id, resolved.ConcurrencyToken,
                new(RuleSetVersion: "invoice-po-1.0", OrderScope: "Partial"), default);
        }
        await using (var scope = services.CreateAsyncScope())
        {
            // The unchanged legacy projection cannot combine invoice_number and a PO-reference alias.
            // Pin a separate compatible reviewed invoice; the full original pair truth stays untouched.
            var compatible = await SupplierPairStoredFixture(seed.OrganizationA, seed.ActorA, "1260.00", "1261.00", includeInvoiceOrderReference: false);
            var replacement = compatible.DocumentLinks.Single(link => link.Role == ReconciliationDocumentRole.Invoice);
            var repinned = await scope.ServiceProvider.GetRequiredService<LinkReconciliationDocumentHandler>().HandleAsync(item.Id, second.ConcurrencyToken,
                new("Invoice", replacement.DocumentId, replacement.ReviewSessionId), default);
            var legacy = await scope.ServiceProvider.GetRequiredService<EvaluateReconciliationCaseHandler>().HandleAsync(item.Id, repinned.ConcurrencyToken, new(), default);
            Assert.Equal(3, legacy.Runs.Count);
            var old = Assert.Single(legacy.Runs.Where(run => run.Id == initial.Id));
            Assert.Equal(initial.Checks!.Select(check => check.Id).Order(), old.Checks!.Select(check => check.Id).Order());
            Assert.Equal(initial.Sources!.Select(source => source.Id).Order(), old.Sources!.Select(source => source.Id).Order());
            Assert.Equal(initial.Fields.Select(field => field.Id).Order(), old.Fields.Select(field => field.Id).Order());
            // DTO records compare every property; decimals compare numeric values rather than JSON scale spelling.
            // Relational Include collections have no promised order, so stable IDs define correspondence only.
            Assert.Equal(initial.Checks!.OrderBy(check => check.Id), old.Checks!.OrderBy(check => check.Id));
            Assert.Equal(initial.Sources!.OrderBy(source => source.Id), old.Sources!.OrderBy(source => source.Id));
            Assert.Equal(initial.Fields.OrderBy(field => field.Id), old.Fields.OrderBy(field => field.Id));
            Assert.Equal(initial.Tolerances, old.Tolerances);
            Assert.Equal(initial.OrderScope, old.OrderScope);
            Assert.Equal(initial.EvaluationActorId, old.EvaluationActorId);
            Assert.Equal(initial.EvaluatedAt, old.EvaluatedAt);
            Assert.Equal(initial.Status, old.Status);
            Assert.Equal(initial.RunNumber, old.RunNumber);
            Assert.Equal(initial.RuleSetVersion, old.RuleSetVersion);
            Assert.Equal(initial.Coverage, old.Coverage);
            var oldPolicy = Assert.Single(legacy.Runs.Where(run => run.RuleSetVersion == "1.0"));
            Assert.Empty(oldPolicy.Checks!);
        }
    }

    [Fact]
    public async Task Supplier_pair_schema_additions_use_exact_precision_scoped_references_and_model_has_no_drift()
    {
        await using var db = _database.CreateContext();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.True(await TableExistsAsync("reconciliation_checks"));
        foreach (var column in new[] { "signed_difference", "tolerance" })
        {
            Assert.Equal(column == "signed_difference" ? 24 : 18, await ScalarAsync<int>("SELECT numeric_precision FROM information_schema.columns WHERE table_name='reconciliation_checks' AND column_name=@name", ("name", column)));
            Assert.Equal(6, await ScalarAsync<int>("SELECT numeric_scale FROM information_schema.columns WHERE table_name='reconciliation_checks' AND column_name=@name", ("name", column)));
        }
        var keys = await QueryStringsAsync("SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conrelid='reconciliation_checks'::regclass AND contype='f' ORDER BY conname");
        Assert.Contains(keys, key => key.Contains("organization_id, reconciliation_run_id") && key.Contains("reconciliation_runs"));
        Assert.Contains(keys, key => key.Contains("finding_id") && key.Contains("reconciliation_findings"));
        Assert.Contains(keys, key => key.Contains("left_field_snapshot_id") && key.Contains("reconciliation_field_snapshots"));
        Assert.Contains(keys, key => key.Contains("right_field_snapshot_id") && key.Contains("reconciliation_field_snapshots"));
    }

    private async Task<ReconciliationCase> SupplierPairStoredFixture(Guid org, Guid actor, string poTotal, string invoiceTotal, string? normalized = null,
        bool includeInvoiceOrderReference = true)
    {
        var now = DateTimeOffset.UtcNow;
        var documents = new List<Document>();
        var item = new ReconciliationCase(Guid.NewGuid(), org, actor, "Independent persisted supplier pair", now);
        foreach (var role in new[] { ReconciliationDocumentRole.PurchaseOrder, ReconciliationDocumentRole.Invoice })
        {
            var document = new Document(Guid.NewGuid(), org, actor, role + ".pdf", Guid.NewGuid().ToString("N") + ".pdf", "application/pdf", 100,
                new string(role == ReconciliationDocumentRole.Invoice ? 'a' : 'b', 64), now);
            var extraction = document.QueueExtraction(Guid.NewGuid(), 1, "local", "local-1.6.0", "2.0", Guid.NewGuid().ToString("N"), now);
            extraction.Start(now);
            var values = new List<(string Key, FieldValueType Type, string Value, int Occurrence)> {
                (role == ReconciliationDocumentRole.Invoice ? "invoice_number" : "purchase_order_number", FieldValueType.Identifier, role == ReconciliationDocumentRole.Invoice ? "SINV-0001" : "SPO-0001", 0),
                ("supplier_trn", FieldValueType.Identifier, "000000000000701", 0), ("currency", FieldValueType.Currency, "AED", 0),
                ("total", FieldValueType.Decimal, role == ReconciliationDocumentRole.Invoice ? invoiceTotal : poTotal, 0),
                ("vat", FieldValueType.Decimal, "60", 0), ("tax_currency", FieldValueType.Currency, "AED", 0),
                ("tax_basis", FieldValueType.String, "net", 0), ("document_total_basis", FieldValueType.String, "gross", 0),
                ("line_item_code", FieldValueType.Identifier, "SRV-01", 1), ("line_description", FieldValueType.String, "Training", 1),
                ("line_quantity", FieldValueType.Decimal, "2", 1), ("line_unit", FieldValueType.String, "EA", 1),
                ("line_unit_price", FieldValueType.Decimal, "600", 1), ("line_price_basis", FieldValueType.String, "1 EA", 1),
                ("line_price_amount_basis", FieldValueType.String, "net", 1), ("line_total", FieldValueType.Decimal, "1260", 1),
                ("line_total_basis", FieldValueType.String, "gross", 1), ("line_tax", FieldValueType.Decimal, "60", 1) };
            if (role == ReconciliationDocumentRole.Invoice && includeInvoiceOrderReference) values.Add(("purchase_order_number", FieldValueType.Identifier, "SPO-0001", 0));
            extraction.AddPage(Guid.NewGuid(), 1, string.Join("\n", values.Select(value => value.Value)), 0);
            var offset = 0;
            foreach (var value in values)
            {
                var field = extraction.AddField(Guid.NewGuid(), value.Key, value.Occurrence, value.Type, value.Value, .95m, now,
                    role == ReconciliationDocumentRole.Invoice && value.Key == "total" ? normalized : value.Value);
                var end = offset + Encoding.UTF8.GetByteCount(value.Value);
                extraction.AddEvidence(field, Guid.NewGuid(), 1, value.Value, offset, end);
                offset = end + 1;
            }
            extraction.RecordProviderMetrics(1, 1, null, null, null);
            extraction.Succeed(role == ReconciliationDocumentRole.Invoice ? "invoice" : "purchase_order", "en", now, "fixture", "independent-inputs");
            document.RecordExtractionSuccess(extraction, now);
            var review = document.OpenReview(Guid.NewGuid(), actor, now);
            var sequence = 0;
            foreach (var field in extraction.Fields)
                review.AddAction(Guid.NewGuid(), actor, ++sequence, ReviewActionType.Confirmed, field.FieldKey, field.OccurrenceIndex, field.Id, null, null, null, now);
            review.Complete(extraction.Fields, now);
            item.LinkReviewedDocument(Guid.NewGuid(), role, document.Id, review.Id, extraction.Id, document.Sha256, now);
            documents.Add(document);
        }
        await using var write = _database.CreateContext();
        write.Documents.AddRange(documents);
        write.ReconciliationCases.Add(item);
        await write.SaveChangesAsync();
        return item;
    }
}
