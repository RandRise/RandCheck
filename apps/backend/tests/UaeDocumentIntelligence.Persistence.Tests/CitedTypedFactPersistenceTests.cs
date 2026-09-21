using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public async Task Typed_facts_read_completed_review_without_model_spending_or_old_hit_membership()
    {
        var seed = await SeedTypedFactCorpus();
        await using var services = CitedServices(seed.Org, seed.Actor, "http://127.0.0.1:1");
        await ExecuteAsync("UPDATE cited_pilot_budgets SET requires_reconciliation=true");
        var workspace = services.GetRequiredService<CitedFactWorkspace>();
        var answer = await workspace.AnswerAsync(seed.Run, default);
        Assert.Equal("Answered", answer.Answer.Status);
        Assert.Equal("00000701", Assert.Single(answer.Answer.Operands).Value);
        Assert.False(answer.Answer.ModelInvoked);
        Assert.False(answer.Answer.PublicationAllowed);
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM retrieval_hits"));
        foreach (var citation in answer.Answer.Claims.SelectMany(c => c.Citations))
            Assert.Equal(JsonSerializer.Serialize(citation), JsonSerializer.Serialize(await workspace.EvidenceAsync(seed.Run, citation.ChunkId, default)));
        Assert.Equal(JsonSerializer.Serialize(answer), JsonSerializer.Serialize(await workspace.AnswerAsync(seed.Run, default)));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_reservations"));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_generation_artifacts"));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_answers"));
        Assert.True(await ScalarAsync<bool>("SELECT requires_reconciliation FROM cited_pilot_budgets"));
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("other_actor")]
    [InlineData("inactive")]
    [InlineData("quarantined")]
    [InlineData("superseded")]
    public async Task Typed_answer_and_evidence_access_revalidate_caller_and_source(string change)
    {
        var seed = await SeedTypedFactCorpus();
        await using var services = CitedServices(seed.Org, seed.Actor, "http://127.0.0.1:1");
        var original = await services.GetRequiredService<CitedFactWorkspace>().AnswerAsync(seed.Run, default);
        var citation = original.Answer.Claims.Single().Citations.Single();
        var org = seed.Org;
        var actor = seed.Actor;
        if (change == "foreign") { org = seed.ForeignOrg; actor = seed.ForeignActor; }
        if (change == "other_actor")
        {
            actor = Guid.NewGuid();
            await using var db = _database.CreateContext();
            db.Actors.Add(new Actor(actor, org, ActorKind.Human, "Other reader", DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        if (change == "inactive")
            await ExecuteAsync("UPDATE actors SET is_active=false WHERE id=@id", ("id", actor));
        if (change is "quarantined" or "superseded")
        {
            await using var db = _database.CreateContext();
            (await db.CitedDocumentIndexes.SingleAsync(i => i.Id == seed.Index)).Retire(change == "quarantined");
            await db.SaveChangesAsync();
        }
        await using var restricted = CitedServices(org, actor, "http://127.0.0.1:1");
        var workspace = restricted.GetRequiredService<CitedFactWorkspace>();
        await SpendDenied("typed_corpus_not_found", () => workspace.AnswerAsync(seed.Run, default));
        await SpendDenied("typed_corpus_not_found", () => workspace.EvidenceAsync(seed.Run, citation.ChunkId, default));
    }

    private async Task<(Guid Org, Guid Actor, Guid ForeignOrg, Guid ForeignActor, Guid Run, Guid Index)> SeedTypedFactCorpus(string pageSuffix = "")
    {
        var seed = await SeedGraphAsync();
        var now = DateTimeOffset.UtcNow;
        var doc = new Document(Guid.NewGuid(), seed.OrganizationA, seed.ActorA, "typed-private.pdf",
            Guid.NewGuid().ToString(), "application/pdf", 100, new string('b', 64), now);
        var extraction = doc.QueueExtraction(Guid.NewGuid(), 1, "test", "1", "2.0", "typed-store", now);
        extraction.Start(now);
        var fields = new[] { ("invoice_number", "INV-009"), ("supplier_trn", "00000701"),
            ("supplier_tin", "00000999"), ("currency", "AED"), ("total", "125.25") };
        var page = string.Join("\n", fields.Select(f => f.Item1 + ": " + f.Item2)) + pageSuffix;
        extraction.AddPage(Guid.NewGuid(), 1, page, 0);
        var offset = 0;
        foreach (var (key, value) in fields)
        {
            var field = extraction.AddField(Guid.NewGuid(), key, 0, FieldValueType.String, value, 1, now);
            var start = offset + Encoding.UTF8.GetByteCount(key + ": ");
            extraction.AddEvidence(field, Guid.NewGuid(), 1, value, start, start + Encoding.UTF8.GetByteCount(value));
            offset += Encoding.UTF8.GetByteCount(key + ": " + value + "\n");
        }
        extraction.RecordProviderMetrics(1, 1, null, null, null);
        extraction.Succeed("invoice", "en", now, "test", "1");
        doc.RecordExtractionSuccess(extraction, now);
        var review = doc.OpenReview(Guid.NewGuid(), seed.ActorA, now);
        foreach (var field in extraction.Fields)
            review.AddAction(Guid.NewGuid(), seed.ActorA, review.Actions.Count + 1, ReviewActionType.Confirmed,
                field.FieldKey, 0, field.Id, null, null, null, now);
        review.Complete(extraction.Fields, now);
        var index = new CitedDocumentIndex(Guid.NewGuid(), doc, review, "page-lines", "1.0.0-b2048-o256", now);
        foreach (var field in extraction.Fields)
            index.AddReviewedFact(Guid.NewGuid(), index.Chunks.Count, review, field.FieldKey, 0, extraction);
        if (pageSuffix.Length > 0)
            index.AddPageChunk(Guid.NewGuid(), index.Chunks.Count, extraction.Pages.Single(), 0, Encoding.UTF8.GetByteCount(page));
        index.Publish();
        var run = new CitedRetrievalRun(Guid.NewGuid(), seed.OrganizationA, seed.ActorA,
            "What tax-registration identifier belongs to the seller?", Guid.NewGuid().ToString(), "typed-test-scope", "{}", [index], now);
        run.MarkRetrieved(0);
        await using var context = _database.CreateContext();
        context.Documents.Add(doc);
        context.CitedDocumentIndexes.Add(index);
        context.CitedRetrievalRuns.Add(run);
        await context.SaveChangesAsync();
        return (seed.OrganizationA, seed.ActorA, seed.OrganizationB, seed.ActorB, run.Id, index.Id);
    }
}
