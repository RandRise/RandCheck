using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using UaeDocumentIntelligence.Api.Security;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Persistence;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public async Task Phase6_deletion_purges_owned_content_keeps_hold_history_and_replays_without_new_work()
    {
        using var f = await DeletionFixtureAsync(); var source = f.Source;
        // Simulate a terminal local extraction containing every owned content type.
        await ExecuteAsync("UPDATE extraction_runs SET status='Running' WHERE document_id=@id", ("id", source.DocumentId));
        await using (var content = _database.CreateContext())
        {
            var run = await content.ExtractionRuns.SingleAsync();
            run.AddPage(Guid.NewGuid(), 1, "Disposable extraction content", 0);
            var field = run.AddField(Guid.NewGuid(), "supplier_name", 0, FieldValueType.String, "Disposable", 1m, source.Clock.UtcNow);
            run.AddEvidence(field, Guid.NewGuid(), 1, "Disposable", 0, 10);
            run.AddWarning(Guid.NewGuid(), "test_notice", ExtractionWarningSeverity.Info, "Disposable test warning", 1);
            await content.SaveChangesAsync();
        }
        await ExecuteAsync("UPDATE extraction_runs SET status='Succeeded' WHERE document_id=@id", ("id", source.DocumentId));
        source.Clock.UtcNow = source.Clock.UtcNow.AddTicks(1); // Journal/database precision regression.
        var hold = await RetentionHold(source, source.DocumentId);
        Assert.Equal(HttpStatusCode.OK, (await source.Admin.PostAsJsonAsync($"/api/organization/retention/holds/{hold.Id}/release", new RetentionEndpoints.ReleaseHold(hold.Version))).StatusCode);
        var plan = await DeletePlan(f); Assert.Empty(plan.Blockers);
        var response = await Delete(f, plan); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Completed", (await response.Content.ReadFromJsonAsync<DocumentDeletionView>())!.State);
        Assert.False(File.Exists(source.File)); Assert.Single(f.Journal.ReadAll());
        await using var db = _database.CreateContext();
        Assert.Empty(await db.Documents.ToArrayAsync()); Assert.Empty(await db.ExtractionRuns.ToArrayAsync()); Assert.Empty(await db.ExtractionJobs.ToArrayAsync());
        Assert.Empty(await db.ExtractionPages.ToArrayAsync()); Assert.Empty(await db.MachineFieldValues.ToArrayAsync());
        Assert.Empty(await db.FieldEvidence.ToArrayAsync()); Assert.Empty(await db.ExtractionWarnings.ToArrayAsync());
        Assert.NotNull((await db.RetentionHolds.SingleAsync()).ReleasedAtUtc);
        var events = await db.SecurityAuditEvents.Where(x => x.TargetId == source.DocumentId).ToArrayAsync();
        Assert.Contains(events, x => x.Action == "document.database-content-removed"); Assert.Contains(events, x => x.Action == "document.deletion-completed");
        var count = await db.SecurityAuditEvents.CountAsync();
        var businessCount = await db.SecurityAuditEvents.CountAsync(x => x.Action != "request.admitted");
        Assert.Equal(HttpStatusCode.OK, (await Delete(f, plan)).StatusCode);
        Assert.Equal(count + 1, await db.SecurityAuditEvents.CountAsync()); // New HTTP attempt, no repeated deletion.
        Assert.Equal(businessCount, await db.SecurityAuditEvents.CountAsync(x => x.Action != "request.admitted"));
        Assert.Equal(HttpStatusCode.NotFound, (await f.User.GetAsync($"/api/documents/{source.DocumentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.User.PostAsync($"/api/documents/{source.DocumentId}/source-access", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.User.GetAsync("/api/documents")).StatusCode);
        using var scope = f.Host.Services.CreateScope();
        var guard = scope.ServiceProvider.GetRequiredService<DocumentRecoveryGuard>();
        await guard.VerifyAsync(null, true, default); var controls = await guard.ControlSnapshotAsync(default);
        await guard.VerifyControlSnapshotAsync(controls, default);
    }
    [Theory]
    [InlineData("held", "retention_hold_active")]
    [InlineData("young", "retention_window_active")]
    [InlineData("processing", "processing_active")]
    [InlineData("cost", "accounting_dependencies")]
    [InlineData("provider", "accounting_dependencies")]
    [InlineData("review", "historical_dependencies")]
    public async Task Phase6_deletion_preserves_blocked_documents(string scenario, string expected)
    {
        using var f = await DeletionFixtureAsync(scenario != "processing"); var s = f.Source;
        if (scenario == "held") await RetentionHold(s, null);
        if (scenario == "young") s.Clock.UtcNow = s.Clock.UtcNow.AddDays(-3);
        if (scenario == "cost") await ExecuteAsync("UPDATE extraction_runs SET estimated_cost=0,cost_currency='USD' WHERE document_id=@id", ("id", s.DocumentId));
        if (scenario == "provider") await ExecuteAsync("UPDATE extraction_runs SET provider_name='unknown-provider' WHERE document_id=@id", ("id", s.DocumentId));
        if (scenario == "review")
        {
            await ExecuteAsync("UPDATE extraction_runs SET status='Succeeded' WHERE document_id=@id", ("id", s.DocumentId));
            await using var db = _database.CreateContext(); var d = await db.Documents.Include(x => x.ExtractionRuns).SingleAsync();
            d.OpenReview(Guid.NewGuid(), s.Member.ActorId, s.Clock.UtcNow); await db.SaveChangesAsync();
        }
        var plan = await DeletePlan(f); Assert.Contains(expected, plan.Blockers);
        Assert.Equal(HttpStatusCode.Conflict, (await Delete(f, plan)).StatusCode);
        Assert.True(File.Exists(s.File)); Assert.Empty(f.Journal.ReadAll());
        await using var verify = _database.CreateContext(); Assert.Equal(1, await verify.Documents.CountAsync()); Assert.Empty(await verify.DocumentDeletions.ToArrayAsync());
    }
    [Fact]
    public async Task Phase6_deletion_rechecks_new_holds_and_policy_versions_after_preview()
    {
        using var f = await DeletionFixtureAsync(); var plan = await DeletePlan(f); var hold = await RetentionHold(f.Source, null);
        Assert.Equal(HttpStatusCode.Conflict, (await Delete(f, plan)).StatusCode);
        await f.Admin.PostAsJsonAsync($"/api/organization/retention/holds/{hold.Id}/release", new RetentionEndpoints.ReleaseHold(hold.Version));
        await f.Admin.PutAsJsonAsync("/api/organization/retention", new RetentionEndpoints.ChangePolicy(plan.PolicyVersion, 5, 1));
        Assert.Equal(HttpStatusCode.Conflict, (await Delete(f, plan)).StatusCode); Assert.True(File.Exists(f.Source.File));
    }
    [Fact]
    public async Task Phase6_deletion_foreign_ids_and_data_roles_do_not_authorize_purge()
    {
        using var a = await DeletionFixtureAsync(); using var b = await SourceFixtureAsync(); var plan = await DeletePlan(a);
        using var otherAdmin = a.Host.CreateClient(); otherAdmin.DefaultRequestHeaders.Authorization = b.Admin.DefaultRequestHeaders.Authorization;
        Assert.Equal(HttpStatusCode.NotFound, (await otherAdmin.GetAsync(DeletePath(a.Source.DocumentId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherAdmin.PostAsJsonAsync(DeletePath(a.Source.DocumentId), new RetentionEndpoints.DeleteDocument(plan.DocumentVersion, plan.PolicyVersion))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await a.User.PostAsJsonAsync(DeletePath(a.Source.DocumentId), new RetentionEndpoints.DeleteDocument(plan.DocumentVersion, plan.PolicyVersion))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await a.User.GetAsync(DeletePath(a.Source.DocumentId))).StatusCode);
        Assert.True(File.Exists(a.Source.File));
    }
    [Fact]
    public async Task Phase6_deletion_disabled_profile_cannot_remove_content()
    {
        using var s = await SourceFixtureAsync(); var p = await s.Admin.GetFromJsonAsync<DocumentDeletionPreview>(DeletePath(s.DocumentId));
        Assert.Contains("deployment_disabled", p!.Blockers);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await s.Admin.PostAsJsonAsync(DeletePath(s.DocumentId), new RetentionEndpoints.DeleteDocument(p.DocumentVersion, p.PolicyVersion))).StatusCode);
        Assert.True(File.Exists(s.File));
    }
    [Theory]
    [InlineData("document.database-content-removed", true, true)]
    [InlineData("document.source-purge-authorized", false, true)]
    [InlineData("document.deletion-completed", false, false)]
    public async Task Phase6_deletion_audit_failure_is_resumable_without_false_completion(string action, bool databasePresent, bool filePresent)
    {
        using var f = await DeletionFixtureAsync(); var plan = await DeletePlan(f);
        await ExecuteAsync($"""
            CREATE FUNCTION p6_delete_audit_outage() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                IF NEW.action='{action}' THEN RAISE EXCEPTION 'test outage'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER p6_delete_audit_outage BEFORE INSERT ON security_audit_events FOR EACH ROW EXECUTE FUNCTION p6_delete_audit_outage();
            """);
        try
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Delete(f, plan)).StatusCode);
            await using var db = _database.CreateContext(); Assert.Equal(databasePresent, await db.Documents.AnyAsync());
            Assert.Equal(filePresent, File.Exists(f.Source.File)); Assert.Single(f.Journal.ReadAll());
            Assert.False(await db.DocumentDeletions.AnyAsync(x => x.CompletedAtUtc != null));
            Assert.Equal(databasePresent ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK, (await f.User.GetAsync("/api/documents")).StatusCode);
        }
        finally { await ExecuteAsync("DROP TRIGGER p6_delete_audit_outage ON security_audit_events; DROP FUNCTION p6_delete_audit_outage()"); }
        Assert.Equal(HttpStatusCode.OK, (await Delete(f, plan)).StatusCode); Assert.False(File.Exists(f.Source.File));
    }
    [Fact]
    public async Task Phase6_deletion_source_integrity_failure_preserves_file_until_reviewed_retry()
    {
        using var f = await DeletionFixtureAsync(); var plan = await DeletePlan(f);
        await File.WriteAllTextAsync(f.Source.File, "DIFFERENT CONTENT");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Delete(f, plan)).StatusCode); Assert.True(File.Exists(f.Source.File));
        await using var db = _database.CreateContext(); Assert.Empty(await db.Documents.ToArrayAsync());
        Assert.Null((await db.DocumentDeletions.SingleAsync()).CompletedAtUtc);
        await File.WriteAllTextAsync(f.Source.File, SourceFixture.Content);
        Assert.Equal(HttpStatusCode.OK, (await Delete(f, plan)).StatusCode);
    }
    [Fact]
    public async Task Phase6_recovery_rejects_restored_document_file_or_missing_journal_and_preserves_tombstones()
    {
        using var f = await DeletionFixtureAsync(); var plan = await DeletePlan(f);
        await using var db = _database.CreateContext(); var original = await db.Documents.AsNoTracking().SingleAsync();
        Assert.Equal(HttpStatusCode.OK, (await Delete(f, plan)).StatusCode);
        await File.WriteAllTextAsync(f.Source.File, SourceFixture.Content);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await f.User.GetAsync("/api/documents")).StatusCode);
        using (var workerScope = f.Host.Services.CreateScope())
            await Assert.ThrowsAsync<InvalidOperationException>(() => WorkerQueueSession.OpenAsync(workerScope.ServiceProvider, default));
        File.Delete(f.Source.File);
        // Simulate an old restore without modifying the authoritative journal.
        db.Documents.Add(original); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await f.User.GetAsync($"/api/documents/{original.Id}")).StatusCode);
        using var scope = f.Host.Services.CreateScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<DocumentRecoveryGuard>().VerifyAsync(null, true, default));
        foreach (var sql in new[] { "DELETE FROM document_deletions", "TRUNCATE document_deletions", "UPDATE document_deletions SET completed_at_utc=NULL", "UPDATE document_deletions SET storage_key='other'" })
            await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(sql));
        await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20260912173517_AddRetentionControls"));
        File.Delete(Path.Combine(f.Source.Root, "deletion-journal", "journal-id"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await f.User.GetAsync("/api/documents")).StatusCode);
    }
    [Fact]
    public async Task Phase6_recovery_control_snapshot_detects_a_hold_missing_or_released_in_the_restored_database()
    {
        using var f = await DeletionFixtureAsync(); using var scope = f.Host.Services.CreateScope();
        var guard = scope.ServiceProvider.GetRequiredService<DocumentRecoveryGuard>(); var before = await guard.ControlSnapshotAsync(default);
        var hold = await RetentionHold(f.Source, null); var held = await guard.ControlSnapshotAsync(default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => guard.VerifyControlSnapshotAsync(before, default));
        await f.Admin.PostAsJsonAsync($"/api/organization/retention/holds/{hold.Id}/release", new RetentionEndpoints.ReleaseHold(hold.Version));
        await Assert.ThrowsAsync<InvalidOperationException>(() => guard.VerifyControlSnapshotAsync(held, default));
        await guard.VerifyControlSnapshotAsync(await guard.ControlSnapshotAsync(default), default);
    }
    [Fact]
    public async Task Phase6_deletion_concurrent_requests_remove_content_once()
    {
        using var f = await DeletionFixtureAsync(); var plan = await DeletePlan(f);
        var responses = await Task.WhenAll(Delete(f, plan), Delete(f, plan));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        await using var db = _database.CreateContext(); Assert.Equal(1, await db.DocumentDeletions.CountAsync());
        Assert.Equal(1, await db.SecurityAuditEvents.CountAsync(x => x.Action == "document.deletion-completed"));
    }

    [Fact]
    public async Task Phase6_recovery_executable_exports_verifies_and_rejects_restored_files_or_changed_holds()
    {
        using var f = await DeletionFixtureAsync(); var plan = await DeletePlan(f);
        Assert.Equal(HttpStatusCode.OK, (await Delete(f, plan)).StatusCode);
        var dll = Path.Combine(AppContext.BaseDirectory, "EnterpriseRecovery.dll");
        if (!File.Exists(dll))
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs/continuation.md"))) root = root.Parent;
            var configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
            dll = Path.Combine(root!.FullName, "apps/backend/tools/EnterpriseRecovery/bin", configuration, "net8.0/EnterpriseRecovery.dll");
        }
        Assert.True(File.Exists(dll));
        var config = Path.Combine(f.Source.Root, "recovery.json");
        await File.WriteAllTextAsync(config, System.Text.Json.JsonSerializer.Serialize(new
        {
            JournalDirectory = Path.Combine(f.Source.Root, "deletion-journal"), f.Journal.JournalId,
            UploadDirectory = Path.Combine(f.Source.Root, "uploads"), QuarantineDirectory = Path.Combine(f.Source.Root, "quarantine"),
            ExpectedDatabase = new NpgsqlConnectionStringBuilder(_database.TestConnectionString).Database,
            ControlFile = Path.Combine(f.Source.Root, "controls.json")
        }));
        async Task<int> Run(string command)
        {
            var start = new System.Diagnostics.ProcessStartInfo("dotnet")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { dll, command, config }) start.ArgumentList.Add(arg);
            start.Environment["PHASE6_RECOVERY_CONNECTION"] = _database.TestConnectionString;
            using var process = System.Diagnostics.Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            Assert.DoesNotContain(f.Source.Root, await output + await error);
            return process.ExitCode;
        }
        Assert.Equal(0, await Run("export-controls")); Assert.Equal(1, await Run("export-controls"));
        Assert.Equal(0, await Run("verify-restore"));
        await File.WriteAllTextAsync(f.Source.File, SourceFixture.Content);
        Assert.Equal(1, await Run("verify-restore")); Assert.True(File.Exists(f.Source.File));
        File.Delete(f.Source.File);
        await RetentionHold(f.Source, null);
        Assert.Equal(1, await Run("verify-restore"));
    }

    [Fact]
    public async Task Phase6_deletion_pending_source_retry_rechecks_a_new_organization_hold()
    {
        using var f = await DeletionFixtureAsync(); var plan = await DeletePlan(f);
        await File.WriteAllTextAsync(f.Source.File, "Changed source");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Delete(f, plan)).StatusCode);
        var hold = await RetentionHold(f.Source, null);
        await File.WriteAllTextAsync(f.Source.File, SourceFixture.Content);
        Assert.Equal(HttpStatusCode.Conflict, (await Delete(f, plan)).StatusCode); Assert.True(File.Exists(f.Source.File));
        Assert.Equal(HttpStatusCode.OK, (await f.Admin.PostAsJsonAsync($"/api/organization/retention/holds/{hold.Id}/release", new RetentionEndpoints.ReleaseHold(hold.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Delete(f, plan)).StatusCode); Assert.False(File.Exists(f.Source.File));
    }

    private static string DeletePath(Guid document) => $"/api/organization/retention/documents/{document}/deletion";
    private static async Task<DocumentDeletionPreview> DeletePlan(DeletionFixture f)
    {
        var response = await f.Admin.GetAsync(DeletePath(f.Source.DocumentId)); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DocumentDeletionPreview>())!;
    }
    private static Task<HttpResponseMessage> Delete(DeletionFixture f, DocumentDeletionPreview plan) => f.Admin.PostAsJsonAsync(DeletePath(f.Source.DocumentId),
        new RetentionEndpoints.DeleteDocument(plan.DocumentVersion, plan.PolicyVersion));
    private async Task<DeletionFixture> DeletionFixtureAsync(bool terminal = true)
    {
        var source = await SourceFixtureAsync("Staff");
        try
        {
            await RetentionPolicy(source); source.Clock.UtcNow = source.Clock.UtcNow.AddDays(3);
            if (terminal)
            {
                await using var db = _database.CreateContext(); var job = await db.ExtractionJobs.Where(x => x.DocumentId == source.DocumentId).Select(x => x.Id).SingleAsync();
                Assert.Equal(HttpStatusCode.OK, (await source.User.PostAsync($"/api/extraction-jobs/{job}/cancel", null)).StatusCode);
            }
            var journalId = Guid.NewGuid(); var directory = Path.Combine(source.Root, "deletion-journal");
            DocumentDeletionJournal.Initialize(directory, journalId);
            var host = source.Host.WithWebHostBuilder(b => b.UseSetting("DocumentDeletion:Enabled", "true").UseSetting("DocumentDeletion:JournalId", journalId.ToString()).UseSetting("DocumentDeletion:JournalDirectory", directory));
            var admin = host.CreateClient(); admin.DefaultRequestHeaders.Authorization = source.Admin.DefaultRequestHeaders.Authorization;
            var user = host.CreateClient(); user.DefaultRequestHeaders.Authorization = source.User.DefaultRequestHeaders.Authorization;
            var journal = new DocumentDeletionJournal(Options.Create(new DocumentDeletionOptions { Enabled = true, JournalId = journalId, JournalDirectory = directory }),
                Options.Create(new StorageOptions { SecurityMode = "Enforced", UploadDirectory = Path.Combine(source.Root, "uploads"), QuarantineDirectory = Path.Combine(source.Root, "quarantine") }));
            return new(source, host, admin, user, journal);
        }
        catch { source.Dispose(); throw; }
    }
    private sealed record DeletionFixture(SourceFixture Source, WebApplicationFactory<global::Program> Host, HttpClient Admin, HttpClient User, DocumentDeletionJournal Journal) : IDisposable
    { public void Dispose() { Admin.Dispose(); User.Dispose(); Host.Dispose(); Source.Dispose(); } }
}
