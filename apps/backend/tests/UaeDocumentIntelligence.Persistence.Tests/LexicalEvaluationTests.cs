using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public async Task Synthetic_question_set_measures_real_postgres_lexical_recall_without_answer_claims()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "samples/phase-5/lexical-v1/evaluation-input.json"))) root = root.Parent;
        Assert.NotNull(root);
        var inputPath = Path.Combine(root!.FullName, "samples/phase-5/lexical-v1/evaluation-input.json");
        using var input = JsonDocument.Parse(await File.ReadAllTextAsync(inputPath));
        var data = input.RootElement;
        Assert.True(data.GetProperty("synthetic").GetBoolean());
        Assert.False(data.GetProperty("claimEligible").GetBoolean());
        var orgs = new Dictionary<string, (Guid Org, Guid Actor)>();
        var indexes = new Dictionary<string, CitedDocumentIndex>();
        var now = DateTimeOffset.UtcNow;
        await using (var db = _database.CreateContext())
        {
            foreach (var source in data.GetProperty("sources").EnumerateArray())
            {
                var orgName = source.GetProperty("organization").GetString()!;
                if (!orgs.TryGetValue(orgName, out var owner))
                {
                    owner = (Guid.NewGuid(), Guid.NewGuid());
                    orgs[orgName] = owner;
                    db.Organizations.Add(new(owner.Org, orgName, orgName, now));
                    db.Actors.Add(new(owner.Actor, owner.Org, ActorKind.System, "Evaluation actor", now));
                }
                var sourceId = source.GetProperty("id").GetString()!;
                var doc = new Document(Guid.NewGuid(), owner.Org, owner.Actor, sourceId + ".pdf",
                    Guid.NewGuid().ToString(), "application/pdf", 100, source.GetProperty("sha256").GetString()!, now);
                var extraction = doc.QueueExtraction(Guid.NewGuid(), 1, "fixture", "1", "2.0", sourceId, now);
                extraction.Start(now);
                foreach (var page in source.GetProperty("pages").EnumerateArray())
                    extraction.AddPage(Guid.NewGuid(), page.GetProperty("pageNumber").GetInt32(), page.GetProperty("canonicalText").GetString()!, 0);
                // Minimal reviewed fixture to pin page-only retrieval. This does not
                // fabricate reviewed business facts or evaluate extraction accuracy.
                var field = extraction.AddField(Guid.NewGuid(), "fixture_marker", 0, FieldValueType.String, sourceId, 1, now);
                var first = extraction.Pages.OrderBy(x => x.PageNumber).First();
                var quote = first.CanonicalText[..1];
                extraction.AddEvidence(field, Guid.NewGuid(), first.PageNumber, quote, 0, Encoding.UTF8.GetByteCount(quote));
                extraction.RecordProviderMetrics(1, extraction.Pages.Count, null, null, null);
                extraction.Succeed("invoice", "en", now, "fixture", "1");
                doc.RecordExtractionSuccess(extraction, now);
                var review = doc.OpenReview(Guid.NewGuid(), owner.Actor, now);
                review.AddAction(Guid.NewGuid(), owner.Actor, 1, ReviewActionType.Confirmed,
                    "fixture_marker", 0, field.Id, null, null, null, now);
                review.Complete(extraction.Fields, now);
                var index = new CitedDocumentIndex(Guid.NewGuid(), doc, review, "page-lines", "1.0.0-b2048-o256", now);
                foreach (var page in extraction.Pages.OrderBy(x => x.PageNumber))
                {
                    Assert.InRange(Encoding.UTF8.GetByteCount(page.CanonicalText), 1, 2048);
                    index.AddPageChunk(Guid.NewGuid(), index.Chunks.Count, page, 0, Encoding.UTF8.GetByteCount(page.CanonicalText));
                }
                index.Publish();
                db.Documents.Add(doc);
                db.CitedDocumentIndexes.Add(index);
                indexes[sourceId] = index;
            }
            await db.SaveChangesAsync();
        }
        var ownerA = orgs["org-primary"];
        await using var services = CitedServices(ownerA.Org, ownerA.Actor, "http://127.0.0.1:1");
        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ICitedRetrievalStore>();
        var repository = scope.ServiceProvider.GetRequiredService<ICitedQuestionRepository>();
        var outcomes = new List<object>();
        var labelCount = 0; var labelsAt1 = 0; var labelsAt5 = 0; var answerable = 0; var completeAt5 = 0;
        var reciprocalRanks = 0.0;
        foreach (var question in data.GetProperty("questions").EnumerateArray())
        {
            var owner = orgs[question.GetProperty("organization").GetString()!];
            var allowed = question.GetProperty("allowed").EnumerateArray().Select(x => indexes[x.GetString()!]).ToArray();
            var plan = question.GetProperty("plan").Deserialize<LexicalPlan>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            var text = question.GetProperty("question").GetString()!;
            var run = new CitedRetrievalRun(Guid.NewGuid(), owner.Org, owner.Actor, text,
                question.GetProperty("id").GetString()!, plan.PolicyVersion,
                question.GetProperty("plan").GetRawText(), allowed, now);
            await repository.AddRunAsync(run, default);
            Assert.Equal(CitedQuestionSaveOutcome.Saved, await repository.SaveChangesAsync(default));
            await store.ExecuteLexicalAsync(run, plan, default);
            var chunks = (await store.GetHitChunksAsync(run, default)).ToDictionary(x => x.Id);
            Assert.All(run.Hits, hit =>
            {
                Assert.Equal(owner.Org, chunks[hit.ChunkId].OrganizationId);
                Assert.Contains(hit.IndexId, allowed.Select(x => x.Id));
            });
            var evidence = question.GetProperty("expected").GetProperty("claims").EnumerateArray()
                .SelectMany(c => c.GetProperty("evidence").EnumerateArray()).ToArray();
            var matchedRanks = evidence.Select(label => run.Hits.Where(hit =>
                    hit.IndexId == indexes[label.GetProperty("source_id").GetString()!].Id &&
                    chunks[hit.ChunkId].Spans.Any(span => span.PageNumber == label.GetProperty("page_number").GetInt32() &&
                        span.Quote.Contains(label.GetProperty("quote").GetString()!, StringComparison.Ordinal)))
                .Select(hit => (int?)hit.Rank).Min()).ToArray();
            if (evidence.Length > 0)
            {
                answerable++;
                labelCount += evidence.Length;
                labelsAt1 += matchedRanks.Count(rank => rank == 1);
                labelsAt5 += matchedRanks.Count(rank => rank <= 5);
                if (matchedRanks.All(rank => rank <= 5)) completeAt5++;
                var firstRelevant = matchedRanks.Min();
                if (firstRelevant.HasValue) reciprocalRanks += 1.0 / firstRelevant.Value;
            }
            outcomes.Add(new { id = run.RequestKey, category = question.GetProperty("category").GetString(),
                expectedStatus = question.GetProperty("expected").GetProperty("status").GetString(),
                hitCount = run.Hits.Count, evidenceCount = evidence.Length,
                evidenceRanks = matchedRanks, retrievalDurationMs = run.RetrievalDurationMs,
                generatedAnswer = false });
        }
        Assert.Equal(30, outcomes.Count);
        Assert.Equal(24, answerable);
        Assert.Equal(46, labelCount);
        var report = new { schemaVersion = "1.0", synthetic = true, claimEligible = false,
            questionSetSha256 = data.GetProperty("questionSetSha256").GetString(),
            inputSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(inputPath))).ToLowerInvariant(),
            policyVersion = LexicalPlan.CurrentVersion, configuration = "english", topK = 5,
            chunkProfile = "page-lines/1.0.0-b2048-o256", sourceCount = indexes.Count,
            questionCount = outcomes.Count, answerableQuestions = answerable, evidenceLabels = labelCount,
            evidenceRecallAt1 = (double)labelsAt1 / labelCount, evidenceRecallAt5 = (double)labelsAt5 / labelCount,
            fullEvidenceQuestionRecallAt5 = (double)completeAt5 / answerable, mrr = reciprocalRanks / answerable,
            retrievalGateMet = (double)completeAt5 / answerable >= 0.95, crossOrganizationHits = 0,
            vectorMeasured = false, hybridMeasured = false, generationMeasured = false, outcomes };
        var output = Path.Combine(root.FullName, "outputs/phase-5/lexical-v1/postgres-evaluation.json");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }
}
