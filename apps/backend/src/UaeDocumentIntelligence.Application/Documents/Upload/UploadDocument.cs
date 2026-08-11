using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Documents.Upload;

public sealed record UploadDocumentCommand(string Filename, string ContentType, long SizeBytes, Stream Content, string CorrelationId);

public sealed class UploadDocumentHandler(
    IDocumentRepository documents,
    IDocumentStorage storage,
    IExtractionClient extraction,
    IExecutionContext execution,
    IClock clock)
{
    private static readonly HashSet<string> AllowedTypes = ["application/pdf", "image/png", "image/jpeg", "text/plain"];

    public async Task<DocumentView> HandleAsync(UploadDocumentCommand command, CancellationToken cancellationToken)
    {
        if (!AllowedTypes.Contains(command.ContentType)) throw new UnsupportedDocumentTypeException();
        if (string.IsNullOrWhiteSpace(command.CorrelationId) || command.CorrelationId.Length > 100)
            throw new ArgumentException("Correlation ID must contain 1 to 100 characters.", nameof(command));
        StoredFile? stored = null;
        var documentPersisted = false;
        try
        {
            stored = await storage.SaveAsync(command.Content, command.Filename, command.SizeBytes, cancellationToken);
            var now = clock.UtcNow;
            var document = new Document(Guid.NewGuid(), execution.OrganizationId, execution.ActorId,
                Path.GetFileName(command.Filename), stored.StorageKey, command.ContentType,
                stored.SizeBytes, stored.Sha256, now);
            await documents.AddAsync(document, cancellationToken);
            var runNumber = await documents.NextRunNumberAsync(execution.OrganizationId, document.Id, cancellationToken);
            var run = document.QueueExtraction(Guid.NewGuid(), runNumber, extraction.ProviderName, extraction.ProviderVersion, "invoice-v1",
                command.CorrelationId, clock.UtcNow);
            run.Start(clock.UtcNow);
            await documents.SaveChangesAsync(cancellationToken);
            documentPersisted = true;
            try
            {
                var result = await extraction.ExtractAsync(stored.AbsolutePath, command.ContentType, cancellationToken);
                foreach (var item in result.Fields)
                {
                    var field = run.AddField(Guid.NewGuid(), item.Name, 0, FieldValueType.String,
                        item.Value, item.Confidence, clock.UtcNow);
                    run.AddEvidence(field, Guid.NewGuid(), item.Page);
                }
                run.Succeed(result.DocumentType, result.Language, clock.UtcNow, result.Extractor);
                document.RecordExtractionSuccess(run, clock.UtcNow);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                run.Cancel("request_cancelled", "Extraction was cancelled before completion.", clock.UtcNow);
                document.RecordExtractionFailure(run, clock.UtcNow);
                using var compensation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await documents.SaveChangesAsync(compensation.Token);
                throw;
            }
            catch (OperationCanceledException)
            {
                run.Cancel("provider_timeout", "The extraction provider timed out.", clock.UtcNow);
                document.RecordExtractionFailure(run, clock.UtcNow);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                run.Fail("extraction_failed", SafeSummary(exception), clock.UtcNow);
                document.RecordExtractionFailure(run, clock.UtcNow);
            }
            using var finalization = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await documents.SaveChangesAsync(finalization.Token);
            return DocumentView.From(document);
        }
        catch
        {
            if (stored is not null && !documentPersisted)
                await storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }
    }

    private static string SafeSummary(Exception exception) => exception switch
    {
        HttpRequestException => "The extraction provider request failed.",
        _ => "The extraction provider could not process this document."
    };
}

public sealed class UnsupportedDocumentTypeException : Exception;
public sealed class DocumentTooLargeException : Exception;
