using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Infrastructure.Tests;

public sealed class PrivateServiceSecurityTests
{
    private static AiServiceOptions Configured() => new()
    {
        AuthenticationMode = "ServiceKeys", BaseUrl = "https://private.test",
        WorkerToken = "legacy-must-not-be-used",
        ExtractionCredential = new() { KeyId = "worker-key", Token = new('e', 43) },
        CitedCredential = new() { KeyId = "retrieval-key", Token = new('r', 43) },
        AnswerCredential = new() { KeyId = "answer-key", Token = new('a', 43) }
    };

    [Theory]
    [InlineData("/internal/extractions", "worker-key", 'e')]
    [InlineData("/internal/cited/chunks", "retrieval-key", 'r')]
    [InlineData("/internal/cited/lexical-plan", "retrieval-key", 'r')]
    [InlineData("/internal/cited/answer-prepare", "answer-key", 'a')]
    [InlineData("/internal/cited/answer-proposal", "answer-key", 'a')]
    public void Each_operation_uses_its_own_credential(string path, string keyId, char token)
    {
        using var client = new HttpClient { BaseAddress = new("https://private.test") };
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        Assert.True(PrivateServiceCredentials.TryApply(request, client, Configured()));
        Assert.Equal(keyId, Assert.Single(request.Headers.GetValues("X-Service-Key-Id")));
        Assert.Equal(new string(token, 43), Assert.Single(request.Headers.GetValues("X-Worker-Token")));
    }

    [Theory]
    [InlineData("http://private.test")]
    [InlineData("http://127.0.0.1")]
    [InlineData("https://name:password@private.test")]
    [InlineData("https://private.test/path")]
    [InlineData("https://private.test?metadata=secret")]
    [InlineData("https://private.test#fragment")]
    [InlineData("https://different.test")]
    public void Unsafe_or_different_origin_never_receives_a_credential(string origin)
    {
        using var client = new HttpClient { BaseAddress = new(origin) };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/extractions");
        Assert.False(PrivateServiceCredentials.TryApply(request, client, Configured()));
        Assert.Empty(request.Headers);
    }

    [Theory]
    [InlineData("https://different.test/internal/extractions")]
    [InlineData("/internal/extractions?token=secret")]
    [InlineData("/internal/extractions/")]
    [InlineData("/internal/unknown")]
    [InlineData("/internal/cited/embedding-proposal")]
    public void Unclassified_or_overridden_target_never_receives_credentials(string target)
    {
        using var client = new HttpClient { BaseAddress = new("https://private.test") };
        using var request = new HttpRequestMessage(HttpMethod.Post, target);
        Assert.False(PrivateServiceCredentials.TryApply(request, client, Configured()));
        Assert.Empty(request.Headers);
    }

    [Theory]
    [InlineData("", "Development", "Development")]
    [InlineData(null, "Testing", "Development")]
    [InlineData(null, "Production", "Locked")]
    [InlineData("Development", "Production", "Locked")]
    [InlineData("Development", "Staging", "Locked")]
    [InlineData("ServiceKeys", "Production", "ServiceKeys")]
    [InlineData("Servicekeys", "Production", "Locked")]
    public void Enterprise_mode_cannot_fall_back_to_development(string? mode, string environment, string expected) =>
        Assert.Equal(expected, AiServiceOptions.ResolveAuthenticationMode(mode, environment));

    [Theory]
    [InlineData("", "valid_key")]
    [InlineData("short", "valid_key")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "bad\nkey")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa+", "valid_key")]
    public void Missing_or_malformed_key_does_not_fall_back_to_shared_token(string token, string keyId)
    {
        var options = Configured();
        options.ExtractionCredential = new() { Token = token, KeyId = keyId };
        using var client = new HttpClient { BaseAddress = new("https://private.test") };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/extractions");
        Assert.False(PrivateServiceCredentials.TryApply(request, client, options));
        Assert.Empty(request.Headers);
    }

    [Fact]
    public async Task Real_cited_client_refuses_insecure_service_mode_before_dispatch()
    {
        using var handler = new NeverSend();
        using var http = new HttpClient(handler) { BaseAddress = new("http://127.0.0.1") };
        var client = new PythonCitedAiClient(http, Options.Create(Configured()));
        var error = await Assert.ThrowsAsync<CitedWorkflowException>(() => client.PlanAsync("private content", 5, default));
        Assert.Equal("private_service_unconfigured", error.Code);
        Assert.DoesNotContain("private content", error.ToString());
        Assert.DoesNotContain("legacy-must-not-be-used", error.ToString());
    }

    [Theory]
    [InlineData("IExtractionClient")]
    [InlineData("ICitedAiClient")]
    [InlineData("ICitedAnswerClient")]
    [InlineData("ICitedEmbeddingClient")]
    public void Every_registered_private_transport_disables_redirects_proxy_cookies_and_http_logging(string name)
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:Database"] = "Data Source=:memory:" }).Build());
        using var provider = services.BuildServiceProvider();
        var current = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(name);
        while (current is DelegatingHandler wrapper)
        {
            Assert.DoesNotContain("Logging", wrapper.GetType().FullName);
            current = wrapper.InnerHandler!;
        }
        var transport = Assert.IsType<HttpClientHandler>(current);
        Assert.False(transport.AllowAutoRedirect);
        Assert.False(transport.UseProxy);
        Assert.False(transport.UseCookies);
    }

    private sealed class NeverSend : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Credential rejection must precede network dispatch");
    }

    [Theory]
    [InlineData("Locked")]
    [InlineData("ServiceKeys")]
    public async Task Enterprise_mode_cannot_enable_the_local_embedding_pilot(string mode)
    {
        using var handler = new NeverSend();
        using var http = new HttpClient(handler) { BaseAddress = new("http://127.0.0.1") };
        var options = Configured();
        options.AuthenticationMode = mode;
        var client = new PythonCitedEmbeddingClient(http, Options.Create(options));
        await Assert.ThrowsAsync<CitedWorkflowException>(() => client.PrepareAsync("{}"u8.ToArray(), default));
    }
}
