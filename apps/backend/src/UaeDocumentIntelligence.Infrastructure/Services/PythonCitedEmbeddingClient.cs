using System.Net;
using Microsoft.Extensions.Options;
using UaeDocumentIntelligence.Application.CitedQuestions;

namespace UaeDocumentIntelligence.Infrastructure.Services;

internal sealed class PythonCitedEmbeddingClient(HttpClient client, IOptions<AiServiceOptions> options) : ICitedEmbeddingClient
{
    public Task<byte[]> PrepareAsync(byte[] source, CancellationToken ct) => Send("embedding-prepare", source, 49152, 8192, ct);
    public Task<byte[]> ProposeAsync(byte[] dispatch, CancellationToken ct) => Send("embedding-proposal", dispatch, 98304, 524288, ct);

    private async Task<byte[]> Send(string path, byte[] body, int maxInput, int maxOutput, CancellationToken ct)
    {
        if (options.Value.AuthenticationMode != "Development" ||
            body.Length is < 1 || body.Length > maxInput || string.IsNullOrWhiteSpace(options.Value.WorkerToken) ||
            client.BaseAddress is not { } address || address.Scheme != "http" ||
            !IPAddress.TryParse(address.Host, out var ip) || !IPAddress.IsLoopback(ip)) throw Failure();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(40));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/cited/" + path)
                { Content = new ByteArrayContent(body) };
            request.Content.Headers.ContentType = new("application/json");
            request.Headers.TryAddWithoutValidation("X-Worker-Token", options.Value.WorkerToken);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (response.StatusCode != HttpStatusCode.OK || response.Content.Headers.ContentLength > maxOutput ||
                response.Content.Headers.ContentType?.MediaType != "application/json" ||
                response.Content.Headers.ContentEncoding.Count != 0) throw Failure();
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var result = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(buffer, deadline.Token)) != 0)
            {
                if (result.Length + read > maxOutput) throw Failure();
                result.Write(buffer, 0, read);
            }
            if (result.Length == 0) throw Failure();
            return result.ToArray();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { throw Failure(); } // Fixed error only; no HTTP body, headers, request or secrets in errors/logs.
    }
    private static CitedWorkflowException Failure() => new("private_embedding_service_failed", "The private embedding service did not return an accepted response.");
}
