using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Application.Documents.Processing;

namespace UaeDocumentIntelligence.Application.Abstractions;

public interface IDocumentStorage
{
    Task<StoredFile> SaveAsync(Stream content, string originalFilename, long declaredSize,
        Guid organizationId, Guid documentId, CancellationToken cancellationToken);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}

public sealed record FileScanReceipt(string Sha256, long SizeBytes, string Policy);

public sealed record StoredFile(string StorageKey, string AbsolutePath, long SizeBytes, string Sha256, FileScanReceipt? Scan = null);

public interface IExtractionClient
{
    string ProviderName
    {
        get;
    }
    string ProviderVersion
    {
        get;
    }
    string SchemaVersion
    {
        get;
    }
    Task<ExtractionResult> ExtractAsync(WorkerExtractionRequest request, CancellationToken cancellationToken);
}

public sealed record WorkerExtractionRequest(
    string SchemaVersion, Guid JobId, Guid DocumentId, Guid ExtractionRunId,
    string StorageKey, string Sha256, long Bytes, string DeclaredContentType,
    string ProviderAlias, int Attempt, DateTimeOffset DeadlineUtc, string CorrelationId);

public sealed class ExtractionProviderException(
    string code, string safeSummary, bool retryable = false, Exception? innerException = null)
    : Exception(safeSummary, innerException)
{
    public string Code { get; } = code;
    public string SafeSummary { get; } = safeSummary;
    public bool Retryable { get; } = retryable;
}

public interface IUploadQueueStore
{
    async Task<Document?> CheckCapacityForIntentAsync(Guid organizationId, long bytes, ProcessingIntent intent,
        CancellationToken cancellationToken)
    {
        await CheckCapacityAsync(organizationId, bytes, cancellationToken);
        return null;
    }
    Task<Document?> FindUploadAsync(Guid organizationId, ProcessingIntent intent, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Durable upload intents are not configured.");
    Task<Document> AddWithIntentAsync(Document document, Guid extractionRunId, Guid jobId,
        FileScanReceipt? scan, ProcessingIntent intent, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Durable upload intents are not configured.");
    Task CheckCapacityAsync(Guid organizationId, long bytes, CancellationToken cancellationToken) => Task.CompletedTask;
    Task<ExtractionJob> AddScannedAsync(Document document, Guid extractionRunId, Guid jobId,
        FileScanReceipt scan, CancellationToken cancellationToken) => throw new InvalidOperationException("Scanned upload admission is not configured.");
    Task<ExtractionJob> AddAsync(Document document, Guid extractionRunId, Guid jobId,
        CancellationToken cancellationToken);
}

public sealed record ClaimedExtractionJob(
    Guid JobId, Guid OrganizationId, Guid DocumentId, Guid ExtractionRunId,
    string StorageKey, string Sha256, long Bytes, string DeclaredContentType,
    string ProviderAlias, string ProviderVersion, string SchemaVersion,
    string CorrelationId, int Attempt, Guid LeaseToken, string LeaseOwner,
    DateTimeOffset DeadlineUtc, DateTimeOffset CreatedAtUtc);

public interface IExtractionJobQueue
{
    Task<bool> TerminalizeOneExpiredAsync(CancellationToken cancellationToken);
    Task<ClaimedExtractionJob?> ClaimAsync(string owner, CancellationToken cancellationToken);
    Task<bool> HeartbeatAsync(Guid jobId, string owner, Guid leaseToken,
        CancellationToken cancellationToken);
    Task<bool> CompleteAsync(ClaimedExtractionJob claim, ExtractionResult result,
        CancellationToken cancellationToken);
    Task<bool> RecordFailureAsync(ClaimedExtractionJob claim, string code, string safeSummary,
        bool retryable, CancellationToken cancellationToken);
    Task<bool> CancelAsync(Guid organizationId, Guid jobId, string code, string safeSummary,
        CancellationToken cancellationToken);
    Task<ExtractionJobSnapshot?> GetLatestAsync(Guid organizationId, Guid documentId,
        CancellationToken cancellationToken);
    Task<QueuedReprocess?> QueueReprocessAsync(Guid organizationId, Guid documentId,
        Guid runId, Guid jobId, string providerAlias, string providerVersion,
        string schemaVersion, string correlationId, CancellationToken cancellationToken);
    Task<QueuedReprocess?> QueueReprocessWithIntentAsync(Guid organizationId, Guid documentId,
        Guid runId, Guid jobId, string providerAlias, string providerVersion,
        string schemaVersion, string correlationId, ProcessingIntent intent, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Durable reprocessing intents are not configured.");
}

public sealed record ExtractionJobSnapshot(
    Guid JobId, Guid ExtractionRunId, ExtractionJobStatus Status, int AttemptCount,
    string? LastErrorCode, string? LastSafeErrorSummary, ExtractionOutcomeValue? ResultOutcome,
    DateTimeOffset AvailableAtUtc, DateTimeOffset? LeaseExpiresAtUtc,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, DateTimeOffset? CompletedAtUtc);

public sealed record QueuedReprocess(Guid JobId, Guid ExtractionRunId, int RunNumber,
    string Status = "Queued", bool Replayed = false);

public enum ExtractionOutcome
{
    Ready, Partial
}

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
    Guid OrganizationId
    {
        get;
    }
    Guid ActorId
    {
        get;
    }
}

public interface IClock
{
    DateTimeOffset UtcNow
    {
        get;
    }
}
