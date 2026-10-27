namespace UaeDocumentIntelligence.Domain.Model;

// Receipt survives source deletion. It contains no filename, contents or storage key.
public sealed class DocumentProcessingRequest
{
    private DocumentProcessingRequest() { }
    public DocumentProcessingRequest(Guid organizationId, Guid actorId, string kind, string key,
        string payloadHash, Guid documentId, Guid runId, Guid jobId, int runNumber)
    {
        OrganizationId = organizationId; ActorId = actorId; Kind = kind; Key = key;
        PayloadHash = payloadHash; DocumentId = documentId; ExtractionRunId = runId;
        JobId = jobId; RunNumber = runNumber;
    }
    public Guid OrganizationId { get; private set; }
    public Guid ActorId { get; private set; }
    public string Kind { get; private set; } = "";
    public string Key { get; private set; } = "";
    public string PayloadHash { get; private set; } = "";
    public Guid DocumentId { get; private set; }
    public Guid ExtractionRunId { get; private set; }
    public Guid JobId { get; private set; }
    public int RunNumber { get; private set; }
}
