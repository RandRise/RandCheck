using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using UaeDocumentIntelligence.Application.Abstractions;

namespace UaeDocumentIntelligence.Api.Security;

// Persist admission before body parsing or endpoint execution. This is evidence of
// an admitted attempt, never evidence that the requested business change committed.
public static class SecurityAuditAdmission
{
    public static async Task InvokeAsync(HttpContext http, RequestDelegate next)
    {
        var permission = EnterprisePermissions.Permission(http);
        if (http.RequestServices.GetRequiredService<EnterpriseSecurityOptions>().Mode != "Entra" ||
            permission is null || (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method) ||
                HttpMethods.IsOptions(http.Request.Method)) && permission != "SourceRead")
        {
            await next(http);
            return;
        }
        var audit = http.RequestServices.GetRequiredService<SecurityAuditContext>();
        try
        {
            if (!audit.IsBound) throw new InvalidOperationException("Audit admission requires a resolved identity.");
            await http.RequestServices.GetRequiredService<ISecurityAuditSink>().WriteAsync(
                audit.Event("request.admitted", SecurityAuditTarget.Type(http), SecurityAuditTarget.Id(http),
                    "Succeeded", "authorized_" + SecurityAuditOperation.Name(http), http.TraceIdentifier), http.RequestAborted);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            await Results.Problem(statusCode: 503, title: "Security audit unavailable").ExecuteAsync(http);
            return;
        }
        await next(http);
    }
}

internal static class SecurityAuditOperation
{
    // Fixed vocabulary from server templates only. A newly authorized mutation must
    // acquire an operation name before admission; never fall back to client paths.
    private static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>
    {
        ["POST /api/organization/memberships"] = "membership-grant",
        ["PUT /api/organization/memberships/{id:guid}"] = "membership-change",
        ["POST /api/organization/retention/documents/{id:guid}/deletion"] = "document-delete",
        ["PUT /api/organization/retention"] = "retention-change",
        ["POST /api/organization/retention/holds"] = "hold-place",
        ["POST /api/organization/retention/holds/{id:guid}/release"] = "hold-release",
        ["POST /api/organization/quarantine/cleanup"] = "quarantine-cleanup",
        ["POST /api/documents/{documentId:guid}/source-access"] = "source-grant",
        ["GET /api/documents/{documentId:guid}/source"] = "source-download",
        ["POST /api/cited-retrievals"] = "evidence-retrieve",
        ["POST /api/cited-questions"] = "question-ask",
        ["POST /api/cited-questions/reviewed"] = "question-reviewed",
        ["POST /api/cited-questions/semantic"] = "question-semantic",
        ["POST /api/cited-indexes"] = "evidence-index",
        ["POST /api/cited-cases/{id:guid}/sources"] = "evidence-prepare",
        ["POST /api/documents/{documentId:guid}/reviews"] = "review-start",
        ["POST /api/documents/{documentId:guid}/reviews/{reviewId:guid}/actions"] = "review-action",
        ["POST /api/documents/{documentId:guid}/reviews/{reviewId:guid}/complete"] = "review-complete",
        ["POST /api/reconciliation-cases"] = "case-create",
        ["PUT /api/reconciliation-cases/{caseId:guid}"] = "case-metadata-update",
        ["PUT /api/reconciliation-cases/{caseId:guid}/documents/{role}"] = "case-link",
        ["PUT /api/reconciliation-cases/{caseId:guid}/draft-documents/{role}"] = "case-draft-change",
        ["POST /api/reconciliation-cases/{caseId:guid}/evaluations"] = "case-evaluate",
        ["POST /api/reconciliation-cases/{caseId:guid}/runs/{runId:guid}/findings/{findingId:guid}/resolutions"] = "finding-resolve",
        ["POST /api/documents"] = "document-upload",
        ["POST /api/documents/{documentId:guid}/reprocess"] = "document-reprocess",
        ["POST /api/extraction-jobs/{jobId:guid}/cancel"] = "processing-cancel"
    };

    public static string Name(HttpContext http) => Names.TryGetValue(
        $"{http.Request.Method} {(http.GetEndpoint() as Microsoft.AspNetCore.Routing.RouteEndpoint)?.RoutePattern.RawText}", out var name)
        ? name : throw new InvalidOperationException("Audit operation is not classified.");
}

public sealed class AuditedAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();
    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy,
        PolicyAuthorizationResult result)
    {
        if (!result.Succeeded && context.RequestServices.GetRequiredService<EnterpriseSecurityOptions>().Mode == "Entra")
        {
            var audit = context.RequestServices.GetRequiredService<SecurityAuditContext>();
            try
            {
                await context.RequestServices.GetRequiredService<ISecurityAuditSink>().WriteAsync(
                    audit.Event("access.denied", SecurityAuditTarget.Type(context), SecurityAuditTarget.Id(context), "Denied",
                        result.Challenged ? "unauthenticated" : "not_authorized", context.TraceIdentifier), context.RequestAborted);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                await Results.Problem(statusCode: 503, title: "Security audit unavailable").ExecuteAsync(context);
                return;
            }
        }
        await _default.HandleAsync(next, context, policy, result);
    }
}

// Successful mutation events are saved in the business transaction. This filter
// records failed admitted operations independently, before writing the response.
public sealed class SecurityAuditOutcomeFilter(EnterpriseSecurityOptions settings,
    SecurityAuditContext audit, ISecurityAuditSink sink) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext invocation, EndpointFilterDelegate next)
    {
        var http = invocation.HttpContext;
        if (settings.Mode != "Entra" || http.Request.Method is "GET" or "HEAD" or "OPTIONS") return await next(invocation);
        object? result;
        try { result = await next(invocation); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { result = Results.Problem(statusCode: 503, title: "Operation could not be completed"); }
        if (result is IStatusCodeHttpResult { StatusCode: >= 400 } status)
        {
            try
            {
                await sink.WriteAsync(audit.Event("request.failed", SecurityAuditTarget.Type(http), SecurityAuditTarget.Id(http), "Failed",
                    $"http_{status.StatusCode}", http.TraceIdentifier), http.RequestAborted);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            { return Results.Problem(statusCode: 503, title: "Security audit unavailable"); }
        }
        return result;
    }
}

internal static class SecurityAuditTarget
{
    // Classify using the server-owned route template. Never persist raw URL/query/body.
    public static string Type(HttpContext http)
    {
        var template = (http.GetEndpoint() as Microsoft.AspNetCore.Routing.RouteEndpoint)?.RoutePattern.RawText ?? "";
        if (EnterprisePermissions.Permission(http) is null) return "unclassified-api";
        return template.Split('/', StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault() is { } area
            ? (area == "organization" ? "organization-access" : area) : "api-endpoint";
    }
    public static Guid? Id(HttpContext http)
    {
        foreach (var name in new[] { "findingId", "hitId", "reviewId", "jobId", "id", "caseId", "documentId" })
            if (Guid.TryParse(http.Request.RouteValues[name]?.ToString(), out var id) && id != Guid.Empty) return id;
        return null;
    }
}
