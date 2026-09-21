using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.Documents.Upload;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Persistence;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Theory]
    [InlineData("clean", HttpStatusCode.Accepted)]
    [InlineData("infected", HttpStatusCode.UnprocessableEntity)]
    [InlineData("unavailable", HttpStatusCode.ServiceUnavailable)]
    [InlineData("audit-outage", HttpStatusCode.ServiceUnavailable)]
    public async Task Phase6_storage_admission_commits_scan_document_job_and_audit_together(string outcome, HttpStatusCode expected)
    {
        var root = Path.Combine(Path.GetTempPath(), "p6-admission-" + Guid.NewGuid().ToString("N"));
        var uploads = Path.Combine(root, "uploads"); var quarantine = Path.Combine(root, "quarantine");
        var scanner = new AdmissionScanner(outcome);
        try
        {
            var a = await SecurityBootstrap();
            using var host = SecurityHost().WithWebHostBuilder(b => b
                .UseSetting("Storage:SecurityMode", "Enforced")
                .UseSetting("Storage:UploadDirectory", uploads)
                .UseSetting("Storage:QuarantineDirectory", quarantine)
                .ConfigureServices(s => { s.RemoveAll<IMalwareScanner>(); s.AddSingleton<IMalwareScanner>(scanner); }));
            using var admin = SecurityHttp(host, a.Subject);
            var subject = Guid.NewGuid(); var member = await SecurityAdd(admin, subject, "Operator");
            using var user = SecurityHttp(host, subject);
            if (outcome == "audit-outage") await ExecuteAsync("""
                CREATE FUNCTION phase6_reject_scan_audit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                    IF NEW.action='document.scan-clean' THEN RAISE EXCEPTION 'test-only scan audit outage'; END IF;
                    RETURN NEW; END $$;
                CREATE TRIGGER phase6_scan_outage BEFORE INSERT ON security_audit_events
                    FOR EACH ROW EXECUTE FUNCTION phase6_reject_scan_audit();
                """);
            try
            {
                using var form = new MultipartFormDataContent();
                var content = new StringContent("PRIVATE SOURCE CONTENT"); content.Headers.ContentType = new("text/plain");
                form.Add(content, "file", "PRIVATE FILENAME.txt");
                var response = await user.PostAsync("/api/documents", form);
                Assert.Equal(expected, response.StatusCode);
                Assert.Equal(1, scanner.Calls);
                await using var db = _database.CreateContext();
                var documents = await db.Documents.ToArrayAsync();
                var jobs = await db.ExtractionJobs.ToArrayAsync();
                var events = await db.SecurityAuditEvents.Where(e => e.ActorId == member.ActorId).ToArrayAsync();
                Assert.DoesNotContain("PRIVATE", JsonSerializer.Serialize(events));
                Assert.Empty(Directory.GetFiles(quarantine));
                if (outcome == "clean")
                {
                    var document = Assert.Single(documents); Assert.Single(jobs);
                    var scan = Assert.Single(events.Where(e => e.Action == "document.scan-clean"));
                    Assert.Equal(document.OrganizationId, scan.OrganizationId);
                    Assert.Equal(document.Id, scan.TargetId);
                    Assert.Equal($"sha256={document.Sha256};bytes={document.SizeBytes}", scan.AfterState);
                    Assert.Single(Directory.GetFiles(uploads, "*", SearchOption.AllDirectories));
                }
                else
                {
                    Assert.Empty(documents); Assert.Empty(jobs);
                    Assert.DoesNotContain(events, e => e.Action == "document.scan-clean");
                    Assert.Contains(events, e => e.Action == "request.failed");
                    Assert.Empty(Directory.GetFiles(uploads, "*", SearchOption.AllDirectories));
                    Assert.DoesNotContain("PRIVATE", await response.Content.ReadAsStringAsync());
                }
                Assert.Equal(0, await db.CitedSpendEvents.CountAsync());
            }
            finally
            {
                if (outcome == "audit-outage") await ExecuteAsync("DROP TRIGGER phase6_scan_outage ON security_audit_events; DROP FUNCTION phase6_reject_scan_audit()");
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("malware_scan_unavailable", true, 3)]
    [InlineData("malware_detected", true, 1)]
    [InlineData("storage_integrity_failed", true, 1)]
    public async Task Phase6_storage_worker_failure_is_bounded_and_audited(string code, bool retryable, int attempts)
    {
        var role = await CreateWorkerRoleAsync();
        try
        {
            using var seeds = CreateQueueServices(); var seed = await SeedQueuedUploadAsync(seeds);
            await using (var db = _database.CreateContext()) await WorkerActorProvisioning.ProvisionAsync(db, seed.OrganizationId, role, default);
            using var services = WorkerServices(role); using var scope = services.CreateScope();
            await using var session = await WorkerQueueSession.OpenAsync(scope.ServiceProvider, default);
            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                var claim = Assert.IsType<ClaimedExtractionJob>(await session.Queue.ClaimAsync("storage-test", default));
                Assert.Equal(attempt, claim.Attempt);
                Assert.True(await session.Queue.RecordFailureAsync(claim, code, "PRIVATE daemon response", retryable, default));
                if (attempt < attempts) await ExecuteAsync("UPDATE extraction_jobs SET available_at_utc=clock_timestamp() WHERE id=@id", ("id", seed.JobId));
            }
            Assert.Equal(ExtractionJobStatus.Failed, (await ReadJobAsync(seed.JobId)).Status);
            await using var check = _database.CreateContext();
            var events = await check.SecurityAuditEvents.Where(e => e.Action == "worker.failed" || e.Action == "worker.retry-scheduled").ToArrayAsync();
            Assert.Equal(attempts, events.Length);
            Assert.All(events, e => { Assert.Equal(code, e.ReasonCode); Assert.Equal("System", e.ActorKind); });
            Assert.DoesNotContain("PRIVATE", JsonSerializer.Serialize(events));
        }
        finally { await DropWorkerRoleAsync(role); }
    }

    private sealed class AdmissionScanner(string outcome) : IMalwareScanner
    {
        public int Calls { get; private set; }
        public Task ScanAsync(Stream content, long length, CancellationToken ct)
        {
            Calls++;
            if (outcome == "infected") throw new MalwareDetectedException();
            if (outcome == "unavailable") throw new MalwareScanUnavailableException();
            return Task.CompletedTask;
        }
    }
}
