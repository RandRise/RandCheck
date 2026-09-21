using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Infrastructure.Tests;

public class PythonCitedEmbeddingClientTests
{
    [Fact]
    public async Task Embedding_transport_preserves_exact_bytes_and_uses_authenticated_private_path()
    {
        var transport = new Handler();
        var result = await Client(transport).PrepareAsync("{\"source\":\"é\"}"u8.ToArray(), default);
        Assert.Equal("{}"u8.ToArray(), result);
        Assert.Equal("/internal/cited/embedding-prepare", transport.Path);
        Assert.Equal("test-secret", transport.Token);
        Assert.Equal("{\"source\":\"é\"}"u8.ToArray(), transport.Body);
        Assert.Equal(1, transport.Calls);
    }

    [Theory]
    [InlineData("redirect")]
    [InlineData("oversize")]
    [InlineData("type")]
    [InlineData("encoding")]
    [InlineData("error")]
    public async Task Embedding_transport_rejects_invalid_responses_without_exposing_body(string mode)
    {
        var transport = new Handler(mode);
        var e = await Assert.ThrowsAsync<CitedWorkflowException>(() => Client(transport).ProposeAsync("{}"u8.ToArray(), default));
        Assert.Equal(1, transport.Calls);
        Assert.DoesNotContain("PRIVATE", e.ToString());
        Assert.Null(e.InnerException);
    }

    [Fact]
    public async Task Embedding_transport_rejects_non_loopback_and_oversized_sources_without_sending()
    {
        var handler = new Handler();
        await Assert.ThrowsAsync<CitedWorkflowException>(() => Client(handler, "http://example.com").PrepareAsync("{}"u8.ToArray(), default));
        await Assert.ThrowsAsync<CitedWorkflowException>(() => Client(handler).PrepareAsync(new byte[49153], default));
        Assert.Equal(0, handler.Calls);
    }

    private static PythonCitedEmbeddingClient Client(Handler handler, string url = "http://127.0.0.1:1234") =>
        new(new HttpClient(handler) { BaseAddress = new Uri(url) }, Options.Create(new AiServiceOptions { WorkerToken = "test-secret" }));
    private sealed class Handler(string mode = "valid") : HttpMessageHandler
    {
        public int Calls;
        public string? Path;
        public string? Token;
        public byte[]? Body;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Path = request.RequestUri!.AbsolutePath;
            Token = request.Headers.GetValues("X-Worker-Token").Single();
            Body = await request.Content!.ReadAsByteArrayAsync(ct);
            if (mode == "error") throw new HttpRequestException("PRIVATE");
            var response = new HttpResponseMessage(mode == "redirect" ? HttpStatusCode.TemporaryRedirect : HttpStatusCode.OK)
                { Content = new ByteArrayContent(mode == "oversize" ? new byte[524289] : "{}"u8.ToArray()) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(mode == "type" ? "text/plain" : "application/json");
            if (mode == "encoding") response.Content.Headers.ContentEncoding.Add("gzip");
            return response;
        }
    }
}
