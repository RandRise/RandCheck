using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Infrastructure.Services;

public static class DevelopmentIdentity
{
    public static readonly Guid OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
}

internal sealed class DevelopmentExecutionContext : IExecutionContext
{
    public Guid OrganizationId => DevelopmentIdentity.OrganizationId;
    public Guid ActorId => DevelopmentIdentity.ActorId;
}

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public static class DevelopmentDataSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (!await db.Organizations.AnyAsync(x => x.Id == DevelopmentIdentity.OrganizationId, cancellationToken))
        {
            var now = DateTimeOffset.UtcNow;
            db.Organizations.Add(new Organization(DevelopmentIdentity.OrganizationId, "local-development",
                "Local Development Organization", now));
            db.Actors.Add(new Actor(DevelopmentIdentity.ActorId, DevelopmentIdentity.OrganizationId,
                ActorKind.System, "Local Development System", now));
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
