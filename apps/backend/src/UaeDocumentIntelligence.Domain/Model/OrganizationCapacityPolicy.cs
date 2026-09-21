namespace UaeDocumentIntelligence.Domain.Model;

public sealed class OrganizationCapacityPolicy
{
    private OrganizationCapacityPolicy() { }
    public OrganizationCapacityPolicy(Guid organizationId, int maxActiveJobs, long maxStoredBytes, DateTimeOffset now)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.");
        OrganizationId = organizationId; Change(maxActiveJobs, maxStoredBytes, now);
    }
    public Guid OrganizationId { get; private set; }
    public int MaxActiveJobs { get; private set; }
    public long MaxStoredBytes { get; private set; }
    public Guid Version { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public void Change(int maxActiveJobs, long maxStoredBytes, DateTimeOffset now)
    {
        if (maxActiveJobs is < 1 or > 100000 || maxStoredBytes is < 1 or > 109951162777600)
            throw new ArgumentOutOfRangeException(nameof(maxActiveJobs), "Capacity limits are outside the supported bounds.");
        MaxActiveJobs = maxActiveJobs; MaxStoredBytes = maxStoredBytes;
        Version = Guid.NewGuid(); UpdatedAtUtc = now;
    }
}
