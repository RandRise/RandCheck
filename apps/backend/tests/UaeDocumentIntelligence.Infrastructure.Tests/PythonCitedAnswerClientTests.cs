using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using UaeDocumentIntelligence.Infrastructure.Services;
using UaeDocumentIntelligence.Application.CitedQuestions;

namespace UaeDocumentIntelligence.Infrastructure.Tests;

public sealed class PythonCitedAnswerClientTests
{
    private const string Good = """{"schemaVersion":"1.0","requestSha256":"hash","promptSha256":"prompt-hash","model":{"providerAlias":"test-only","model":"fake","version":"1"},"outcome":"completed","proposalUtf8":"e30=","inputTokens":100,"outputTokens":50}""";

    [Fact]
    public async Task Answer_transport_preserves_exact_bytes_token_and_route()
    {
        var transport = new AnswerTransport(new StringContent(Good));
        var result = await Client(transport).ProposeAsync("{\"question\":\"é total?\"}"u8.ToArray(), default);
        Assert.Equal("/internal/cited/answer-proposal", transport.Path);
        Assert.Equal("test-only-token", transport.Token);
        Assert.Equal("{\"question\":\"é total?\"}", transport.Body);
        Assert.Equal("{}", Encoding.UTF8.GetString(result.ProposalUtf8));
        Assert.Equal(1, transport.Calls);
        Assert.DoesNotContain("hash", result.ToString());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"schemaVersion\":\"SECRET\"}")]
    [InlineData("{\"inputTokens\":1,\"inputTokens\":2}")]
    [InlineData("SECRET NOT JSON")]
    public async Task Invalid_answer_envelopes_do_not_expose_response(string body)
    {
        var transport = new AnswerTransport(new StringContent(body));
        var error = await Assert.ThrowsAsync<CitedWorkflowException>(() => Client(transport).ProposeAsync("{}"u8.ToArray(), default));
        Assert.Equal("invalid_private_result", error.Code);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("SECRET", error.Message);
        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    public async Task Oversized_request_is_not_sent()
    {
        var transport = new AnswerTransport(new StringContent(Good));
        await Assert.ThrowsAsync<CitedWorkflowException>(() => Client(transport).ProposeAsync(new byte[98305], default));
        Assert.Equal(0, transport.Calls);
    }

    [Fact]
    public async Task Oversized_stream_without_content_length_is_rejected()
    {
        var transport = new AnswerTransport(new UnknownLengthContent(new byte[131073]));
        await Assert.ThrowsAsync<CitedWorkflowException>(() => Client(transport).ProposeAsync("{}"u8.ToArray(), default));
        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    public async Task Missing_secret_and_caller_cancellation_do_not_retry()
    {
        var transport = new AnswerTransport(new StringContent(Good));
        var unconfigured = new PythonCitedAiClient(new HttpClient(transport), Options.Create(new AiServiceOptions()));
        await Assert.ThrowsAsync<CitedWorkflowException>(() => unconfigured.ProposeAsync("{}"u8.ToArray(), default));
        Assert.Equal(0, transport.Calls);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(transport).ProposeAsync("{}"u8.ToArray(), cancelled.Token));
        Assert.InRange(transport.Calls, 0, 1);
    }

    [Fact]
    public async Task Private_failure_body_is_not_read_or_retried()
    {
        var transport = new AnswerTransport(new StringContent("SECRET"), HttpStatusCode.ServiceUnavailable);
        var error = await Assert.ThrowsAsync<CitedWorkflowException>(() => Client(transport).ProposeAsync("{}"u8.ToArray(), default));
        Assert.Equal("private_service_rejected", error.Code);
        Assert.DoesNotContain("SECRET", error.Message);
        Assert.Equal(1, transport.Calls);
    }

    private static PythonCitedAiClient Client(AnswerTransport transport) => new(
        new HttpClient(transport) { BaseAddress = new Uri("http://127.0.0.1:1") },
        Options.Create(new AiServiceOptions { WorkerToken = "test-only-token" }));

    [Fact]
    public async Task Preparation_uses_separate_endpoint_and_preserves_receipt()
    {
        const string receipt = """{"schemaVersion":"1.0","requestSha256":"source","promptSha256":"prompt","model":{"providerAlias":"test-only","model":"fake","version":"1"},"promptVersion":"cited-answer-prompt-v1","counterVersion":"test-utf8-bytes-v1","inputTokens":1234,"maximumOutputTokens":4000}""";
        var transport = new AnswerTransport(new StringContent(receipt));
        var result = await Client(transport).PrepareAsync("{}"u8.ToArray(), default);
        Assert.Equal("/internal/cited/answer-prepare", transport.Path);
        Assert.Equal("test-only-token", transport.Token);
        Assert.Equal(1234, result.InputTokens);
        Assert.Equal("prompt", result.PromptSha256);
        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    public async Task Preparation_request_limit_applies_before_transport()
    {
        var transport = new AnswerTransport(new StringContent("{}"));
        await Assert.ThrowsAsync<CitedWorkflowException>(() => Client(transport).PrepareAsync(new byte[65537], default));
        Assert.Equal(0, transport.Calls);
    }

    [Fact]
    public async Task Preparation_response_stream_is_bounded_to_small_receipt()
    {
        var transport = new AnswerTransport(new UnknownLengthContent(new byte[4097]));
        await Assert.ThrowsAsync<CitedWorkflowException>(() => Client(transport).PrepareAsync("{}"u8.ToArray(), default));
        Assert.Equal(1, transport.Calls);
    }

    [Theory]
    [InlineData("{\"inputTokens\":true}")]
    [InlineData("{\"promptVersion\":\"v1\",\"promptVersion\":\"v2\"}")]
    [InlineData("SECRET INVALID RECEIPT")]
    public async Task Preparation_malformed_receipt_is_rejected_without_echo(string body)
    {
        var transport = new AnswerTransport(new StringContent(body));
        var error = await Assert.ThrowsAsync<CitedWorkflowException>(() => Client(transport).PrepareAsync("{}"u8.ToArray(), default));
        Assert.Equal("invalid_private_result", error.Code);
        Assert.DoesNotContain("SECRET", error.Message);
        Assert.Null(error.InnerException);
    }

    private sealed class AnswerTransport(HttpContent content, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int Calls;
        public string? Path, Token, Body;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            ct.ThrowIfCancellationRequested();
            Path = request.RequestUri!.AbsolutePath;
            Token = request.Headers.GetValues("X-Worker-Token").Single();
            Body = await request.Content!.ReadAsStringAsync(ct);
            return new(status) { Content = content };
        }
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(bytes));
    }
}
