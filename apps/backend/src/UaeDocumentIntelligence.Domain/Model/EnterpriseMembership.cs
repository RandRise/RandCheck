namespace UaeDocumentIntelligence.Domain.Model;

public enum OrganizationAccessRole { None, Viewer, Reviewer, Operator }

public sealed class EnterpriseIdentity
{
    private EnterpriseIdentity() { }
    public EnterpriseIdentity(Guid id, Guid tenantId, Guid objectId, DateTimeOffset now)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || objectId == Guid.Empty)
            throw new ArgumentException("Identity IDs are required.");
        Id = id; TenantId = tenantId; ObjectId = objectId; CreatedAtUtc = now;
    }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ObjectId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}

public sealed class OrganizationMembership
{
    private OrganizationMembership() { }
    public OrganizationMembership(Guid id, Guid organizationId, Guid identityId, Guid actorId,
        OrganizationAccessRole role, bool canManageMembers, DateTimeOffset now)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || identityId == Guid.Empty || actorId == Guid.Empty)
            throw new ArgumentException("Membership IDs are required.");
        Id = id; OrganizationId = organizationId; IdentityId = identityId; ActorId = actorId;
        CreatedAtUtc = now;
        ChangeAccess(role, canManageMembers, true, now);
    }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid IdentityId { get; private set; }
    public Guid ActorId { get; private set; }
    public OrganizationAccessRole Role { get; private set; }
    public bool CanManageMembers { get; private set; }
    public bool IsActive { get; private set; }
    public Guid Version { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public void ChangeAccess(OrganizationAccessRole role, bool canManageMembers, bool active, DateTimeOffset now)
    {
        if (!Enum.IsDefined(role)) throw new ArgumentException("Unknown organization role.");
        Role = role; CanManageMembers = canManageMembers; IsActive = active;
        UpdatedAtUtc = now; Version = Guid.NewGuid();
    }
    public string AccessState => $"{Role};manage={CanManageMembers};active={IsActive}";
}
