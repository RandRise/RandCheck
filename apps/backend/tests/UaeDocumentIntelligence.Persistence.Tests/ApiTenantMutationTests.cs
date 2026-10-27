using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Api;
using UaeDocumentIntelligence.Application.Documents.Review;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public async Task Phase6_api_tenant_upload_cancel_and_reprocess_preserve_foreign_jobs()
    {
        using var a = await SourceFixtureAsync("Staff"); using var b = await SourceFixtureAsync("Staff");
        await using var role = await ApiRoleAsync(); using var host = role.Host(a.Host);
        using var userA = WithAuthority(host, a.User); using var userB = WithAuthority(host, b.User);
        await using var db = _database.CreateContext();
        var job = await db.ExtractionJobs.AsNoTracking().SingleAsync(j => j.DocumentId == a.DocumentId);
        Assert.Equal(HttpStatusCode.Conflict, (await userB.PostAsync($"/api/extraction-jobs/{job.Id}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await userB.PostAsync($"/api/documents/{a.DocumentId}/reprocess", null)).StatusCode);
        Assert.Equal(job.Status, (await db.ExtractionJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id)).Status);
        Assert.Equal(HttpStatusCode.OK, (await userA.PostAsync($"/api/extraction-jobs/{job.Id}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await userA.PostAsync($"/api/documents/{a.DocumentId}/reprocess", null)).StatusCode);
        using var form = new MultipartFormDataContent(); var content = new StringContent("Disposable restricted upload");
        content.Headers.ContentType = new("text/plain"); form.Add(content, "file", "restricted.txt");
        var created = await SuccessfulJson(await userB.PostAsync("/api/documents", form)); var id = created.GetProperty("id").GetGuid();
        var document = await db.Documents.SingleAsync(d => d.Id == id);
        Assert.Equal(await SourceOrganization(b), document.OrganizationId); Assert.Equal(b.Member.ActorId, document.CreatedByActorId);
        Assert.Equal(HttpStatusCode.NotFound, (await userA.GetAsync($"/api/documents/{id}")).StatusCode);
    }
    private static async Task<HttpResponseMessage> Versioned(HttpClient client, HttpMethod method, string path, string etag, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path); request.Headers.TryAddWithoutValidation("If-Match", etag);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
    private static async Task<JsonElement> SuccessfulJson(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    [Fact]
    public async Task Phase6_api_tenant_review_reconciliation_and_cited_mutations_use_restricted_login()
    {
        var s = await SeedGraphAsync(); var a = await SecurityAttachFixtureHuman(s.OrganizationA); var b = await SecurityAttachFixtureHuman(s.OrganizationB);
        await using var python = await PrivateCitedServer.StartAsync();
        await using var role = await ApiRoleAsync(); using var original = SecurityHost(python.Url); using var host = role.Host(original);
        using var userA = SecurityHttp(host, a); using var userB = SecurityHttp(host, b);
        var reviewAResponse = await userA.GetAsync($"/api/documents/{s.DocumentA}/review");
        var reviewA = (await reviewAResponse.Content.ReadFromJsonAsync<ReviewWorkspaceView>())!;
        var reviewBResponse = await userB.PostAsync($"/api/documents/{s.DocumentB}/reviews", null);
        Assert.Equal(HttpStatusCode.Created, reviewBResponse.StatusCode);
        var reviewB = (await reviewBResponse.Content.ReadFromJsonAsync<ReviewWorkspaceView>())!;
        var actionsBefore = await ScalarAsync<long>("SELECT count(*) FROM review_actions");
        Assert.Equal(HttpStatusCode.NotFound, (await userB.PostAsync($"/api/documents/{s.DocumentA}/reviews", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Versioned(userB, HttpMethod.Post,
            $"/api/documents/{s.DocumentB}/reviews/{s.SessionA}/complete", reviewBResponse.Headers.ETag!.ToString())).StatusCode);
        var foreignField = await Versioned(userB, HttpMethod.Post, $"/api/documents/{s.DocumentB}/reviews/{reviewB.Session!.Id}/actions",
            reviewBResponse.Headers.ETag!.ToString(), new ReviewActionBody("Confirmed", s.FieldA, null, null, null, null, null));
        Assert.Equal(HttpStatusCode.NotFound, foreignField.StatusCode);
        Assert.Equal(actionsBefore, await ScalarAsync<long>("SELECT count(*) FROM review_actions"));
        var ownAction = await Versioned(userB, HttpMethod.Post, $"/api/documents/{s.DocumentB}/reviews/{reviewB.Session.Id}/actions",
            reviewBResponse.Headers.ETag!.ToString(), new ReviewActionBody("Confirmed", s.FieldB, null, null, null, null, null));
        Assert.Equal(HttpStatusCode.OK, ownAction.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Versioned(userB, HttpMethod.Post, $"/api/documents/{s.DocumentB}/reviews/{reviewB.Session.Id}/complete", ownAction.Headers.ETag!.ToString())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Versioned(userA, HttpMethod.Post, $"/api/documents/{s.DocumentA}/reviews/{reviewA.Session!.Id}/complete", reviewAResponse.Headers.ETag!.ToString())).StatusCode);
        var caseAResponse = await userA.PostAsJsonAsync("/api/reconciliation-cases", new { name = "Private tenant A case" });
        var caseA = (await SuccessfulJson(caseAResponse)).GetProperty("id").GetGuid();
        var caseBResponse = await userB.PostAsJsonAsync("/api/reconciliation-cases", new { name = "Tenant B case" });
        var caseB = (await SuccessfulJson(caseBResponse)).GetProperty("id").GetGuid();
        var bodyA = new { documentId = s.DocumentA, reviewSessionId = s.SessionA };
        Assert.Equal(HttpStatusCode.NotFound, (await Versioned(userB, HttpMethod.Put, $"/api/reconciliation-cases/{caseA}/documents/Invoice", caseAResponse.Headers.ETag!.ToString(), bodyA)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Versioned(userB, HttpMethod.Put, $"/api/reconciliation-cases/{caseB}/documents/Invoice", caseBResponse.Headers.ETag!.ToString(), bodyA)).StatusCode);
        var link = await Versioned(userA, HttpMethod.Put, $"/api/reconciliation-cases/{caseA}/documents/Invoice", caseAResponse.Headers.ETag!.ToString(), bodyA);
        Assert.Equal(HttpStatusCode.OK, link.StatusCode);
        // Repeating the same pin is an idempotent read, not an unlink/link mutation.
        var replacement = await Versioned(userA, HttpMethod.Put, $"/api/reconciliation-cases/{caseA}/documents/Invoice", link.Headers.ETag!.ToString(), bodyA);
        Assert.Equal(HttpStatusCode.OK, replacement.StatusCode);
        Assert.Equal(link.Headers.ETag, replacement.Headers.ETag);
        var evaluateBody = new { quantityTolerance = 0m, unitPriceTolerance = 0m, taxTolerance = 0m, lineTotalTolerance = 0m, documentTotalTolerance = 0m };
        Assert.Equal(HttpStatusCode.NotFound, (await Versioned(userB, HttpMethod.Post, $"/api/reconciliation-cases/{caseA}/evaluations", replacement.Headers.ETag!.ToString(), evaluateBody)).StatusCode);
        var evaluation = await Versioned(userA, HttpMethod.Post, $"/api/reconciliation-cases/{caseA}/evaluations", replacement.Headers.ETag!.ToString(), evaluateBody);
        Assert.Equal(HttpStatusCode.OK, evaluation.StatusCode);
        await using var db = _database.CreateContext();
        var run = await db.ReconciliationRuns.SingleAsync(); var finding = await db.ReconciliationFindings.FirstAsync();
        var resolvePath = $"/api/reconciliation-cases/{caseA}/runs/{run.Id}/findings/{finding.Id}/resolutions";
        var resolution = new { action = "Accepted", reason = "Reviewed in a disposable test" };
        Assert.Equal(HttpStatusCode.NotFound, (await Versioned(userB, HttpMethod.Post, resolvePath, evaluation.Headers.ETag!.ToString(), resolution)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Versioned(userA, HttpMethod.Post, resolvePath, evaluation.Headers.ETag!.ToString(), resolution)).StatusCode);
        var snapshotsBefore = await db.ReconciliationDocumentSnapshots.AsNoTracking().Select(x => x.Id).ToArrayAsync();
        var currentCase = await userA.GetAsync($"/api/reconciliation-cases/{caseA}");
        var linkBefore = await db.ReconciliationDocumentLinks.Select(x => x.Id).SingleAsync();
        // A different reviewed invoice exercises the real unlink audit and rollback.
        var nextInvoice = await SeedReplacementInvoiceAsync(s);
        var replacementBody = new { documentId = nextInvoice.DocumentId, reviewSessionId = nextInvoice.ReviewId };
        await ExecuteAsync("""
            CREATE FUNCTION api_unlink_audit_outage() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
              IF NEW.action='reconciliation.document-unlinked' THEN RAISE EXCEPTION 'test outage'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER api_unlink_audit_outage BEFORE INSERT ON security_audit_events FOR EACH ROW EXECUTE FUNCTION api_unlink_audit_outage();
            """);
        try
        {
            Assert.Equal(HttpStatusCode.Conflict, (await Versioned(userA, HttpMethod.Put, $"/api/reconciliation-cases/{caseA}/documents/Invoice", currentCase.Headers.ETag!.ToString(), replacementBody)).StatusCode);
            Assert.Equal(linkBefore, await db.ReconciliationDocumentLinks.Select(x => x.Id).SingleAsync());
        }
        finally { await ExecuteAsync("DROP TRIGGER api_unlink_audit_outage ON security_audit_events; DROP FUNCTION api_unlink_audit_outage()"); }
        Assert.Equal(HttpStatusCode.OK, (await Versioned(userA, HttpMethod.Put, $"/api/reconciliation-cases/{caseA}/documents/Invoice", currentCase.Headers.ETag!.ToString(), replacementBody)).StatusCode);
        Assert.Equal(nextInvoice.DocumentId, await db.ReconciliationDocumentLinks.Select(x => x.DocumentId).SingleAsync());
        Assert.Equal(snapshotsBefore, await db.ReconciliationDocumentSnapshots.AsNoTracking().Select(x => x.Id).ToArrayAsync());
        var linkedB = await Versioned(userB, HttpMethod.Put, $"/api/reconciliation-cases/{caseB}/documents/Invoice", caseBResponse.Headers.ETag!.ToString(),
            new { documentId = s.DocumentB, reviewSessionId = reviewB.Session.Id });
        Assert.Equal(HttpStatusCode.OK, linkedB.StatusCode);
        var evaluatedB = await Versioned(userB, HttpMethod.Post, $"/api/reconciliation-cases/{caseB}/evaluations", linkedB.Headers.ETag!.ToString(), evaluateBody);
        Assert.Equal(HttpStatusCode.OK, evaluatedB.StatusCode);
        var runB = await db.ReconciliationRuns.SingleAsync(r => r.ReconciliationCaseId == caseB);
        var findingB = await db.ReconciliationFindings.FirstAsync(f => f.ReconciliationRunId == runB.Id);
        var latestA = await userA.GetAsync($"/api/reconciliation-cases/{caseA}");
        foreach (var swapped in new[] { $"runs/{runB.Id}/findings/{findingB.Id}", $"runs/{run.Id}/findings/{findingB.Id}" })
            Assert.Equal(HttpStatusCode.NotFound, (await Versioned(userA, HttpMethod.Post, $"/api/reconciliation-cases/{caseA}/{swapped}/resolutions", latestA.Headers.ETag!.ToString(), resolution)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Versioned(userB, HttpMethod.Post, $"/api/reconciliation-cases/{caseB}/runs/{run.Id}/findings/{finding.Id}/resolutions", evaluatedB.Headers.ETag!.ToString(), resolution)).StatusCode);
        Assert.Equal(1, await db.FindingResolutions.CountAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await userB.PostAsync($"/api/cited-cases/{caseA}/sources", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await userA.PostAsync($"/api/cited-cases/{caseA}/sources", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await userB.PostAsJsonAsync("/api/cited-indexes", bodyA)).StatusCode);
        var index = (await SuccessfulJson(await userA.PostAsJsonAsync("/api/cited-indexes", bodyA))).GetProperty("id").GetGuid();
        var question = new { question = "What is the invoice total?", requestKey = "tenant-reviewed", indexIds = new[] { index } };
        var ownerAnswer = await userA.PostAsJsonAsync("/api/cited-questions/reviewed", question); Assert.Equal(HttpStatusCode.OK, ownerAnswer.StatusCode);
        var savedQuestion = (await SuccessfulJson(ownerAnswer)).GetProperty("retrieval").GetProperty("id").GetGuid();
        var peerSubject = await SecurityAttachFixtureHuman(s.OrganizationA); using var peer = SecurityHttp(host, peerSubject);
        Assert.Equal(HttpStatusCode.NotFound, (await peer.GetAsync($"/api/cited-questions/{savedQuestion}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await userB.GetAsync($"/api/cited-questions/{savedQuestion}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await userA.GetAsync($"/api/cited-questions/{savedQuestion}")).StatusCode);
        var foreignAnswer = await userB.PostAsJsonAsync("/api/cited-questions/reviewed", question); Assert.InRange((int)foreignAnswer.StatusCode, 400, 499);
        Assert.Empty(await db.CitedRetrievalRuns.Where(r => r.OrganizationId == s.OrganizationB).ToArrayAsync());
        Assert.Contains(await db.SecurityAuditEvents.ToArrayAsync(), e => e.Action == "reconciliation.document-unlinked" && e.OrganizationId == s.OrganizationA);
    }

    private async Task<(Guid DocumentId, Guid ReviewId)> SeedReplacementInvoiceAsync(SeedData seed)
    {
        var documentId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();

        await ExecuteAsync(InsertDocumentSql,
            DocumentParameters(seed.OrganizationA, seed.ActorA, documentId, "replacement-invoice.pdf", ValidSha));
        await ExecuteAsync(InsertRunSql, RunParameters(seed.OrganizationA, documentId, runId, 1, "replacement-invoice"));
        await ExecuteAsync(InsertPageSql, PageParameters(seed.OrganizationA, runId, Guid.NewGuid()));
        await ExecuteAsync(InsertFieldSql, FieldParameters(seed.OrganizationA, runId, fieldId, "total", 0));
        await ExecuteAsync(InsertEvidenceSql, EvidenceParameters(seed.OrganizationA, runId, fieldId, Guid.NewGuid()));
        await ExecuteAsync(InsertReviewSessionSql, ReviewSessionParameters(seed.OrganizationA, documentId, runId, reviewId, 1));
        await ExecuteAsync(InsertReviewActionSql,
            ReviewActionParameters(seed.OrganizationA, reviewId, runId, fieldId, seed.ActorA, Guid.NewGuid(), "total", 1));
        await ExecuteAsync("UPDATE review_sessions SET status='Completed',completed_at_utc=@now WHERE id=@id",
            ("now", DateTimeOffset.UtcNow), ("id", reviewId));

        return (documentId, reviewId);
    }

    [Theory]
    [InlineData("vector")]
    [InlineData("hybrid")]
    [InlineData("hybrid-nonredundant")]
    public async Task Phase6_api_tenant_semantic_prepared_outcomes_are_actor_and_organization_bound(string mode)
    {
        var g = await SeedCitedGraphAsync(); var subject = await SecurityAttachFixtureHuman(g.Seed.OrganizationA);
        var peer = await SecurityAttachFixtureHuman(g.Seed.OrganizationA); var foreign = await SecurityAttachFixtureHuman(g.Seed.OrganizationB);
        await using var db = _database.CreateContext();
        var actor = await (from m in db.OrganizationMemberships join i in db.EnterpriseIdentities on m.IdentityId equals i.Id where i.ObjectId == subject select m.ActorId).SingleAsync();
        g = g with { Seed = g.Seed with { ActorA = actor } };
        // The synthetic prepared query needs pilot writes; the HTTP host below
        // remains ordinary application registration with generation disabled.
        await using (var seedServices = CitedPilotServices(g.Seed.OrganizationA, actor, "http://127.0.0.1:1"))
            await SemanticSeed(seedServices, g);
        var reservationCount = await db.CitedSpendReservations.CountAsync(); var events = await db.CitedSpendEvents.CountAsync();
        await using var python = await PrivateCitedServer.StartAsync(); await using var role = await ApiRoleAsync();
        using var original = SecurityHost(python.Url); using var host = role.Host(original); using var owner = SecurityHttp(host, subject);
        using var otherActor = SecurityHttp(host, peer); using var otherOrg = SecurityHttp(host, foreign);
        var request = new { question = "total", requestKey = "semantic-tenant", indexIds = new[] { g.Index.Id }, mode, topK = 5 };
        var hitCount = await db.RetrievalHits.CountAsync();
        owner.DefaultRequestHeaders.Add("X-Request-ID", "semantic-audit-failed");
        await ExecuteAsync("""
            CREATE FUNCTION semantic_audit_outage() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                IF NEW.action='question.saved' THEN RAISE EXCEPTION 'test semantic audit outage'; END IF;
                RETURN NEW; END $$;
            CREATE TRIGGER semantic_audit_outage BEFORE INSERT ON security_audit_events
                FOR EACH ROW EXECUTE FUNCTION semantic_audit_outage();
            """);
        try
        {
            var failed = await owner.PostAsJsonAsync("/api/cited-questions/semantic", request);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
            Assert.False(await db.CitedRetrievalRuns.AnyAsync(r => r.RequestKey == "semantic-tenant"));
            Assert.Equal(hitCount, await db.RetrievalHits.CountAsync());
            var attempted = await db.SecurityAuditEvents.Where(e => e.RequestId == "semantic-audit-failed").ToArrayAsync();
            Assert.Contains(attempted, e => e.Action == "request.admitted" && e.ReasonCode == "authorized_question-semantic");
            Assert.DoesNotContain(attempted, e => e.Action == "question.saved");
        }
        finally
        {
            await ExecuteAsync("DROP TRIGGER semantic_audit_outage ON security_audit_events; DROP FUNCTION semantic_audit_outage()");
        }
        owner.DefaultRequestHeaders.Remove("X-Request-ID");
        owner.DefaultRequestHeaders.Add("X-Request-ID", "semantic-audit-committed");
        var first = await owner.PostAsJsonAsync("/api/cited-questions/semantic", request); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var savedRun = await db.CitedRetrievalRuns.SingleAsync(r => r.RequestKey == "semantic-tenant");
        var savedEvents = await db.SecurityAuditEvents.Where(e => e.Action == "question.saved" && e.TargetId == savedRun.Id).ToArrayAsync();
        var retrieved = Assert.Single(savedEvents, e => e.AfterState == "Retrieved");
        Assert.Equal(g.Seed.OrganizationA, retrieved.OrganizationId);
        Assert.Equal(actor, retrieved.ActorId);
        Assert.Equal("Human", retrieved.ActorKind);
        Assert.Equal("semantic-audit-committed", retrieved.RequestId);
        Assert.Null(retrieved.BeforeState);
        Assert.DoesNotContain("total", System.Text.Json.JsonSerializer.Serialize(savedEvents));
        owner.DefaultRequestHeaders.Remove("X-Request-ID");
        owner.DefaultRequestHeaders.Add("X-Request-ID", "semantic-audit-replay");
        var again = await owner.PostAsJsonAsync("/api/cited-questions/semantic", request); Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(savedEvents.Length, await db.SecurityAuditEvents.CountAsync(e => e.Action == "question.saved" && e.TargetId == savedRun.Id));
        Assert.Single(await db.SecurityAuditEvents.Where(e => e.Action == "request.admitted" && e.RequestId == "semantic-audit-replay").ToArrayAsync());
        foreach (var client in new[] { otherActor, otherOrg })
        {
            var denied = await client.PostAsJsonAsync("/api/cited-questions/semantic", request); Assert.InRange((int)denied.StatusCode, 400, 499);
        }
        Assert.Equal(1, await db.CitedRetrievalRuns.CountAsync(r => r.RequestKey == "semantic-tenant"));
        Assert.Equal(reservationCount, await db.CitedSpendReservations.CountAsync()); Assert.Equal(events, await db.CitedSpendEvents.CountAsync());
    }
}
