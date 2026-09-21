using System.Diagnostics.Metrics;
using System.Globalization;
using Microsoft.AspNetCore.Routing;

namespace UaeDocumentIntelligence.Api.Security;

public static class RequestLimitMiddleware
{
    private static readonly Meter Meter = new("DocumentIntelligence.RequestLimits");
    private static readonly Counter<long> Rejections = Meter.CreateCounter<long>("request_limit_rejections");

    public static async Task IngressAsync(HttpContext context, RequestDelegate next)
    {
        var options = context.RequestServices.GetRequiredService<RequestLimitOptions>();
        if (!options.Enabled || !context.Request.Path.StartsWithSegments("/api")) { await next(context); return; }
        // Use the transport peer only. Forwarded headers are not trusted by this app.
        var address = context.Connection.RemoteIpAddress;
        if (address?.IsIPv4MappedToIPv6 == true) address = address.MapToIPv4();
        var budgets = context.RequestServices.GetRequiredService<RequestBudgets>();
        using var lease = budgets.TryAcquire([
            new("global", options.GlobalRequests, options.GlobalConcurrent),
            new("ip:" + (address?.ToString() ?? "unknown"), options.IpRequests, options.IpConcurrent)], out var retry);
        if (lease is null) { await Reject(context, retry, "ingress"); return; }
        await next(context);
    }

    public static async Task AuthorizedAsync(HttpContext context, RequestDelegate next)
    {
        var options = context.RequestServices.GetRequiredService<RequestLimitOptions>();
        if (!options.Enabled || !context.Request.Path.StartsWithSegments("/api")) { await next(context); return; }
        var identity = context.RequestServices.GetRequiredService<RequestIdentity>();
        // This middleware follows authorization, which alone binds these IDs.
        var organization = identity.OrganizationId.ToString("N");
        var actor = organization + ":" + identity.ActorId.ToString("N");
        List<RequestBudget> limits = [new("org:" + organization, options.OrganizationRequests, options.OrganizationConcurrent),
            new("actor:" + actor, options.ActorRequests, options.ActorConcurrent)];
        var template = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) ||
            template == "/api/documents/{documentId:guid}/source")
        {
            limits.Add(new("org-work:" + organization, options.OrganizationWorkRequests, options.OrganizationWorkConcurrent));
            limits.Add(new("actor-work:" + actor, options.ActorWorkRequests, options.ActorWorkConcurrent));
        }
        using var lease = context.RequestServices.GetRequiredService<RequestBudgets>().TryAcquire(limits, out var retry);
        if (lease is null) { await Reject(context, retry, "authorized"); return; }
        await next(context);
    }

    private static Task Reject(HttpContext context, int retry, string stage)
    {
        Rejections.Add(1, new KeyValuePair<string, object?>("stage", stage));
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers.RetryAfter = retry.ToString(CultureInfo.InvariantCulture);
        return Results.Problem(statusCode: 429, title: "request_limit_exceeded",
            detail: "Too many requests. Wait before trying again.").ExecuteAsync(context);
    }
}
