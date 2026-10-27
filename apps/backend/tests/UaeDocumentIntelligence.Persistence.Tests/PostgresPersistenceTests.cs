using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure;

namespace UaeDocumentIntelligence.Persistence.Tests;

[Collection("Disposable PostgreSQL")]
public sealed partial class PostgresPersistenceTests(PostgresDatabaseFixture database) :
    IClassFixture<PostgresDatabaseFixture>, IAsyncLifetime
{
    private const string ValidSha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private readonly PostgresDatabaseFixture _database = database;

    public Task InitializeAsync() => _database.ResetToLatestMigrationAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Migration_apply_is_idempotent_and_rollback_reapply_succeeds()
    {
        await using var context = _database.CreateContext();
        var before = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.Equal(26, before.Length);
        Assert.True(await ScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE tablename='reconciliation_cases' AND indexname='IX_reconciliation_cases_organization_id_created_by_actor_id' AND indexdef NOT LIKE '%WHERE%')"));
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());

        await context.Database.MigrateAsync();
        Assert.Equal(before, (await context.Database.GetAppliedMigrationsAsync()).ToArray());

        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20260811073607_AddReviewActionValueTypeConstraint");
        Assert.False(await TableExistsAsync("extraction_pages"));

        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var evidenceId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await ExecuteAsync(
            "INSERT INTO organizations (id,slug,display_name,created_at_utc) VALUES (@id,'legacy-org','Legacy Organization',@now)",
            ("id", organizationId), ("now", now));
        await ExecuteAsync(
            "INSERT INTO actors (id,organization_id,kind,display_name,is_active,created_at_utc) VALUES (@id,@org,'System','Legacy Actor',true,@now)",
            ("id", actorId), ("org", organizationId), ("now", now));
        await ExecuteAsync(InsertDocumentSql,
            DocumentParameters(organizationId, actorId, documentId, "legacy.pdf", ValidSha));
        await ExecuteAsync(LegacyInsertRunSql,
            RunParameters(organizationId, documentId, runId, 1, "legacy-run"));
        await ExecuteAsync(InsertFieldSql,
            FieldParameters(organizationId, runId, fieldId, "total", 0));
        await ExecuteAsync(LegacyInsertEvidenceSql,
            ("id", evidenceId), ("organization", organizationId), ("field", fieldId), ("page", 51));

        await migrator.MigrateAsync();
        Assert.True(await TableExistsAsync("extraction_pages"));
        Assert.Equal(1, await ScalarAsync<long>(
            "SELECT count(*) FROM extraction_pages WHERE extraction_run_id=@run AND page_number=51",
            ("run", runId)));
        Assert.Equal(1, await ScalarAsync<long>(
            "SELECT count(*) FROM field_evidence WHERE id=@id AND page_number=51 AND quoted_text IS NULL AND byte_start IS NULL AND byte_end IS NULL",
            ("id", evidenceId)));

        await migrator.MigrateAsync(Migration.InitialDatabase);
        Assert.False(await TableExistsAsync("organizations"));

        await migrator.MigrateAsync();
        Assert.True(await TableExistsAsync("organizations"));
        Assert.Equal(before, (await context.Database.GetAppliedMigrationsAsync()).ToArray());
    }

    [Fact]
    public async Task Expected_schema_constraints_and_indexes_exist()
    {
        var tables = await QueryStringsAsync(
            "SELECT table_name FROM information_schema.tables WHERE table_schema='public' ORDER BY table_name");
        Assert.Equal(new[]
        {
            "__EFMigrationsHistory", "actors", "answer_citations", "answer_claims", "chunk_evidence_spans",
            "cited_answers", "cited_chunk_embeddings", "cited_document_indexes", "cited_generation_artifacts", "cited_pilot_budgets", "cited_recovery_audits", "cited_retrieval_runs",
            "cited_spend_events", "cited_spend_reservations", "document_chunks",
            "document_deletions", "document_processing_requests", "documents", "enterprise_identities", "extraction_jobs", "extraction_pages", "extraction_runs",
            "extraction_warnings", "field_evidence", "finding_resolutions", "machine_field_values", "organization_capacity_policies", "organization_memberships", "organization_retention_policies", "organizations",
            "reconciliation_cases", "reconciliation_checks", "reconciliation_document_links", "reconciliation_document_snapshots", "reconciliation_draft_requests",
            "reconciliation_field_snapshots", "reconciliation_findings", "reconciliation_reference_counters", "reconciliation_runs",
            "retention_holds", "retrieval_hits", "retrieval_sources", "review_actions", "review_sessions", "security_audit_events"
        }, tables);

        var constraints = await QueryStringsAsync(
            "SELECT conname FROM pg_constraint WHERE connamespace='public'::regnamespace ORDER BY conname");
        var expectedConstraints = new[]
        {
            "PK_reconciliation_draft_requests", "ck_reconciliation_cases_drafts", "ck_reconciliation_cases_workflow",
            "ck_reconciliation_draft_requests_role", "ck_reconciliation_draft_requests_key", "ck_reconciliation_draft_requests_hash",
            "FK_reconciliation_draft_requests_actors_organization_id_actor_~",
            "FK_reconciliation_draft_requests_reconciliation_cases_organiza~",
            "FK_reconciliation_cases_documents_organization_id_draft_invoic~",
            "FK_reconciliation_cases_documents_organization_id_draft_purcha~",
            "PK_reconciliation_checks", "ck_reconciliation_checks_finding", "ck_reconciliation_checks_numeric",
            "ck_reconciliation_checks_occurrence", "ck_reconciliation_checks_status",
            "ck_reconciliation_runs_pair_context", "ck_reconciliation_field_snapshots_source_kind",
            "FK_reconciliation_runs_actors_organization_id_evaluation_actor~",
            "FK_reconciliation_checks_reconciliation_field_snapshots_organi~",
            "FK_reconciliation_checks_reconciliation_field_snapshots_organ~1",
            "FK_reconciliation_checks_reconciliation_findings_organization_~",
            "FK_reconciliation_checks_reconciliation_runs_organization_id_r~",
            "PK_document_processing_requests",
            "FK_document_processing_requests_actors_organization_id_actor_id",
            "FK_document_processing_requests_organizations_organization_id",
            "ck_document_processing_requests_hash", "ck_document_processing_requests_key",
            "ck_document_processing_requests_kind", "ck_document_processing_requests_run",
            "ck_documents_sha256", "ck_documents_size", "ck_documents_status",
            "ck_extraction_jobs_attempts", "ck_extraction_jobs_completion",
            "ck_extraction_jobs_lease_shape", "ck_extraction_jobs_result",
            "ck_extraction_jobs_status", "ck_extraction_jobs_timestamps",
            "ck_extraction_pages_number", "ck_extraction_pages_rotation", "ck_extraction_pages_text_bytes",
            "ck_extraction_runs_cost", "ck_extraction_runs_duration", "ck_extraction_runs_number",
            "ck_extraction_runs_page_count", "ck_extraction_runs_provider_duration",
            "ck_extraction_warnings_code", "ck_extraction_warnings_field_scope",
            "ck_extraction_warnings_page", "ck_extraction_warnings_severity",
            "ck_field_evidence_offsets", "ck_field_evidence_page", "ck_field_evidence_polygon_shape",
            "ck_field_evidence_polygon_unit", "ck_field_evidence_quote_offsets", "ck_machine_fields_confidence",
            "ck_review_actions_matrix", "ck_review_actions_value_type", "ck_review_sessions_revision",
            "ck_reconciliation_cases_status", "ck_reconciliation_document_links_role",
            "ck_reconciliation_cases_category", "ck_reconciliation_cases_reference",
            "reconciliation_reference_counters_pkey", "reconciliation_reference_counters_organization_id_fkey",
            "reconciliation_reference_counters_reference_year_check", "reconciliation_reference_counters_last_number_check",
            "ck_reconciliation_document_links_sha256", "ck_reconciliation_document_snapshots_role",
            "ck_reconciliation_document_snapshots_sha256", "ck_reconciliation_field_snapshots_evidence",
            "ck_reconciliation_field_snapshots_occurrence", "ck_reconciliation_field_snapshots_source",
            "ck_reconciliation_field_snapshots_value_type", "ck_reconciliation_findings_numeric",
            "ck_reconciliation_findings_occurrence", "ck_reconciliation_findings_roles",
            "ck_reconciliation_findings_severity", "ck_reconciliation_runs_number",
            "ck_reconciliation_runs_status", "ck_reconciliation_runs_tolerances",
            "ck_finding_resolutions_action", "ck_finding_resolutions_sequence"
        };
        foreach (var expected in expectedConstraints) Assert.Contains(expected, constraints);

        var indexes = await QueryStringsAsync("SELECT indexname FROM pg_indexes WHERE schemaname='public'");
        Assert.Contains("ux_reconciliation_draft_requests_intent", indexes);
        Assert.Contains("ix_reconciliation_cases_workflow_created", indexes);
        Assert.Contains("IX_reconciliation_cases_organization_id_draft_invoice_document~", indexes);
        Assert.Contains("IX_reconciliation_cases_organization_id_draft_purchase_order_d~", indexes);
        Assert.Contains("IX_reconciliation_draft_requests_organization_id_reconciliatio~", indexes);
        Assert.Contains("PK_document_processing_requests", indexes);
        Assert.Contains("ix_documents_org_sha256", indexes);
        Assert.Contains("ux_documents_storage_key", indexes);
        Assert.Contains("ux_extraction_runs_org_document_number", indexes);
        Assert.Contains("ux_extraction_jobs_org_run", indexes);
        Assert.Contains("ux_extraction_jobs_org_document_active", indexes);
        Assert.Contains("IX_reconciliation_runs_organization_id_evaluation_actor_id", indexes);
        Assert.Contains("IX_reconciliation_checks_organization_id_reconciliation_run_id~", indexes);
        Assert.Contains("IX_reconciliation_checks_organization_id_reconciliation_run_i~1", indexes);
        Assert.Contains("IX_reconciliation_checks_organization_id_reconciliation_run_i~2", indexes);
        Assert.Contains("IX_reconciliation_checks_organization_id_reconciliation_run_i~3", indexes);
        Assert.Contains("ix_extraction_jobs_due", indexes);
        Assert.Contains("ix_extraction_jobs_expired_lease", indexes);
        Assert.Contains("ux_extraction_warnings_global", indexes);
        Assert.Contains("ux_extraction_warnings_page", indexes);
        Assert.Contains("ux_extraction_warnings_field", indexes);
        Assert.Contains("ux_extraction_warnings_page_field", indexes);
        Assert.Contains("ux_review_sessions_one_active", indexes);
        Assert.Contains("ux_review_actions_org_session_sequence", indexes);
        Assert.Contains("ux_reconciliation_links_org_case_role", indexes);
        Assert.Contains("ux_reconciliation_runs_org_case_number", indexes);
        Assert.Contains("ux_reconciliation_runs_one_running", indexes);
        Assert.Contains("ux_reconciliation_fields_org_run_document_key_occurrence", indexes);
        Assert.Contains("ux_finding_resolutions_org_finding_sequence", indexes);
        Assert.Contains("reconciliation_reference_counters_pkey", indexes);
        Assert.Contains("ix_reconciliation_cases_org_category_created", indexes);
        Assert.Contains("ix_reconciliation_cases_org_created_id", indexes);
        Assert.Contains("ux_reconciliation_cases_org_reference", indexes);

        Assert.Equal("PRIMARY KEY (organization_id, reference_year)", await ScalarAsync<string>(
            "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conrelid='reconciliation_reference_counters'::regclass AND contype='p'"));
        Assert.Equal("FOREIGN KEY (organization_id) REFERENCES organizations(id) ON DELETE RESTRICT", await ScalarAsync<string>(
            "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname='reconciliation_reference_counters_organization_id_fkey'"));
        Assert.Equal("CHECK (((reference_year >= 1) AND (reference_year <= 9999)))", await ScalarAsync<string>(
            "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname='reconciliation_reference_counters_reference_year_check'"));
        Assert.Equal("CHECK ((last_number > 0))", await ScalarAsync<string>(
            "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname='reconciliation_reference_counters_last_number_check'"));

        Assert.Equal("PRIMARY KEY (organization_id, actor_id, kind, request_key)", await ScalarAsync<string>(
            "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conrelid='document_processing_requests'::regclass AND contype='p'"));
        Assert.Equal("FOREIGN KEY (organization_id, actor_id) REFERENCES actors(organization_id, id) ON DELETE RESTRICT", await ScalarAsync<string>(
            "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname='FK_document_processing_requests_actors_organization_id_actor_id'"));
        Assert.Equal("FOREIGN KEY (organization_id) REFERENCES organizations(id) ON DELETE RESTRICT", await ScalarAsync<string>(
            "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname='FK_document_processing_requests_organizations_organization_id'"));
        // No source FK: deletion leaves the accepted request as an unavailable
        // tombstone, rather than permitting recreation under its original key.
        Assert.Equal(2, await ScalarAsync<long>(
            "SELECT count(*) FROM pg_constraint WHERE conrelid='document_processing_requests'::regclass AND contype='f'"));

        var hashIndex = await ScalarAsync<string>(
            "SELECT indexdef FROM pg_indexes WHERE schemaname='public' AND indexname='ix_documents_org_sha256'");
        Assert.DoesNotContain("UNIQUE", hashIndex, StringComparison.OrdinalIgnoreCase);
        var activeReviewIndex = await ScalarAsync<string>(
            "SELECT indexdef FROM pg_indexes WHERE schemaname='public' AND indexname='ux_review_sessions_one_active'");
        Assert.Contains("WHERE", activeReviewIndex, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Pending", activeReviewIndex, StringComparison.Ordinal);
        Assert.Contains("InProgress", activeReviewIndex, StringComparison.Ordinal);

        var evidenceColumns = await QueryStringsAsync(
            "SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name='field_evidence'");
        Assert.Contains("byte_start", evidenceColumns);
        Assert.Contains("byte_end", evidenceColumns);
        Assert.DoesNotContain("character_start", evidenceColumns);
        Assert.DoesNotContain("character_end", evidenceColumns);
    }

    [Fact]
    public async Task Core_check_constraints_reject_invalid_persisted_values()
    {
        var seed = await SeedGraphAsync();

        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation,
            "UPDATE documents SET size_bytes=0 WHERE id=@id", ("id", seed.DocumentA));
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation,
            "UPDATE documents SET sha256='INVALID' WHERE id=@id", ("id", seed.DocumentA));
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation,
            "UPDATE machine_field_values SET confidence=1.0001 WHERE id=@id", ("id", seed.FieldA));
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation,
            "UPDATE field_evidence SET page_number=0 WHERE id=@id", ("id", seed.EvidenceA));
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation,
            "UPDATE review_actions SET corrected_value_type='String' WHERE id=@id", ("id", seed.ActionA));
    }

    [Fact]
    public async Task Reconciliation_graph_round_trips_with_frozen_sources_findings_and_resolutions()
    {
        var seed = await SeedGraphAsync();
        await ExecuteAsync(
            "UPDATE review_sessions SET status='Completed', completed_at_utc=@now WHERE id=@id",
            ("now", DateTimeOffset.UtcNow), ("id", seed.SessionA));
        var now = DateTimeOffset.UtcNow;
        var item = new ReconciliationCase(Guid.NewGuid(), seed.OrganizationA, seed.ActorA,
            "August procurement case", now);
        item.LinkReviewedDocument(Guid.NewGuid(), ReconciliationDocumentRole.Invoice,
            seed.DocumentA, seed.SessionA, seed.RunA, ValidSha, now);
        var run = item.BeginEvaluation(Guid.NewGuid(),
            new Dictionary<ReconciliationDocumentRole, Guid>
            {
                [ReconciliationDocumentRole.Invoice] = Guid.NewGuid()
            }, "1.0", ReconciliationTolerancePolicy.Default, now);
        var document = Assert.Single(run.DocumentSnapshots);
        var field = run.AddFieldSnapshot(Guid.NewGuid(), document.Id, "total", 0,
            FieldValueType.Decimal, "100.00", seed.FieldA, seed.ActionA, 1,
            "100.00", now);
        var finding = run.AddFinding(Guid.NewGuid(), "DOC-001", "1.0",
            ReconciliationFindingSeverity.Warning, ReconciliationDocumentRole.Invoice,
            ReconciliationDocumentRole.PurchaseOrder, field.Id, null, "total", 0,
            "100.00", null, null, null, "Purchase order is missing.", now);
        item.CompleteEvaluation(run, now.AddSeconds(1));
        run.AddResolution(finding.Id, Guid.NewGuid(), seed.ActorA,
            FindingResolutionAction.Accepted, "Invoice-only exception approved for development.",
            now.AddSeconds(2));

        await using (var write = _database.CreateContext())
        {
            write.ReconciliationCases.Add(item);
            await write.SaveChangesAsync();
            var reference = item.Reference;
            item.UpdateMetadata("Renamed without changing evidence", ReconciliationCaseCategory.Services, now.AddMinutes(1));
            await write.SaveChangesAsync();
            Assert.Equal(reference, item.Reference);
        }

        await using var read = _database.CreateContext();
        var saved = await read.ReconciliationCases.AsNoTracking()
            .Include(candidate => candidate.DocumentLinks)
            .Include(candidate => candidate.Runs)
                .ThenInclude(candidate => candidate.DocumentSnapshots)
                    .ThenInclude(candidate => candidate.Fields)
            .Include(candidate => candidate.Runs)
                .ThenInclude(candidate => candidate.Findings)
                    .ThenInclude(candidate => candidate.Resolutions)
            .SingleAsync(candidate => candidate.Id == item.Id);

        Assert.Equal(ReconciliationCaseStatus.Evaluated, saved.Status);
        Assert.Single(saved.DocumentLinks);
        var savedRun = Assert.Single(saved.Runs);
        Assert.Equal("1.0", savedRun.RuleSetVersion);
        Assert.Equal(0.01m, savedRun.DocumentTotalTolerance);
        Assert.Single(Assert.Single(savedRun.DocumentSnapshots).Fields);
        var savedFinding = Assert.Single(savedRun.Findings);
        Assert.Equal("DOC-001", savedFinding.RuleCode);
        Assert.Equal(FindingResolutionAction.Accepted,
            Assert.Single(savedFinding.Resolutions).Action);
    }

    [Fact]
    public async Task Nullable_confidence_accepts_null_and_preserves_unit_interval_checks()
    {
        var seed = await SeedGraphAsync();

        await ExecuteAsync("UPDATE machine_field_values SET confidence=NULL WHERE id=@id",
            ("id", seed.FieldA));

        await using var context = _database.CreateContext();
        var field = await context.MachineFieldValues.AsNoTracking()
            .SingleAsync(item => item.Id == seed.FieldA);
        Assert.Null(field.Confidence);
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation,
            "UPDATE machine_field_values SET confidence=-0.0001 WHERE id=@id", ("id", seed.FieldA));
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation,
            "UPDATE machine_field_values SET confidence=1.0001 WHERE id=@id", ("id", seed.FieldA));
    }

    [Fact]
    public async Task Extraction_page_constraints_reject_invalid_rotation_number_and_text_size()
    {
        var seed = await SeedGraphAsync();

        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation,
            "UPDATE extraction_pages SET rotation_degrees=45 WHERE id=@id", ("id", seed.PageA));
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation,
            "UPDATE extraction_pages SET page_number=0 WHERE id=@id", ("id", seed.PageA));
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation,
            "UPDATE extraction_pages SET canonical_text=@text WHERE id=@id",
            ("text", new string('x', 1_048_577)), ("id", seed.PageA));
    }

    [Fact]
    public async Task Evidence_cannot_pair_a_field_with_a_page_from_another_run()
    {
        var seed = await SeedGraphAsync();
        var secondRun = Guid.NewGuid();
        var secondPage = Guid.NewGuid();
        await ExecuteAsync(InsertRunSql,
            RunParameters(seed.OrganizationA, seed.DocumentA, secondRun, 2, "second-run"));
        await ExecuteAsync(InsertPageSql,
            PageParameters(seed.OrganizationA, secondRun, secondPage));

        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation, InsertEvidenceSql,
            EvidenceParameters(seed.OrganizationA, secondRun, seed.FieldA, Guid.NewGuid()));
    }

    [Fact]
    public async Task Warning_constraints_scopes_and_uniqueness_are_enforced()
    {
        var seed = await SeedGraphAsync();
        var warningId = Guid.NewGuid();
        await ExecuteAsync(InsertWarningSql,
            WarningParameters(seed.OrganizationA, seed.RunA, warningId, "page_ocr_degraded",
                "Degraded", 1, null, null));

        await AssertSqlStateAsync(PostgresErrorCodes.UniqueViolation, InsertWarningSql,
            WarningParameters(seed.OrganizationA, seed.RunA, Guid.NewGuid(), "page_ocr_degraded",
                "Degraded", 1, null, null));
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation, InsertWarningSql,
            WarningParameters(seed.OrganizationA, seed.RunA, Guid.NewGuid(), "Invalid-Code",
                "Info", null, null, null));
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation, InsertWarningSql,
            WarningParameters(seed.OrganizationA, seed.RunA, Guid.NewGuid(), "unknown_severity",
                "Unknown", null, null, null));
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation, InsertWarningSql,
            WarningParameters(seed.OrganizationA, seed.RunA, Guid.NewGuid(), "missing_occurrence",
                "Info", null, "total", null));
        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation, InsertWarningSql,
            WarningParameters(seed.OrganizationA, seed.RunA, Guid.NewGuid(), "missing_page",
                "Info", 2, null, null));
        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation, InsertWarningSql,
            WarningParameters(seed.OrganizationA, seed.RunA, Guid.NewGuid(), "missing_field",
                "Info", null, "subtotal", 0));
    }

    [Fact]
    public async Task Normalized_extraction_aggregate_roundtrips_through_ef()
    {
        var seed = await SeedGraphAsync();
        await ExecuteAsync(InsertWarningSql,
            WarningParameters(seed.OrganizationA, seed.RunA, Guid.NewGuid(), "layout_detected",
                "Info", 1, "total", 0));

        await using var context = _database.CreateContext();
        var document = await context.Documents.AsNoTracking()
            .Include(item => item.ExtractionRuns).ThenInclude(run => run.Pages)
            .Include(item => item.ExtractionRuns).ThenInclude(run => run.Fields).ThenInclude(field => field.Evidence)
            .Include(item => item.ExtractionRuns).ThenInclude(run => run.Warnings)
            .AsSplitQuery()
            .SingleAsync(item => item.Id == seed.DocumentA);

        var run = Assert.Single(document.ExtractionRuns);
        var page = Assert.Single(run.Pages);
        var field = Assert.Single(run.Fields);
        var evidence = Assert.Single(field.Evidence);
        var warning = Assert.Single(run.Warnings);
        Assert.Equal("Total 100.00", page.CanonicalText);
        Assert.Equal((short)0, page.RotationDegrees);
        Assert.Equal(0.9m, field.Confidence);
        Assert.Equal(seed.RunA, evidence.ExtractionRunId);
        Assert.Equal("100.00", evidence.QuotedText);
        Assert.Equal(6, evidence.ByteStart);
        Assert.Equal(12, evidence.ByteEnd);
        Assert.Equal("[[0, 0], [1, 0], [1, 1], [0, 1]]", evidence.BoundingPolygon);
        Assert.Equal("normalized", evidence.CoordinateUnit);
        Assert.Equal("layout_detected", warning.Code);
        Assert.Equal(ExtractionWarningSeverity.Info, warning.Severity);
        Assert.Equal(1, warning.PageNumber);
        Assert.Equal("total", warning.FieldKey);
    }

    [Fact]
    public async Task Repository_list_uses_summary_shape_while_detailed_get_loads_normalized_evidence()
    {
        var seed = await SeedGraphAsync();
        await ExecuteAsync(InsertWarningSql,
            WarningParameters(seed.OrganizationA, seed.RunA, Guid.NewGuid(), "layout_detected",
                "Info", 1, "total", 0));
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = _database.TestConnectionString
            }).Build();
        services.AddInfrastructure(configuration);
        await using var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<
                UaeDocumentIntelligence.Application.Abstractions.IDocumentRepository>();
            var listed = Assert.Single(await repository.ListAsync(seed.OrganizationA, CancellationToken.None));
            var run = Assert.Single(listed.ExtractionRuns);
            Assert.Empty(run.Pages);
            Assert.Empty(run.Warnings);
            Assert.Single(Assert.Single(run.Fields).Evidence);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<
                UaeDocumentIntelligence.Application.Abstractions.IDocumentRepository>();
            var detailed = await repository.GetAsync(seed.OrganizationA, seed.DocumentA, CancellationToken.None);
            Assert.NotNull(detailed);
            var run = Assert.Single(detailed.ExtractionRuns);
            Assert.Single(run.Pages);
            Assert.Single(run.Warnings);
            Assert.Single(Assert.Single(run.Fields).Evidence);
        }
    }

    [Fact]
    public async Task Review_action_value_type_constraint_rejects_undefined_persisted_enum()
    {
        var seed = await SeedGraphAsync();

        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync("""
            UPDATE review_actions
            SET action='Corrected', corrected_value_type='Undefined',
                corrected_value_text='105.00', reason='reason'
            WHERE id=@id
            """, ("id", seed.ActionA)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_review_actions_value_type", exception.ConstraintName);
    }

    [Fact]
    public async Task Unique_keys_are_enforced_while_same_tenant_duplicate_hashes_are_allowed()
    {
        var seed = await SeedGraphAsync();

        await AssertSqlStateAsync(PostgresErrorCodes.UniqueViolation,
            "UPDATE documents SET storage_key=@key WHERE id=@id",
            ("key", "org-a-document.pdf"), ("id", seed.DocumentB));
        await AssertSqlStateAsync(PostgresErrorCodes.UniqueViolation, InsertRunSql,
            RunParameters(seed.OrganizationA, seed.DocumentA, Guid.NewGuid(), 1, "duplicate-run"));
        await AssertSqlStateAsync(PostgresErrorCodes.UniqueViolation, InsertFieldSql,
            FieldParameters(seed.OrganizationA, seed.RunA, Guid.NewGuid(), "total", 0));
        await AssertSqlStateAsync(PostgresErrorCodes.UniqueViolation, InsertReviewSessionSql,
            ReviewSessionParameters(seed.OrganizationA, seed.DocumentA, seed.RunA, Guid.NewGuid(), 2));

        var duplicateDocumentId = Guid.NewGuid();
        await ExecuteAsync(InsertDocumentSql,
            DocumentParameters(seed.OrganizationA, seed.ActorA, duplicateDocumentId,
                "org-a-duplicate.pdf", ValidSha));
        var count = await ScalarAsync<long>(
            "SELECT count(*) FROM documents WHERE organization_id=@organization AND sha256=@sha",
            ("organization", seed.OrganizationA), ("sha", ValidSha));
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Composite_relationships_reject_cross_tenant_references()
    {
        var seed = await SeedGraphAsync();

        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation, InsertDocumentSql,
            DocumentParameters(seed.OrganizationA, seed.ActorB, Guid.NewGuid(), "cross-actor.pdf", ValidSha));
        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation, InsertRunSql,
            RunParameters(seed.OrganizationA, seed.DocumentB, Guid.NewGuid(), 2, "cross-document"));
        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation, InsertFieldSql,
            FieldParameters(seed.OrganizationA, seed.RunB, Guid.NewGuid(), "total", 0));
        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation, InsertEvidenceSql,
            EvidenceParameters(seed.OrganizationA, seed.RunB, seed.FieldB, Guid.NewGuid()));

        var thirdDocument = Guid.NewGuid();
        await ExecuteAsync(InsertDocumentSql,
            DocumentParameters(seed.OrganizationA, seed.ActorA, thirdDocument, "third.pdf", ValidSha));
        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation, InsertReviewSessionSql,
            ReviewSessionParameters(seed.OrganizationA, thirdDocument, seed.RunB, Guid.NewGuid(), 1));
    }

    [Fact]
    public async Task Extraction_retries_persist_history_and_hydrate_selected_successful_output()
    {
        var seed = await SeedGraphAsync();
        var failedRun = Guid.NewGuid();
        await ExecuteAsync(InsertRunSql,
            RunParameters(seed.OrganizationA, seed.DocumentA, failedRun, 2, "retry-failed", "Failed"));

        await using var context = _database.CreateContext();
        var document = await context.Documents.AsNoTracking()
            .Include(item => item.ExtractionRuns).ThenInclude(run => run.Pages)
            .Include(item => item.ExtractionRuns).ThenInclude(run => run.Fields)
            .AsSplitQuery()
            .SingleAsync(item => item.Id == seed.DocumentA);

        Assert.Equal(2, document.ExtractionRuns.Count);
        Assert.Equal(ExtractionRunStatus.Failed,
            document.ExtractionRuns.Single(run => run.Id == failedRun).Status);
        var selected = document.SelectedExtractionRun();
        Assert.NotNull(selected);
        Assert.Equal(seed.RunA, selected.Id);
        Assert.Single(selected.Fields);
        Assert.Equal("100.00", selected.Fields.Single().ValueText);
    }

    [Fact]
    public async Task Review_action_composite_foreign_keys_protect_session_run_field_and_actor_identity()
    {
        var seed = await SeedGraphAsync();

        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation, InsertReviewActionSql,
            ReviewActionParameters(seed.OrganizationA, seed.SessionA, seed.RunB, seed.FieldB,
                seed.ActorA, Guid.NewGuid(), "total", 2));
        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation, InsertReviewActionSql,
            ReviewActionParameters(seed.OrganizationA, seed.SessionA, seed.RunA, seed.FieldA,
                seed.ActorA, Guid.NewGuid(), "subtotal", 2));
        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation, InsertReviewActionSql,
            ReviewActionParameters(seed.OrganizationA, seed.SessionA, seed.RunA, seed.FieldA,
                seed.ActorB, Guid.NewGuid(), "total", 2));
    }

    [Fact]
    public async Task Restrict_and_cascade_delete_contract_is_enforced()
    {
        var seed = await SeedGraphAsync();

        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation,
            "DELETE FROM organizations WHERE id=@id", ("id", seed.OrganizationA));
        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation,
            "DELETE FROM actors WHERE id=@id", ("id", seed.ActorA));
        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation,
            "DELETE FROM documents WHERE id=@id", ("id", seed.DocumentA));
        await AssertSqlStateAsync(PostgresErrorCodes.ForeignKeyViolation,
            "DELETE FROM review_sessions WHERE id=@id", ("id", seed.SessionA));

        await ExecuteAsync("DELETE FROM extraction_runs WHERE id=@id", ("id", seed.RunB));
        Assert.Equal(0, await ScalarAsync<long>(
            "SELECT count(*) FROM machine_field_values WHERE id=@id", ("id", seed.FieldB)));
        Assert.Equal(0, await ScalarAsync<long>(
            "SELECT count(*) FROM field_evidence WHERE id=@id", ("id", seed.EvidenceB)));
        Assert.Equal(1, await ScalarAsync<long>(
            "SELECT count(*) FROM documents WHERE id=@id", ("id", seed.DocumentB)));
    }

    [Fact]
    public async Task Concurrent_review_starts_persist_exactly_one_active_session()
    {
        var seed = await SeedGraphAsync();
        await ExecuteAsync("DELETE FROM review_actions WHERE review_session_id=@id", ("id", seed.SessionA));
        await ExecuteAsync("DELETE FROM review_sessions WHERE id=@id", ("id", seed.SessionA));
        await using var firstContext = _database.CreateContext();
        await using var secondContext = _database.CreateContext();
        var first = await LoadDocumentAsync(firstContext, seed.DocumentA);
        var second = await LoadDocumentAsync(secondContext, seed.DocumentA);

        first.OpenReview(Guid.NewGuid(), seed.ActorA, DateTimeOffset.UtcNow);
        second.OpenReview(Guid.NewGuid(), seed.ActorA, DateTimeOffset.UtcNow);
        await firstContext.SaveChangesAsync();
        var conflict = await Record.ExceptionAsync(() => secondContext.SaveChangesAsync());

        Assert.IsAssignableFrom<DbUpdateException>(conflict);
        Assert.Equal(1, await ScalarAsync<long>(
            "SELECT count(*) FROM review_sessions WHERE organization_id=@org AND document_id=@document AND status IN ('Pending','InProgress')",
            ("org", seed.OrganizationA), ("document", seed.DocumentA)));
    }

    [Fact]
    public async Task Concurrent_review_actions_persist_exactly_one_next_sequence()
    {
        var seed = await SeedGraphAsync();
        await using var firstContext = _database.CreateContext();
        await using var secondContext = _database.CreateContext();
        var first = await LoadDocumentAsync(firstContext, seed.DocumentA);
        var second = await LoadDocumentAsync(secondContext, seed.DocumentA);
        var firstSession = first.ReviewSessions.Single(x => x.Id == seed.SessionA);
        var secondSession = second.ReviewSessions.Single(x => x.Id == seed.SessionA);
        var firstField = first.ExtractionRuns.Single(x => x.Id == seed.RunA).Fields.Single(x => x.Id == seed.FieldA);
        var secondField = second.ExtractionRuns.Single(x => x.Id == seed.RunA).Fields.Single(x => x.Id == seed.FieldA);

        firstSession.AddAction(Guid.NewGuid(), seed.ActorA, 2, ReviewActionType.Corrected,
            firstField.FieldKey, firstField.OccurrenceIndex, firstField.Id,
            FieldValueType.Decimal, "101.00", "First correction", DateTimeOffset.UtcNow);
        secondSession.AddAction(Guid.NewGuid(), seed.ActorA, 2, ReviewActionType.Corrected,
            secondField.FieldKey, secondField.OccurrenceIndex, secondField.Id,
            FieldValueType.Decimal, "102.00", "Second correction", DateTimeOffset.UtcNow);
        await firstContext.SaveChangesAsync();
        var conflict = await Record.ExceptionAsync(() => secondContext.SaveChangesAsync());

        Assert.IsAssignableFrom<DbUpdateException>(conflict);
        Assert.Equal(1, await ScalarAsync<long>(
            "SELECT count(*) FROM review_actions WHERE review_session_id=@session AND sequence_number=2",
            ("session", seed.SessionA)));
    }

    [Fact]
    public async Task Upload_store_inserts_document_run_and_job_atomically_and_enforces_unique_run_job()
    {
        using var services = CreateQueueServices();
        var tenant = await SeedTenantAsync();
        var documentId = Guid.NewGuid();
        var document = CreateQueuedDocument(tenant, documentId, DateTimeOffset.UtcNow);
        var run = Assert.Single(document.ExtractionRuns);
        var invalidJobId = Guid.NewGuid();
        using (var scope = services.CreateScope())
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => scope.ServiceProvider
                .GetRequiredService<IUploadQueueStore>()
                .AddAsync(document, Guid.NewGuid(), invalidJobId, CancellationToken.None));
        }

        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM documents WHERE id=@id",
            ("id", documentId)));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM extraction_runs WHERE id=@id",
            ("id", run.Id)));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM extraction_jobs WHERE id=@id",
            ("id", invalidJobId)));

        var queued = await SeedQueuedUploadAsync(services);
        await ExecuteAsync("""
            UPDATE extraction_jobs
            SET status='Cancelled', completed_at_utc=clock_timestamp(), updated_at_utc=clock_timestamp(),
                last_error_code='request_cancelled', last_safe_error_summary='Cancelled.'
            WHERE id=@id
            """, ("id", queued.JobId));
        var duplicate = new ExtractionJob(Guid.NewGuid(), queued.OrganizationId, queued.DocumentId,
            queued.RunId, DateTimeOffset.UtcNow);
        await using var context = _database.CreateContext();
        context.ExtractionJobs.Add(duplicate);
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("ux_extraction_jobs_org_run", postgres.ConstraintName);
    }

    [Fact]
    public async Task Claim_skips_a_locked_first_job_and_claims_the_next_due_job()
    {
        using var services = CreateQueueServices();
        var first = await SeedQueuedUploadAsync(services, DateTimeOffset.UtcNow.AddMinutes(-2));
        var second = await SeedQueuedUploadAsync(services, DateTimeOffset.UtcNow.AddMinutes(-1));
        await using var lockConnection = await _database.OpenConnectionAsync();
        await using var transaction = await lockConnection.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand(
            "SELECT id FROM extraction_jobs WHERE id=@id FOR UPDATE", lockConnection, transaction))
        {
            command.Parameters.AddWithValue("id", first.JobId);
            Assert.Equal(first.JobId, await command.ExecuteScalarAsync());
        }

        using var scope = services.CreateScope();
        var claim = await scope.ServiceProvider.GetRequiredService<IExtractionJobQueue>()
            .ClaimAsync("worker-skip-locked", CancellationToken.None);

        Assert.NotNull(claim);
        Assert.Equal(second.JobId, claim.JobId);
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task Concurrent_claimers_never_receive_the_same_job()
    {
        using var services = CreateQueueServices();
        var queued = await SeedQueuedUploadAsync(services);
        using var firstScope = services.CreateScope();
        using var secondScope = services.CreateScope();
        var firstQueue = firstScope.ServiceProvider.GetRequiredService<IExtractionJobQueue>();
        var secondQueue = secondScope.ServiceProvider.GetRequiredService<IExtractionJobQueue>();

        var claims = await Task.WhenAll(
            firstQueue.ClaimAsync("worker-a", CancellationToken.None),
            secondQueue.ClaimAsync("worker-b", CancellationToken.None));

        var claimed = Assert.Single(claims.Where(item => item is not null));
        Assert.Equal(queued.JobId, claimed!.JobId);
        Assert.Single(claims.Where(item => item is null));
        Assert.Equal(1, await ScalarAsync<long>(
            "SELECT attempt_count FROM extraction_jobs WHERE id=@id", ("id", queued.JobId)));
    }

    [Fact]
    public async Task Claim_and_heartbeat_use_database_time_and_enforce_lease_fencing()
    {
        using var services = CreateQueueServices();
        var queued = await SeedQueuedUploadAsync(services);
        var before = await DatabaseNowAsync();
        using var scope = services.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IExtractionJobQueue>();

        var claim = Assert.IsType<ClaimedExtractionJob>(
            await queue.ClaimAsync("worker-clock", CancellationToken.None));
        var after = await DatabaseNowAsync();
        await using var context = _database.CreateContext();
        var leased = await context.ExtractionJobs.AsNoTracking().SingleAsync(x => x.Id == queued.JobId);

        Assert.InRange(leased.LastClaimedAtUtc!.Value, before, after);
        Assert.InRange(leased.LeaseExpiresAtUtc!.Value,
            leased.LastClaimedAtUtc.Value.AddSeconds(179),
            leased.LastClaimedAtUtc.Value.AddSeconds(181));
        Assert.InRange(claim.DeadlineUtc,
            leased.LastClaimedAtUtc.Value.AddSeconds(119),
            leased.LastClaimedAtUtc.Value.AddSeconds(121));
        Assert.False(await queue.HeartbeatAsync(claim.JobId, "wrong-worker", claim.LeaseToken,
            CancellationToken.None));
        Assert.False(await queue.HeartbeatAsync(claim.JobId, claim.LeaseOwner, Guid.NewGuid(),
            CancellationToken.None));
        Assert.True(await queue.HeartbeatAsync(claim.JobId, claim.LeaseOwner, claim.LeaseToken,
            CancellationToken.None));
    }

    [Fact]
    public async Task Upload_job_uses_database_clock_despite_future_application_clock_and_remains_claimable()
    {
        using var services = CreateQueueServices();
        var tenant = await SeedTenantAsync();
        var before = await DatabaseNowAsync();
        var applicationNow = before.AddDays(2);
        var document = CreateQueuedDocument(tenant, Guid.NewGuid(), applicationNow);
        var run = Assert.Single(document.ExtractionRuns);
        ExtractionJob job;
        using (var uploadScope = services.CreateScope())
        {
            job = await uploadScope.ServiceProvider.GetRequiredService<IUploadQueueStore>()
                .AddAsync(document, run.Id, Guid.NewGuid(), CancellationToken.None);
        }
        var after = await DatabaseNowAsync();

        Assert.InRange(job.CreatedAtUtc, before, after);
        Assert.Equal(job.CreatedAtUtc, job.AvailableAtUtc);
        Assert.Equal(job.CreatedAtUtc, job.UpdatedAtUtc);
        Assert.True(job.CreatedAtUtc < document.CreatedAtUtc);
        using var queueScope = services.CreateScope();
        var queue = queueScope.ServiceProvider.GetRequiredService<IExtractionJobQueue>();
        var claim = Assert.IsType<ClaimedExtractionJob>(
            await queue.ClaimAsync("worker-clock-skew", CancellationToken.None));
        Assert.Equal(job.Id, claim.JobId);
        Assert.True(await queue.RecordFailureAsync(claim, "provider_timeout", "Timed out.",
            true, CancellationToken.None));
        var retried = await ReadJobAsync(job.Id);
        Assert.Equal(ExtractionJobStatus.RetryScheduled, retried.Status);
        Assert.True(retried.AvailableAtUtc >= retried.UpdatedAtUtc.AddSeconds(5));
        await ExecuteAsync(
            "UPDATE extraction_jobs SET available_at_utc=created_at_utc WHERE id=@id",
            ("id", job.Id));
        using (var reclaimScope = services.CreateScope())
        {
            var reclaimQueue = reclaimScope.ServiceProvider.GetRequiredService<IExtractionJobQueue>();
            var reclaimed = Assert.IsType<ClaimedExtractionJob>(
                await reclaimQueue.ClaimAsync("worker-clock-skew-reclaim", CancellationToken.None));
            Assert.Equal(job.Id, reclaimed.JobId);
            Assert.Equal(2, reclaimed.Attempt);
            Assert.True(await reclaimQueue.CompleteAsync(reclaimed, CreateReadyResult(reclaimed),
                CancellationToken.None));
        }
        var terminal = await ReadJobAsync(job.Id);
        Assert.Equal(ExtractionJobStatus.Succeeded, terminal.Status);
        await using var context = _database.CreateContext();
        var persistedDocument = await context.Documents.AsNoTracking()
            .SingleAsync(item => item.Id == document.Id);
        var persistedRun = await context.ExtractionRuns.AsNoTracking()
            .SingleAsync(item => item.Id == run.Id);
        Assert.Equal(DocumentProcessingStatus.ReadyForReview, persistedDocument.ProcessingStatus);
        Assert.Equal(ExtractionRunStatus.Succeeded, persistedRun.Status);
        Assert.True(persistedDocument.UpdatedAtUtc >= document.CreatedAtUtc);
        Assert.True(persistedRun.CompletedAtUtc >= run.CreatedAtUtc);
    }

    [Fact]
    public async Task Retry_schedule_is_transient_only_and_uses_exact_five_then_thirty_second_delays()
    {
        using var services = CreateQueueServices();
        var queued = await SeedQueuedUploadAsync(services);
        using var firstScope = services.CreateScope();
        var firstQueue = firstScope.ServiceProvider.GetRequiredService<IExtractionJobQueue>();
        var first = Assert.IsType<ClaimedExtractionJob>(
            await firstQueue.ClaimAsync("worker-retry-1", CancellationToken.None));
        var beforeFive = await DatabaseNowAsync();
        Assert.True(await firstQueue.RecordFailureAsync(first, "provider_timeout", "Timed out.",
            true, CancellationToken.None));
        var afterFive = await DatabaseNowAsync();
        var afterFirst = await ReadJobAsync(queued.JobId);
        Assert.Equal(ExtractionJobStatus.RetryScheduled, afterFirst.Status);
        Assert.InRange(afterFirst.AvailableAtUtc, beforeFive.AddSeconds(5), afterFive.AddSeconds(5));

        await ExecuteAsync(
            "UPDATE extraction_jobs SET available_at_utc=created_at_utc WHERE id=@id",
            ("id", queued.JobId));
        using var secondScope = services.CreateScope();
        var secondQueue = secondScope.ServiceProvider.GetRequiredService<IExtractionJobQueue>();
        var second = Assert.IsType<ClaimedExtractionJob>(
            await secondQueue.ClaimAsync("worker-retry-2", CancellationToken.None));
        Assert.Equal(first.JobId, second.JobId);
        Assert.Equal(first.ExtractionRunId, second.ExtractionRunId);
        var beforeThirty = await DatabaseNowAsync();
        Assert.True(await secondQueue.RecordFailureAsync(second, "provider_unavailable", "Unavailable.",
            true, CancellationToken.None));
        var afterThirty = await DatabaseNowAsync();
        var afterSecond = await ReadJobAsync(queued.JobId);
        Assert.Equal(ExtractionJobStatus.RetryScheduled, afterSecond.Status);
        Assert.InRange(afterSecond.AvailableAtUtc,
            beforeThirty.AddSeconds(30), afterThirty.AddSeconds(30));

        var permanent = await SeedQueuedUploadAsync(services);
        using var permanentScope = services.CreateScope();
        var permanentQueue = permanentScope.ServiceProvider.GetRequiredService<IExtractionJobQueue>();
        var permanentClaim = Assert.IsType<ClaimedExtractionJob>(
            await permanentQueue.ClaimAsync("worker-permanent", CancellationToken.None));
        Assert.True(await permanentQueue.RecordFailureAsync(permanentClaim,
            "unsupported_document", "Unsupported.", true, CancellationToken.None));
        Assert.Equal(ExtractionJobStatus.Failed, (await ReadJobAsync(permanent.JobId)).Status);
    }

    [Fact]
    public async Task Manual_reprocess_after_terminal_creates_new_run_and_job_and_preserves_reviewable_output()
    {
        using var services = CreateQueueServices();
        var initial = await SeedQueuedUploadAsync(services);
        using (var completionScope = services.CreateScope())
        {
            var queue = completionScope.ServiceProvider.GetRequiredService<IExtractionJobQueue>();
            var claim = Assert.IsType<ClaimedExtractionJob>(
                await queue.ClaimAsync("worker-initial", CancellationToken.None));
            Assert.True(await queue.CompleteAsync(claim, CreateReadyResult(claim),
                CancellationToken.None));
        }

        QueuedReprocess? reprocess;
        ExtractionJobSnapshot? latest;
        using (var reprocessScope = services.CreateScope())
        {
            var queue = reprocessScope.ServiceProvider.GetRequiredService<IExtractionJobQueue>();
            reprocess = await queue.QueueReprocessAsync(initial.OrganizationId, initial.DocumentId,
                Guid.NewGuid(), Guid.NewGuid(), "local", "local-1.0.0", "2.0",
                "request-reprocess", CancellationToken.None);
            latest = await queue.GetLatestAsync(initial.OrganizationId, initial.DocumentId,
                CancellationToken.None);
        }

        Assert.NotNull(reprocess);
        Assert.Equal(2, reprocess.RunNumber);
        Assert.NotEqual(initial.RunId, reprocess.ExtractionRunId);
        Assert.NotEqual(initial.JobId, reprocess.JobId);
        Assert.NotNull(latest);
        Assert.Equal(reprocess.JobId, latest.JobId);
        Assert.Equal(reprocess.ExtractionRunId, latest.ExtractionRunId);
        Assert.Equal(ExtractionJobStatus.Pending, latest.Status);
        await using var context = _database.CreateContext();
        var document = await context.Documents.AsSplitQuery()
            .Include(item => item.ExtractionRuns).ThenInclude(run => run.Pages)
            .Include(item => item.ExtractionRuns).ThenInclude(run => run.Fields)
                .ThenInclude(field => field.Evidence)
            .SingleAsync(item => item.Id == initial.DocumentId);
        Assert.Equal(DocumentProcessingStatus.ReadyForReview, document.ProcessingStatus);
        Assert.Equal(initial.RunId, document.SelectedExtractionRun()!.Id);
        Assert.Equal(2, document.ExtractionRuns.Count);
        Assert.Equal(ExtractionRunStatus.Queued,
            document.ExtractionRuns.Single(run => run.Id == reprocess.ExtractionRunId).Status);
    }

    [Fact]
    public async Task Active_job_blocks_manual_reprocess_and_latest_is_tenant_scoped()
    {
        using var services = CreateQueueServices();
        var initial = await SeedQueuedUploadAsync(services);
        using var scope = services.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IExtractionJobQueue>();

        var blocked = await queue.QueueReprocessAsync(initial.OrganizationId, initial.DocumentId,
            Guid.NewGuid(), Guid.NewGuid(), "local", "local-1.0.0", "2.0",
            "request-blocked", CancellationToken.None);
        var correctTenant = await queue.GetLatestAsync(initial.OrganizationId, initial.DocumentId,
            CancellationToken.None);
        var wrongTenant = await queue.GetLatestAsync(Guid.NewGuid(), initial.DocumentId,
            CancellationToken.None);

        Assert.Null(blocked);
        Assert.NotNull(correctTenant);
        Assert.Equal(initial.JobId, correctTenant.JobId);
        Assert.Null(wrongTenant);
        Assert.Equal(1, await ScalarAsync<long>(
            "SELECT count(*) FROM extraction_runs WHERE document_id=@document",
            ("document", initial.DocumentId)));
        Assert.Equal(1, await ScalarAsync<long>(
            "SELECT count(*) FROM extraction_jobs WHERE document_id=@document",
            ("document", initial.DocumentId)));
    }

    [Fact]
    public async Task Reclaimed_job_rejects_stale_completion_and_current_completion_is_idempotent()
    {
        using var services = CreateQueueServices();
        var queued = await SeedQueuedUploadAsync(services);
        using var oldScope = services.CreateScope();
        var oldQueue = oldScope.ServiceProvider.GetRequiredService<IExtractionJobQueue>();
        var oldClaim = Assert.IsType<ClaimedExtractionJob>(
            await oldQueue.ClaimAsync("worker-old", CancellationToken.None));
        await ExecuteAsync(
            "UPDATE extraction_jobs SET lease_expires_at_utc=clock_timestamp()-interval '1 second' WHERE id=@id",
            ("id", queued.JobId));
        using var currentScope = services.CreateScope();
        var currentQueue = currentScope.ServiceProvider.GetRequiredService<IExtractionJobQueue>();
        var currentClaim = Assert.IsType<ClaimedExtractionJob>(
            await currentQueue.ClaimAsync("worker-current", CancellationToken.None));
        var result = CreateReadyResult(currentClaim);

        Assert.False(await oldQueue.CompleteAsync(oldClaim, result, CancellationToken.None));
        Assert.Equal(0, await ScalarAsync<long>(
            "SELECT count(*) FROM extraction_pages WHERE extraction_run_id=@run", ("run", queued.RunId)));
        Assert.True(await currentQueue.CompleteAsync(currentClaim, result, CancellationToken.None));
        Assert.False(await currentQueue.CompleteAsync(currentClaim, result, CancellationToken.None));
        Assert.Equal(1, await ScalarAsync<long>(
            "SELECT count(*) FROM extraction_pages WHERE extraction_run_id=@run", ("run", queued.RunId)));
        Assert.Equal(1, await ScalarAsync<long>(
            "SELECT count(*) FROM machine_field_values WHERE extraction_run_id=@run", ("run", queued.RunId)));
        var job = await ReadJobAsync(queued.JobId);
        Assert.Equal(ExtractionJobStatus.Succeeded, job.Status);
        Assert.Equal(ExtractionOutcomeValue.Ready, job.ResultOutcome);
    }

    [Fact]
    public async Task Cancelling_a_leased_job_invalidates_token_and_stale_worker_writes_nothing()
    {
        using var services = CreateQueueServices();
        var queued = await SeedQueuedUploadAsync(services);
        using var scope = services.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IExtractionJobQueue>();
        var claim = Assert.IsType<ClaimedExtractionJob>(
            await queue.ClaimAsync("worker-cancel", CancellationToken.None));

        Assert.True(await queue.CancelAsync(queued.OrganizationId, queued.JobId,
            "request_cancelled", "Cancelled.", CancellationToken.None));
        Assert.False(await queue.CompleteAsync(claim, CreateReadyResult(claim), CancellationToken.None));
        Assert.False(await queue.HeartbeatAsync(claim.JobId, claim.LeaseOwner, claim.LeaseToken,
            CancellationToken.None));
        var job = await ReadJobAsync(queued.JobId);
        Assert.Equal(ExtractionJobStatus.Cancelled, job.Status);
        Assert.Null(job.LeaseToken);
        await using var context = _database.CreateContext();
        var run = await context.ExtractionRuns.AsNoTracking().SingleAsync(x => x.Id == queued.RunId);
        var document = await context.Documents.AsNoTracking().SingleAsync(x => x.Id == queued.DocumentId);
        Assert.Equal(ExtractionRunStatus.Cancelled, run.Status);
        Assert.Equal(DocumentProcessingStatus.ProcessingCancelled, document.ProcessingStatus);
    }

    [Fact]
    public async Task Cancelling_a_pending_job_terminalizes_job_run_and_document_without_a_worker_claim()
    {
        using var services = CreateQueueServices();
        var queued = await SeedQueuedUploadAsync(services);
        using var scope = services.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IExtractionJobQueue>();

        Assert.True(await queue.CancelAsync(queued.OrganizationId, queued.JobId,
            "request_cancelled", "Cancelled.", CancellationToken.None));
        Assert.Null(await queue.ClaimAsync("worker-after-cancel", CancellationToken.None));

        var job = await ReadJobAsync(queued.JobId);
        Assert.Equal(ExtractionJobStatus.Cancelled, job.Status);
        Assert.Equal(0, job.AttemptCount);
        Assert.Null(job.LeaseOwner);
        Assert.Null(job.LeaseToken);
        Assert.Null(job.LeaseExpiresAtUtc);
        Assert.Null(job.LastHeartbeatAtUtc);

        await using var context = _database.CreateContext();
        var run = await context.ExtractionRuns.AsNoTracking().SingleAsync(x => x.Id == queued.RunId);
        var document = await context.Documents.AsNoTracking().SingleAsync(x => x.Id == queued.DocumentId);
        Assert.Equal(ExtractionRunStatus.Cancelled, run.Status);
        Assert.Equal(DocumentProcessingStatus.ProcessingCancelled, document.ProcessingStatus);
    }

    [Fact]
    public async Task Final_expired_claim_is_terminalized_as_worker_lease_expired()
    {
        using var services = CreateQueueServices();
        var queued = await SeedQueuedUploadAsync(services);
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            using var scope = services.CreateScope();
            var queue = scope.ServiceProvider.GetRequiredService<IExtractionJobQueue>();
            var claim = Assert.IsType<ClaimedExtractionJob>(
                await queue.ClaimAsync($"worker-{attempt}", CancellationToken.None));
            await ExecuteAsync(
                "UPDATE extraction_jobs SET lease_expires_at_utc=clock_timestamp()-interval '1 second' WHERE id=@id",
                ("id", queued.JobId));
            if (attempt < 3)
            {
                using var reclaimScope = services.CreateScope();
                var reclaim = reclaimScope.ServiceProvider.GetRequiredService<IExtractionJobQueue>();
                var next = Assert.IsType<ClaimedExtractionJob>(
                    await reclaim.ClaimAsync($"worker-{attempt}-reclaim", CancellationToken.None));
                Assert.Equal(attempt + 1, next.Attempt);
                attempt++;
                await ExecuteAsync(
                    "UPDATE extraction_jobs SET lease_expires_at_utc=clock_timestamp()-interval '1 second' WHERE id=@id",
                    ("id", queued.JobId));
            }
        }

        using var terminalScope = services.CreateScope();
        var terminalQueue = terminalScope.ServiceProvider.GetRequiredService<IExtractionJobQueue>();
        Assert.True(await terminalQueue.TerminalizeOneExpiredAsync(CancellationToken.None));
        Assert.False(await terminalQueue.TerminalizeOneExpiredAsync(CancellationToken.None));
        var job = await ReadJobAsync(queued.JobId);
        Assert.Equal(ExtractionJobStatus.Failed, job.Status);
        Assert.Equal(3, job.AttemptCount);
        Assert.Equal("worker_lease_expired", job.LastErrorCode);
    }

    private ServiceProvider CreateQueueServices()
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = _database.TestConnectionString,
            ["Storage:UploadDirectory"] = Path.Combine(Path.GetTempPath(), "queue-persistence-tests"),
            ["AiService:BaseUrl"] = "http://localhost:8001",
            ["ExtractionWorker:Enabled"] = "false"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider();
    }

    private async Task<QueueSeed> SeedQueuedUploadAsync(ServiceProvider services,
        DateTimeOffset? createdAt = null)
    {
        var tenant = await SeedTenantAsync();
        var now = createdAt ?? DateTimeOffset.UtcNow;
        var document = CreateQueuedDocument(tenant, Guid.NewGuid(), now);
        var run = Assert.Single(document.ExtractionRuns);
        using var scope = services.CreateScope();
        var jobId = Guid.NewGuid();
        var job = await scope.ServiceProvider.GetRequiredService<IUploadQueueStore>()
            .AddAsync(document, run.Id, jobId, CancellationToken.None);
        return new QueueSeed(tenant.OrganizationId, tenant.ActorId, document.Id, run.Id, job.Id);
    }

    private async Task<TenantSeed> SeedTenantAsync()
    {
        var tenant = new TenantSeed(Guid.NewGuid(), Guid.NewGuid());
        var slug = $"queue-{tenant.OrganizationId:N}";
        var now = DateTimeOffset.UtcNow;
        await ExecuteAsync(
            "INSERT INTO organizations (id,slug,display_name,created_at_utc) VALUES (@id,@slug,'Queue Test',@now)",
            ("id", tenant.OrganizationId), ("slug", slug), ("now", now));
        await ExecuteAsync(
            "INSERT INTO actors (id,organization_id,kind,display_name,is_active,created_at_utc) VALUES (@id,@org,'System','Queue Actor',true,@now)",
            ("id", tenant.ActorId), ("org", tenant.OrganizationId), ("now", now));
        return tenant;
    }

    private static Document CreateQueuedDocument(TenantSeed tenant, Guid documentId,
        DateTimeOffset now)
    {
        var document = new Document(documentId, tenant.OrganizationId, tenant.ActorId,
            "invoice.pdf",
            $"organizations/{tenant.OrganizationId:N}/documents/{documentId:N}/source.pdf",
            "application/pdf", 12345, ValidSha, now);
        document.QueueExtraction(Guid.NewGuid(), 1, "local", "local-1.0.0", "2.0",
            $"request-{documentId:N}", now);
        return document;
    }

    private async Task<ExtractionJob> ReadJobAsync(Guid jobId)
    {
        await using var context = _database.CreateContext();
        return await context.ExtractionJobs.AsNoTracking().SingleAsync(x => x.Id == jobId);
    }

    private async Task<DateTimeOffset> DatabaseNowAsync()
    {
        await using var connection = await _database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("SELECT clock_timestamp()", connection);
        var value = await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("PostgreSQL clock was unavailable.");
        return value switch
        {
            DateTimeOffset offset => offset,
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            _ => throw new InvalidOperationException("PostgreSQL returned an unexpected clock value.")
        };
    }

    private static ExtractionResult CreateReadyResult(ClaimedExtractionJob claim) => new(
        claim.SchemaVersion, ExtractionOutcome.Ready, "invoice", "en",
        new ExtractionProviderResult(claim.ProviderAlias, claim.ProviderVersion,
            "tesseract-eng", "5.3.0"),
        new ExtractionMetricsResult(25, 1, null, null, null),
        [new ExtractionPageResult(1, "105.00", 0)],
        [new ExtractionField("total", 0, FieldValueType.Decimal, "105.00", "105.00", 0.98m,
            [new ExtractionEvidenceResult(1, "105.00", 0, 6,
                "[[0.1,0.1],[0.9,0.1],[0.9,0.9],[0.1,0.9]]", "normalized")])], []);

    private static Task<Document> LoadDocumentAsync(
        UaeDocumentIntelligence.Infrastructure.Persistence.AppDbContext context, Guid documentId) =>
        context.Documents
            .Include(x => x.ExtractionRuns).ThenInclude(x => x.Pages)
            .Include(x => x.ExtractionRuns).ThenInclude(x => x.Fields).ThenInclude(x => x.Evidence)
            .Include(x => x.ExtractionRuns).ThenInclude(x => x.Warnings)
            .Include(x => x.ReviewSessions).ThenInclude(x => x.Actions)
            .AsSplitQuery()
            .SingleAsync(x => x.Id == documentId);

    private async Task<SeedData> SeedGraphAsync()
    {
        var seed = SeedData.Create();
        var now = DateTimeOffset.UtcNow;
        await ExecuteAsync(
            "INSERT INTO organizations (id,slug,display_name,created_at_utc) VALUES (@a,'org-a','Organization A',@now),(@b,'org-b','Organization B',@now)",
            ("a", seed.OrganizationA), ("b", seed.OrganizationB), ("now", now));
        await ExecuteAsync(
            "INSERT INTO actors (id,organization_id,kind,display_name,is_active,created_at_utc) VALUES (@actorA,@orgA,'System','Actor A',true,@now),(@actorB,@orgB,'System','Actor B',true,@now)",
            ("actorA", seed.ActorA), ("orgA", seed.OrganizationA),
            ("actorB", seed.ActorB), ("orgB", seed.OrganizationB), ("now", now));
        await ExecuteAsync(InsertDocumentSql,
            DocumentParameters(seed.OrganizationA, seed.ActorA, seed.DocumentA, "org-a-document.pdf", ValidSha));
        await ExecuteAsync(InsertDocumentSql,
            DocumentParameters(seed.OrganizationB, seed.ActorB, seed.DocumentB, "org-b-document.pdf", ValidSha));
        await ExecuteAsync(InsertRunSql,
            RunParameters(seed.OrganizationA, seed.DocumentA, seed.RunA, 1, "org-a-run"));
        await ExecuteAsync(InsertRunSql,
            RunParameters(seed.OrganizationB, seed.DocumentB, seed.RunB, 1, "org-b-run"));
        await ExecuteAsync(InsertPageSql,
            PageParameters(seed.OrganizationA, seed.RunA, seed.PageA));
        await ExecuteAsync(InsertPageSql,
            PageParameters(seed.OrganizationB, seed.RunB, seed.PageB));
        await ExecuteAsync(InsertFieldSql,
            FieldParameters(seed.OrganizationA, seed.RunA, seed.FieldA, "total", 0));
        await ExecuteAsync(InsertFieldSql,
            FieldParameters(seed.OrganizationB, seed.RunB, seed.FieldB, "total", 0));
        await ExecuteAsync(InsertEvidenceSql,
            EvidenceParameters(seed.OrganizationA, seed.RunA, seed.FieldA, seed.EvidenceA));
        await ExecuteAsync(InsertEvidenceSql,
            EvidenceParameters(seed.OrganizationB, seed.RunB, seed.FieldB, seed.EvidenceB));
        await ExecuteAsync(InsertReviewSessionSql,
            ReviewSessionParameters(seed.OrganizationA, seed.DocumentA, seed.RunA, seed.SessionA, 1));
        await ExecuteAsync(InsertReviewActionSql,
            ReviewActionParameters(seed.OrganizationA, seed.SessionA, seed.RunA, seed.FieldA,
                seed.ActorA, seed.ActionA, "total", 1));
        return seed;
    }

    private async Task AssertSqlStateAsync(string sqlState, string sql,
        params (string Name, object Value)[] parameters)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(sql, parameters));
        Assert.Equal(sqlState, exception.SqlState);
    }

    private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await _database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await _database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        var result = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(result!, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task<string[]> QueryStringsAsync(string sql)
    {
        var values = new List<string>();
        await using var connection = await _database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) values.Add(reader.GetString(0));
        return values.ToArray();
    }

    private async Task<bool> TableExistsAsync(string table) => await ScalarAsync<bool>(
        "SELECT EXISTS(SELECT 1 FROM information_schema.tables WHERE table_schema='public' AND table_name=@table)",
        ("table", table));

    private const string InsertDocumentSql = """
        INSERT INTO documents
        (id,organization_id,created_by_actor_id,original_filename,storage_key,declared_content_type,
         size_bytes,sha256,processing_status,concurrency_token,created_at_utc,updated_at_utc)
        VALUES (@id,@organization,@actor,'invoice.pdf',@storage,'application/pdf',100,@sha,
                'ReadyForReview',@token,@now,@now)
        """;

    private const string InsertRunSql = """
        INSERT INTO extraction_runs
        (id,organization_id,document_id,run_number,status,document_type,language,provider_name,
         provider_version,extraction_schema_version,correlation_id,created_at_utc,started_at_utc,
         completed_at_utc,duration_ms,model_name,model_version,provider_duration_ms,page_count,
         failure_code,safe_failure_summary)
        VALUES (@id,@organization,@document,@number,@status,'invoice','en','mock','1','2.0',
                @correlation,@now,@now,@now,1,'mock-invoice','2.0',
                CASE WHEN @status='Succeeded' THEN 25 ELSE NULL END,
                CASE WHEN @status='Succeeded' THEN 1 ELSE NULL END,
                CASE WHEN @status='Failed' THEN 'provider_failure' ELSE NULL END,
                CASE WHEN @status='Failed' THEN 'Safe failure.' ELSE NULL END)
        """;

    private const string LegacyInsertRunSql = """
        INSERT INTO extraction_runs
        (id,organization_id,document_id,run_number,status,document_type,language,provider_name,
         provider_version,extraction_schema_version,correlation_id,created_at_utc,started_at_utc,
         completed_at_utc,duration_ms,failure_code,safe_failure_summary)
        VALUES (@id,@organization,@document,@number,@status,'invoice','en','mock','1','invoice-v1',
                @correlation,@now,@now,@now,1,NULL,NULL)
        """;

    private const string InsertPageSql = """
        INSERT INTO extraction_pages
        (id,organization_id,extraction_run_id,page_number,canonical_text,rotation_degrees)
        VALUES (@id,@organization,@run,1,'Total 100.00',0)
        """;

    private const string InsertFieldSql = """
        INSERT INTO machine_field_values
        (id,organization_id,extraction_run_id,field_key,occurrence_index,value_type,value_text,
         normalized_value_text,confidence,created_at_utc)
        VALUES (@id,@organization,@run,@key,@occurrence,'Decimal','100.00','100.00',0.9000,@now)
        """;

    private const string InsertEvidenceSql = """
        INSERT INTO field_evidence
        (id,organization_id,extraction_run_id,machine_field_value_id,page_number,quoted_text,
         byte_start,byte_end,bounding_polygon,coordinate_unit)
        VALUES (@id,@organization,@run,@field,1,'100.00',6,12,
                '[[0,0],[1,0],[1,1],[0,1]]'::jsonb,'normalized')
        """;

    private const string LegacyInsertEvidenceSql = """
        INSERT INTO field_evidence
        (id,organization_id,machine_field_value_id,page_number,quoted_text,character_start,character_end,
         bounding_polygon,coordinate_unit)
        VALUES (@id,@organization,@field,@page,'Total 100.00',0,12,'0,0,1,0,1,1,0,1','pixels')
        """;

    private const string InsertWarningSql = """
        INSERT INTO extraction_warnings
        (id,organization_id,extraction_run_id,code,severity,safe_summary,page_number,field_key,occurrence_index)
        VALUES (@id,@organization,@run,@code,@severity,'Safe warning.',@page,@field,@occurrence)
        """;

    private const string InsertReviewSessionSql = """
        INSERT INTO review_sessions
        (id,organization_id,document_id,extraction_run_id,revision_number,status,concurrency_token,
         created_at_utc,started_at_utc)
        VALUES (@id,@organization,@document,@run,@revision,'InProgress',@token,@now,@now)
        """;

    private const string InsertReviewActionSql = """
        INSERT INTO review_actions
        (id,organization_id,review_session_id,extraction_run_id,machine_field_value_id,actor_id,
         field_key,occurrence_index,sequence_number,action,created_at_utc)
        VALUES (@id,@organization,@session,@run,@field,@actor,@key,0,@sequence,'Confirmed',@now)
        """;

    private static (string, object)[] DocumentParameters(Guid organization, Guid actor, Guid id,
        string storage, string sha) =>
    [
        ("id", id), ("organization", organization), ("actor", actor), ("storage", storage),
        ("sha", sha), ("token", Guid.NewGuid()), ("now", DateTimeOffset.UtcNow)
    ];

    private static (string, object)[] RunParameters(Guid organization, Guid document, Guid id,
        int number, string correlation, string status = "Succeeded") =>
    [
        ("id", id), ("organization", organization), ("document", document), ("number", number),
        ("status", status), ("correlation", correlation), ("now", DateTimeOffset.UtcNow)
    ];

    private static (string, object)[] FieldParameters(Guid organization, Guid run, Guid id,
        string key, int occurrence) =>
    [
        ("id", id), ("organization", organization), ("run", run), ("key", key),
        ("occurrence", occurrence), ("now", DateTimeOffset.UtcNow)
    ];

    private static (string, object)[] PageParameters(Guid organization, Guid run, Guid id) =>
    [
        ("id", id), ("organization", organization), ("run", run)
    ];

    private static (string, object)[] EvidenceParameters(Guid organization, Guid run, Guid field, Guid id) =>
    [
        ("id", id), ("organization", organization), ("run", run), ("field", field)
    ];

    private static (string, object)[] WarningParameters(Guid organization, Guid run, Guid id,
        string code, string severity, int? page, string? field, int? occurrence) =>
    [
        ("id", id), ("organization", organization), ("run", run), ("code", code),
        ("severity", severity), ("page", page ?? (object)DBNull.Value),
        ("field", field ?? (object)DBNull.Value), ("occurrence", occurrence ?? (object)DBNull.Value)
    ];

    private static (string, object)[] ReviewSessionParameters(Guid organization, Guid document,
        Guid run, Guid id, int revision) =>
    [
        ("id", id), ("organization", organization), ("document", document), ("run", run),
        ("revision", revision), ("token", Guid.NewGuid()), ("now", DateTimeOffset.UtcNow)
    ];

    private static (string, object)[] ReviewActionParameters(Guid organization, Guid session,
        Guid run, Guid field, Guid actor, Guid id, string key, int sequence) =>
    [
        ("id", id), ("organization", organization), ("session", session), ("run", run),
        ("field", field), ("actor", actor), ("key", key), ("sequence", sequence),
        ("now", DateTimeOffset.UtcNow)
    ];

    private sealed record SeedData(
        Guid OrganizationA, Guid OrganizationB, Guid ActorA, Guid ActorB,
        Guid DocumentA, Guid DocumentB, Guid RunA, Guid RunB,
        Guid PageA, Guid PageB, Guid FieldA, Guid FieldB, Guid EvidenceA, Guid EvidenceB,
        Guid SessionA, Guid ActionA)
    {
        public static SeedData Create() => new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid());
    }

    private sealed record TenantSeed(Guid OrganizationId, Guid ActorId);
    private sealed record QueueSeed(Guid OrganizationId, Guid ActorId, Guid DocumentId,
        Guid RunId, Guid JobId);
}
