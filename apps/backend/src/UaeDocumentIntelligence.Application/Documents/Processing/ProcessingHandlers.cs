using System.Text.RegularExpressions;
using UaeDocumentIntelligence.Application.Abstractions;

namespace UaeDocumentIntelligence.Application.Documents.Processing;

public sealed class GetDocumentProcessingStatusHandler(
    IExtractionJobQueue queue, IExecutionContext execution)
{
    public Task<ExtractionJobSnapshot?> HandleAsync(Guid documentId,
        CancellationToken cancellationToken) =>
        queue.GetLatestAsync(execution.OrganizationId, documentId, cancellationToken);
}

public sealed class CancelExtractionHandler(IExtractionJobQueue queue, IExecutionContext execution)
{
    public Task<bool> HandleAsync(Guid jobId, CancellationToken cancellationToken) =>
        queue.CancelAsync(execution.OrganizationId, jobId, "request_cancelled",
            "Extraction was cancelled by an authorized workflow request.", cancellationToken);
}

public sealed class ReprocessDocumentHandler(
    IExtractionJobQueue queue, IExtractionClient extraction, IExecutionContext execution)
{
    public Task<QueuedReprocess?> HandleAsync(Guid documentId, string correlationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 100 ||
            !Regex.IsMatch(correlationId, "^[A-Za-z0-9._:-]{1,100}$"))
            throw new ArgumentException("Correlation ID must contain 1 to 100 safe characters.",
                nameof(correlationId));
        return queue.QueueReprocessAsync(execution.OrganizationId, documentId, Guid.NewGuid(),
            Guid.NewGuid(), extraction.ProviderName, extraction.ProviderVersion,
            extraction.SchemaVersion, correlationId, cancellationToken);
    }
}
