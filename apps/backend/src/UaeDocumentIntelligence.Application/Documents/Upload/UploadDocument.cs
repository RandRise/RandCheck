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
            var run = document.QueueExtraction(Guid.NewGuid(), runNumber, extraction.ProviderName,
                extraction.ProviderVersion, extraction.SchemaVersion,
                command.CorrelationId, clock.UtcNow);
            run.Start(clock.UtcNow);
            await documents.SaveChangesAsync(cancellationToken);
            documentPersisted = true;
            try
            {
                var result = await extraction.ExtractAsync(stored.AbsolutePath, command.ContentType, cancellationToken);
                if (!string.Equals(result.SchemaVersion, extraction.SchemaVersion, StringComparison.Ordinal))
                    throw new InvalidDataException("The extraction result schema is unsupported.");
                if (!string.Equals(result.Provider.Alias, extraction.ProviderName, StringComparison.Ordinal))
                    throw new InvalidDataException("The extraction provider identity does not match the requested adapter.");
                if (!string.Equals(result.Provider.AdapterVersion, extraction.ProviderVersion,
                        StringComparison.Ordinal))
                    throw new InvalidDataException("The extraction adapter version does not match the requested adapter.");
                if (string.IsNullOrWhiteSpace(result.Provider.Model) ||
                    string.IsNullOrWhiteSpace(result.Provider.ModelVersion))
                    throw new InvalidDataException("The extraction model identity is incomplete.");
                foreach (var item in result.Pages.OrderBy(page => page.PageNumber))
                    run.AddPage(Guid.NewGuid(), item.PageNumber, item.CanonicalText, item.RotationDegrees);
                foreach (var item in result.Fields)
                {
                    var field = run.AddField(Guid.NewGuid(), item.Key, item.Occurrence, item.ValueType,
                        item.Value, item.Confidence, clock.UtcNow, item.NormalizedValue);
                    foreach (var evidence in item.Evidence)
                        run.AddEvidence(field, Guid.NewGuid(), evidence.PageNumber, evidence.Quote,
                            evidence.ByteStart, evidence.ByteEnd, evidence.BoundingPolygon,
                            evidence.CoordinateUnit);
                }
                foreach (var warning in result.Warnings)
                    run.AddWarning(Guid.NewGuid(), warning.Code, warning.Severity, warning.SafeSummary,
                        warning.PageNumber, warning.FieldKey, warning.Occurrence);
                run.RecordProviderMetrics(result.Metrics.ProviderDurationMs, result.Metrics.PageCount,
                    result.Metrics.EstimatedCost, result.Metrics.CostCurrency, result.Metrics.PriceBasis);
                if (result.Outcome == ExtractionOutcome.Ready)
                    run.Succeed(result.DocumentType, result.Language, clock.UtcNow,
                        result.Provider.Model, result.Provider.ModelVersion);
                else
                    run.CompletePartial(result.DocumentType, result.Language, clock.UtcNow,
                        result.Provider.Model, result.Provider.ModelVersion);
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
                run.Fail("provider_timeout", "The extraction provider timed out.", clock.UtcNow);
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
