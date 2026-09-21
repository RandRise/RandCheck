namespace UaeDocumentIntelligence.Domain.Model;

public sealed class OrganizationRetentionPolicy
{
    private OrganizationRetentionPolicy() { }
    public OrganizationRetentionPolicy(Guid organizationId, int? documentDays, int? quarantineHours, DateTimeOffset now)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.");
        OrganizationId = organizationId;
        Change(documentDays, quarantineHours, now);
    }
    public Guid OrganizationId { get; private set; }
    public int? DocumentDays { get; private set; }
    public int? QuarantineHours { get; private set; }
    public Guid Version { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public void Change(int? documentDays, int? quarantineHours, DateTimeOffset now)
    {
        if (documentDays is < 1 or > 36500 || quarantineHours is < 1 or > 87600)
            throw new ArgumentOutOfRangeException(nameof(documentDays));
        DocumentDays = documentDays; QuarantineHours = quarantineHours;
        Version = Guid.NewGuid(); UpdatedAtUtc = now;
    }
}

public enum RetentionHoldReason { Investigation, LegalHold, AdministrativeReview }

public sealed class RetentionHold
{
    private RetentionHold() { }
    public RetentionHold(Guid id, Guid organizationId, Guid? documentId, RetentionHoldReason reason, DateTimeOffset now)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || documentId == Guid.Empty || !Enum.IsDefined(reason))
            throw new ArgumentException("Invalid hold.");
        Id = id; OrganizationId = organizationId; DocumentId = documentId; Reason = reason;
        CreatedAtUtc = now; Version = Guid.NewGuid();
    }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid? DocumentId { get; private set; }
    public RetentionHoldReason Reason { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ReleasedAtUtc { get; private set; }
    public Guid Version { get; private set; }
    public void Release(DateTimeOffset now)
    {
        if (ReleasedAtUtc.HasValue || now < CreatedAtUtc) throw new InvalidOperationException("Hold cannot be released.");
        ReleasedAtUtc = now; Version = Guid.NewGuid();
    }
}
