using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Abstractions;

// Bound by the authenticated request boundary or the private provisioning tool.
// No default development actor and no document/claim/token payloads.
public sealed class SecurityAuditContext
{
    public Guid? OrganizationId { get; private set; }
    public Guid? ActorId { get; private set; }
    public string ActorKind { get; private set; } = "Anonymous";
    public string RequestId { get; private set; } = "unbound";
    public bool IsBound => OrganizationId.HasValue && ActorId.HasValue;
    public void Bind(Guid organizationId, Guid actorId, string actorKind, string requestId)
    {
        _ = new SecurityAuditEvent(organizationId, actorId, actorKind, "access.bound", "request", null,
            "Succeeded", "authorized", requestId);
        if (IsBound && (OrganizationId != organizationId || ActorId != actorId))
            throw new InvalidOperationException("Audit identity cannot change within a scope.");
        OrganizationId = organizationId; ActorId = actorId; ActorKind = actorKind; RequestId = requestId;
    }
    public SecurityAuditEvent Event(string action, string targetType, Guid? targetId, string outcome,
        string reasonCode, string? requestId = null, string? before = null, string? after = null) =>
        new(OrganizationId, ActorId, ActorKind, action, targetType, targetId, outcome, reasonCode,
            requestId ?? RequestId, before, after);
}

public interface ISecurityAuditSink
{
    Task WriteAsync(SecurityAuditEvent auditEvent, CancellationToken cancellationToken);
}
