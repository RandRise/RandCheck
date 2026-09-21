using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Application.Documents.Review;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Theory]
    [InlineData("Confirmed", "2026-08-31")]
    [InlineData("Corrected", "2026-09-15")]
    [InlineData("Cleared", null)]
    public async Task Due_date_native_extraction_review_and_citation_roundtrip(string decision, string? expected)
    {
        var seed = await SeedGraphAsync();
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "samples/phase-5/due-date-v0.1.0")))
            root = root.Parent;
        Assert.NotNull(root);
        var directory = Path.Combine(root!.FullName, "samples/phase-5/due-date-v0.1.0");
        var bytes = await File.ReadAllBytesAsync(Path.Combine(directory, "extraction.json"));
        using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(directory, "manifest.json")));
        Assert.Equal(manifest.RootElement.GetProperty("extractionSha256").GetString(),
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        using var payload = JsonDocument.Parse(bytes);
        var data = payload.RootElement;
        Assert.Equal("local-1.3.0", data.GetProperty("provider").GetProperty("adapter_version").GetString());
        var now = DateTimeOffset.UtcNow;
        var doc = new Document(Guid.NewGuid(), seed.OrganizationA, seed.ActorA, "synthetic-due-date.pdf",
            Guid.NewGuid().ToString(), "application/pdf", 100,
            manifest.RootElement.GetProperty("sourceSha256").GetString()!, now);
        var extraction = doc.QueueExtraction(Guid.NewGuid(), 1, "local", "local-1.3.0", "2.0", "due-date-fixture", now);
        extraction.Start(now);
        foreach (var page in data.GetProperty("pages").EnumerateArray())
            extraction.AddPage(Guid.NewGuid(), page.GetProperty("page_number").GetInt32(),
                page.GetProperty("canonical_text").GetString()!, page.GetProperty("rotation_degrees").GetInt16());
        foreach (var value in data.GetProperty("fields").EnumerateArray())
        {
            var field = extraction.AddField(Guid.NewGuid(), value.GetProperty("key").GetString()!,
                value.GetProperty("occurrence").GetInt32(),
                Enum.Parse<FieldValueType>(value.GetProperty("value_type").GetString()!),
                value.GetProperty("value").GetString()!, null, now, value.GetProperty("normalized_value").GetString());
            foreach (var evidence in value.GetProperty("evidence").EnumerateArray())
                extraction.AddEvidence(field, Guid.NewGuid(), evidence.GetProperty("page_number").GetInt32(),
                    evidence.GetProperty("quote").GetString(), evidence.GetProperty("start_utf8").GetInt32(),
                    evidence.GetProperty("end_utf8").GetInt32());
        }
        extraction.RecordProviderMetrics(0, extraction.Pages.Count, null, null, null);
        extraction.Succeed("invoice", "en", now, "pdfplumber", "native-fixture");
        doc.RecordExtractionSuccess(extraction, now);
        Assert.Equal(FieldValueType.Date, extraction.Fields.Single(f => f.FieldKey == "due_date").ValueType);
        var review = doc.OpenReview(Guid.NewGuid(), seed.ActorA, now);
        foreach (var field in extraction.Fields)
        {
            var action = field.FieldKey == "due_date" ? Enum.Parse<ReviewActionType>(decision) : ReviewActionType.Confirmed;
            review.AddAction(Guid.NewGuid(), seed.ActorA, review.Actions.Count + 1, action,
                field.FieldKey, field.OccurrenceIndex, field.Id,
                action == ReviewActionType.Corrected ? FieldValueType.Date : null,
                action == ReviewActionType.Corrected ? expected : null,
                action == ReviewActionType.Confirmed ? null : "Synthetic review scenario", now);
        }
        review.Complete(extraction.Fields, now);
        var index = new CitedDocumentIndex(Guid.NewGuid(), doc, review, "page-lines", "1.0.0-b2048-o256", now);
        foreach (var field in extraction.Fields.Where(f => decision != "Cleared" || f.FieldKey != "due_date"))
            index.AddReviewedFact(Guid.NewGuid(), index.Chunks.Count, review, field.FieldKey, field.OccurrenceIndex, extraction);
        index.Publish();
        var run = NewSearch(seed.OrganizationA, seed.ActorA, index, "When is the invoice due?");
        run.MarkRetrieved(0);
        await using (var db = _database.CreateContext())
        {
            db.Documents.Add(doc);
            db.CitedDocumentIndexes.Add(index);
            db.CitedRetrievalRuns.Add(run);
            await db.SaveChangesAsync();
        }
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var workspace = services.GetRequiredService<CitedFactWorkspace>();
        var result = await workspace.AnswerAsync(run.Id, default);
        Assert.False(result.Answer.ModelInvoked);
        Assert.False(result.Answer.PublicationAllowed);
        if (expected is null)
        {
            Assert.Equal("InsufficientEvidence", result.Answer.Status);
            Assert.Empty(result.Answer.Claims);
        }
        else
        {
            Assert.Equal("Answered", result.Answer.Status);
            var citation = Assert.Single(Assert.Single(result.Answer.Claims).Citations);
            Assert.Equal(expected, citation.ReviewedValue);
            Assert.Equal("due_date[0]: " + expected, citation.SupportText);
            Assert.All(citation.OriginalPageEvidence, e => Assert.Equal("2026-08-31", e.Quote));
            Assert.NotEmpty(citation.OriginalPageEvidence);
            Assert.Equal(JsonSerializer.Serialize(citation),
                JsonSerializer.Serialize(await workspace.EvidenceAsync(run.Id, citation.ChunkId, default)));
        }
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(await workspace.AnswerAsync(run.Id, default)));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_reservations"));
    }
}
