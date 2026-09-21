namespace UaeDocumentIntelligence.Api;

public static class CitedRequestBoundary
{
    public const int MaxBodyBytes = 64 * 1024;
    public static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(15);
    private static readonly string[] Paths =
        ["/api/cited-indexes", "/api/cited-retrievals", "/api/cited-questions", "/api/cited-sources", "/api/cited-cases"];

    public static IApplicationBuilder UseCitedResponsePolicy(this IApplicationBuilder app) =>
        app.Use(InvokeAsync);

    public static async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!Paths.Any(path => context.Request.Path.StartsWithSegments(path)))
        {
            await next(context);
            return;
        }

        context.Response.Headers.CacheControl = "no-store";
        if (context.Request.ContentLength > MaxBodyBytes)
        {
            await RejectAsync(context, 413, "cited_request_too_large");
            return;
        }

        // Bound actual bytes, including chunked/unknown-length bodies. Never spool to disk.
        // Leave the host's separate wire/protocol limit unchanged: setting it to the
        // payload limit also rejects exact-limit chunked requests on Kestrel.
        var original = context.Request.Body;
        using var buffered = new MemoryStream(MaxBodyBytes + 1);
        using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted))
        {
            deadline.CancelAfter(ReadTimeout);
            var scratch = new byte[8192];
            try
            {
                while (true)
                {
                    var count = await original.ReadAsync(scratch.AsMemory(0,
                        Math.Min(scratch.Length, MaxBodyBytes + 1 - (int)buffered.Length)), deadline.Token);
                    if (count == 0) break;
                    buffered.Write(scratch, 0, count);
                    if (buffered.Length > MaxBodyBytes)
                    {
                        await RejectAsync(context, 413, "cited_request_too_large");
                        return;
                    }
                }
            }
            catch (BadHttpRequestException error) when (error.StatusCode == 413)
            {
                await RejectAsync(context, 413, "cited_request_too_large");
                return;
            }
            catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested && deadline.IsCancellationRequested)
            {
                await RejectAsync(context, 408, "cited_request_read_timeout");
                return;
            }
        }

        buffered.Position = 0;
        context.Request.Body = buffered;
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            // An unexpected dependency/serialization error can contain private content.
            // Do not send that exception to framework error-page or exception logging.
            if (context.Response.HasStarted)
            {
                LogRejection(context, 500, "cited_response_aborted");
                context.Abort();
            }
            else await RejectAsync(context, 500, "cited_request_failed");
        }
        finally { context.Request.Body = original; }
    }

    public static async Task RejectAsync(HttpContext context, int status, string code)
    {
        // Do not attach payloads, headers, paths, or parsing exceptions to rejection logs.
        LogRejection(context, status, code);
        await Results.Problem(statusCode: status, title: code,
            detail: status == 413 ? "The cited request body exceeds the 64 KiB limit." :
                status == 408 ? "The cited request body was not received in time." :
                status == 500 ? "The cited request could not be completed." :
                "The cited request body is invalid or uses an unsupported content type.")
            .ExecuteAsync(context);
    }

    private static void LogRejection(HttpContext context, int status, string code) =>
        context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("CitedRequestBoundary")
            .Log(status >= 500 ? LogLevel.Error : LogLevel.Warning, new EventId(5108, "CitedRequestRejected"),
                "Cited request rejected: {Code}; status {Status}", code, status);
}
