using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Abstractions;

public interface IDocumentRepository
{
    Task AddAsync(Document document, CancellationToken cancellationToken);
    Task<Document?> GetAsync(Guid organizationId, Guid documentId, CancellationToken cancellationToken);
    Task<Document?> GetForReviewAsync(Guid organizationId, Guid documentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Document>> ListAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<int> NextRunNumberAsync(Guid organizationId, Guid documentId, CancellationToken cancellationToken);
    Task<DocumentMetrics> GetMetricsAsync(Guid organizationId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<ReviewSaveOutcome> SaveReviewChangesAsync(CancellationToken cancellationToken);
}

public enum ReviewSaveOutcome { Saved, ConcurrencyConflict, ConstraintConflict }

public sealed record DocumentMetrics(int TotalDocuments, int ReadyDocuments, int FailedDocuments, double? AverageProcessingMs);

public interface IDocumentStorage
{
    Task<StoredFile> SaveAsync(Stream content, string originalFilename, long declaredSize, CancellationToken cancellationToken);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}

public sealed record StoredFile(string StorageKey, string AbsolutePath, long SizeBytes, string Sha256);

public interface IExtractionClient
{
    string ProviderName { get; }
    string ProviderVersion { get; }
    Task<ExtractionResult> ExtractAsync(string absolutePath, string contentType, CancellationToken cancellationToken);
}

public sealed record ExtractionField(string Name, string Value, decimal Confidence, int Page);
public sealed record ExtractionResult(string DocumentType, string Language, string Extractor,
    IReadOnlyList<ExtractionField> Fields);

public interface IExecutionContext
{
    Guid OrganizationId { get; }
    Guid ActorId { get; }
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
