using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public async Task Pilot_source_connection_recovery_only_accepts_the_named_incident_and_retains_cost()
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1", pilotClock: true);
        var holds = services.GetRequiredService<ICitedPilotUnknownHold>();
        foreach (var _ in Enumerable.Range(0, 2))
        {
            var prior = await SourceUnknownHold(services);
            await holds.RetainSourceGenerationFailureAsync(prior.Id, true, default);
        }
        var count = await SourceUnknownHold(services, countStage: true);
        await holds.RetainSourceCountFailureAsync(count.Id, true, default);
        var incident = await SourceUnknownHold(services, operationId: Guid.Parse("7351e37b-3261-48aa-8294-71c3c521710f"));
        var result = await holds.RetainSourceGenerationFailureAsync(incident.Id, true, default);
        Assert.Null(result.ActualUsd);
        Assert.Equal(.002m, result.ReservedUsd);
        Assert.False(await services.GetRequiredService<ICitedGenerationLedger>().TryMarkDispatchedAsync(incident.Id, true, default));
        var other = await SourceUnknownHold(services);
        await SpendDenied("pilot_unknown_hold_limit", () => holds.RetainSourceGenerationFailureAsync(other.Id, true, default));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("prompt")]
    [InlineData("response")]
    [InlineData("code")]
    [InlineData("kind")]
    [InlineData("counted")]
    public async Task Pilot_source_count_hold_requires_pre_generation_network_evidence(string defect)
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1", pilotClock: true);
        var spend = await SourceUnknownHold(services, defect, countStage: defect != "counted");
        var holds = services.GetRequiredService<ICitedPilotUnknownHold>();
        if (defect != "none")
        {
            await SpendDenied("pilot_unknown_hold_ineligible", () => holds.RetainSourceCountFailureAsync(spend.Id, true, default));
            Assert.True(await ScalarAsync<bool>("SELECT requires_reconciliation FROM cited_pilot_budgets"));
            return;
        }
        var held = await holds.RetainSourceCountFailureAsync(spend.Id, true, default);
        Assert.Equal(CitedSpendState.HeldUnknown, held.State);
        Assert.Null(held.ActualUsd);
        Assert.Equal(.002m, held.ReservedUsd);
        Assert.True((await holds.RetainSourceCountFailureAsync(spend.Id, true, default)).Replayed);
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        Assert.False(await ledger.TryMarkDispatchedAsync(spend.Id, true, default));
        await SpendDenied("invalid_spend_transition", () => ledger.SettleAsync(spend.Id, 0, "fake-zero", default));
        await SpendDenied("invalid_spend_transition", () => ledger.CancelBeforeDispatchAsync(spend.Id, default));
    }

    [Fact]
    public async Task Pilot_source_count_hold_enforces_separate_count_cap_and_global_budget()
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1", pilotClock: true);
        var holds = services.GetRequiredService<ICitedPilotUnknownHold>();
        foreach (var _ in Enumerable.Range(0, 27))
        {
            var first = await SourceUnknownHold(services, countStage: true);
            await holds.RetainSourceCountFailureAsync(first.Id, true, default);
        }
        await ExecuteAsync("UPDATE cited_pilot_budgets SET settled_usd=7.945");
        await SpendDenied("pilot_budget_exhausted", () => SourceUnknownHold(services, countStage: true));
        await ExecuteAsync("UPDATE cited_pilot_budgets SET settled_usd=0");
        var next = await SourceUnknownHold(services, countStage: true);
        await SpendDenied("pilot_unknown_hold_limit", () => holds.RetainSourceCountFailureAsync(next.Id, true, default));
        Assert.True(await ScalarAsync<bool>("SELECT requires_reconciliation FROM cited_pilot_budgets"));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("count")]
    [InlineData("prompt")]
    [InlineData("response")]
    [InlineData("large")]
    public async Task Pilot_source_generation_hold_requires_bound_count_and_keeps_unknown_cost(string defect)
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1", pilotClock: true);
        var spend = await SourceUnknownHold(services, defect);
        if (defect == "large") await ExecuteAsync("UPDATE cited_spend_reservations SET reserved_usd=.002001");
        var holds = services.GetRequiredService<ICitedPilotUnknownHold>();
        if (defect != "none")
        {
            await SpendDenied("pilot_unknown_hold_ineligible", () => holds.RetainSourceGenerationFailureAsync(spend.Id, true, default));
            Assert.True(await ScalarAsync<bool>("SELECT requires_reconciliation FROM cited_pilot_budgets"));
            return;
        }
        var result = await holds.RetainSourceGenerationFailureAsync(spend.Id, true, default);
        Assert.Equal(CitedSpendState.HeldUnknown, result.State);
        Assert.Null(result.ActualUsd);
        Assert.Equal(.002m, result.ReservedUsd);
        Assert.True((await holds.RetainSourceGenerationFailureAsync(spend.Id, true, default)).Replayed);
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        Assert.False(await ledger.TryMarkDispatchedAsync(spend.Id, true, default));
        await SpendDenied("invalid_spend_transition", () => ledger.SettleAsync(spend.Id, 0, "fake-zero", default));
        Assert.Equal(0m, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
    }

    [Fact]
    public async Task Pilot_source_generation_hold_stops_at_aggregate_cap_and_preserves_global_budget()
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1", pilotClock: true);
        var holds = services.GetRequiredService<ICitedPilotUnknownHold>();
        foreach (var _ in Enumerable.Range(0, 2))
        {
            var spend = await SourceUnknownHold(services);
            await holds.RetainSourceGenerationFailureAsync(spend.Id, true, default);
        }
        await ExecuteAsync("UPDATE cited_pilot_budgets SET settled_usd=7.995");
        await SpendDenied("pilot_budget_exhausted", () => SourceUnknownHold(services));
        await ExecuteAsync("UPDATE cited_pilot_budgets SET settled_usd=0");
        var third = await SourceUnknownHold(services);
        await SpendDenied("pilot_unknown_hold_limit", () => holds.RetainSourceGenerationFailureAsync(third.Id, true, default));
        Assert.Equal(.004m, await ScalarAsync<decimal>("SELECT sum(reserved_usd) FROM cited_spend_reservations WHERE state='HeldUnknown'"));
        Assert.True(await ScalarAsync<bool>("SELECT requires_reconciliation FROM cited_pilot_budgets"));
    }

    private static async Task<CitedSpendView> SourceUnknownHold(IServiceProvider services, string defect = "none", bool countStage = false, Guid? operationId = null)
    {
        var hash = new string('a', 64);
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { preparation = new {
            promptVersion = defect == "prompt" ? "cited-answer-prompt-v3" : "cited-answer-prompt-v4",
            openAi = new { reasoningEffort = "low", generationSha256 = hash } } });
        var command = ArtifactCommand(bytes) with { OperationId = operationId ?? Guid.NewGuid(), Rate = CitedOpenAiPilot.Rate,
            MaximumInputTokens = 8000, MaximumOutputTokens = 4000 };
        var journal = services.GetRequiredService<ICitedGenerationJournal>();
        var spend = await journal.ReservePreparedAsync(command, bytes, true, default);
        await services.GetRequiredService<ICitedGenerationLedger>().TryMarkDispatchedAsync(spend.Id, true, default);
        var outcome = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new {
            outcome = "transport_uncertain", inputTokens = 0, outputTokens = 0,
            openAi = new { generationSha256 = hash, accountingKnown = false, countRequestId = countStage ? null : "req_count_fixture",
                countResponseUtf8 = countStage ? null : "{\"object\":\"response.input_tokens\",\"input_tokens\":2265}"u8.ToArray(),
                measuredInputTokens = countStage ? (int?)null : defect == "count" ? 1234 : 2265,
                transportErrorCode = defect == "code" ? "unclassified" : "openai_transport_uncertain",
                transportFailureKind = defect == "kind" ? "protocol" : "network",
                providerRequestId = defect == "response" ? "req_partial_response" : (string?)null,
                responseId = (string?)null, responseSha256 = (string?)null, responseUtf8 = (string?)null, httpStatus = (int?)null } });
        await journal.RecordOutcomeAsync(spend.Id, outcome, null, null, true, default);
        return spend;
    }

    [Theory]
    [InlineData("none")]
    [InlineData("counted")]
    [InlineData("large")]
    public async Task Pilot_low_preflight_hold_keeps_full_cost_and_rejects_counted_or_large_incidents(string defect)
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1", pilotClock: true);
        var hash = new string('a',64);
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new {
            preparation = new { openAi = new { reasoningEffort="low", generationSha256=hash } } });
        var command = ArtifactCommand(bytes) with { Rate=CitedOpenAiPilot.Rate, MaximumInputTokens=8000, MaximumOutputTokens=4000 };
        var journal=services.GetRequiredService<ICitedGenerationJournal>();
        var ledger=services.GetRequiredService<ICitedGenerationLedger>();
        var spend=await journal.ReservePreparedAsync(command,bytes,true,default);
        await ledger.TryMarkDispatchedAsync(spend.Id,true,default);
        var outcome=System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new {
            outcome="transport_uncertain",inputTokens=0,outputTokens=0,
            openAi=new { generationSha256=hash,accountingKnown=false,countRequestId=defect=="counted"?"req_count":null,
                countResponseUtf8=(string?)null,measuredInputTokens=(int?)null,providerRequestId=(string?)null,
                responseId=(string?)null,responseSha256=(string?)null,responseUtf8=(string?)null,httpStatus=(int?)null } });
        await journal.RecordOutcomeAsync(spend.Id,outcome,null,null,true,default);
        if(defect=="large") await ExecuteAsync("UPDATE cited_spend_reservations SET reserved_usd=.002001");
        var holds=services.GetRequiredService<ICitedPilotUnknownHold>();
        if(defect!="none")
        {
            await SpendDenied("pilot_unknown_hold_ineligible",()=>holds.RetainLowPreflightFailureAsync(spend.Id,true,default));
            Assert.True(await ScalarAsync<bool>("SELECT requires_reconciliation FROM cited_pilot_budgets"));
            return;
        }
        var retained=await holds.RetainLowPreflightFailureAsync(spend.Id,true,default);
        Assert.Equal(CitedSpendState.HeldUnknown,retained.State);
        Assert.Null(retained.ActualUsd);
        Assert.Equal(.002m,retained.ReservedUsd);
        Assert.False(await ledger.TryMarkDispatchedAsync(spend.Id,true,default));
        Assert.True((await holds.RetainLowPreflightFailureAsync(spend.Id,true,default)).Replayed);
        Assert.False(await ScalarAsync<bool>("SELECT requires_reconciliation FROM cited_pilot_budgets"));
        Assert.Equal(0m,await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
    }

    private static async Task<CitedSpendView> UnknownHold(IServiceProvider services)
    {
        var bytes = PreparationArtifact();
        var command = EmbeddingSpendCommand(bytes) with { MaximumInputTokens = 16,
            Rate = new("openai", CitedEmbeddingStorage.Model, CitedEmbeddingStorage.Model, "test-rate",
                DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1), .02m, 0m) };
        var journal = services.GetRequiredService<ICitedGenerationJournal>();
        var spend = await journal.ReservePreparedAsync(command, bytes, true, default);
        await services.GetRequiredService<ICitedGenerationLedger>().TryMarkDispatchedAsync(spend.Id, true, default);
        await journal.RecordOutcomeAsync(spend.Id,
            "{\"outcome\":\"transport_uncertain\",\"accountingKnown\":false,\"inputTokens\":null}"u8.ToArray(),
            null, null, true, default);
        return spend;
    }

    [Fact]
    public async Task Pilot_unknown_hold_retains_cost_replays_and_admits_new_work_without_redispatch()
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var old = await UnknownHold(services);
        var holds = services.GetRequiredService<ICitedPilotUnknownHold>();
        var results = await Task.WhenAll(holds.RetainAsync(old.Id, true, default), holds.RetainAsync(old.Id, true, default));
        Assert.Single(results.Where(x => !x.Replayed));
        Assert.All(results, r => { Assert.Equal(CitedSpendState.HeldUnknown, r.State); Assert.Null(r.ActualUsd); });
        Assert.Equal(4, await ScalarAsync<int>("SELECT max(sequence) FROM cited_spend_events"));
        Assert.Equal(0m, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
        Assert.False(await ScalarAsync<bool>("SELECT requires_reconciliation FROM cited_pilot_budgets"));
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        Assert.False(await ledger.TryMarkDispatchedAsync(old.Id, true, default));
        await SpendDenied("invalid_spend_transition", () => ledger.CancelBeforeDispatchAsync(old.Id, default));
        await SpendDenied("invalid_spend_transition", () => ledger.SettleAsync(old.Id, 0, "pretend-zero", default));
        var next = await ledger.ReserveAsync(EmbeddingSpendCommand(), true, default);
        Assert.True(await ledger.TryMarkDispatchedAsync(next.Id, true, default));
        Assert.Equal(.000001m, await ScalarAsync<decimal>("SELECT reserved_usd FROM cited_spend_reservations WHERE state='HeldUnknown'"));
    }

    [Fact]
    public async Task Pilot_unknown_hold_requires_explicit_opt_in_owner_and_active_actor()
    {
        var seed = await SeedGraphAsync();
        await using var a = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        await using var b = CitedServices(seed.OrganizationB, seed.ActorB, "http://127.0.0.1:1");
        var old = await UnknownHold(a);
        await SpendDenied("pilot_unknown_hold_disabled", () => a.GetRequiredService<ICitedPilotUnknownHold>().RetainAsync(old.Id, false, default));
        await SpendDenied("reservation_not_found", () => b.GetRequiredService<ICitedPilotUnknownHold>().RetainAsync(old.Id, true, default));
        await ExecuteAsync("UPDATE actors SET is_active=false WHERE id=@id", ("id",seed.ActorA));
        await SpendDenied("reservation_not_found", () => a.GetRequiredService<ICitedPilotUnknownHold>().RetainAsync(old.Id, true, default));
        Assert.Equal("Uncertain", await ScalarAsync<string>("SELECT state FROM cited_spend_reservations"));
    }

    [Fact]
    public async Task Pilot_unknown_hold_is_still_counted_at_budget_ceiling_and_cannot_be_released_in_SQL()
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var old = await UnknownHold(services);
        await services.GetRequiredService<ICitedPilotUnknownHold>().RetainAsync(old.Id, true, default);
        await ExecuteAsync("UPDATE cited_pilot_budgets SET settled_usd=7.999840");
        await SpendDenied("pilot_budget_exhausted", () => services.GetRequiredService<ICitedGenerationLedger>()
            .ReserveAsync(EmbeddingSpendCommand(), true, default));
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation,
            "UPDATE cited_spend_reservations SET state='Cancelled' WHERE state='HeldUnknown'");
        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation,
            "UPDATE cited_spend_reservations SET reserved_usd=.0000001 WHERE state='HeldUnknown'");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pilot_unknown_hold_never_waives_known_overruns_or_flagged_settled_outcomes(bool flag)
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        var bytes = PreparationArtifact();
        var prior = await services.GetRequiredService<ICitedGenerationJournal>().ReservePreparedAsync(ArtifactCommand(bytes), bytes, true, default);
        await ledger.TryMarkDispatchedAsync(prior.Id, true, default);
        await services.GetRequiredService<ICitedGenerationJournal>().RecordOutcomeAsync(prior.Id,
            flag ? "{\"requiresReconciliation\":true}"u8.ToArray() : "{}"u8.ToArray(),
            flag ? .000001m : 1m, "test-proof", flag, default);
        // Test fixture constructs the later incident; production would have rejected admission.
        await ExecuteAsync("UPDATE cited_pilot_budgets SET requires_reconciliation=false");
        var old = await UnknownHold(services);
        await SpendDenied("pilot_other_reconciliation_required", () => services.GetRequiredService<ICitedPilotUnknownHold>().RetainAsync(old.Id, true, default));
        Assert.True(await ScalarAsync<bool>("SELECT requires_reconciliation FROM cited_pilot_budgets"));
    }

    [Fact]
    public async Task Pilot_unknown_hold_rejects_large_reservations_and_other_providers()
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var old = await UnknownHold(services);
        await ExecuteAsync("UPDATE cited_spend_reservations SET reserved_usd=.000161");
        await SpendDenied("pilot_unknown_hold_ineligible", () => services.GetRequiredService<ICitedPilotUnknownHold>().RetainAsync(old.Id, true, default));
        await ExecuteAsync("UPDATE cited_spend_reservations SET reserved_usd=.000001, provider='different-provider'");
        await SpendDenied("pilot_unknown_hold_ineligible", () => services.GetRequiredService<ICitedPilotUnknownHold>().RetainAsync(old.Id, true, default));
        Assert.Equal("Uncertain", await ScalarAsync<string>("SELECT state FROM cited_spend_reservations"));
    }
}
