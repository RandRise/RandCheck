using UaeDocumentIntelligence.Application.CitedQuestions;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Theory]
    [InlineData("zero")]
    [InlineData("oversized")]
    [InlineData("source_hash")]
    [InlineData("model")]
    [InlineData("prompt_version")]
    [InlineData("counter_version")]
    [InlineData("output_limit")]
    [InlineData("prompt_hash")]
    public async Task Prompt_preflight_invalid_receipts_never_reserve_or_generate(string defect)
    {
        var graph = await SeedCitedGraphAsync();
        var runId = await GenerationRun(graph);
        var client = new AnswerReply
        {
            ChangePreparation = receipt => Task.FromResult(defect switch
            {
                "zero" => receipt with { InputTokens = 0 },
                "oversized" => receipt with { InputTokens = 8001 },
                "source_hash" => receipt with { RequestSha256 = new string('f', 64) },
                "model" => receipt with { Model = receipt.Model with { Version = "other" } },
                "prompt_version" => receipt with { PromptVersion = "unapproved" },
                "counter_version" => receipt with { CounterVersion = "unverified" },
                "output_limit" => receipt with { MaximumOutputTokens = 3999 },
                _ => receipt with { PromptSha256 = "private-invalid-hash" }
            })
        };
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        await SpendDenied("invalid_prompt_preparation", () => Coordinator(services, client).ProposeAsync(runId, default));
        Assert.Equal(0, client.Calls);
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_reservations"));
    }

    [Fact]
    public async Task Prompt_preflight_timeout_is_safe_and_has_no_spending_side_effect()
    {
        var graph = await SeedCitedGraphAsync();
        var runId = await GenerationRun(graph);
        var client = new AnswerReply { ChangePreparation = _ => throw new TimeoutException("SECRET INVOICE") };
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        await SpendDenied("prompt_preparation_failed", () => Coordinator(services, client).ProposeAsync(runId, default));
        Assert.Equal(1, client.Preparations);
        Assert.Equal(0, client.Calls);
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_reservations"));
    }

    [Fact]
    public async Task Prompt_preflight_quarantine_during_measurement_blocks_reservation()
    {
        var graph = await SeedCitedGraphAsync();
        var runId = await GenerationRun(graph);
        var client = new AnswerReply { ChangePreparation = async receipt =>
        {
            await ExecuteAsync("UPDATE cited_document_indexes SET status='Quarantined' WHERE id=@id", ("id", graph.Index.Id));
            return receipt;
        } };
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        await SpendDenied("evidence_unavailable", () => Coordinator(services, client).ProposeAsync(runId, default));
        Assert.Equal(0, client.Calls);
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_reservations"));
    }

    [Fact]
    public async Task Prompt_preflight_saved_result_replay_does_not_remeasure_or_redispatch()
    {
        var graph = await SeedCitedGraphAsync();
        var runId = await GenerationRun(graph);
        var settings = new CitedGenerationSettings(true, SpendCommand().Rate);
        var client = new AnswerReply();
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        await Coordinator(services, client, settings).ProposeAsync(runId, default);
        client.ChangePreparation = receipt => Task.FromResult(receipt with { InputTokens = 1001 });
        Assert.True((await Coordinator(services, client, settings).ProposeAsync(runId, default)).Replayed);
        Assert.Equal(1, client.Preparations);
        Assert.Equal(1, client.Calls);
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_reservations"));
    }

    [Fact]
    public async Task Prompt_preflight_usage_over_reserved_input_keeps_hold()
    {
        var graph = await SeedCitedGraphAsync();
        var runId = await GenerationRun(graph);
        var client = new AnswerReply((result, _) => Task.FromResult(result with { InputTokens = 1001 }));
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        await SpendDenied("generation_outcome_uncertain", () => Coordinator(services, client).ProposeAsync(runId, default));
        Assert.Equal("Uncertain", await ScalarAsync<string>("SELECT state FROM cited_spend_reservations"));
        Assert.Equal(0m, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
    }

    [Fact]
    public async Task Prompt_preflight_measured_reservation_admits_only_within_remaining_allowance()
    {
        var graph = await SeedCitedGraphAsync();
        var runId = await GenerationRun(graph);
        await ExecuteAsync("UPDATE cited_pilot_budgets SET settled_usd=7.9983");
        var client = new AnswerReply();
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        await Coordinator(services, client).ProposeAsync(runId, default);
        Assert.Equal(0.00165m, await ScalarAsync<decimal>("SELECT reserved_usd FROM cited_spend_reservations"));
        Assert.Equal(7.998325m, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
    }
}
