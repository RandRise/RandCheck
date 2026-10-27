using System.Data.Common;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.CitedQuestions;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Theory]
    [InlineData("completed", "Settled", false)]
    [InlineData("refusal", "Settled", false)]
    [InlineData("incomplete", "Settled", false)]
    [InlineData("invalid_citation", "Settled", false)]
    [InlineData("payment_claim", "Settled", false)]
    [InlineData("insufficient", "Settled", false)]
    [InlineData("unknown_usage", "Uncertain", true)]
    [InlineData("http_failure", "Uncertain", true)]
    [InlineData("overrun", "Settled", true)]
    public async Task OpenAi_pilot_private_roundtrip_accounts_before_validation_and_never_redispatches(
        string mode, string state, bool freeze)
    {
        var graph = await SeedCitedGraphAsync();
        var runId = await GenerationRun(graph);
        await using var server = await PrivateCitedServer.StartAsync(openAiOutcome: mode);
        await using var services = CitedPilotServices(graph.Seed.OrganizationA, graph.Seed.ActorA, server.Url, openAi: true);
        var coordinator = services.GetRequiredService<CitedGenerationCoordinator>();
        if (mode is "completed" or "overrun" or "insufficient")
        {
            var answer = await coordinator.ProposeAsync(runId, default);
            Assert.False(answer.PublicationAllowed);
            if (mode == "insufficient") Assert.Empty(answer.Validation.Claims);
            else Assert.Single(answer.Validation.Claims);
            Assert.Equal(CitedPaymentClaimPolicy.Version, answer.PaymentPolicyVersion);
            Assert.False(answer.Validation.SemanticSupportEstablished);
        }
        else await SpendDenied(state == "Uncertain" ? "generation_outcome_uncertain" : "answer_proposal_not_accepted",
            () => coordinator.ProposeAsync(runId, default));
        Assert.Equal(state, await ScalarAsync<string>("SELECT state FROM cited_spend_reservations"));
        Assert.Equal(.002m, await ScalarAsync<decimal>("SELECT reserved_usd FROM cited_spend_reservations"));
        Assert.Equal(state == "Uncertain" ? 0m : mode == "overrun" ? .000053m : .000052m,
            await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
        Assert.Equal(freeze, await ScalarAsync<bool>("SELECT requires_reconciliation FROM cited_pilot_budgets"));
        Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM cited_generation_artifacts"));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_answers WHERE retrieval_run_id=@id", ("id", runId)));
        var saved = await services.GetRequiredService<ICitedGenerationJournal>().GetSavedAsync(runId, default);
        var outcome = JsonSerializer.Deserialize<PrivateAnswerResult>(saved!.OutcomeUtf8!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(1000, outcome.OpenAi!.MeasuredInputTokens);
        Assert.NotNull(outcome.OpenAi.CountResponseUtf8);
        Assert.Equal("req_generation_fixture", outcome.OpenAi.ProviderRequestId);
        // Restart the whole .NET scope. The replacement private service must not be contacted.
        await using var restarted = CitedPilotServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1", openAi: true);
        var replay = restarted.GetRequiredService<CitedGenerationCoordinator>();
        if (mode is "completed" or "overrun" or "insufficient") Assert.True((await replay.ProposeAsync(runId, default)).Replayed);
        else await SpendDenied(state == "Uncertain" ? "generation_already_dispatched" : "answer_proposal_not_accepted",
            () => replay.ProposeAsync(runId, default));
        using var http = new HttpClient();
        Assert.Equal(new[] { "count", "generate" }, await http.GetFromJsonAsync<string[]>(server.Url + "/probe"));
    }

    [Fact]
    public async Task OpenAi_pilot_requires_exact_explicit_profile_and_current_scope()
    {
        var graph = await SeedCitedGraphAsync();
        var runId = await GenerationRun(graph);
        await using var server = await PrivateCitedServer.StartAsync(openAiOutcome: "completed");
        await using var services = CitedPilotServices(graph.Seed.OrganizationA, graph.Seed.ActorA, server.Url, openAi: true);
        await using (var clockDb = new UaeDocumentIntelligence.Infrastructure.Persistence.AppDbContext(
            services.GetRequiredService<DbContextOptions<UaeDocumentIntelligence.Infrastructure.Persistence.AppDbContext>>()))
        {
            var fixtureNow = await clockDb.Database.SqlQueryRaw<DateTimeOffset>("SELECT clock_timestamp() AS \"Value\"").SingleAsync();
            Assert.InRange(fixtureNow, CitedOpenAiPilot.Rate.ValidFromUtc, CitedOpenAiPilot.Rate.ValidUntilUtc.AddTicks(-1));
        }
        var client = services.GetRequiredService<ICitedAnswerClient>();
        await SpendDenied("generation_live_not_ready", () => Coordinator(services, client,
            new(true, CitedOpenAiPilot.Rate)).ProposeAsync(runId, default));
        await SpendDenied("generation_live_not_ready", () => Coordinator(services, client,
            new(true, CitedOpenAiPilot.Rate with { ModelVersion = "unapproved-model" }, true)).ProposeAsync(runId, default));
        await using var foreign = CitedPilotServices(graph.Seed.OrganizationB, graph.Seed.ActorB, server.Url, openAi: true);
        await SpendDenied("retrieval_not_found", () => foreign.GetRequiredService<CitedGenerationCoordinator>().ProposeAsync(runId, default));
        await ExecuteAsync("UPDATE cited_document_indexes SET status='Quarantined' WHERE id=@id", ("id", graph.Index.Id));
        await SpendDenied("evidence_unavailable", () => services.GetRequiredService<CitedGenerationCoordinator>().ProposeAsync(runId, default));
        using var http = new HttpClient();
        Assert.Empty((await http.GetFromJsonAsync<string[]>(server.Url + "/probe"))!);
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_reservations"));
    }

    [Theory]
    [InlineData("source_completed", true)]
    [InlineData("source_wrong_excerpt", false)]
    public async Task OpenAi_source_aware_low_private_roundtrip_preserves_usage_and_excerpt_policy(string mode, bool accepted)
    {
        var graph = await SeedCitedGraphAsync();
        var runId = await GenerationRun(graph);
        await using var server = await PrivateCitedServer.StartAsync(openAiOutcome: mode);
        await using var services = CitedPilotServices(graph.Seed.OrganizationA, graph.Seed.ActorA, server.Url, openAi: true);
        var settings = new CitedGenerationSettings(true, CitedOpenAiPilot.Rate, true, true, true);
        var coordinator = Coordinator(services, services.GetRequiredService<ICitedAnswerClient>(), settings);
        if (accepted)
        {
            var result = await coordinator.ProposeAsync(runId, default);
            Assert.False(result.PublicationAllowed);
            Assert.Equal(CitedAnswerValidator.SourceVersion, result.Validation.ValidatorVersion);
            Assert.Equal(graph.Chunk.Content, result.Validation.Claims.Single().Citations.Single().SupportText);
            Assert.True((await coordinator.ProposeAsync(runId, default)).Replayed);
        }
        else
        {
            await SpendDenied("answer_proposal_not_accepted", () => coordinator.ProposeAsync(runId, default));
            await SpendDenied("answer_proposal_not_accepted", () => coordinator.ProposeAsync(runId, default));
        }
        Assert.Equal("Settled", await ScalarAsync<string>("SELECT state FROM cited_spend_reservations"));
        Assert.Equal(.000052m, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_answers WHERE retrieval_run_id=@id", ("id", runId)));
        using var http = new HttpClient();
        Assert.Equal(new[] { "count", "generate" }, await http.GetFromJsonAsync<string[]>(server.Url + "/probe"));
    }

    // Only this test configuration substitutes the DB clock; production still uses clock_timestamp().
    private sealed class PilotFixtureClock : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("SELECT clock_timestamp() AS \"Value\"", StringComparison.Ordinal))
            {
                var rate = CitedOpenAiPilot.Rate;
                var fixtureTime = rate.ValidFromUtc.AddTicks((rate.ValidUntilUtc - rate.ValidFromUtc).Ticks / 2);
                command.CommandText = command.CommandText.Replace("clock_timestamp()",
                    $"TIMESTAMPTZ '{fixtureTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture)}'", StringComparison.Ordinal);
            }
            return ValueTask.FromResult(result);
        }
    }
}
