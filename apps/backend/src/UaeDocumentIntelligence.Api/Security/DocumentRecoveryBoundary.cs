using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Api.Security;

public sealed class DocumentRecoveryBoundary : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (EnterprisePermissions.Permission(context.HttpContext) is not ("Retention" or "ManageMembers" or "Audit" or "Session"))
        {
            try
            {
                var services = context.HttpContext.RequestServices;
                await services.GetRequiredService<DocumentRecoveryGuard>().VerifyAsync(
                    services.GetRequiredService<IExecutionContext>().OrganizationId, false, context.HttpContext.RequestAborted);
            }
            catch (OperationCanceledException) { throw; }
            catch { return Results.Problem(statusCode: 503, title: "document_recovery_verification_required"); }
        }
        return await next(context);
    }
}
