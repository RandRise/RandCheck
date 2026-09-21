using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

public sealed class MembershipException(string code, int status) : Exception(code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

public sealed record MembershipView(Guid Id, Guid ActorId, Guid TenantId, Guid ObjectId,
    string DisplayName, string Role, bool CanManageMembers, bool IsActive, Guid Version);
public sealed record BootstrapOrganization(Guid OrganizationId, Guid AdministratorActorId, Guid MembershipId);

public sealed class EnterpriseMembershipService(AppDbContext db, IExecutionContext execution, SecurityAuditContext audit)
{
    public async Task<IReadOnlyList<MembershipView>> ListAsync(CancellationToken ct)
    {
        await RequireAdministrator(ct);
        return await (from m in db.OrganizationMemberships.AsNoTracking()
            join i in db.EnterpriseIdentities.AsNoTracking() on m.IdentityId equals i.Id
            join a in db.Actors.AsNoTracking() on new { m.OrganizationId, Id = m.ActorId } equals new { a.OrganizationId, a.Id }
            where m.OrganizationId == execution.OrganizationId
            orderby m.Id
            select new MembershipView(m.Id, a.Id, i.TenantId, i.ObjectId, a.DisplayName, m.Role.ToString(),
                m.CanManageMembers, m.IsActive, m.Version)).Take(500).ToArrayAsync(ct);
    }

    public async Task<MembershipView> AddAsync(Guid tenant, Guid subject, string displayName,
        OrganizationAccessRole role, bool manage, CancellationToken ct)
    {
        Validate(tenant, subject, role);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockOrganization(execution.OrganizationId, ct);
        await RequireAdministrator(ct);
        var identity = await db.EnterpriseIdentities.SingleOrDefaultAsync(x => x.TenantId == tenant && x.ObjectId == subject, ct);
        if (identity is not null && await db.OrganizationMemberships.AnyAsync(x => x.IdentityId == identity.Id &&
            (x.IsActive || x.OrganizationId == execution.OrganizationId), ct))
            throw new MembershipException("membership_already_exists", 409);
        identity ??= new EnterpriseIdentity(Guid.NewGuid(), tenant, subject, DateTimeOffset.UtcNow);
        if (db.Entry(identity).State == EntityState.Detached) db.EnterpriseIdentities.Add(identity);
        var actor = new Actor(Guid.NewGuid(), execution.OrganizationId, ActorKind.Human, displayName,
            DateTimeOffset.UtcNow, $"entra:{tenant:D}:{subject:D}");
        var membership = new OrganizationMembership(Guid.NewGuid(), execution.OrganizationId, identity.Id, actor.Id,
            role, manage, DateTimeOffset.UtcNow);
        db.Actors.Add(actor); db.OrganizationMemberships.Add(membership);
        await Save(ct);
        await transaction.CommitAsync(ct);
        return View(membership, identity, actor);
    }

    public async Task<MembershipView> ChangeAsync(Guid id, Guid expectedVersion, OrganizationAccessRole role,
        bool manage, bool active, CancellationToken ct)
    {
        if (expectedVersion == Guid.Empty || !Enum.IsDefined(role)) throw new MembershipException("invalid_membership_request", 400);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockOrganization(execution.OrganizationId, ct);
        await RequireAdministrator(ct);
        var member = await db.OrganizationMemberships.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == execution.OrganizationId, ct)
            ?? throw new MembershipException("membership_not_found", 404);
        if (member.Version != expectedVersion) throw new MembershipException("membership_version_conflict", 409);
        if (member.IsActive && member.CanManageMembers && (!active || !manage))
        {
            var administrators = await (from m in db.OrganizationMemberships
                join a in db.Actors on m.ActorId equals a.Id
                where m.OrganizationId == execution.OrganizationId && m.IsActive && m.CanManageMembers && a.IsActive && a.Kind == ActorKind.Human
                select m.Id).CountAsync(ct);
            if (administrators <= 1) throw new MembershipException("last_administrator_required", 409);
        }
        if (active && await db.OrganizationMemberships.AnyAsync(x => x.IdentityId == member.IdentityId && x.Id != id && x.IsActive, ct))
            throw new MembershipException("identity_has_active_organization", 409);
        if (member.Role == role && member.CanManageMembers == manage && member.IsActive == active)
            throw new MembershipException("membership_unchanged", 409);
        member.ChangeAccess(role, manage, active, DateTimeOffset.UtcNow);
        await Save(ct);
        await transaction.CommitAsync(ct);
        var identity = await db.EnterpriseIdentities.SingleAsync(x => x.Id == member.IdentityId, ct);
        var actor = await db.Actors.SingleAsync(x => x.Id == member.ActorId && x.OrganizationId == member.OrganizationId, ct);
        return View(member, identity, actor);
    }

    public async Task<IReadOnlyList<SecurityAuditEvent>> AuditAsync(int offset, CancellationToken ct)
    {
        if (offset is < 0 or > 10000) throw new MembershipException("invalid_audit_offset", 400);
        await RequireAdministrator(ct);
        return await db.SecurityAuditEvents.AsNoTracking().Where(x => x.OrganizationId == execution.OrganizationId)
            .OrderByDescending(x => x.RecordedAtUtc).ThenByDescending(x => x.Id).Skip(offset).Take(100).ToArrayAsync(ct);
    }

    // Private operator capability: not registered as an HTTP route. Requires a migrated,
    // operator-controlled database; no automatic migration or existing actor reassignment.
    public async Task<BootstrapOrganization> BootstrapAsync(string slug, string name, Guid tenant, Guid subject,
        string administratorName, string requestId, CancellationToken ct)
    {
        Validate(tenant, subject, OrganizationAccessRole.None);
        if (!db.Database.IsNpgsql()) throw new InvalidOperationException("Enterprise provisioning requires PostgreSQL.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var identity = await db.EnterpriseIdentities.SingleOrDefaultAsync(x => x.TenantId == tenant && x.ObjectId == subject, ct);
        if (identity is not null && await db.OrganizationMemberships.AnyAsync(x => x.IdentityId == identity.Id && x.IsActive, ct))
            throw new MembershipException("identity_has_active_organization", 409);
        var now = DateTimeOffset.UtcNow;
        var organization = new Organization(Guid.NewGuid(), slug, name, now);
        var system = new Actor(Guid.NewGuid(), organization.Id, ActorKind.System, "Enterprise provisioning operator", now);
        var administrator = new Actor(Guid.NewGuid(), organization.Id, ActorKind.Human, administratorName, now, $"entra:{tenant:D}:{subject:D}");
        identity ??= new EnterpriseIdentity(Guid.NewGuid(), tenant, subject, now);
        if (db.Entry(identity).State == EntityState.Detached) db.EnterpriseIdentities.Add(identity);
        var membership = new OrganizationMembership(Guid.NewGuid(), organization.Id, identity.Id, administrator.Id,
            OrganizationAccessRole.None, true, now);
        audit.Bind(organization.Id, system.Id, "System", requestId);
        db.Organizations.Add(organization); db.Actors.AddRange(system, administrator); db.OrganizationMemberships.Add(membership);
        await Save(ct);
        await transaction.CommitAsync(ct);
        return new(organization.Id, administrator.Id, membership.Id);
    }

    private async Task RequireAdministrator(CancellationToken ct)
    {
        if (!audit.IsBound || audit.OrganizationId != execution.OrganizationId || audit.ActorId != execution.ActorId ||
            !await (from m in db.OrganizationMemberships.AsNoTracking()
                join a in db.Actors.AsNoTracking() on m.ActorId equals a.Id
                where m.OrganizationId == execution.OrganizationId && m.ActorId == execution.ActorId &&
                    m.IsActive && m.CanManageMembers && a.IsActive && a.Kind == ActorKind.Human
                select m.Id).AnyAsync(ct)) throw new MembershipException("membership_administration_denied", 403);
    }
    private async Task LockOrganization(Guid id, CancellationToken ct)
    {
        if (!db.Database.IsNpgsql()) throw new InvalidOperationException("Membership administration requires PostgreSQL.");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM organizations WHERE id={id} FOR UPDATE", ct);
    }
    private async Task Save(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new MembershipException("membership_version_conflict", 409); }
        catch (DbUpdateException error) when (error.InnerException is Npgsql.PostgresException
            { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation,
              TableName: "enterprise_identities" or "organization_memberships" or "actors" or "organizations" })
        { throw new MembershipException("membership_write_conflict", 409); }
        // An audit/storage outage is not a membership conflict. Let the API's
        // independent failure boundary report 503 after the transaction rolls back.
    }
    private static void Validate(Guid tenant, Guid subject, OrganizationAccessRole role)
    {
        if (tenant == Guid.Empty || subject == Guid.Empty || !Enum.IsDefined(role)) throw new MembershipException("invalid_membership_request", 400);
    }
    private static MembershipView View(OrganizationMembership m, EnterpriseIdentity i, Actor a) =>
        new(m.Id, a.Id, i.TenantId, i.ObjectId, a.DisplayName, m.Role.ToString(), m.CanManageMembers, m.IsActive, m.Version);
}
