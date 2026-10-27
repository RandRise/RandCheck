using UaeDocumentIntelligence.Domain.Common;

namespace UaeDocumentIntelligence.Domain.Model;

// Append-only intent identity. DocumentId deliberately survives source deletion.
public sealed class ReconciliationDraftRequest
{
    private ReconciliationDraftRequest() { }
    public ReconciliationDraftRequest(Guid id, Guid organizationId, Guid actorId, Guid caseId,
        ReconciliationDocumentRole role, Guid? documentId, string requestKey, string payloadHash,
        Guid expectedVersion, Guid? draftVersion, DateTimeOffset now)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || actorId == Guid.Empty || caseId == Guid.Empty ||
            expectedVersion == Guid.Empty || documentId == Guid.Empty || draftVersion == Guid.Empty)
            throw new ArgumentException("Request, organization, actor, case and version identities must be valid.");
        if (role is not (ReconciliationDocumentRole.Invoice or ReconciliationDocumentRole.PurchaseOrder))
            throw new ArgumentOutOfRangeException(nameof(role));
        Id = id; OrganizationId = organizationId; ActorId = actorId; ReconciliationCaseId = caseId;
        Role = role; DocumentId = documentId; RequestKey = Guard.Required(requestKey, nameof(requestKey), 100);
        PayloadHash = Guard.Required(payloadHash, nameof(payloadHash), 64);
        ExpectedVersion = expectedVersion; DraftVersion = draftVersion; CreatedAtUtc = now;
    }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ActorId { get; private set; }
    public Guid ReconciliationCaseId { get; private set; }
    public ReconciliationDocumentRole Role { get; private set; }
    public Guid? DocumentId { get; private set; }
    public string RequestKey { get; private set; } = "";
    public string PayloadHash { get; private set; } = "";
    public Guid ExpectedVersion { get; private set; }
    public Guid? DraftVersion { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
