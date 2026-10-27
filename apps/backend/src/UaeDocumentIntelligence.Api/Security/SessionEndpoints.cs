using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Api.Security;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/session", async (RequestIdentity identity, EnterpriseSecurityOptions settings,
            AppDbContext db, CancellationToken ct) =>
        {
            if (settings.Mode == "Development") return Results.Ok(new SessionView(identity.OrganizationId,
                "Local development", identity.ActorId, "Development identity", null, "Staff"));
            var session = await (from m in db.OrganizationMemberships.AsNoTracking()
                join a in db.Actors.AsNoTracking() on m.ActorId equals a.Id
                join o in db.Organizations.AsNoTracking() on m.OrganizationId equals o.Id
                join i in db.EnterpriseIdentities.AsNoTracking() on m.IdentityId equals i.Id
                where m.OrganizationId == identity.OrganizationId && m.ActorId == identity.ActorId &&
                    a.OrganizationId == identity.OrganizationId && m.IsActive && a.IsActive
                select new SessionView(o.Id, o.DisplayName, a.Id, a.DisplayName, i.TenantId,
                    m.Role.ToString())).SingleOrDefaultAsync(ct);
            return session is null ? Results.Forbid() : Results.Ok(session);
        });
        return endpoints;
    }
    public sealed record SessionView(Guid OrganizationId, string OrganizationName, Guid ActorId,
        string DisplayName, Guid? TenantId, string Role);
}
