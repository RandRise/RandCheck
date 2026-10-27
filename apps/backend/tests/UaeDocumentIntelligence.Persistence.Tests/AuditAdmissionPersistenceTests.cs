using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Api.Security;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public async Task Phase6_real_membership_uniqueness_violation_remains_a_conflict()
    {
        var a = await SecurityBootstrap();
        var audit = new SecurityAuditContext();
        await using var db = SecurityDb(audit);
        var slug = await db.Organizations.Where(o => o.Id == a.Organization.OrganizationId).Select(o => o.Slug).SingleAsync();
        var service = new EnterpriseMembershipService(db, new RequestIdentity(), audit);
        var error = await Assert.ThrowsAsync<MembershipException>(() => service.BootstrapAsync(
            slug, "Duplicate organization", SecurityTenant, Guid.NewGuid(), "Duplicate administrator", "duplicate-bootstrap", default));
        Assert.Equal(409, error.Status);
        Assert.Equal("membership_write_conflict", error.Code);
        await using var fresh = _database.CreateContext();
        Assert.Equal(1, await fresh.Organizations.CountAsync());
        Assert.Equal(1, await fresh.OrganizationMemberships.CountAsync());
        Assert.Equal(2, await fresh.SecurityAuditEvents.CountAsync());
    }

    [Fact]
    public async Task Phase6_admitted_binding_failure_persists_under_restricted_login_and_is_tenant_scoped()
    {
        var a = await SecurityBootstrap(); var b = await SecurityBootstrap();
        await using var role = await ApiRoleAsync(); using var original = SecurityHost();
        using var host = role.Host(original);
        using var admin = SecurityHttp(host, a.Subject); using var other = SecurityHttp(host, b.Subject);
        admin.DefaultRequestHeaders.Add("X-Request-ID", "admitted-malformed");
        admin.DefaultRequestHeaders.Add("X-Organization-ID", b.Organization.OrganizationId.ToString());
        using var response = await admin.PostAsync("/api/organization/memberships?private=secret",
            new StringContent("{\"private-body\":", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = _database.CreateContext();
        var item = Assert.Single(await db.SecurityAuditEvents.Where(e => e.RequestId == "admitted-malformed").ToArrayAsync());
        Assert.Equal("request.admitted", item.Action);
        Assert.Equal("authorized_membership-grant", item.ReasonCode);
        Assert.Equal(a.Organization.OrganizationId, item.OrganizationId);
        Assert.NotEqual(default, item.RecordedAtUtc);
        Assert.Equal(2, await db.OrganizationMemberships.CountAsync());
        var own = await admin.GetStringAsync("/api/organization/audit");
        var foreign = await other.GetStringAsync("/api/organization/audit");
        Assert.Contains(item.Id.ToString(), own);
        Assert.DoesNotContain(item.Id.ToString(), foreign);
        Assert.DoesNotContain("private-body", JsonSerializer.Serialize(item));
        Assert.DoesNotContain("secret", JsonSerializer.Serialize(item));

        admin.DefaultRequestHeaders.Remove("X-Request-ID");
        admin.DefaultRequestHeaders.Add("X-Request-ID", "admitted-success");
        await SecurityAdd(admin, Guid.NewGuid());
        var committed = await db.SecurityAuditEvents.Where(e => e.RequestId == "admitted-success").ToArrayAsync();
        Assert.Equal(2, committed.Length);
        Assert.Contains(committed, e => e.Action == "request.admitted");
        Assert.Contains(committed, e => e.Action == "membership.granted");
    }

    [Fact]
    public async Task Phase6_admission_insert_outage_blocks_business_writes_and_allows_retry()
    {
        var a = await SecurityBootstrap();
        using var host = SecurityHost(); using var admin = SecurityHttp(host, a.Subject);
        var subject = Guid.NewGuid();
        await using var db = _database.CreateContext();
        var before = await db.SecurityAuditEvents.LongCountAsync();
        await ExecuteAsync("""
            CREATE FUNCTION admission_test_outage() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                IF NEW.action='request.admitted' THEN RAISE EXCEPTION 'test admission outage'; END IF;
                RETURN NEW; END $$;
            CREATE TRIGGER admission_test_outage BEFORE INSERT ON security_audit_events
                FOR EACH ROW EXECUTE FUNCTION admission_test_outage();
            """);
        try
        {
            using var response = await admin.PostAsJsonAsync("/api/organization/memberships",
                new MembershipEndpoints.AddMember(SecurityTenant, subject, "Not admitted", "Staff"));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.False(await db.EnterpriseIdentities.AnyAsync(i => i.ObjectId == subject));
            Assert.False(await db.Actors.AnyAsync(i => i.DisplayName == "Not admitted"));
            Assert.Equal(1, await db.OrganizationMemberships.CountAsync());
            Assert.Equal(before, await db.SecurityAuditEvents.LongCountAsync());
        }
        finally
        {
            await ExecuteAsync("DROP TRIGGER admission_test_outage ON security_audit_events; DROP FUNCTION admission_test_outage()");
        }
        await SecurityAdd(admin, subject);
    }
}
