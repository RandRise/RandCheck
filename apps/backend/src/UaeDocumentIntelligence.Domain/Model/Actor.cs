using UaeDocumentIntelligence.Domain.Common;

namespace UaeDocumentIntelligence.Domain.Model;

public sealed class Actor
{
    private Actor() { }

    public Actor(Guid id, Guid organizationId, ActorKind kind, string displayName, DateTimeOffset createdAtUtc,
        string? externalSubject = null)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty) throw new ArgumentException("Actor and organization IDs are required.");
        Id = id;
        OrganizationId = organizationId;
        Kind = kind;
        DisplayName = Guard.Required(displayName, nameof(displayName), 200);
        ExternalSubject = string.IsNullOrWhiteSpace(externalSubject) ? null : Guard.Required(externalSubject, nameof(externalSubject), 200);
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public ActorKind Kind { get; private set; }
    public string? ExternalSubject { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
