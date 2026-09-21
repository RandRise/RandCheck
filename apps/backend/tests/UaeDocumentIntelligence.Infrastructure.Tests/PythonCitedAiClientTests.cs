using System.Net;
using Microsoft.Extensions.Options;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Infrastructure.Tests;

public sealed class PythonCitedAiClientTests
{
    [Fact]
    public async Task Private_token_and_endpoint_are_used_without_source_paths()
    {
        var transport = new Reply("""{"schemaVersion":"1.0","policyVersion":"lexical-or-english-v1","questionSha256":"hash","configuration":"english","searchText":"total","topK":5}""");
        var client = Client(transport);
        var result = await client.PlanAsync("total", 5, default);
        Assert.Equal("lexical-or-english-v1", result.PolicyVersion);
        Assert.Equal("/internal/cited/lexical-plan", transport.Path);
        Assert.Equal("test-only-token", transport.Token);
        Assert.DoesNotContain("filePath", transport.Body);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("not JSON")]
    [InlineData("{\"unknown\":1}")]
    public async Task Malformed_results_are_safe_failures(string body)
    {
        var error = await Assert.ThrowsAsync<CitedWorkflowException>(() => Client(new Reply(body)).PlanAsync("secret question", 5, default));
        Assert.Equal("invalid_private_result", error.Code);
        Assert.DoesNotContain("secret", error.Message);
    }

    [Fact]
    public async Task Unconfigured_client_never_dispatches()
    {
        var transport = new Reply("{}");
        var client = new PythonCitedAiClient(new HttpClient(transport), Options.Create(new AiServiceOptions()));
        await Assert.ThrowsAsync<CitedWorkflowException>(() => client.PlanAsync("x", 5, default));
        Assert.Null(transport.Path);
    }

    [Fact]
    public async Task Error_body_is_not_exposed()
    {
        var error = await Assert.ThrowsAsync<CitedWorkflowException>(() =>
            Client(new Reply("SECRET SOURCE", HttpStatusCode.InternalServerError)).PlanAsync("x", 5, default));
        Assert.DoesNotContain("SECRET", error.Message);
        Assert.Equal(502, error.Status);
    }

    private static PythonCitedAiClient Client(Reply reply) => new(
        new HttpClient(reply) { BaseAddress = new Uri("http://localhost:8001") },
        Options.Create(new AiServiceOptions { WorkerToken = "test-only-token" }));

    private sealed class Reply(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string? Path, Token, Body;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Path = request.RequestUri!.AbsolutePath;
            Token = request.Headers.GetValues("X-Worker-Token").Single();
            Body = await request.Content!.ReadAsStringAsync(ct);
            return new(status) { Content = new StringContent(body) };
        }
    }
}
