using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.CitedQuestions;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Theory]
    [InlineData("cited-answer-prompt-v1", false)]
    [InlineData("cited-answer-prompt-v2", false)]
    [InlineData("cited-answer-prompt-v1", true)]
    [InlineData("cited-answer-prompt-v2", true)]
    public async Task Quality_fixes_legacy_preparations_cannot_dispatch_but_settled_replay_uses_current_policy(
        string version, bool unsafePayment)
    {
        var graph = await SeedCitedGraphAsync();
        var runId = await GenerationRun(graph);
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        PrivateAnswerPreparation? legacy = null;
        var client = new AnswerReply
        {
            ChangePreparation = p => Task.FromResult(legacy = p with { PromptVersion = version })
        };
        await SpendDenied("invalid_prompt_preparation", () => Coordinator(services, client).ProposeAsync(runId, default));
        Assert.Equal(0, client.Calls);
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_reservations"));

        // Seed a historical settled test-only artifact through the real immutable
        // journal. Never update saved rows or access the retained paid database.
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var dispatch = JsonSerializer.SerializeToUtf8Bytes(new { requestUtf8 = client.LastRequestUtf8, preparation = legacy }, json);
        var digest = Convert.ToHexString(SHA256.HashData(dispatch)).ToLowerInvariant();
        var journal = services.GetRequiredService<ICitedGenerationJournal>();
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        var spend = await journal.ReservePreparedAsync(new(runId, digest, SpendCommand().Rate, 1000, 4000), dispatch, true, default);
        Assert.True(await ledger.TryMarkDispatchedAsync(spend.Id, true, default));
        var prior = await client.ProposeAsync(dispatch, default); // In-memory fixture only.
        if (unsafePayment)
        {
            var proposal = JsonNode.Parse(prior.ProposalUtf8)!;
            proposal["claims"]![0]!["text"] = "The invoice was paid on 2026-08-31.";
            prior = prior with { ProposalUtf8 = JsonSerializer.SerializeToUtf8Bytes(proposal, json) };
        }
        var outcome = JsonSerializer.SerializeToUtf8Bytes(prior, json);
        await journal.RecordOutcomeAsync(spend.Id, outcome, .000025m, "test-usage:legacy", false, default);
        var offline = new AnswerReply { ChangePreparation = _ => throw new InvalidOperationException("Offline") };
        if (unsafePayment)
            await SpendDenied("answer_proposal_not_accepted", () => Coordinator(services, offline).ProposeAsync(runId, default));
        else
        {
            var replay = await Coordinator(services, offline).ProposeAsync(runId, default);
            Assert.True(replay.Replayed);
            Assert.False(replay.PublicationAllowed);
            Assert.Equal(CitedPaymentClaimPolicy.Version, replay.PaymentPolicyVersion);
        }
        Assert.Equal(0, offline.Calls);
        Assert.Equal(0, offline.Preparations);
        Assert.Equal(outcome, (await journal.GetSavedAsync(runId, default))!.OutcomeUtf8);
        Assert.Equal(3, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_events"));
        Assert.Equal(.000025m, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
    }
}
