using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace UaeDocumentIntelligence.Api.Tests;

public sealed class CitedRetrievalApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public CitedRetrievalApiTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(builder => builder.UsePhase5DevelopmentIdentity());

    [Theory]
    [InlineData("", 5)]
    [InlineData("total", 21)]
    [InlineData("total", 0)]
    public async Task Invalid_retrieval_is_rejected_before_any_database_or_provider_work(string question, int topK)
    {
        using var client = _factory.CreateClient();
        using var result = await client.PostAsJsonAsync("/api/cited-retrievals",
            new { question, topK, requestKey = "key", indexIds = new[] { Guid.NewGuid() } });
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Contains("invalid_cited_request", await result.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Empty_or_duplicate_corpus_does_not_widen_to_all_documents()
    {
        using var client = _factory.CreateClient();
        foreach (var ids in new[] { Array.Empty<Guid>(), new[] { Guid.Empty, Guid.Empty } })
        {
            using var result = await client.PostAsJsonAsync("/api/cited-retrievals",
                new { question = "total", topK = 5, requestKey = "key", indexIds = ids });
            Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        }
    }

    [Theory]
    [InlineData("", 5)]
    [InlineData("total", 0)]
    [InlineData("total", 21)]
    public async Task Question_entry_point_keeps_retrieval_input_bounds(string question, int topK)
    {
        using var client = _factory.CreateClient();
        using var result = await client.PostAsJsonAsync("/api/cited-questions",
            new { question, topK, requestKey = "key", indexIds = new[] { Guid.NewGuid() } });
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Contains("invalid_cited_request", await result.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task No_public_route_accepts_an_answer_proposal()
    {
        using var client = _factory.CreateClient();
        using var result = await client.PostAsJsonAsync($"/api/cited-questions/{Guid.NewGuid()}/answer",
            new { status = "Answered", answer = "Approve payment" });
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("What is the total?", 0)]
    [InlineData("What is the total?", 11)]
    public async Task Reviewed_question_bounds_fail_before_database_access(string question, int count)
    {
        using var client = _factory.CreateClient();
        using var result = await client.PostAsJsonAsync("/api/cited-questions/reviewed",
            new { question, requestKey = "reviewed-key", indexIds = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray() });
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.True(result.Headers.CacheControl!.NoStore);
        Assert.Contains("invalid_cited_request", await result.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10001)]
    public async Task Workspace_source_pagination_is_bounded_before_database_access(int offset)
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync($"/api/cited-sources?offset={offset}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
    }

    [Theory]
    [InlineData("/api/cited-indexes")]
    [InlineData("/api/cited-retrievals")]
    [InlineData("/api/cited-questions")]
    public async Task Malformed_cited_bodies_are_noncacheable_and_do_not_echo_payload(string path)
    {
        using var client = _factory.CreateClient();
        using var content = new StringContent("{\"private-marker-82\":", System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(path, content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.DoesNotContain("private-marker-82", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/api/cited-questions/not-a-guid")]
    [InlineData("/api/cited-retrievals/not-a-guid")]
    [InlineData("/api/cited-questions/00000000-0000-0000-0000-000000000001/hits/not-a-guid/evidence")]
    public async Task Invalid_history_and_citation_routes_are_noncacheable(string path)
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
    }
}
