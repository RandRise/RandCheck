using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Api;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.Documents.Processing;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public async Task Document_repeat_upload_concurrency_and_terminal_replay_commit_one_document_run_job_and_business_audit()
    {
        var tenant = await SeedTenantAsync();
        using var services = CreateQueueServices();
        var intent = new ProcessingIntent(tenant.ActorId, "same-upload", ValidSha);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 6).Select(async _ => {
            await using var scope = services.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<SecurityAuditContext>().Bind(tenant.OrganizationId, tenant.ActorId, "System", "repeat-upload");
            var candidate = CreateQueuedDocument(tenant, Guid.NewGuid(), DateTimeOffset.UtcNow);
            await gate.Task;
            return await scope.ServiceProvider.GetRequiredService<IUploadQueueStore>().AddWithIntentAsync(candidate,
                candidate.ExtractionRuns.Single().Id, Guid.NewGuid(), null, intent, default);
        }).ToArray();
        gate.SetResult();
        var results = await Task.WhenAll(tasks);
        var documentId = Assert.Single(results.Select(x => x.Id).Distinct());
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM documents"));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM extraction_runs"));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM extraction_jobs"));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM document_processing_requests"));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM security_audit_events WHERE action='document.uploaded'"));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM security_audit_events WHERE action='processing.queued'"));
        await using (var scope = services.CreateAsyncScope())
        {
            var queue = scope.ServiceProvider.GetRequiredService<IExtractionJobQueue>();
            var claim = Assert.IsType<ClaimedExtractionJob>(await queue.ClaimAsync("repeat-worker", default));
            Assert.True(await queue.CompleteAsync(claim, CreateReadyResult(claim), default));
        }
        using var restarted = CreateQueueServices();
        await using var later = restarted.CreateAsyncScope();
        var store = later.ServiceProvider.GetRequiredService<IUploadQueueStore>();
        var replay = await store.FindUploadAsync(tenant.OrganizationId, intent, default);
        Assert.NotNull(replay); Assert.Equal(documentId, replay.Id);
        Assert.Equal(DocumentProcessingStatus.ReadyForReview, replay.ProcessingStatus);
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM extraction_runs"));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM extraction_jobs"));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM extraction_pages"));
        // Synthetic fixture-only deletion honors the database's required deletion
        // receipt and models removed source data. Retry receipts must survive it.
        await using (var removal = _database.CreateContext())
        {
            var original = await removal.Documents.SingleAsync(x => x.Id == documentId);
            removal.DocumentDeletions.Add(new(Guid.NewGuid(), tenant.OrganizationId, documentId,
                original.StorageKey, original.Sha256, original.SizeBytes, original.ConcurrencyToken,
                Guid.NewGuid(), DateTimeOffset.UtcNow));
            await removal.SaveChangesAsync();
        }
        await ExecuteAsync("DELETE FROM extraction_jobs WHERE document_id=@id", ("id", documentId));
        await ExecuteAsync("DELETE FROM extraction_runs WHERE document_id=@id", ("id", documentId));
        await ExecuteAsync("DELETE FROM documents WHERE id=@id", ("id", documentId));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM document_processing_requests"));
        var unavailable = await Assert.ThrowsAsync<ProcessingIntentException>(() => store.FindUploadAsync(tenant.OrganizationId, intent, default));
        Assert.Equal("operation_source_unavailable", unavailable.Code);
        var recreate = await Assert.ThrowsAsync<ProcessingIntentException>(() => RepeatInsertAsync(restarted, tenant, intent));
        Assert.Equal("operation_source_unavailable", recreate.Code);
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM documents"));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM extraction_jobs"));
    }

    [Fact]
    public async Task Document_repeat_upload_payload_conflict_and_actor_tenant_scope_preserve_original()
    {
        var tenant = await SeedTenantAsync(); var foreign = await SeedTenantAsync();
        var peer = Guid.NewGuid();
        await ExecuteAsync("INSERT INTO actors(id,organization_id,kind,display_name,is_active,created_at_utc) VALUES(@id,@org,'System','Peer',true,now())",
            ("id", peer), ("org", tenant.OrganizationId));
        using var services = CreateQueueServices();
        var original = await RepeatInsertAsync(services, tenant, new(tenant.ActorId, "shared-key", ValidSha));
        await using (var scope = services.CreateAsyncScope())
        {
            var conflict = await Assert.ThrowsAsync<ProcessingIntentException>(() => scope.ServiceProvider.GetRequiredService<IUploadQueueStore>()
                .FindUploadAsync(tenant.OrganizationId, new(tenant.ActorId, "shared-key", new string('a', 64)), default));
            Assert.Equal("operation_payload_changed", conflict.Code);
        }
        var peerUpload = await RepeatInsertAsync(services, new(tenant.OrganizationId, peer), new(peer, "shared-key", ValidSha));
        var foreignUpload = await RepeatInsertAsync(services, foreign, new(foreign.ActorId, "shared-key", ValidSha));
        Assert.Equal(3, new[] { original.Id, peerUpload.Id, foreignUpload.Id }.Distinct().Count());
        Assert.Equal(3, await ScalarAsync<long>("SELECT count(*) FROM document_processing_requests"));
        Assert.Equal(3, await ScalarAsync<long>("SELECT count(*) FROM documents"));
    }

    [Fact]
    public async Task Document_repeat_upload_failed_transaction_does_not_consume_intent_or_leave_document_run_job()
    {
        var tenant = await SeedTenantAsync(); using var services = CreateQueueServices();
        var intent = new ProcessingIntent(tenant.ActorId, "failed-upload", ValidSha);
        var failed = CreateQueuedDocument(tenant, Guid.NewGuid(), DateTimeOffset.UtcNow);
        await using (var scope = services.CreateAsyncScope())
            await Assert.ThrowsAsync<DbUpdateException>(() => scope.ServiceProvider.GetRequiredService<IUploadQueueStore>()
                .AddWithIntentAsync(failed, Guid.NewGuid(), Guid.NewGuid(), null, intent, default));
        foreach (var table in new[] { "documents", "extraction_runs", "extraction_jobs", "document_processing_requests" })
            Assert.Equal(0, await ScalarAsync<long>($"SELECT count(*) FROM {table}"));
        var recovered = await RepeatInsertAsync(services, tenant, intent);
        Assert.NotEqual(failed.Id, recovered.Id);
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM document_processing_requests"));
    }

    [Fact]
    public async Task Document_repeat_reprocess_terminal_replay_and_changed_payload_cannot_enqueue_extra_run()
    {
        using var services = CreateQueueServices(); var initial = await SeedQueuedUploadAsync(services);
        await using (var scope = services.CreateAsyncScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<IExtractionJobQueue>().CancelAsync(initial.OrganizationId,
                initial.JobId, "request_cancelled", "Disposable test", default));
        var intent = new ProcessingIntent(initial.ActorId, "reprocess-intent", ValidSha);
        async Task<QueuedReprocess?> Reprocess(ProcessingIntent key)
        {
            using var provider = CreateQueueServices(); await using var scope = provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IExtractionJobQueue>().QueueReprocessWithIntentAsync(initial.OrganizationId,
                initial.DocumentId, Guid.NewGuid(), Guid.NewGuid(), "local", "local-1.0.0", "2.0", "repeat-reprocess", key, default);
        }
        var queued = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Reprocess(intent)));
        Assert.All(queued, x => Assert.NotNull(x));
        var same = Assert.Single(queued.Select(x => x!.JobId).Distinct());
        Assert.Equal(2, queued[0]!.RunNumber);
        await using (var scope = services.CreateAsyncScope())
        {
            var queue = scope.ServiceProvider.GetRequiredService<IExtractionJobQueue>();
            var claim = Assert.IsType<ClaimedExtractionJob>(await queue.ClaimAsync("terminal-reprocess", default));
            Assert.Equal(same, claim.JobId);
            Assert.True(await queue.CompleteAsync(claim, CreateReadyResult(claim), default));
        }
        var terminalReplay = Assert.IsType<QueuedReprocess>(await Reprocess(intent));
        Assert.Equal(queued[0]!.JobId, terminalReplay.JobId);
        Assert.Equal(queued[0]!.ExtractionRunId, terminalReplay.ExtractionRunId);
        Assert.Equal(queued[0]!.RunNumber, terminalReplay.RunNumber);
        Assert.Equal("Ready", terminalReplay.Status); Assert.True(terminalReplay.Replayed);
        var conflict = await Assert.ThrowsAsync<ProcessingIntentException>(() => Reprocess(intent with { PayloadHash = new string('a', 64) }));
        Assert.Equal("operation_payload_changed", conflict.Code);
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM extraction_runs"));
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM extraction_jobs"));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM document_processing_requests"));
    }

    [Fact]
    public async Task Document_repeat_api_upload_replay_uses_current_permissions_and_saves_scans_once()
    {
        using var fixture = await SourceFixtureAsync("Staff");
        await using var role = await ApiRoleAsync(); using var host = role.Host(fixture.Host);
        using var owner = WithAuthority(host, fixture.User);
        async Task<HttpResponseMessage> Upload(string content = "Repeat source", string mime = "text/plain", string filename = "repeat.txt")
        {
            using var form = new MultipartFormDataContent(); var bytes = new StringContent(content);
            bytes.Headers.ContentType = new(mime); form.Add(bytes, "file", filename);
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/documents") { Content = form };
            request.Headers.Add("X-Operation-Key", "api-upload-intent");
            return await owner.SendAsync(request);
        }
        var first = await Upload(); Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var id = (await SuccessfulJson(first)).GetProperty("id").GetGuid();
        Assert.Equal(1, fixture.Scanner.Calls);
        var files = Directory.GetFiles(Path.Combine(fixture.Root, "uploads"), "*", SearchOption.AllDirectories).Length;
        var replay = await Upload(); Assert.Equal(HttpStatusCode.Accepted, replay.StatusCode);
        Assert.Equal(id, (await SuccessfulJson(replay)).GetProperty("id").GetGuid());
        Assert.Equal(1, fixture.Scanner.Calls);
        Assert.Equal(files, Directory.GetFiles(Path.Combine(fixture.Root, "uploads"), "*", SearchOption.AllDirectories).Length);
        foreach (var changed in new[] { await Upload("Different source"), await Upload(mime: "application/pdf"), await Upload(filename: "different.txt") })
            Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        var member = fixture.Member;
        foreach (var access in new[] { ("Admin", true, true), ("Staff", false, true), ("Staff", false, false), ("Admin", true, false) })
        {
            var update = await SecurityChange(fixture.Admin, member, access.Item1, access.Item2, access.Item3);
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
            member = (await update.Content.ReadFromJsonAsync<MembershipView>())!;
            var response = await Upload();
            Assert.Equal(access.Item3 ? HttpStatusCode.Accepted : HttpStatusCode.Forbidden, response.StatusCode);
        }
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM document_processing_requests"));
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM documents"));
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM extraction_jobs"));
        await using var db = role.Db(); await ApiDatabasePolicy.VerifyAsync(db, role.Role, true, default);
    }

    [Fact]
    public async Task Document_repeat_api_reprocess_terminal_replay_keeps_original_run_and_checks_current_role()
    {
        using var fixture = await SourceFixtureAsync("Staff");
        await using var db = _database.CreateContext();
        var initialJob = await db.ExtractionJobs.SingleAsync(x => x.DocumentId == fixture.DocumentId);
        Assert.Equal(HttpStatusCode.OK, (await fixture.User.PostAsync($"/api/extraction-jobs/{initialJob.Id}/cancel", null)).StatusCode);
        await using var role = await ApiRoleAsync(); using var host = role.Host(fixture.Host);
        using var user = WithAuthority(host, fixture.User);
        async Task<HttpResponseMessage> Reprocess()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/documents/{fixture.DocumentId}/reprocess");
            request.Headers.Add("X-Operation-Key", "api-reprocess-intent");
            return await user.SendAsync(request);
        }
        var first = await Reprocess(); Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var saved = await SuccessfulJson(first);
        var jobId = saved.GetProperty("jobId").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await user.PostAsync($"/api/extraction-jobs/{jobId}/cancel", null)).StatusCode);
        var replay = await Reprocess(); Assert.Equal(HttpStatusCode.Accepted, replay.StatusCode);
        var terminalReplay = await SuccessfulJson(replay);
        Assert.Equal(jobId, terminalReplay.GetProperty("jobId").GetGuid());
        Assert.Equal("Cancelled", terminalReplay.GetProperty("status").GetString());
        Assert.True(terminalReplay.GetProperty("replayed").GetBoolean());
        var member = fixture.Member;
        foreach (var access in new[] { ("Admin", true, true), ("Staff", false, true), ("Staff", false, false), ("Admin", true, false) })
        {
            var update = await SecurityChange(fixture.Admin, member, access.Item1, access.Item2, access.Item3);
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
            member = (await update.Content.ReadFromJsonAsync<MembershipView>())!;
            var response = await Reprocess();
            Assert.Equal(access.Item3 ? HttpStatusCode.Accepted : HttpStatusCode.Forbidden, response.StatusCode);
        }
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM extraction_runs"));
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM extraction_jobs"));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM document_processing_requests"));
    }

    [Fact]
    public async Task Document_repeat_api_concurrent_uploads_at_capacity_admit_one_source_and_compensate_racing_files()
    {
        using var fixture = await SourceFixtureAsync("Staff"); var org = await SourceOrganization(fixture);
        await SetCapacity(org, 2, SourceFixture.Content.Length + "Racing source".Length);
        var arrived = 0; var scanned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Scanner.AfterScan = async () => { if (Interlocked.Increment(ref arrived) == 2) scanned.TrySetResult(); await scanned.Task.WaitAsync(TimeSpan.FromSeconds(20)); };
        await using var role = await ApiRoleAsync(); using var hostA = role.Host(fixture.Host); using var hostB = role.Host(fixture.Host);
        using var a = WithAuthority(hostA, fixture.User); using var b = WithAuthority(hostB, fixture.User);
        static async Task<HttpResponseMessage> Upload(HttpClient user, string key)
        {
            using var form = new MultipartFormDataContent(); var content = new StringContent("Racing source");
            content.Headers.ContentType = new("text/plain"); form.Add(content, "file", "racing.txt");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/documents") { Content = form };
            request.Headers.Add("X-Operation-Key", key); return await user.SendAsync(request);
        }
        var results = await Task.WhenAll(Upload(a, "racing-intent"), Upload(b, "racing-intent"));
        Assert.All(results, x => Assert.Equal(HttpStatusCode.Accepted, x.StatusCode)); Assert.Equal(2, arrived);
        var ids = new List<Guid>(); foreach (var result in results) ids.Add((await SuccessfulJson(result)).GetProperty("id").GetGuid());
        Assert.Single(ids.Distinct()); fixture.Scanner.AfterScan = null;
        // Retry at full quota succeeds, while a deliberate new operation remains denied.
        Assert.Equal(HttpStatusCode.Accepted, (await Upload(a, "racing-intent")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Upload(a, "new-intent")).StatusCode);
        Assert.Equal(2, Directory.GetFiles(Path.Combine(fixture.Root, "uploads"), "*", SearchOption.AllDirectories).Length);
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM documents"));
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM extraction_jobs"));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM document_processing_requests"));
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM security_audit_events WHERE action='document.uploaded'"));
    }

    [Fact]
    public async Task Document_repeat_api_invalid_operation_keys_fail_before_scanning_or_business_mutation()
    {
        using var fixture = await SourceFixtureAsync("Staff");
        await using var role = await ApiRoleAsync(); using var host = role.Host(fixture.Host);
        using var user = WithAuthority(host, fixture.User);
        // HttpClient omits an empty/whitespace-only header; that exercises the
        // intentionally supported legacy no-key path, not a malformed key.
        foreach (var value in new[] { "changed key", new string('a', 101), "invalid,second", "مفتاح" })
        {
            using var form = new MultipartFormDataContent(); var content = new StringContent("Header source");
            content.Headers.ContentType = new("text/plain"); form.Add(content, "file", "header.txt");
            using var upload = new HttpRequestMessage(HttpMethod.Post, "/api/documents") { Content = form };
            upload.Headers.TryAddWithoutValidation("X-Operation-Key", value);
            var rejected = await user.SendAsync(upload); Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            Assert.Contains("invalid_operation_key", await rejected.Content.ReadAsStringAsync());
            using var reprocess = new HttpRequestMessage(HttpMethod.Post, $"/api/documents/{fixture.DocumentId}/reprocess");
            reprocess.Headers.TryAddWithoutValidation("X-Operation-Key", value);
            rejected = await user.SendAsync(reprocess); Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            Assert.Contains("invalid_operation_key", await rejected.Content.ReadAsStringAsync());
        }
        Assert.Equal(0, fixture.Scanner.Calls);
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM document_processing_requests"));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM documents"));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM extraction_jobs"));
    }

    [Fact]
    public async Task Document_repeat_api_failed_scan_capacity_and_business_audit_leave_intent_available_and_compensate_files()
    {
        using var fixture = await SourceFixtureAsync("Staff");
        var organization = await SourceOrganization(fixture);
        await using var role = await ApiRoleAsync(); using var host = role.Host(fixture.Host);
        using var owner = WithAuthority(host, fixture.User);
        async Task<HttpResponseMessage> Upload()
        {
            using var form = new MultipartFormDataContent(); var content = new StringContent("Recovery source");
            content.Headers.ContentType = new("text/plain"); form.Add(content, "file", "recovery.txt");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/documents") { Content = form };
            request.Headers.Add("X-Operation-Key", "failed-admission-intent");
            return await owner.SendAsync(request);
        }
        async Task CheckRollback()
        {
            Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM document_processing_requests"));
            Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM documents"));
            Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM extraction_jobs"));
            Assert.Single(Directory.GetFiles(Path.Combine(fixture.Root, "uploads"), "*", SearchOption.AllDirectories));
        }
        fixture.Scanner.Outcome = "unavailable";
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Upload()).StatusCode);
        await CheckRollback(); fixture.Scanner.Outcome = "clean";
        var capacity = await SetCapacity(organization, 1, 100000);
        Assert.Equal(HttpStatusCode.Conflict, (await Upload()).StatusCode);
        await CheckRollback(); await SetCapacity(organization, 10, 100000, capacity.PolicyVersion);
        await ExecuteAsync("""
            CREATE FUNCTION repeat_upload_audit_outage() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
              IF NEW.action='document.uploaded' THEN RAISE EXCEPTION 'disposable outage'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER repeat_upload_audit_outage BEFORE INSERT ON security_audit_events FOR EACH ROW EXECUTE FUNCTION repeat_upload_audit_outage();
            """);
        try { Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Upload()).StatusCode); await CheckRollback(); }
        finally { await ExecuteAsync("DROP TRIGGER repeat_upload_audit_outage ON security_audit_events; DROP FUNCTION repeat_upload_audit_outage()"); }
        Assert.Equal(HttpStatusCode.Accepted, (await Upload()).StatusCode);
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM document_processing_requests"));
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM documents"));
        Assert.Equal(2, Directory.GetFiles(Path.Combine(fixture.Root, "uploads"), "*", SearchOption.AllDirectories).Length);
    }

    private static async Task<Document> RepeatInsertAsync(ServiceProvider services, TenantSeed tenant, ProcessingIntent intent)
    {
        await using var scope = services.CreateAsyncScope();
        var document = CreateQueuedDocument(tenant, Guid.NewGuid(), DateTimeOffset.UtcNow);
        return await scope.ServiceProvider.GetRequiredService<IUploadQueueStore>().AddWithIntentAsync(document,
            document.ExtractionRuns.Single().Id, Guid.NewGuid(), null, intent, default);
    }
}
