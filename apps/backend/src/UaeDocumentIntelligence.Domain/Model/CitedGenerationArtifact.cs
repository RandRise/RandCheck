namespace UaeDocumentIntelligence.Domain.Model;

public enum CitedGenerationArtifactKind { Preparation, Outcome }

public sealed class CitedGenerationArtifact
{
    private CitedGenerationArtifact() { }

    public CitedGenerationArtifact(Guid organizationId, Guid actorId, Guid reservationId,
        CitedGenerationArtifactKind kind, byte[] payloadUtf8, string payloadSha256,
        string commandSha256, DateTimeOffset createdAtUtc)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ActorId = actorId;
        ReservationId = reservationId;
        Kind = kind;
        PayloadUtf8 = payloadUtf8.ToArray();
        PayloadSha256 = payloadSha256;
        CommandSha256 = commandSha256;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ActorId { get; private set; }
    public Guid ReservationId { get; private set; }
    public CitedGenerationArtifactKind Kind { get; private set; }
    public byte[] PayloadUtf8 { get; private set; } = [];
    public string PayloadSha256 { get; private set; } = "";
    public string CommandSha256 { get; private set; } = "";
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public override string ToString() => "Generation artifact (content omitted)";
}
