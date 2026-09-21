using UaeDocumentIntelligence.Application.CitedQuestions;

namespace UaeDocumentIntelligence.Api;

public static class CitedRetrievalEndpoints
{
    public static IEndpointRouteBuilder MapCitedRetrievalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/cited-cases/{id:guid}/sources", async (Guid id, CitedCaseSourcesHandler handler, CancellationToken ct) =>
            await Execute(async () => Results.Ok(await handler.HandleAsync(id, ct))));
        endpoints.MapPost("/api/cited-questions/semantic", async (HttpContext context, CitedSemanticQuestionHandler handler, CancellationToken ct) =>
            await ReadBody<SemanticBody>(context, body => Execute(async () => Results.Ok(await handler.HandleAsync(
                body.Question, body.RequestKey, body.IndexIds, body.Mode, body.TopK, ct)))))
            .Accepts<SemanticBody>("application/json");
        endpoints.MapPost("/api/cited-indexes", async (HttpContext context, IndexReviewedDocumentHandler handler, CancellationToken ct) =>
            await ReadBody<IndexBody>(context, body => Execute(async () => Results.Ok(await handler.HandleAsync(body.DocumentId, body.ReviewSessionId, ct)))))
            .Accepts<IndexBody>("application/json");
        endpoints.MapPost("/api/cited-retrievals", async (HttpContext context, RetrieveEvidenceHandler handler, CancellationToken ct) =>
            await ReadBody<RetrieveBody>(context, body => Execute(async () => Results.Ok(await handler.HandleAsync(body.Question, body.RequestKey, body.IndexIds, body.TopK, ct)))))
            .Accepts<RetrieveBody>("application/json");
        endpoints.MapGet("/api/cited-retrievals/{id:guid}", async (Guid id, RetrieveEvidenceHandler handler, CancellationToken ct) =>
            await Execute(async () => Results.Ok(await handler.GetAsync(id, ct))));
        endpoints.MapPost("/api/cited-questions", async (HttpContext context, CitedQuestionHandler handler, CancellationToken ct) =>
            await ReadBody<RetrieveBody>(context, body => Execute(async () => Results.Ok(await handler.HandleAsync(body.Question, body.RequestKey, body.IndexIds, body.TopK, ct)))))
            .Accepts<RetrieveBody>("application/json");
        endpoints.MapGet("/api/cited-questions/{id:guid}", async (Guid id, CitedQuestionHandler handler, CancellationToken ct) =>
            await Execute(async () => Results.Ok(await handler.GetAsync(id, ct))));
        endpoints.MapPost("/api/cited-questions/reviewed", async (HttpContext context, CitedReviewedQuestionHandler handler, CancellationToken ct) =>
            await ReadBody<ReviewedBody>(context, body => Execute(async () => Results.Ok(await handler.HandleAsync(
                body.Question, body.RequestKey, body.IndexIds, ct)))))
            .Accepts<ReviewedBody>("application/json");
        endpoints.MapGet("/api/cited-sources", async (int? offset, CitedWorkspaceHandler handler, HttpContext context, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return await Execute(async () => Results.Ok(await handler.SourcesAsync(offset ?? 0, ct)));
        });
        endpoints.MapGet("/api/cited-questions/{id:guid}/hits/{hitId:guid}/evidence",
            async (Guid id, Guid hitId, Guid? spanId, CitedWorkspaceHandler handler, HttpContext context, CancellationToken ct) =>
            {
                context.Response.Headers.CacheControl = "no-store";
                return await Execute(async () => Results.Ok(await handler.EvidenceAsync(id, hitId, spanId, ct)));
            });
        return endpoints;
    }

    private static async Task<IResult> ReadBody<T>(HttpContext context, Func<T, Task<IResult>> operation)
    {
        if (!context.Request.HasJsonContentType())
        {
            await CitedRequestBoundary.RejectAsync(context, 415, "unsupported_cited_content_type");
            return Results.Empty;
        }
        T? body;
        try { body = await context.Request.ReadFromJsonAsync<T>(context.RequestAborted); }
        // Framework implicit binding logs JsonException paths, which can contain private JSON keys.
        // Parse explicitly and discard diagnostics instead of logging attacker-controlled text.
        catch (System.Text.Json.JsonException) { body = default; }
        catch (InvalidOperationException) { body = default; }
        if (body is null)
        {
            await CitedRequestBoundary.RejectAsync(context, 400, "invalid_cited_request");
            return Results.Empty;
        }
        return await operation(body);
    }

    private static async Task<IResult> Execute(Func<Task<IResult>> operation)
    {
        try { return await operation(); }
        catch (CitedWorkflowException error)
        { return Results.Problem(statusCode: error.Status, title: error.Code, detail: error.Message); }
        catch (ArgumentException)
        { return Results.Problem(statusCode: 400, title: "invalid_cited_request", detail: "The cited request is invalid."); }
        catch (OperationCanceledException)
        { return Results.Problem(statusCode: 504, title: "cited_request_timeout", detail: "The cited request was cancelled or timed out."); }
    }

    public sealed record IndexBody(Guid DocumentId, Guid ReviewSessionId);
    public sealed record ReviewedBody(string Question, string RequestKey, IReadOnlyList<Guid> IndexIds);
    public sealed record SemanticBody(string Question, string RequestKey, IReadOnlyList<Guid> IndexIds, string Mode, int TopK = 5);
    public sealed record RetrieveBody(string Question, string RequestKey, IReadOnlyList<Guid> IndexIds, int TopK = 5);
}
