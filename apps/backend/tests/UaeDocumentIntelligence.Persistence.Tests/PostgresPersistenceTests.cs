using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed class PostgresPersistenceTests(PostgresDatabaseFixture database) :
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
        Assert.Equal(2, before.Length);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());

        await context.Database.MigrateAsync();
        Assert.Equal(before, (await context.Database.GetAppliedMigrationsAsync()).ToArray());

        var migrator = context.GetService<IMigrator>();
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
            "__EFMigrationsHistory", "actors", "documents", "extraction_runs", "field_evidence",
            "machine_field_values", "organizations", "review_actions", "review_sessions"
        }, tables);

        var constraints = await QueryStringsAsync(
            "SELECT conname FROM pg_constraint WHERE connamespace='public'::regnamespace ORDER BY conname");
        var expectedConstraints = new[]
        {
            "ck_documents_sha256", "ck_documents_size", "ck_documents_status",
            "ck_extraction_runs_duration", "ck_extraction_runs_number",
            "ck_field_evidence_page", "ck_machine_fields_confidence",
            "ck_review_actions_matrix", "ck_review_actions_value_type", "ck_review_sessions_revision"
        };
        foreach (var expected in expectedConstraints) Assert.Contains(expected, constraints);

        var indexes = await QueryStringsAsync("SELECT indexname FROM pg_indexes WHERE schemaname='public'");
        Assert.Contains("ix_documents_org_sha256", indexes);
        Assert.Contains("ux_documents_storage_key", indexes);
        Assert.Contains("ux_extraction_runs_org_document_number", indexes);
        Assert.Contains("ux_review_sessions_one_active", indexes);
        Assert.Contains("ux_review_actions_org_session_sequence", indexes);

        var hashIndex = await ScalarAsync<string>(
            "SELECT indexdef FROM pg_indexes WHERE schemaname='public' AND indexname='ix_documents_org_sha256'");
        Assert.DoesNotContain("UNIQUE", hashIndex, StringComparison.OrdinalIgnoreCase);
        var activeReviewIndex = await ScalarAsync<string>(
            "SELECT indexdef FROM pg_indexes WHERE schemaname='public' AND indexname='ux_review_sessions_one_active'");
        Assert.Contains("WHERE", activeReviewIndex, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Pending", activeReviewIndex, StringComparison.Ordinal);
        Assert.Contains("InProgress", activeReviewIndex, StringComparison.Ordinal);
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
            EvidenceParameters(seed.OrganizationA, seed.FieldB, Guid.NewGuid()));

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
            .Include(item => item.ExtractionRuns)
            .ThenInclude(run => run.Fields)
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

    private static Task<Document> LoadDocumentAsync(
        UaeDocumentIntelligence.Infrastructure.Persistence.AppDbContext context, Guid documentId) =>
        context.Documents
            .Include(x => x.ExtractionRuns).ThenInclude(x => x.Fields).ThenInclude(x => x.Evidence)
            .Include(x => x.ReviewSessions).ThenInclude(x => x.Actions)
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
        await ExecuteAsync(InsertFieldSql,
            FieldParameters(seed.OrganizationA, seed.RunA, seed.FieldA, "total", 0));
        await ExecuteAsync(InsertFieldSql,
            FieldParameters(seed.OrganizationB, seed.RunB, seed.FieldB, "total", 0));
        await ExecuteAsync(InsertEvidenceSql,
            EvidenceParameters(seed.OrganizationA, seed.FieldA, seed.EvidenceA));
        await ExecuteAsync(InsertEvidenceSql,
            EvidenceParameters(seed.OrganizationB, seed.FieldB, seed.EvidenceB));
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
         completed_at_utc,duration_ms,failure_code,safe_failure_summary)
        VALUES (@id,@organization,@document,@number,@status,'invoice','en','mock','1','invoice-v1',
                @correlation,@now,@now,@now,1,
                CASE WHEN @status='Failed' THEN 'provider_failure' ELSE NULL END,
                CASE WHEN @status='Failed' THEN 'Safe failure.' ELSE NULL END)
        """;

    private const string InsertFieldSql = """
        INSERT INTO machine_field_values
        (id,organization_id,extraction_run_id,field_key,occurrence_index,value_type,value_text,
         normalized_value_text,confidence,created_at_utc)
        VALUES (@id,@organization,@run,@key,@occurrence,'Decimal','100.00','100.00',0.9000,@now)
        """;

    private const string InsertEvidenceSql = """
        INSERT INTO field_evidence
        (id,organization_id,machine_field_value_id,page_number,quoted_text,character_start,character_end)
        VALUES (@id,@organization,@field,1,'Total 100.00',0,12)
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

    private static (string, object)[] EvidenceParameters(Guid organization, Guid field, Guid id) =>
    [
        ("id", id), ("organization", organization), ("field", field)
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
        Guid FieldA, Guid FieldB, Guid EvidenceA, Guid EvidenceB,
        Guid SessionA, Guid ActionA)
    {
        public static SeedData Create() => new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid());
    }
}
