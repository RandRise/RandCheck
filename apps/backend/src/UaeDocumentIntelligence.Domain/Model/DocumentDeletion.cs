namespace UaeDocumentIntelligence.Domain.Model;

public sealed class DocumentDeletion
{
    private DocumentDeletion() { }
    public DocumentDeletion(Guid id, Guid organizationId, Guid documentId, string storageKey, string sha256,
        long sizeBytes, Guid documentVersion, Guid policyVersion, DateTimeOffset requestedAtUtc)
    {
        Id = id; OrganizationId = organizationId; DocumentId = documentId; StorageKey = storageKey;
        Sha256 = sha256; SizeBytes = sizeBytes; DocumentVersion = documentVersion; PolicyVersion = policyVersion;
        RequestedAtUtc = requestedAtUtc;
    }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid DocumentId { get; private set; }
    public string StorageKey { get; private set; } = "";
    public string Sha256 { get; private set; } = "";
    public long SizeBytes { get; private set; }
    public Guid DocumentVersion { get; private set; }
    public Guid PolicyVersion { get; private set; }
    public DateTimeOffset RequestedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public void Complete(DateTimeOffset now)
    {
        if (CompletedAtUtc.HasValue || now < RequestedAtUtc) throw new InvalidOperationException();
        CompletedAtUtc = now;
    }
}
