using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using UaeDocumentIntelligence.Application.Documents.Processing;

namespace UaeDocumentIntelligence.Application.Documents.Upload;

public sealed record UploadDocumentCommand(string Filename, string ContentType, long SizeBytes, Stream Content, string CorrelationId,
    string? RequestKey = null);

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
        using var buffered = command.RequestKey is null ? null : new MemoryStream();
        ProcessingIntent? intent = null;
        if (ProcessingIntent.ValidateKey(command.RequestKey) is { } key)
        {
            // Match the public upload boundary and never buffer unbounded bytes.
            if (command.SizeBytes is <= 0 or > 10_485_760) throw new DocumentTooLargeException();
            var block = new byte[81_920];
            int read;
            while ((read = await command.Content.ReadAsync(block, cancellationToken)) > 0)
            {
                if (buffered!.Length + read > command.SizeBytes) throw new DocumentTooLargeException();
                await buffered.WriteAsync(block.AsMemory(0, read), cancellationToken);
            }
            if (buffered!.Length != command.SizeBytes) throw new DocumentTooLargeException();
            var digest = Convert.ToHexString(SHA256.HashData(buffered.GetBuffer().AsSpan(0, (int)buffered.Length))).ToLowerInvariant();
            intent = new(execution.ActorId, key, ProcessingIntent.Hash(Path.GetFileName(command.Filename), command.ContentType,
                command.SizeBytes, digest));
            buffered.Position = 0;
            if (await uploadQueue.FindUploadAsync(execution.OrganizationId, intent, cancellationToken) is { } replay)
                return DocumentView.From(replay);
        }
        var persisted = false;
        var documentId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        try
        {
            if (intent is not null)
            {
                if (await uploadQueue.CheckCapacityForIntentAsync(execution.OrganizationId, command.SizeBytes, intent, cancellationToken) is { } admittedReplay)
                    return DocumentView.From(admittedReplay);
            }
            else await uploadQueue.CheckCapacityAsync(execution.OrganizationId, command.SizeBytes, cancellationToken);
            stored = await storage.SaveAsync(buffered ?? command.Content, command.Filename, command.SizeBytes,
                execution.OrganizationId, documentId, cancellationToken);
            var now = clock.UtcNow;
            var document = new Document(documentId, execution.OrganizationId, execution.ActorId,
                Path.GetFileName(command.Filename), stored.StorageKey, command.ContentType,
                stored.SizeBytes, stored.Sha256, now);
            document.QueueExtraction(runId, 1, extraction.ProviderName,
                extraction.ProviderVersion, extraction.SchemaVersion,
                command.CorrelationId, clock.UtcNow);
            if (intent is not null)
            {
                var accepted = await uploadQueue.AddWithIntentAsync(document, runId, jobId, stored.Scan, intent, cancellationToken);
                if (accepted.Id != document.Id)
                {
                    using var compensation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await storage.DeleteAsync(stored.StorageKey, compensation.Token);
                    stored = null;
                }
                document = accepted;
            }
            else if (stored.Scan is { } scan) await uploadQueue.AddScannedAsync(document, runId, jobId, scan, cancellationToken);
            else await uploadQueue.AddAsync(document, runId, jobId, cancellationToken);
            persisted = true;
            return DocumentView.From(document);
        }
        catch
        {
            if (stored is not null && !persisted)
            {
                using var compensation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var mayDelete = intent is null;
                if (intent is not null)
                {
                    // A commit acknowledgement can be lost after the transaction commits.
                    // Delete only when an independent lookup confirms this file is not owned.
                    try
                    {
                        var committed = await uploadQueue.FindUploadAsync(execution.OrganizationId, intent, compensation.Token);
                        mayDelete = committed is null || committed.Id != documentId;
                    }
                    catch { mayDelete = false; } // Uncertain ownership belongs to orphan recovery.
                }
                if (mayDelete) await storage.DeleteAsync(stored.StorageKey, compensation.Token);
            }
            throw;
        }
    }
}

public sealed class UnsupportedDocumentTypeException : Exception;
public sealed class DocumentTooLargeException : Exception;
public sealed class MalwareDetectedException : Exception;
public sealed class MalwareScanUnavailableException : Exception;
