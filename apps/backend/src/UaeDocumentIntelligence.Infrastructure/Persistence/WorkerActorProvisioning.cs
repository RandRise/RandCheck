using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

public static class WorkerActorProvisioning
{
    public static async Task<Guid> ProvisionAsync(AppDbContext db, Guid organizationId, string role, CancellationToken ct)
    {
        var subject = WorkerDatabasePolicy.Subject(role);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var organization = await db.Organizations.FromSqlInterpolated($"SELECT * FROM organizations WHERE id={organizationId} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw new InvalidOperationException("Organization does not exist.");
        var existing = await db.Actors.SingleOrDefaultAsync(a => a.OrganizationId == organization.Id && a.ExternalSubject == subject, ct);
        if (existing is not null)
        {
            if (existing.Kind != ActorKind.System || !existing.IsActive)
                throw new InvalidOperationException("Existing service identity cannot be remapped or reactivated by provisioning.");
            await tx.CommitAsync(ct);
            return existing.Id;
        }
        var actor = new Actor(Guid.NewGuid(), organization.Id, ActorKind.System, "Extraction worker", DateTimeOffset.UtcNow, subject);
        db.Actors.Add(actor);
        db.SecurityAuditEvents.Add(new SecurityAuditEvent(organization.Id, actor.Id, "System", "worker.actor-provisioned",
            "actor", actor.Id, "Succeeded", "committed", Guid.NewGuid().ToString("N"), afterState: "System;active=True"));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return actor.Id;
    }
}
