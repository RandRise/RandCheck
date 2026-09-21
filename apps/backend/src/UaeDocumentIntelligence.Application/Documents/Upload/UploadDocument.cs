using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;
using System.Text.RegularExpressions;

namespace UaeDocumentIntelligence.Application.Documents.Upload;

public sealed record UploadDocumentCommand(string Filename, string ContentType, long SizeBytes, Stream Content, string CorrelationId);

public sealed class UploadDocumentHandler(
    IUploadQueueStore uploadQueue,
    IDocumentStorage storage,
    IExtractionClient extraction,
    IExecutionContext execution,
    IClock clock)
{
    private static readonly HashSet<string> AllowedTypes = ["application/pdf", "image/png", "image/jpeg", "text/plain"];

    public async Task<DocumentView> HandleAsync(UploadDocumentCommand command, CancellationToken cancellationToken)
    {
        if (!AllowedTypes.Contains(command.ContentType)) throw new UnsupportedDocumentTypeException();
        if (string.IsNullOrWhiteSpace(command.CorrelationId) || command.CorrelationId.Length > 100 ||
            !Regex.IsMatch(command.CorrelationId, "^[A-Za-z0-9._:-]{1,100}$"))
            throw new ArgumentException("Correlation ID must contain 1 to 100 characters.", nameof(command));
        StoredFile? stored = null;
        var persisted = false;
        var documentId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        try
        {
            await uploadQueue.CheckCapacityAsync(execution.OrganizationId, command.SizeBytes, cancellationToken);
            stored = await storage.SaveAsync(command.Content, command.Filename, command.SizeBytes,
                execution.OrganizationId, documentId, cancellationToken);
            var now = clock.UtcNow;
            var document = new Document(documentId, execution.OrganizationId, execution.ActorId,
                Path.GetFileName(command.Filename), stored.StorageKey, command.ContentType,
                stored.SizeBytes, stored.Sha256, now);
            document.QueueExtraction(runId, 1, extraction.ProviderName,
                extraction.ProviderVersion, extraction.SchemaVersion,
                command.CorrelationId, clock.UtcNow);
            if (stored.Scan is { } scan) await uploadQueue.AddScannedAsync(document, runId, jobId, scan, cancellationToken);
            else await uploadQueue.AddAsync(document, runId, jobId, cancellationToken);
            persisted = true;
            return DocumentView.From(document);
        }
        catch
        {
            if (stored is not null && !persisted)
            {
                using var compensation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await storage.DeleteAsync(stored.StorageKey, compensation.Token);
            }
            throw;
        }
    }
}

public sealed class UnsupportedDocumentTypeException : Exception;
public sealed class DocumentTooLargeException : Exception;
public sealed class MalwareDetectedException : Exception;
public sealed class MalwareScanUnavailableException : Exception;
