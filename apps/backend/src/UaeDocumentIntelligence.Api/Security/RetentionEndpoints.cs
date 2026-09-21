using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Api.Security;

public static class RetentionEndpoints
{
    public static IEndpointRouteBuilder MapRetentionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints = endpoints.MapGroup("").AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var environment = http.RequestServices.GetRequiredService<IHostEnvironment>();
            if (!http.Request.IsHttps && !environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
                return Results.Problem(statusCode: 503, title: "retention_requires_https");
            return await next(context);
        });
        endpoints.MapGet("/api/organization/retention", (EnterpriseRetentionService service, CancellationToken ct) => Run(async () => Results.Ok(await service.PolicyAsync(ct))));
        endpoints.MapGet("/api/organization/retention/deletions", (int? offset, EnterpriseRetentionService service, CancellationToken ct) =>
            Run(async () => Results.Ok(await service.DeletionsAsync(offset ?? 0, ct))));
        endpoints.MapGet("/api/organization/retention/documents/{id:guid}/deletion", (Guid id, EnterpriseRetentionService service, CancellationToken ct) =>
            Run(async () => Results.Ok(await service.DeletionPreviewAsync(id, ct))));
        endpoints.MapPost("/api/organization/retention/documents/{id:guid}/deletion", (Guid id, DeleteDocument body, EnterpriseRetentionService service, CancellationToken ct) =>
            Run(async () => Results.Ok(await service.DeleteDocumentAsync(id, body.ExpectedDocumentVersion, body.ExpectedPolicyVersion, ct))));
        endpoints.MapPut("/api/organization/retention", (ChangePolicy body, EnterpriseRetentionService service, CancellationToken ct) =>
            Run(async () => Results.Ok(await service.ChangePolicyAsync(body.ExpectedVersion, body.DocumentDays, body.QuarantineHours, ct))));
        endpoints.MapGet("/api/organization/retention/preview", (int? offset, EnterpriseRetentionService service, CancellationToken ct) =>
            Run(async () => Results.Ok(await service.PreviewAsync(offset ?? 0, ct))));
        endpoints.MapGet("/api/organization/retention/holds", (int? offset, EnterpriseRetentionService service, CancellationToken ct) =>
            Run(async () => Results.Ok(await service.HoldsAsync(offset ?? 0, ct))));
        endpoints.MapPost("/api/organization/retention/holds", (PlaceHold body, EnterpriseRetentionService service, CancellationToken ct) => Run(async () =>
        {
            if (!Enum.TryParse<RetentionHoldReason>(body.Reason, false, out var reason) || !Enum.IsDefined(reason) || reason.ToString() != body.Reason)
                return Results.Problem(statusCode: 400, title: "invalid_hold_reason");
            return Results.Json(await service.PlaceHoldAsync(body.DocumentId, reason, ct), statusCode: 201);
        }));
        endpoints.MapPost("/api/organization/retention/holds/{id:guid}/release", (Guid id, ReleaseHold body, EnterpriseRetentionService service, CancellationToken ct) =>
            Run(async () => Results.Ok(await service.ReleaseHoldAsync(id, body.ExpectedVersion, ct))));
        endpoints.MapPost("/api/organization/quarantine/cleanup", (Cleanup body, EnterpriseRetentionService service, CancellationToken ct) =>
            Run(async () => Results.Ok(await service.CleanupAsync(body.ExpectedPolicyVersion, body.Execute, ct))));
        return endpoints;
    }
    public sealed record ChangePolicy(Guid ExpectedVersion, int? DocumentDays, int? QuarantineHours);
    public sealed record DeleteDocument(Guid ExpectedDocumentVersion, Guid ExpectedPolicyVersion);
    public sealed record PlaceHold(Guid? DocumentId, string Reason);
    public sealed record ReleaseHold(Guid ExpectedVersion);
    public sealed record Cleanup(Guid ExpectedPolicyVersion, bool Execute = false);
    private static async Task<IResult> Run(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (RetentionException e) { return Results.Problem(statusCode: e.Status, title: e.Code); }
        catch (ArgumentException) { return Results.Problem(statusCode: 400, title: "invalid_retention_request"); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return Results.Problem(statusCode: 503, title: "retention_unavailable"); }
    }
}
