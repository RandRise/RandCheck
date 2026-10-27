using UaeDocumentIntelligence.Application.Reconciliation;

namespace UaeDocumentIntelligence.Api;

public static partial class ReconciliationEndpoints
{
    public static IEndpointRouteBuilder MapReconciliationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/reconciliation-cases", CreateAsync);
        endpoints.MapGet("/api/reconciliation-cases", ListAsync);
        endpoints.MapGet("/api/reconciliation-cases/{caseId:guid}", GetAsync);
        endpoints.MapGet("/api/reconciliation-cases/{caseId:guid}/runs/{runId:guid}/report", ReportAsync);
        endpoints.MapPut("/api/reconciliation-cases/{caseId:guid}", UpdateMetadataAsync);
        endpoints.MapPut("/api/reconciliation-cases/{caseId:guid}/documents/{role}", LinkAsync);
        endpoints.MapPut("/api/reconciliation-cases/{caseId:guid}/draft-documents/{role}", SetDraftAsync);
        endpoints.MapPost("/api/reconciliation-cases/{caseId:guid}/evaluations", EvaluateAsync);
        endpoints.MapPost("/api/reconciliation-cases/{caseId:guid}/runs/{runId:guid}/findings/{findingId:guid}/resolutions",
            ResolveAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(int? offset, string? q, string? category,
        string? status, string? createdFrom, string? createdTo, string? workflowKind,
        ListReconciliationCasesHandler handler, CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await handler.HandleAsync(offset ?? 0, cancellationToken,
                new ReconciliationCaseQuery(q, category, status, createdFrom, createdTo, workflowKind)));
        }
        catch (ReconciliationWorkflowException exception)
        {
            return ToProblem(exception);
        }
    }

    private static async Task<IResult> CreateAsync(CreateReconciliationCaseBody body,
        HttpContext context, CreateReconciliationCaseHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await handler.HandleAsync(body.Name, cancellationToken, body.Category, body.RequestKey, body.WorkflowKind);
            SetEtag(context, result);
            return Results.Created($"/api/reconciliation-cases/{result.Id}", result);
        }
        catch (ArgumentException exception)
        {
            return Problem(StatusCodes.Status400BadRequest, "invalid_case_name", exception.Message);
        }
        catch (ReconciliationWorkflowException exception)
        {
            return ToProblem(exception);
        }
    }

    private static async Task<IResult> UpdateMetadataAsync(Guid caseId, UpdateReconciliationCaseMetadataBody body,
        HttpContext context, UpdateReconciliationCaseMetadataHandler handler, CancellationToken cancellationToken)
    {
        if (!TryReadEtag(context, out var expected, out var failure)) return failure!;
        try
        {
            var result = await handler.HandleAsync(caseId, expected, body.Name, body.Category, cancellationToken);
            SetEtag(context, result);
            return Results.Ok(result);
        }
        catch (ReconciliationWorkflowException exception) { return ToProblem(exception); }
    }

    private static async Task<IResult> GetAsync(Guid caseId, HttpContext context,
        GetReconciliationCaseHandler handler, CancellationToken cancellationToken)
    {
        try
        {
            var result = await handler.HandleAsync(caseId, cancellationToken);
            SetEtag(context, result);
            return Results.Ok(result);
        }
        catch (ReconciliationWorkflowException exception)
        {
            return ToProblem(exception);
        }
    }

    private static async Task<IResult> LinkAsync(Guid caseId, string role,
        LinkReconciliationDocumentBody body, HttpContext context,
        LinkReconciliationDocumentHandler handler, CancellationToken cancellationToken)
    {
        if (!TryReadEtag(context, out var expected, out var failure)) return failure!;
        try
        {
            var result = await handler.HandleAsync(caseId, expected,
                new LinkReconciliationDocumentCommand(role, body.DocumentId,
                    body.ReviewSessionId), cancellationToken);
            SetEtag(context, result);
            return Results.Ok(result);
        }
        catch (ReconciliationWorkflowException exception)
        {
            return ToProblem(exception);
        }
    }

    private static async Task<IResult> SetDraftAsync(Guid caseId, string role,
        SetReconciliationDraftDocumentBody body, HttpContext context,
        SetReconciliationDraftDocumentHandler handler, CancellationToken cancellationToken)
    {
        if (!TryReadEtag(context, out var expected, out var failure)) return failure!;
        try
        {
            var result = await handler.HandleAsync(caseId, role, expected,
                new SetReconciliationDraftDocumentCommand(body.DocumentId, body.RequestKey), cancellationToken);
            SetEtag(context, result);
            return Results.Ok(result);
        }
        catch (ReconciliationWorkflowException exception) { return ToProblem(exception); }
    }

    private static async Task<IResult> EvaluateAsync(Guid caseId,
        EvaluateReconciliationCaseBody body, HttpContext context,
        EvaluateReconciliationCaseHandler handler, CancellationToken cancellationToken)
    {
        if (!TryReadEtag(context, out var expected, out var failure)) return failure!;
        try
        {
            var result = await handler.HandleAsync(caseId, expected,
                new EvaluateReconciliationCaseCommand(body.QuantityTolerance,
                    body.UnitPriceTolerance, body.TaxTolerance, body.LineTotalTolerance,
                    body.DocumentTotalTolerance, body.RuleSetVersion, body.OrderScope), cancellationToken);
            SetEtag(context, result);
            return Results.Ok(result);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Problem(StatusCodes.Status400BadRequest, "invalid_tolerance", exception.Message);
        }
        catch (ReconciliationWorkflowException exception)
        {
            return ToProblem(exception);
        }
    }

    private static async Task<IResult> ResolveAsync(Guid caseId, Guid runId,
        Guid findingId, ResolveFindingBody body, HttpContext context,
        ResolveReconciliationFindingHandler handler, CancellationToken cancellationToken)
    {
        if (!TryReadEtag(context, out var expected, out var failure)) return failure!;
        try
        {
            var result = await handler.HandleAsync(caseId, runId, findingId, expected,
                new ResolveFindingCommand(body.Action, body.Reason), cancellationToken);
            SetEtag(context, result);
            return Results.Ok(result);
        }
        catch (ReconciliationWorkflowException exception)
        {
            return ToProblem(exception);
        }
    }

    private static void SetEtag(HttpContext context, ReconciliationCaseView item) =>
        context.Response.Headers.ETag = $"\"{item.ConcurrencyToken:D}\"";

    private static bool TryReadEtag(HttpContext context, out Guid expected,
        out IResult? failure)
    {
        expected = Guid.Empty;
        failure = null;
        var value = context.Request.Headers["If-Match"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value))
        {
            failure = Problem(StatusCodes.Status428PreconditionRequired,
                "reconciliation_precondition_required",
                "An If-Match header containing the current reconciliation ETag is required.");
            return false;
        }
        if (value.StartsWith("W/", StringComparison.OrdinalIgnoreCase) || value.Length < 3 ||
            value[0] != '"' || value[^1] != '"' ||
            !Guid.TryParse(value[1..^1], out expected))
        {
            failure = Problem(StatusCodes.Status400BadRequest,
                "invalid_reconciliation_etag",
                "If-Match must contain one strong quoted reconciliation ETag.");
            return false;
        }
        return true;
    }

    private static IResult ToProblem(ReconciliationWorkflowException exception) => exception switch
    {
        ReconciliationNotFoundException => Problem(StatusCodes.Status404NotFound,
            exception.Code, exception.Message),
        ReconciliationValidationException => Problem(StatusCodes.Status400BadRequest,
            exception.Code, exception.Message),
        ReconciliationPreconditionException => Problem(StatusCodes.Status412PreconditionFailed,
            exception.Code, exception.Message),
        _ => Problem(StatusCodes.Status409Conflict, exception.Code, exception.Message)
    };

    private static IResult Problem(int status, string code, string detail) =>
        Results.Problem(statusCode: status, title: "Reconciliation operation failed",
            detail: detail, extensions: new Dictionary<string, object?> { ["code"] = code });
}

public sealed record CreateReconciliationCaseBody(string Name, string? Category = null, string? RequestKey = null, string? WorkflowKind = null);
public sealed record SetReconciliationDraftDocumentBody(Guid? DocumentId, string RequestKey);
public sealed record UpdateReconciliationCaseMetadataBody(string Name, string? Category);
public sealed record LinkReconciliationDocumentBody(Guid DocumentId, Guid ReviewSessionId);
public sealed record EvaluateReconciliationCaseBody(decimal? QuantityTolerance,
    decimal? UnitPriceTolerance, decimal? TaxTolerance, decimal? LineTotalTolerance,
    decimal? DocumentTotalTolerance, string? RuleSetVersion = null, string? OrderScope = null);
public sealed record ResolveFindingBody(string Action, string Reason);
