using System.Net;
using System.Net.Http.Json;
using UaeDocumentIntelligence.Api.Security;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Api.Tests;

public sealed partial class EnterpriseSecurityTests
{
    [Theory]
    [InlineData("{\"role\":\"Staff\"}")]
    [InlineData("{\"role\":\"Viewer\",\"isActive\":true}")]
    [InlineData("{\"role\":\"10\",\"isActive\":true}")]
    [InlineData("{\"role\":\"Admin\",\"isActive\":true,\"canManageMembers\":false}")]
    public async Task Membership_change_rejects_missing_status_and_obsolete_contracts(string body)
    {
        using var factory = Host(new ActorStore { Actor = new(Guid.NewGuid(), Guid.NewGuid(), OrganizationAccessRole.Admin) });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token());
        var payload = System.Text.Json.Nodes.JsonNode.Parse(body)!;
        payload["expectedVersion"] = Guid.NewGuid().ToString();
        using var result = await client.PutAsync($"/api/organization/memberships/{Guid.NewGuid()}",
            new StringContent(payload.ToJsonString(), System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Theory]
    [InlineData("Viewer")]
    [InlineData("Reviewer")]
    [InlineData("Operator")]
    [InlineData("None")]
    [InlineData("staff")]
    [InlineData("10")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Membership_creation_rejects_legacy_numeric_or_missing_roles(string? role)
    {
        using var factory = Host(new ActorStore { Actor = new(Guid.NewGuid(), Guid.NewGuid(), OrganizationAccessRole.Admin) });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token());
        using var result = await client.PostAsJsonAsync("/api/organization/memberships",
            new { tenantId = Tenant, objectId = Guid.NewGuid(), displayName = "Test", role });
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Removed_admin_flag_is_rejected_instead_of_silently_ignored(bool flag)
    {
        using var factory = Host(new ActorStore { Actor = new(Guid.NewGuid(), Guid.NewGuid(), OrganizationAccessRole.Admin) });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token());
        using var result = await client.PostAsJsonAsync("/api/organization/memberships",
            new { tenantId = Tenant, objectId = Guid.NewGuid(), displayName = "Test", role = "Staff", canManageMembers = flag });
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }
}
