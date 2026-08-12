using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.Documents.Processing;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Api;

public static class ProcessingEndpoints
{
    public static IEndpointRouteBuilder MapProcessingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/documents/{documentId:guid}/processing", GetStatusAsync);
        endpoints.MapPost("/api/documents/{documentId:guid}/reprocess", ReprocessAsync);
        endpoints.MapPost("/api/extraction-jobs/{jobId:guid}/cancel", CancelAsync);
        return endpoints;
    }

    private static async Task<IResult> GetStatusAsync(Guid documentId,
        GetDocumentProcessingStatusHandler handler, CancellationToken cancellationToken)
    {
        var job = await handler.HandleAsync(documentId, cancellationToken);
        return job is null ? Results.NotFound() : Results.Ok(ToView(job));
    }

    private static async Task<IResult> ReprocessAsync(Guid documentId, HttpContext context,
        ReprocessDocumentHandler handler, CancellationToken cancellationToken)
    {
        var queued = await handler.HandleAsync(documentId, context.TraceIdentifier, cancellationToken);
        return queued is null
            ? Results.Conflict(new { code = "reprocess_not_available", detail = "The document was not found or already has active processing." })
            : Results.Accepted($"/api/documents/{documentId}/processing", new
            {
                queued.JobId, queued.ExtractionRunId, queued.RunNumber, status = "Queued"
            });
    }

    private static async Task<IResult> CancelAsync(Guid jobId, CancelExtractionHandler handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(jobId, cancellationToken)
            ? Results.Ok(new { jobId, status = "Cancelled" })
            : Results.Conflict(new { code = "cancellation_not_available", detail = "The job was not found or is already terminal." });

    private static object ToView(ExtractionJobSnapshot job) => new
    {
        job.JobId,
        job.ExtractionRunId,
        status = job.Status switch
        {
            ExtractionJobStatus.Pending => "Queued",
            ExtractionJobStatus.Leased => "Processing",
            ExtractionJobStatus.RetryScheduled => "Queued",
            ExtractionJobStatus.Succeeded when job.ResultOutcome?.ToString() == "Partial" => "Partial",
            ExtractionJobStatus.Succeeded => "Ready",
            ExtractionJobStatus.Failed => "Failed",
            ExtractionJobStatus.Cancelled => "Cancelled",
            _ => "Processing"
        },
        job.AttemptCount,
        retryAt = job.Status == ExtractionJobStatus.RetryScheduled ? (DateTimeOffset?)job.AvailableAtUtc : null,
        job.LeaseExpiresAtUtc,
        errorCode = job.LastErrorCode,
        errorMessage = job.LastSafeErrorSummary,
        job.CreatedAtUtc,
        job.UpdatedAtUtc,
        job.CompletedAtUtc
    };
}
