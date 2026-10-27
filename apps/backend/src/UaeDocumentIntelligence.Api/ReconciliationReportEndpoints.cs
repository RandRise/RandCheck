using UaeDocumentIntelligence.Application.Reconciliation;
using System.Text.Json;

namespace UaeDocumentIntelligence.Api;

public static partial class ReconciliationEndpoints
{
    private static readonly JsonSerializerOptions ReportJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new ReportDecimalConverter() }
    };
    private static async Task<IResult> ReportAsync(Guid caseId, Guid runId, HttpContext context,
        GetReconciliationReportHandler handler, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!TryReadEtag(context, out var expected, out var failure)) return failure!;
        try
        {
            var report = await handler.HandleAsync(caseId, runId, expected, cancellationToken);
            context.Response.Headers.ETag = $"\"{report.CaseVersion:D}\"";
            return Results.Json(report, ReportJson);
        }
        catch (ReconciliationWorkflowException exception) { return ToProblem(exception); }
    }
}
