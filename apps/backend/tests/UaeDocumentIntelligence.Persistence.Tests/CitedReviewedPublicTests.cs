using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public async Task Reviewed_public_answer_saves_claims_citations_and_replays_one_atomic_request()
    {
        var seed = await SeedTypedFactCorpus();
        var key = Guid.NewGuid().ToString();
        async Task<CitedQuestionView> Ask()
        {
            await using var services = CitedServices(seed.Org, seed.Actor, "http://127.0.0.1:1");
            return await services.GetRequiredService<CitedReviewedQuestionHandler>().HandleAsync(
                "What is the supplier TRN on the invoice?", key, [seed.Index], default);
        }
        var concurrent = await Task.WhenAll(Ask(), Ask());
        Assert.Equal(concurrent[0].Retrieval.Id, concurrent[1].Retrieval.Id);
        Assert.Equal(1, concurrent.Count(x => !x.Retrieval.Replayed));
        var answer = concurrent[0];
        Assert.Equal("ReviewedAnswer", answer.Stage);
        Assert.False(answer.AnswerGenerationEnabled);
        Assert.Equal("Invoice supplier TRN: 00000701.", Assert.Single(answer.ReviewedAnswer!.Claims).Text);
        Assert.Single(answer.Retrieval.Hits);
        await using var reader = CitedServices(seed.Org, seed.Actor, "http://127.0.0.1:1");
        var history = await reader.GetRequiredService<CitedQuestionHandler>().GetAsync(answer.Retrieval.Id, default);
        Assert.Equal(JsonSerializer.Serialize(answer.ReviewedAnswer), JsonSerializer.Serialize(history.ReviewedAnswer));
        var citation = answer.ReviewedAnswer.Claims[0].Citations.Single();
        var evidence = await reader.GetRequiredService<CitedWorkspaceHandler>().EvidenceAsync(
            answer.Retrieval.Id, citation.HitId, citation.SpanId, default);
        Assert.Equal("supplier_trn[0]: 00000701", evidence.ReviewedFact);
        Assert.Equal("Confirmed", evidence.Review!.Action);
        var span = answer.Retrieval.Hits.Single().Spans.Single();
        var page = await reader.GetRequiredService<CitedWorkspaceHandler>().EvidenceAsync(
            answer.Retrieval.Id, citation.HitId, span.Id, default);
        Assert.Equal("00000701", page.Page!.Quote);
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_answers"));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_reservations"));
        await SpendDenied("request_key_reused", () => reader.GetRequiredService<CitedReviewedQuestionHandler>()
            .HandleAsync("What is the invoice total?", key, [seed.Index], default));
    }

    [Theory]
    [InlineData("What is the buyer TIN?", "unsupported_scope")]
    [InlineData("When is the invoice due?", "incomplete_evidence")]
    public async Task Reviewed_public_refusal_is_saved_and_reopened_without_claims(string question, string reason)
    {
        var seed = await SeedTypedFactCorpus();
        await using var services = CitedServices(seed.Org, seed.Actor, "http://127.0.0.1:1");
        var first = await services.GetRequiredService<CitedReviewedQuestionHandler>().HandleAsync(question,
            Guid.NewGuid().ToString(), [seed.Index], default);
        Assert.Equal("InsufficientEvidence", first.ReviewedAnswer!.Status);
        Assert.Equal(reason, first.ReviewedAnswer.ReasonCode);
        Assert.Empty(first.ReviewedAnswer.Claims);
        Assert.Empty(first.Retrieval.Hits);
        var saved = await services.GetRequiredService<CitedQuestionHandler>().GetAsync(first.Retrieval.Id, default);
        Assert.Equal(JsonSerializer.Serialize(first.ReviewedAnswer), JsonSerializer.Serialize(saved.ReviewedAnswer));
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("quarantined")]
    [InlineData("other_actor")]
    [InlineData("foreign")]
    public async Task Reviewed_public_history_retrieval_and_evidence_recheck_access(string change)
    {
        var seed = await SeedTypedFactCorpus();
        await using var original = CitedServices(seed.Org, seed.Actor, "http://127.0.0.1:1");
        var answer = await original.GetRequiredService<CitedReviewedQuestionHandler>().HandleAsync(
            "What is the supplier TRN?", Guid.NewGuid().ToString(), [seed.Index], default);
        var actor = seed.Actor;
        var org = seed.Org;
        if (change == "inactive") await ExecuteAsync("UPDATE actors SET is_active=false WHERE id=@id", ("id", actor));
        if (change == "quarantined")
        {
            await using var db = _database.CreateContext();
            (await db.CitedDocumentIndexes.SingleAsync(i => i.Id == seed.Index)).Retire(true);
            await db.SaveChangesAsync();
        }
        if (change == "foreign") { org = seed.ForeignOrg; actor = seed.ForeignActor; }
        if (change == "other_actor") actor = Guid.NewGuid();
        await using var restricted = CitedServices(org, actor, "http://127.0.0.1:1");
        await Assert.ThrowsAsync<CitedWorkflowException>(() => restricted.GetRequiredService<CitedQuestionHandler>()
            .GetAsync(answer.Retrieval.Id, default));
        await Assert.ThrowsAsync<CitedWorkflowException>(() => restricted.GetRequiredService<RetrieveEvidenceHandler>()
            .GetAsync(answer.Retrieval.Id, default));
        await Assert.ThrowsAsync<CitedWorkflowException>(() => restricted.GetRequiredService<CitedWorkspaceHandler>()
            .EvidenceAsync(answer.Retrieval.Id, answer.Retrieval.Hits.Single().HitId, null, default));
        await Assert.ThrowsAsync<CitedWorkflowException>(() => restricted.GetRequiredService<CitedReviewedQuestionHandler>()
            .HandleAsync("What is the supplier TRN?", Guid.NewGuid().ToString(), [seed.Index], default));
    }

    [Fact]
    public async Task Reviewed_public_answer_insert_failure_rolls_back_run_hits_and_claims()
    {
        var seed = await SeedTypedFactCorpus();
        var before = await ScalarAsync<long>("SELECT count(*) FROM cited_retrieval_runs");
        await ExecuteAsync("""
            CREATE FUNCTION reject_test_answer() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'synthetic answer failure'; END $$;
            CREATE TRIGGER reject_test_answer BEFORE INSERT ON cited_answers FOR EACH ROW EXECUTE FUNCTION reject_test_answer();
            """);
        await using var services = CitedServices(seed.Org, seed.Actor, "http://127.0.0.1:1");
        await Assert.ThrowsAsync<DbUpdateException>(() => services.GetRequiredService<CitedReviewedQuestionHandler>()
            .HandleAsync("What is the supplier TRN?", Guid.NewGuid().ToString(), [seed.Index], default));
        Assert.Equal(before, await ScalarAsync<long>("SELECT count(*) FROM cited_retrieval_runs"));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM retrieval_hits"));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_answers"));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM answer_claims"));
    }
}
