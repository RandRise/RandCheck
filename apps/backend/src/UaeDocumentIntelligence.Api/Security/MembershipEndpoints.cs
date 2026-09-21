using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Api.Security;

public static class MembershipEndpoints
{
    public static IEndpointRouteBuilder MapMembershipEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/organization/memberships", (EnterpriseMembershipService members, CancellationToken ct) =>
            Execute(async () => Results.Ok(await members.ListAsync(ct))));
        endpoints.MapPost("/api/organization/memberships", (AddMember body, EnterpriseMembershipService members,
            EnterpriseSecurityOptions settings, CancellationToken ct) => Execute(async () =>
        {
            if (body.TenantId != settings.TenantId || !ParseRole(body.Role, out var role)) return BadRequest();
            return Results.Json(await members.AddAsync(body.TenantId, body.ObjectId, body.DisplayName, role, body.CanManageMembers, ct), statusCode: 201);
        }));
        endpoints.MapPut("/api/organization/memberships/{id:guid}", (Guid id, ChangeMember body,
            EnterpriseMembershipService members, CancellationToken ct) => Execute(async () =>
        {
            if (!ParseRole(body.Role, out var role)) return BadRequest();
            return Results.Ok(await members.ChangeAsync(id, body.ExpectedVersion, role, body.CanManageMembers, body.IsActive, ct));
        }));
        endpoints.MapGet("/api/organization/audit", (int? offset, EnterpriseMembershipService members, CancellationToken ct) =>
            Execute(async () => Results.Ok(await members.AuditAsync(offset ?? 0, ct))));
        return endpoints;
    }
    public sealed record AddMember(Guid TenantId, Guid ObjectId, string DisplayName, string Role, bool CanManageMembers);
    public sealed record ChangeMember(Guid ExpectedVersion, string Role, bool CanManageMembers, bool IsActive);
    private static bool ParseRole(string text, out OrganizationAccessRole role) =>
        Enum.TryParse(text, false, out role) && Enum.IsDefined(role) && role.ToString() == text;
    private static IResult BadRequest() => Results.Problem(statusCode: 400, title: "Invalid membership request");
    private static async Task<IResult> Execute(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (MembershipException error) { return Results.Problem(statusCode: error.Status, title: error.Code, extensions: new Dictionary<string, object?> { ["code"] = error.Code }); }
        catch (ArgumentException) { return BadRequest(); }
    }
}
