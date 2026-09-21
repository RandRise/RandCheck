using UaeDocumentIntelligence.Domain.Common;

namespace UaeDocumentIntelligence.Domain.Model;

public sealed class SecurityAuditEvent
{
    private SecurityAuditEvent() { }
    public SecurityAuditEvent(Guid? organizationId, Guid? actorId, string actorKind, string action,
        string targetType, Guid? targetId, string outcome, string reasonCode, string requestId,
        string? beforeState = null, string? afterState = null)
    {
        if (organizationId == Guid.Empty || actorId == Guid.Empty || targetId == Guid.Empty ||
            (actorId.HasValue && !organizationId.HasValue)) throw new ArgumentException("Invalid audit identity.");
        if (actorKind is not ("Human" or "System" or "Anonymous") ||
            outcome is not ("Succeeded" or "Denied" or "Failed")) throw new ArgumentException("Invalid audit outcome.");
        Id = Guid.NewGuid(); OrganizationId = organizationId; ActorId = actorId; ActorKind = actorKind;
        Action = Code(action, 80); TargetType = Code(targetType, 64); TargetId = targetId;
        Outcome = outcome; ReasonCode = Code(reasonCode, 64);
        RequestId = Code(requestId, 100); BeforeState = beforeState; AfterState = afterState;
        if (beforeState?.Length > 200 || afterState?.Length > 200) throw new ArgumentException("Audit state is too long.");
    }
    private static string Code(string value, int maximum)
    {
        value = Guard.Required(value, nameof(value), maximum);
        if (!System.Text.RegularExpressions.Regex.IsMatch(value, "^[A-Za-z0-9._:-]+$"))
            throw new ArgumentException("Audit codes must be safe identifiers.");
        return value;
    }
    public Guid Id { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid? ActorId { get; private set; }
    public string ActorKind { get; private set; } = "";
    public string Action { get; private set; } = "";
    public string TargetType { get; private set; } = "";
    public Guid? TargetId { get; private set; }
    public string Outcome { get; private set; } = "";
    public string ReasonCode { get; private set; } = "";
    public string RequestId { get; private set; } = "";
    public string? BeforeState { get; private set; }
    public string? AfterState { get; private set; }
    public DateTimeOffset RecordedAtUtc { get; private set; }
}
