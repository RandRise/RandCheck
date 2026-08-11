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
    string SchemaVersion { get; }
    Task<ExtractionResult> ExtractAsync(string absolutePath, string contentType, CancellationToken cancellationToken);
}

public enum ExtractionOutcome { Ready, Partial }

public sealed record ExtractionProviderResult(
    string Alias, string AdapterVersion, string Model, string ModelVersion);

public sealed record ExtractionMetricsResult(
    long ProviderDurationMs, int PageCount, decimal? EstimatedCost, string? CostCurrency, string? PriceBasis);

public sealed record ExtractionPageResult(int PageNumber, string CanonicalText, short RotationDegrees);

public sealed record ExtractionEvidenceResult(
    int PageNumber, string? Quote, int? ByteStart, int? ByteEnd,
    string? BoundingPolygon, string? CoordinateUnit);

public sealed record ExtractionField(
    string Key, int Occurrence, FieldValueType ValueType, string Value,
    string? NormalizedValue, decimal? Confidence, IReadOnlyList<ExtractionEvidenceResult> Evidence);

public sealed record ExtractionWarningResult(
    string Code, ExtractionWarningSeverity Severity, string SafeSummary,
    int? PageNumber, string? FieldKey, int? Occurrence);

public sealed record ExtractionResult(
    string SchemaVersion, ExtractionOutcome Outcome, string DocumentType, string Language,
    ExtractionProviderResult Provider, ExtractionMetricsResult Metrics,
    IReadOnlyList<ExtractionPageResult> Pages, IReadOnlyList<ExtractionField> Fields,
    IReadOnlyList<ExtractionWarningResult> Warnings);

public interface IExecutionContext
{
    Guid OrganizationId { get; }
    Guid ActorId { get; }
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
