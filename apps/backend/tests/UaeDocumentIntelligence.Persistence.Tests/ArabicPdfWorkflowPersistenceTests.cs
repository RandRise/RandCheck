using System.Security.Cryptography;
using System.Text;
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
    [InlineData("mixed-native-two-page.pdf")]
    [InlineData("mixed-scanned-two-page.pdf")]
    public async Task Arabic_pdf_review_answers_and_original_evidence_survive_reopening(string filename)
    {
        var seed = await SeedGraphAsync();
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "samples/phase-7/pdf-v0.1.0")))
            root = root.Parent;
        Assert.NotNull(root);
        var directory = Path.Combine(root!.FullName, "samples/phase-7/pdf-v0.1.0");
        using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(directory, "manifest.json")));
        var receipt = manifest.RootElement.GetProperty("receipts").EnumerateArray()
            .Single(r => r.GetProperty("source").GetProperty("name").GetString() == filename);
        var bytes = await File.ReadAllBytesAsync(Path.Combine(directory, receipt.GetProperty("extractionFile").GetString()!));
        Assert.Equal(receipt.GetProperty("extractionSha256").GetString(),
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        using var payload = JsonDocument.Parse(bytes);
        var data = payload.RootElement;
        Assert.Equal("Partial", data.GetProperty("outcome").GetString());
        Assert.Equal(2, data.GetProperty("pages").GetArrayLength());
        var expected = receipt.GetProperty("expected").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetString()!);
        var provider = data.GetProperty("provider");
        var now = DateTimeOffset.UtcNow;
        var doc = new Document(Guid.NewGuid(), seed.OrganizationA, seed.ActorA, filename,
            Guid.NewGuid().ToString(), "application/pdf", receipt.GetProperty("source").GetProperty("bytes").GetInt64(),
            receipt.GetProperty("source").GetProperty("sha256").GetString()!, now);
        var extraction = doc.QueueExtraction(Guid.NewGuid(), 1, "local",
            provider.GetProperty("adapter_version").GetString()!, "2.0", "phase7-pdf-fixture", now);
        extraction.Start(now);
        foreach (var page in data.GetProperty("pages").EnumerateArray())
            extraction.AddPage(Guid.NewGuid(), page.GetProperty("page_number").GetInt32(),
                page.GetProperty("canonical_text").GetString()!, page.GetProperty("rotation_degrees").GetInt16());
        foreach (var value in data.GetProperty("fields").EnumerateArray())
        {
            var field = extraction.AddField(Guid.NewGuid(), value.GetProperty("key").GetString()!,
                value.GetProperty("occurrence").GetInt32(),
                Enum.Parse<FieldValueType>(value.GetProperty("value_type").GetString()!),
                value.GetProperty("value").GetString()!,
                value.GetProperty("confidence").ValueKind == JsonValueKind.Null ? null : value.GetProperty("confidence").GetDecimal(),
                now, value.GetProperty("normalized_value").GetString());
            foreach (var evidence in value.GetProperty("evidence").EnumerateArray())
                extraction.AddEvidence(field, Guid.NewGuid(), evidence.GetProperty("page_number").GetInt32(),
                    evidence.GetProperty("quote").GetString(), evidence.GetProperty("start_utf8").GetInt32(),
                    evidence.GetProperty("end_utf8").GetInt32());
        }
        foreach (var warning in data.GetProperty("warnings").EnumerateArray())
            extraction.AddWarning(Guid.NewGuid(), warning.GetProperty("code").GetString()!,
                Enum.Parse<ExtractionWarningSeverity>(warning.GetProperty("severity").GetString()!, ignoreCase: true),
                warning.GetProperty("safe_summary").GetString()!,
                OptionalInt(warning, "page_number"), warning.GetProperty("field_key").GetString(),
                OptionalInt(warning, "occurrence"));
        extraction.RecordProviderMetrics(data.GetProperty("metrics").GetProperty("provider_duration_ms").GetInt64(),
            extraction.Pages.Count, null, null, null);
        extraction.CompletePartial(data.GetProperty("document_type").GetString()!, data.GetProperty("language").GetString()!,
            now, provider.GetProperty("model").GetString()!, provider.GetProperty("model_version").GetString()!);
        doc.RecordExtractionSuccess(extraction, now);
        await using (var db = _database.CreateContext())
        {
            db.Documents.Add(doc);
            await db.SaveChangesAsync();
        }

        await using var python = await PrivateCitedServer.StartAsync();
        ServiceProvider Services() => CitedServices(seed.OrganizationA, seed.ActorA, python.Url);
        ReviewWorkspaceView review;
        await using (var services = Services())
            review = (await services.GetRequiredService<StartReviewHandler>().HandleAsync(doc.Id, default)).Workspace;
        var staleToken = review.Session!.ConcurrencyToken;
        var reviewId = review.Session.Id;
        const string reason = "تمت المراجعة من المستند الأصلي — PDF page 2 / AED";
        var decisions = new Dictionary<string, string>();
        foreach (var field in review.Fields.ToArray())
        {
            // Missing fields are added by the reviewer; unexpected machine fields are
            // explicitly cleared. The extraction receipt and source truth stay immutable.
            var known = expected.TryGetValue(field.FieldKey, out var correct) && field.OccurrenceIndex == 0;
            if (!known && field.Machine is null) continue;
            var machine = extraction.Fields.SingleOrDefault(f => f.Id == field.Machine?.Id);
            var action = !known ? "Cleared" : machine is null ? "Added" :
                (machine.NormalizedValueText ?? machine.ValueText) == correct ? "Confirmed" : "Corrected";
            var replaces = action is "Added" or "Corrected";
            var type = field.FieldKey switch
            {
                "subtotal" or "vat" or "total" => "Decimal",
                "invoice_date" or "due_date" => "Date",
                "supplier_trn" => "Identifier", "currency" => "Currency", _ => "String"
            };
            await using var services = Services();
            review = await services.GetRequiredService<RecordReviewActionHandler>().HandleAsync(doc.Id, reviewId,
                review.Session!.ConcurrencyToken, new(action, field.Machine?.Id,
                    field.Machine is null ? field.FieldKey : null, field.Machine is null ? field.OccurrenceIndex : null,
                    replaces ? type : null, replaces ? correct : null, action == "Confirmed" ? null : reason), default);
            if (known) decisions[field.FieldKey] = action;
        }
        Assert.Equal("Corrected", decisions["payment_terms"]);
        Assert.Equal("Confirmed", decisions["total"]);
        await using (var services = Services())
        {
            await Assert.ThrowsAsync<ReviewPreconditionException>(() => services.GetRequiredService<CompleteReviewHandler>()
                .HandleAsync(doc.Id, reviewId, staleToken, default));
        }
        await using (var services = Services())
            review = await services.GetRequiredService<CompleteReviewHandler>()
                .HandleAsync(doc.Id, reviewId, review.Session!.ConcurrencyToken, default);
        Assert.Equal("Completed", review.Session!.Status);

        Guid indexId;
        await using (var services = Services())
        {
            var reopened = await services.GetRequiredService<GetReviewWorkspaceHandler>().HandleAsync(doc.Id, default);
            Assert.Equal(JsonSerializer.Serialize(review.Fields), JsonSerializer.Serialize(reopened.Fields));
            Assert.Equal(reviewId, reopened.Session!.Id);
            Assert.Equal("Completed", reopened.Session.Status);
            Assert.Equal(review.Session.ConcurrencyToken, reopened.Session.ConcurrencyToken);
            // PostgreSQL timestamps retain microseconds; .NET can retain 100 ns ticks.
            Assert.Equal(review.Session.CompletedAt!.Value.UtcTicks / 10,
                reopened.Session.CompletedAt!.Value.UtcTicks / 10);
            var history = await services.GetRequiredService<GetReviewHistoryHandler>().HandleAsync(doc.Id, default);
            Assert.Contains(Assert.Single(history.Sessions).Actions, a => a.Reason == reason);
            indexId = (await services.GetRequiredService<IndexReviewedDocumentHandler>()
                .HandleAsync(doc.Id, reviewId, default)).Id;
        }

        foreach (var (question, key) in new[] {
            ("ما إجمالي الفاتورة؟", "total"), ("What is the invoice total?", "total"),
            ("ما شروط دفع الفاتورة؟", "payment_terms"),
            ("ما تاريخ استحقاق الفاتورة؟", "due_date"),
            ("ما ضريبة القيمة المضافة؟", "vat") })
        {
            CitedQuestionView answer;
            var requestKey = Guid.NewGuid().ToString();
            await using (var services = Services())
                answer = await services.GetRequiredService<CitedReviewedQuestionHandler>()
                    .HandleAsync(question, requestKey, [indexId], default);
            Assert.Equal("Answered", answer.ReviewedAnswer!.Status);
            Assert.False(answer.AnswerGenerationEnabled);
            var claim = Assert.Single(answer.ReviewedAnswer.Claims);
            var citation = Assert.Single(claim.Citations.Where(c => answer.Retrieval.Hits
                .Single(h => h.HitId == c.HitId).Content.StartsWith($"{key}[0]: ", StringComparison.Ordinal)));
            await using var reader = Services();
            var saved = await reader.GetRequiredService<CitedQuestionHandler>().GetAsync(answer.Retrieval.Id, default);
            Assert.Equal(JsonSerializer.Serialize(answer.ReviewedAnswer), JsonSerializer.Serialize(saved.ReviewedAnswer));
            var replay = await reader.GetRequiredService<CitedReviewedQuestionHandler>()
                .HandleAsync(question, requestKey, [indexId], default);
            Assert.True(replay.Retrieval.Replayed);
            Assert.Equal(answer.Retrieval.Id, replay.Retrieval.Id);
            var evidenceHandler = reader.GetRequiredService<CitedWorkspaceHandler>();
            if (key is "total" or "vat")
            {
                Assert.Equal(2, claim.Citations.Count);
                var currency = Assert.Single(claim.Citations.Where(c => c.HitId != citation.HitId));
                var currencyEvidence = await evidenceHandler.EvidenceAsync(answer.Retrieval.Id,
                    currency.HitId, currency.SpanId, default);
                Assert.Equal("currency[0]: AED", currencyEvidence.ReviewedFact);
                Assert.Equal("Confirmed", currencyEvidence.Review!.Action);
            }
            var evidence = await evidenceHandler.EvidenceAsync(answer.Retrieval.Id, citation.HitId, citation.SpanId, default);
            Assert.Equal($"{key}[0]: {expected[key]}", evidence.ReviewedFact);
            Assert.Equal(decisions[key], evidence.Review!.Action);
            var spans = answer.Retrieval.Hits.Single(h => h.HitId == citation.HitId).Spans;
            if (decisions[key] == "Added")
                Assert.Empty(spans); // A reviewer addition must never invent page evidence.
            else
            {
                Assert.NotEmpty(spans);
                foreach (var span in spans)
                {
                    var opened = await evidenceHandler.EvidenceAsync(answer.Retrieval.Id, citation.HitId, span.Id, default);
                    var page = opened.Page!;
                    var original = extraction.Pages.Single(p => p.PageNumber == page.PageNumber).CanonicalText;
                    Assert.Equal(original, page.Before + page.Quote + page.After);
                    Assert.Equal(page.Quote, Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(original)
                        .AsSpan(page.ByteStart, page.ByteEnd - page.ByteStart)));
                    if (key is "total" or "payment_terms" or "vat") Assert.Equal(2, page.PageNumber);
                    if (key == "payment_terms") Assert.Equal("خلال 30 يوما", page.Quote);
                }
            }
            await using var other = CitedServices(seed.OrganizationB, seed.ActorB, python.Url);
            Assert.Equal(404, (await Assert.ThrowsAsync<CitedWorkflowException>(() =>
                other.GetRequiredService<CitedWorkspaceHandler>().EvidenceAsync(
                    answer.Retrieval.Id, citation.HitId, citation.SpanId, default))).Status);
        }
        await using (var services = Services())
        {
            var refusal = await services.GetRequiredService<CitedReviewedQuestionHandler>()
                .HandleAsync("ما إجمالي الفاتورة وتوقع الأرباح؟", Guid.NewGuid().ToString(), [indexId], default);
            Assert.Equal("InsufficientEvidence", refusal.ReviewedAnswer!.Status);
            Assert.Equal("unsupported_scope", refusal.ReviewedAnswer.ReasonCode);
            Assert.Empty(refusal.ReviewedAnswer.Claims);
        }
        await using (var db = _database.CreateContext())
        {
            var retained = await db.ExtractionPages.Where(p => p.ExtractionRunId == extraction.Id).OrderBy(p => p.PageNumber).ToListAsync();
            Assert.Equal(extraction.Pages.OrderBy(p => p.PageNumber).Select(p => p.CanonicalText), retained.Select(p => p.CanonicalText));
        }
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_reservations"));
    }

    private static int? OptionalInt(JsonElement value, string property) =>
        value.GetProperty(property).ValueKind == JsonValueKind.Null ? null : value.GetProperty(property).GetInt32();
}
