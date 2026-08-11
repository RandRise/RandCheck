using UaeDocumentIntelligence.Application.Documents.Review;

namespace UaeDocumentIntelligence.Api;

public static class ReviewEndpoints
{
    public static IEndpointRouteBuilder MapReviewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/documents/{documentId:guid}/review", GetWorkspaceAsync);
        endpoints.MapPost("/api/documents/{documentId:guid}/reviews", StartReviewAsync);
        endpoints.MapPost("/api/documents/{documentId:guid}/reviews/{reviewId:guid}/actions", RecordActionAsync);
        endpoints.MapPost("/api/documents/{documentId:guid}/reviews/{reviewId:guid}/complete", CompleteReviewAsync);
        return endpoints;
    }

    private static async Task<IResult> GetWorkspaceAsync(Guid documentId, HttpContext context,
        GetReviewWorkspaceHandler handler, CancellationToken cancellationToken)
    {
        try
        {
            var workspace = await handler.HandleAsync(documentId, cancellationToken);
            SetEtag(context, workspace);
            return Results.Ok(workspace);
        }
        catch (ReviewWorkflowException exception)
        {
            return ToProblem(exception);
        }
    }

    private static async Task<IResult> StartReviewAsync(Guid documentId, HttpContext context,
        StartReviewHandler handler, CancellationToken cancellationToken)
    {
        try
        {
            var result = await handler.HandleAsync(documentId, cancellationToken);
            SetEtag(context, result.Workspace);
            return result.Created
                ? Results.Created($"/api/documents/{documentId}/review", result.Workspace)
                : Results.Ok(result.Workspace);
        }
        catch (ReviewWorkflowException exception)
        {
            return ToProblem(exception);
        }
    }

    private static async Task<IResult> RecordActionAsync(Guid documentId, Guid reviewId,
        ReviewActionBody body, HttpContext context, RecordReviewActionHandler handler,
        CancellationToken cancellationToken)
    {
        if (!TryReadEtag(context, out var expected, out var failure)) return failure!;
        try
        {
            var workspace = await handler.HandleAsync(documentId, reviewId, expected,
                new RecordReviewActionCommand(body.Action, body.MachineFieldValueId, body.FieldKey,
                    body.OccurrenceIndex, body.ValueType, body.Value, body.Reason), cancellationToken);
            SetEtag(context, workspace);
            return Results.Ok(workspace);
        }
        catch (ReviewWorkflowException exception)
        {
            return ToProblem(exception);
        }
    }

    private static async Task<IResult> CompleteReviewAsync(Guid documentId, Guid reviewId,
        HttpContext context, CompleteReviewHandler handler, CancellationToken cancellationToken)
    {
        if (!TryReadEtag(context, out var expected, out var failure)) return failure!;
        try
        {
            var workspace = await handler.HandleAsync(documentId, reviewId, expected, cancellationToken);
            SetEtag(context, workspace);
            return Results.Ok(workspace);
        }
        catch (ReviewWorkflowException exception)
        {
            return ToProblem(exception);
        }
    }

    private static void SetEtag(HttpContext context, ReviewWorkspaceView workspace)
    {
        if (workspace.Session is not null)
            context.Response.Headers.ETag = $"\"{workspace.Session.ConcurrencyToken:D}\"";
    }

    private static bool TryReadEtag(HttpContext context, out Guid expected, out IResult? failure)
    {
        expected = Guid.Empty;
        failure = null;
        var value = context.Request.Headers["If-Match"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value))
        {
            failure = Problem(StatusCodes.Status428PreconditionRequired, "review_precondition_required",
                "An If-Match header containing the current review ETag is required.");
            return false;
        }
        if (value.StartsWith("W/", StringComparison.OrdinalIgnoreCase) || value.Length < 3 ||
            value[0] != '"' || value[^1] != '"' || !Guid.TryParse(value[1..^1], out expected))
        {
            failure = Problem(StatusCodes.Status400BadRequest, "invalid_review_etag",
                "If-Match must contain one strong quoted review ETag.");
            return false;
        }
        return true;
    }

    private static IResult ToProblem(ReviewWorkflowException exception) => exception switch
    {
        ReviewNotFoundException => Problem(StatusCodes.Status404NotFound, exception.Code, exception.Message),
        ReviewValidationException => Problem(StatusCodes.Status400BadRequest, exception.Code, exception.Message),
        ReviewPreconditionException => Problem(StatusCodes.Status412PreconditionFailed, exception.Code, exception.Message),
        ReviewConflictException conflict => Problem(StatusCodes.Status409Conflict, conflict.Code,
            conflict.Message, conflict.PendingFields),
        _ => Problem(StatusCodes.Status409Conflict, exception.Code, exception.Message)
    };

    private static IResult Problem(int status, string code, string detail,
        IReadOnlyList<string>? pendingFields = null)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = code };
        if (pendingFields is not null) extensions["pendingFields"] = pendingFields;
        return Results.Problem(statusCode: status, title: "Review operation failed",
            detail: detail, extensions: extensions);
    }
}

public sealed record ReviewActionBody(string Action, Guid? MachineFieldValueId,
    string? FieldKey, int? OccurrenceIndex, string? ValueType, string? Value, string? Reason);
