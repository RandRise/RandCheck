using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.Documents;
using UaeDocumentIntelligence.Application.Documents.Queries;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public async Task Document_library_pages_ten_stably_searches_beyond_page_and_scopes_every_filter()
    {
        var seed = await SeedGraphAsync();
        for (var i = 0; i < 22; i++)
        {
            var id = Guid.NewGuid();
            await ExecuteAsync(InsertDocumentSql, DocumentParameters(seed.OrganizationA, seed.ActorA, id, $"item-{i}.pdf", ValidSha));
            await ExecuteAsync("UPDATE documents SET original_filename=@name,created_at_utc='2026-01-01' WHERE id=@id", ("name", $"item-{i:D2}.pdf"), ("id", id));
        }
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        await using var scope = services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<DocumentLibraryQueries>();
        var first = await queries.SearchAsync(DocumentLibraryQuery.Create(null, null, false, 0), default);
        var second = await queries.SearchAsync(DocumentLibraryQuery.Create(null, null, false, 10), default);
        var last = await queries.SearchAsync(DocumentLibraryQuery.Create(null, null, false, 20), default);
        Assert.Equal(23, first.Total); Assert.Equal(10, first.Documents.Count); Assert.Equal(10, second.Documents.Count); Assert.Equal(3, last.Documents.Count);
        Assert.Equal(23, first.Documents.Concat(second.Documents).Concat(last.Documents).Select(x => x.Id).Distinct().Count());
        Assert.DoesNotContain(first.Documents.Concat(second.Documents).Concat(last.Documents), x => x.Id == seed.DocumentB);
        var found = await queries.SearchAsync(DocumentLibraryQuery.Create("ITEM-21", "All", false, 0), default);
        Assert.Equal("item-21.pdf", Assert.Single(found.Documents).OriginalFilename);
        var foreign = await queries.SearchAsync(DocumentLibraryQuery.Create("org-b-document", "All", true, 0), default);
        Assert.Empty(foreign.Documents);
        Assert.Equal(20, (await queries.SearchAsync(DocumentLibraryQuery.Create(null, null, false, 999), default)).Offset);
        Assert.Empty((await queries.SearchAsync(DocumentLibraryQuery.Create("%_", null, false, 0), default)).Documents);
    }

    [Fact]
    public async Task Document_library_search_uses_current_effective_values_without_confirming_or_reviving_cleared_values()
    {
        var seed = await SeedGraphAsync();
        var field = Guid.NewGuid();
        await ExecuteAsync(InsertFieldSql, FieldParameters(seed.OrganizationA, seed.RunA, field, "supplier_name", 0));
        await ExecuteAsync("UPDATE machine_field_values SET value_text='Raw supplier',value_type='String' WHERE id=@id", ("id", field));
        async Task<int> Find(string term)
        {
            await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
            await using var scope = services.CreateAsyncScope();
            return (await scope.ServiceProvider.GetRequiredService<DocumentLibraryQueries>().SearchAsync(DocumentLibraryQuery.Create(term, null, false, 0), default)).Total;
        }
        async Task Add(ReviewActionType action, string key, Guid? machine, int sequence, string? value)
        {
            await using var context = _database.CreateContext();
            var doc = await LoadDocumentAsync(context, seed.DocumentA);
            doc.ReviewSessions.Single(s => s.Id == seed.SessionA).AddAction(Guid.NewGuid(), seed.ActorA, sequence, action,
                key, 0, machine, value is null ? null : FieldValueType.String, value, "Checked source", DateTimeOffset.UtcNow);
            await context.SaveChangesAsync();
        }
        Assert.Equal(1, await Find("raw SUPPLIER"));
        await Add(ReviewActionType.Corrected, "supplier_name", field, 2, "المورد الجديد 123");
        Assert.Equal(0, await Find("Raw supplier")); Assert.Equal(1, await Find("المورد الجديد"));
        await Add(ReviewActionType.Cleared, "supplier_name", field, 3, null);
        Assert.Equal(0, await Find("المورد الجديد"));
        await Add(ReviewActionType.Added, "po_number", null, 4, "PO-308");
        Assert.Equal(1, await Find("PO-308"));
        await Add(ReviewActionType.Cleared, "po_number", null, 5, null);
        Assert.Equal(0, await Find("PO-308"));
        Assert.Equal(5, await ScalarAsync<long>("SELECT count(*) FROM review_actions WHERE review_session_id=@id", ("id", seed.SessionA)));
        Assert.Equal("InProgress", await ScalarAsync<string>("SELECT status FROM review_sessions WHERE id=@id", ("id", seed.SessionA)));
    }

    [Fact]
    public async Task Document_library_filters_normalized_types_keeps_unknown_and_reads_active_older_review()
    {
        var seed = await SeedGraphAsync();
        await ExecuteAsync("UPDATE extraction_runs SET document_type='purchase_order' WHERE id=@id", ("id", seed.RunA));
        await ExecuteAsync(InsertFieldSql, FieldParameters(seed.OrganizationA, seed.RunA, Guid.NewGuid(), "invoice_number", 0));
        var fresh = Guid.NewGuid();
        await ExecuteAsync(InsertRunSql, RunParameters(seed.OrganizationA, seed.DocumentA, fresh, 2, "newer"));
        await ExecuteAsync("UPDATE extraction_runs SET document_type=' QUO-TE ' WHERE id=@id", ("id", fresh));
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        await using var scope = services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<DocumentLibraryQueries>();
        Assert.Empty((await queries.SearchAsync(DocumentLibraryQuery.Create(null, "All", false, 0), default)).Documents);
        var quotation = await queries.SearchAsync(DocumentLibraryQuery.Create("100", "All", true, 0), default);
        Assert.Equal(seed.DocumentA, Assert.Single(quotation.Documents).Id);
        Assert.True(quotation.Documents[0].ReviewUsesOlderExtraction);
        Assert.Empty((await queries.SearchAsync(DocumentLibraryQuery.Create(null, "Invoice", true, 0), default)).Documents);
        await ExecuteAsync("UPDATE extraction_runs SET document_type='Purchase Order' WHERE id=@id", ("id", fresh));
        Assert.Single((await queries.SearchAsync(DocumentLibraryQuery.Create(null, "PurchaseOrder", false, 0), default)).Documents);
    }

    [Fact]
    public async Task Document_library_real_api_validates_paging_and_requires_document_access()
    {
        using var own = await SourceFixtureAsync("Staff");
        using var foreign = await SourceFixtureAsync("Staff");
        var response = await own.User.GetAsync("/api/documents/library");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        Assert.Equal(own.DocumentId, Assert.Single((await response.Content.ReadFromJsonAsync<DocumentLibraryPage>())!.Documents).Id);
        foreach (var query in new[] { "offset=-1", "type=Quotation", "offset=1000001", "includeQuotations=wrong", "q=" + new string('x', 201) })
            Assert.Equal(HttpStatusCode.BadRequest, (await own.User.GetAsync("/api/documents/library?" + query)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SecurityChange(own.Admin, own.Member, "Staff", false, false)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await own.User.GetAsync("/api/documents/library?includeQuotations=true")).StatusCode);
    }
}
