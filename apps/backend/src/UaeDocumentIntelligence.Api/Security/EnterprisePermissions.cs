using Microsoft.AspNetCore.Routing;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Api.Security;

public static class EnterprisePermissions
{
    // Exact endpoint templates, never raw client paths. New routes deny until classified.
    public static readonly IReadOnlyDictionary<string, string> Routes = new Dictionary<string, string>
    {
        ["GET /api/session"] = "Session",
        ["GET /api/organization/memberships"] = "ManageMembers",
        ["POST /api/organization/memberships"] = "ManageMembers",
        ["PUT /api/organization/memberships/{id:guid}"] = "ManageMembers",
        ["GET /api/organization/audit"] = "Audit",
        ["GET /api/organization/retention"] = "Retention",
        ["GET /api/organization/retention/deletions"] = "Retention",
        ["GET /api/organization/retention/documents/{id:guid}/deletion"] = "Retention",
        ["POST /api/organization/retention/documents/{id:guid}/deletion"] = "Retention",
        ["PUT /api/organization/retention"] = "Retention",
        ["GET /api/organization/retention/preview"] = "Retention",
        ["GET /api/organization/retention/holds"] = "Retention",
        ["POST /api/organization/retention/holds"] = "Retention",
        ["POST /api/organization/retention/holds/{id:guid}/release"] = "Retention",
        ["POST /api/organization/quarantine/cleanup"] = "Retention",
        ["GET /api/documents"] = "Read",
        ["GET /api/documents/{id:guid}"] = "Read",
        ["POST /api/documents/{documentId:guid}/source-access"] = "SourceRead",
        ["GET /api/documents/{documentId:guid}/source"] = "SourceRead",
        ["GET /api/metrics"] = "Read",
        ["GET /api/documents/{documentId:guid}/review"] = "Read",
        ["GET /api/documents/{documentId:guid}/review-history"] = "Read",
        ["GET /api/documents/{documentId:guid}/processing"] = "Read",
        ["GET /api/reconciliation-cases/{caseId:guid}"] = "Read",
        ["GET /api/cited-retrievals/{id:guid}"] = "Read",
        ["GET /api/cited-questions/{id:guid}"] = "Read",
        ["GET /api/cited-sources"] = "Read",
        ["GET /api/cited-questions/{id:guid}/hits/{hitId:guid}/evidence"] = "Read",
        ["POST /api/cited-retrievals"] = "Ask",
        ["POST /api/cited-questions"] = "Ask",
        ["POST /api/cited-questions/reviewed"] = "Ask",
        ["POST /api/cited-questions/semantic"] = "Ask",
        ["POST /api/cited-indexes"] = "Review",
        ["POST /api/cited-cases/{id:guid}/sources"] = "Review",
        ["POST /api/documents/{documentId:guid}/reviews"] = "Review",
        ["POST /api/documents/{documentId:guid}/reviews/{reviewId:guid}/actions"] = "Review",
        ["POST /api/documents/{documentId:guid}/reviews/{reviewId:guid}/complete"] = "Review",
        ["POST /api/reconciliation-cases"] = "Review",
        ["PUT /api/reconciliation-cases/{caseId:guid}/documents/{role}"] = "Review",
        ["POST /api/reconciliation-cases/{caseId:guid}/evaluations"] = "Review",
        ["POST /api/reconciliation-cases/{caseId:guid}/runs/{runId:guid}/findings/{findingId:guid}/resolutions"] = "Review",
        ["POST /api/documents"] = "Operate",
        ["POST /api/documents/{documentId:guid}/reprocess"] = "Operate",
        ["POST /api/extraction-jobs/{jobId:guid}/cancel"] = "Operate"
    };

    public static string? Permission(HttpContext context) =>
        Routes.GetValueOrDefault($"{context.Request.Method} {(context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText}");

    public static bool Allows(HttpContext context, OrganizationAccessRole role, bool canManageMembers) =>
        Permission(context) switch
        {
            "Session" => Enum.IsDefined(role),
            "ManageMembers" or "Audit" or "Retention" => canManageMembers,
            "Read" or "Ask" or "SourceRead" => role is OrganizationAccessRole.Viewer or OrganizationAccessRole.Reviewer or OrganizationAccessRole.Operator,
            "Review" => role is OrganizationAccessRole.Reviewer or OrganizationAccessRole.Operator,
            "Operate" => role is OrganizationAccessRole.Operator,
            _ => false
        };
}
