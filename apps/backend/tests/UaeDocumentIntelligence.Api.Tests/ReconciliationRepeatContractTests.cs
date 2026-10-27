using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace UaeDocumentIntelligence.Api.Tests;

public sealed partial class ReconciliationApiContractTests
{
    [Fact]
    public async Task Create_request_key_replays_same_identity_and_conflicts_on_changed_payload()
    {
        var repository = new FakeRepository();
        using var factory = WithRepository(repository); using var client = factory.CreateClient();
        using var first = await client.PostAsJsonAsync("/api/reconciliation-cases", new { name = " Case ", requestKey = "intent-api" });
        using var retry = await client.PostAsJsonAsync("/api/reconciliation-cases", new { name = "Case", category = "Uncategorized", requestKey = "intent-api" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode); Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(first.Headers.Location, retry.Headers.Location); Assert.Equal(first.Headers.ETag, retry.Headers.ETag);
        var body = await retry.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.TryGetProperty("creationPayloadHash", out _)); Assert.False(body.TryGetProperty("creationRequestKey", out _));
        using var conflict = await client.PostAsJsonAsync("/api/reconciliation-cases", new { name = "Changed", requestKey = "intent-api" });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("request_key_reused", (await conflict.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        using var differentIntent = await client.PostAsJsonAsync("/api/reconciliation-cases", new { name = "Case", requestKey = "intent-api-2" });
        Assert.Equal(HttpStatusCode.Created, differentIntent.StatusCode); Assert.NotEqual(first.Headers.Location, differentIntent.Headers.Location);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" padded-key ")]
    public async Task Create_rejects_empty_explicit_request_key(string requestKey)
    {
        var repository = new FakeRepository();
        using var factory = WithRepository(repository); using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/reconciliation-cases", new { name = "Case", requestKey });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Null(repository.Item);
        Assert.Equal("invalid_request_key", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Create_rejects_oversize_request_key()
    {
        var repository = new FakeRepository();
        using var factory = WithRepository(repository); using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/reconciliation-cases", new { name = "Case", requestKey = new string('a', 101) });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Null(repository.Item);
    }
}
