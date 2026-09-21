namespace UaeDocumentIntelligence.Application.Abstractions;

public sealed record DocumentSource(Guid OrganizationId, Guid DocumentId, string StorageKey, string Sha256, long SizeBytes);

public interface IDocumentSourceReader
{
    Task<byte[]> ReadVerifiedAsync(DocumentSource source, CancellationToken ct);
}

public sealed class DocumentSourceUnavailableException : Exception;
