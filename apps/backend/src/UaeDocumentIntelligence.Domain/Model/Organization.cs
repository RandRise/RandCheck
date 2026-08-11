using UaeDocumentIntelligence.Domain.Common;

namespace UaeDocumentIntelligence.Domain.Model;

public sealed class Organization
{
    private Organization() { }

    public Organization(Guid id, string slug, string displayName, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Organization ID is required.", nameof(id));
        Id = id;
        Slug = Guard.Required(slug, nameof(slug), 64).ToLowerInvariant();
        DisplayName = Guard.Required(displayName, nameof(displayName), 200);
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
