using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Api.Tests;

public sealed partial class ReconciliationApiContractTests
{
    [Fact]
    public async Task Metadata_put_preserves_identity_history_and_reference_and_requires_current_etag()
    {
        var item = new ReconciliationCase(Guid.NewGuid(), OrganizationId, ActorId, "Original", Now);
        using var factory = WithRepository(new FakeRepository(item));
        using var client = factory.CreateClient();
        var before = item.ConcurrencyToken;
        async Task<HttpResponseMessage> Update(string? etag, string name = "شركة الاختبار · AED 100", string category = "Services")
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/reconciliation-cases/{item.Id}")
            { Content = JsonContent.Create(new { name, category }) };
            if (etag is not null) request.Headers.TryAddWithoutValidation("If-Match", etag);
            return await client.SendAsync(request);
        }
        using var missing = await Update(null);
        Assert.Equal((HttpStatusCode)428, missing.StatusCode);
        using var weak = await Update($"W/\"{before}\"");
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        using var changed = await Update($"\"{before}\"");
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var body = await changed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(item.Id, body.GetProperty("id").GetGuid());
        Assert.Equal("شركة الاختبار · AED 100", body.GetProperty("name").GetString());
        Assert.Equal("Services", body.GetProperty("category").GetString());
        Assert.Equal(item.Reference, body.GetProperty("reference").GetString());
        Assert.Empty(body.GetProperty("runs").EnumerateArray());
        Assert.NotEqual(before, item.ConcurrencyToken);
        using var stale = await Update($"\"{before}\"", "Rejected rename");
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        using var same = await Update(changed.Headers.ETag!.Tag);
        Assert.Equal(changed.Headers.ETag.Tag, same.Headers.ETag!.Tag);
        item.Close(Now.AddDays(1));
        using var closed = await Update($"\"{item.ConcurrencyToken}\"");
        Assert.Equal(HttpStatusCode.Conflict, closed.StatusCode);
        Assert.Equal("شركة الاختبار · AED 100", item.Name);
    }

    [Theory]
    [InlineData("category=Unknown", "invalid_case_category")]
    [InlineData("category=1", "invalid_case_category")]
    [InlineData("status=Open", "invalid_case_status")]
    [InlineData("status=1", "invalid_case_status")]
    [InlineData("createdFrom=2026-02-30", "invalid_case_dates")]
    [InlineData("createdTo=2026-01-01T00:00:00Z", "invalid_case_dates")]
    [InlineData("createdFrom=2026-09-30&createdTo=2026-09-29", "invalid_case_dates")]
    public async Task Invalid_filters_are_rejected_before_repository_query(string query, string code)
    {
        var repository = new FakeRepository(); using var factory = WithRepository(repository); using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/reconciliation-cases?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(0, repository.ListCalls);
    }

    [Fact]
    public async Task List_binds_search_and_created_day_filters_with_utc_boundaries()
    {
        var repository = new FakeRepository(); using var factory = WithRepository(repository); using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/reconciliation-cases?q=REC-2026&category=OfficeSupplies&status=Evaluated&createdFrom=2026-01-01&createdTo=2026-01-01");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("REC-2026", repository.Filter!.Search);
        Assert.Equal(ReconciliationCaseCategory.OfficeSupplies, repository.Filter.Category);
        Assert.Equal(ReconciliationCaseStatus.Evaluated, repository.Filter.Status);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), repository.Filter.CreatedFrom);
        Assert.Equal(repository.Filter.CreatedFrom!.Value.AddDays(1).AddTicks(-1), repository.Filter.CreatedTo);
        using var tooLong = await client.GetAsync($"/api/reconciliation-cases?q={new string('x', 201)}");
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    [Theory]
    [InlineData(null, "Uncategorized")]
    [InlineData("Equipment", "Equipment")]
    [InlineData("OfficeSupplies", "OfficeSupplies")]
    public async Task Create_supports_optional_category(string? category, string expected)
    {
        using var factory = WithRepository(new FakeRepository()); using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/reconciliation-cases", new { name = "Office equipment", category });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(expected, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("category").GetString());
    }

    [Theory]
    [InlineData("", "Services")]
    [InlineData("Valid title", "goods")]
    [InlineData("Valid title", null)]
    public async Task Metadata_validation_does_not_change_case(string name, string? category)
    {
        var item = new ReconciliationCase(Guid.NewGuid(), OrganizationId, ActorId, "Original", Now);
        using var factory = WithRepository(new FakeRepository(item)); using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/reconciliation-cases/{item.Id}")
        { Content = JsonContent.Create(new { name, category }) };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{item.ConcurrencyToken}\"");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Original", item.Name);
        Assert.Equal(ReconciliationCaseCategory.Uncategorized, item.Category);
    }

    [Fact]
    public async Task Metadata_update_does_not_reveal_or_modify_another_tenants_case()
    {
        var item = new ReconciliationCase(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Foreign", Now);
        using var factory = WithRepository(new FakeRepository(item)); using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/reconciliation-cases/{item.Id}")
        { Content = JsonContent.Create(new { name = "Renamed", category = "Services" }) };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{item.ConcurrencyToken}\"");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Foreign", item.Name);
    }
}
