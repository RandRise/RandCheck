using Microsoft.AspNetCore.Routing;

namespace UaeDocumentIntelligence.Api.Security;

public static class RequestBodyLimit
{
    public const int JsonBytes = 64 * 1024;
    // Ten MiB file allowance plus bounded multipart framing, with a total cap even
    // for multiple sections and unknown-length bodies. The existing file cap stays.
    public const int UploadBytes = 10 * 1024 * 1024 + JsonBytes;
    public static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(15);

    public static async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!context.RequestServices.GetRequiredService<RequestLimitOptions>().Enabled ||
            !context.Request.Path.StartsWithSegments("/api")) { await next(context); return; }
        var limit = HttpMethods.IsPost(context.Request.Method) && (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText == "/api/documents"
            ? UploadBytes : JsonBytes;
        if (context.Request.ContentLength > limit) { await Reject(context, 413); return; }
        var original = context.Request.Body;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        deadline.CancelAfter(ReadTimeout);
        using var bounded = new BoundedBody(original, limit, deadline.Token);
        context.Request.Body = bounded;
        try { await next(context); }
        catch (BadHttpRequestException error) when (error.StatusCode == 413 && !context.Response.HasStarted)
        { await Reject(context, 413); }
        catch (OperationCanceledException) when (bounded.ReadTimedOut && !context.RequestAborted.IsCancellationRequested && !context.Response.HasStarted)
        { await Reject(context, 408); }
        finally { context.Request.Body = original; }
    }
    private static Task Reject(HttpContext context, int status) => Results.Problem(statusCode: status,
        title: status == 413 ? "request_body_too_large" : "request_body_read_timeout").ExecuteAsync(context);

    private sealed class BoundedBody(Stream inner, long limit, CancellationToken deadline) : Stream
    {
        private long _read;
        public bool ReadTimedOut { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Synchronous request reads are disabled.");
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline, cancellationToken);
            int count;
            try { count = await inner.ReadAsync(buffer[..(int)Math.Min(buffer.Length, limit + 1 - _read)], linked.Token); }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested) { ReadTimedOut = true; throw; }
            _read += count;
            if (_read > limit) throw new BadHttpRequestException("Request body exceeds the limit.", 413);
            return count;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        // The server owns the original request stream.
    }
}
