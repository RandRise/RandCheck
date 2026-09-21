using System.Security.Cryptography;
using System.Text.Json;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Tests;

public sealed class CitedEmbeddingBudgetPolicyTests
{
    [Fact]
    public void Retained_unknown_amount_is_counted_without_claiming_settlement()
    {
        var budget = Budget with { SettledUsd = 7.999840m, RetainedUnknownUsd = .000001m };
        Denied("pilot_budget_exhausted", budget: budget);
        Assert.NotNull(Quote(budget: budget with { SettledUsd = 7.999839m }));
        Denied("invalid_budget_configuration", budget: Budget with { RetainedUnknownUsd = -.000001m });
        var generationRate = Rate with { MaximumOutputUsdPerMillion = .02m };
        Assert.Equal("pilot_budget_exhausted", Assert.Throws<CitedWorkflowException>(() =>
            CitedGenerationBudgetPolicy.Quote(generationRate,
                Budget with { SettledUsd = 7.999998m, RetainedUnknownUsd = .000002m }, Now, 1, 1, true)).Code);
    }
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-10T00:00:00Z");
    private static readonly CitedGenerationRate Rate = new("test-only", "fake-embedding", "1", "test-rate",
        Now.AddHours(-1), Now.AddHours(1), .02m, 0m);
    private static readonly CitedBudgetSnapshot Budget = new(Guid.NewGuid(), "USD", 8m, .001951m, 0m, 0, false);

    [Fact]
    public void Embedding_quote_is_input_only_and_uses_existing_shared_allowance()
    {
        var quote = Quote();
        Assert.Equal(.000160m, quote.ReservedUsd);
        Assert.Equal(0, quote.MaximumOutputTokens);
        Assert.Equal(CitedSpendWorkload.Embedding, quote.Workload);
        Assert.Equal(CitedGenerationBudgetPolicy.EmbeddingVersion, quote.PolicyVersion);
        Assert.Equal(Budget.BudgetId, quote.BudgetId);
        Assert.Equal(.001951m, Budget.SettledUsd); // A quote cannot spend.
        Assert.Equal(.000001m, Quote(input: 1).ReservedUsd);
        Assert.Equal(.000001m, Quote(input: 1, rate: Rate with
            { MaximumInputUsdPerMillion = .0000000000000000000000000001m }).ReservedUsd);
    }

    [Fact]
    public void Embedding_is_disabled_by_default_and_does_not_weaken_generation()
    {
        Assert.Equal("embedding_disabled", Assert.Throws<CitedWorkflowException>(() =>
            CitedGenerationBudgetPolicy.QuoteEmbedding(Rate, Budget, Now, 8000)).Code);
        Denied("invalid_budget_configuration", rate: Rate with { MaximumOutputUsdPerMillion = .4m });
        Denied("invalid_budget_configuration", rate: Rate with { MaximumOutputUsdPerMillion = -.4m });
        Assert.Equal("invalid_budget_configuration", Assert.Throws<CitedWorkflowException>(() =>
            CitedGenerationBudgetPolicy.Quote(Rate, Budget, Now, 8000, 1, true)).Code);
        Assert.Equal("generation_token_limit", Assert.Throws<CitedWorkflowException>(() =>
            CitedGenerationBudgetPolicy.Quote(Rate with { MaximumOutputUsdPerMillion = .4m },
                Budget, Now, 8000, 0, true)).Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(8001)]
    public void Embedding_batch_total_is_bounded(int input) => Denied("embedding_token_limit", input: input);

    [Fact]
    public void Embedding_requires_unfrozen_shared_budget_and_valid_rates()
    {
        Denied("budget_reconciliation_required", budget: Budget with { OutstandingUsd = .000001m });
        Denied("budget_reconciliation_required", budget: Budget with { RequiresReconciliation = true });
        Denied("generation_in_flight", budget: Budget with { InFlight = 1 });
        Denied("rate_or_model_expired", rate: Rate with { ValidUntilUtc = Now });
        Denied("rate_or_model_expired", rate: Rate with { ValidFromUtc = Now.AddTicks(1) });
        Assert.NotNull(Quote(rate: Rate with { ValidFromUtc = Now }));
        Denied("invalid_budget_configuration", rate: Rate with { MaximumInputUsdPerMillion = 0m });
        Denied("invalid_budget_configuration", rate: Rate with { MaximumInputUsdPerMillion = decimal.MaxValue });
        Denied("invalid_budget_configuration", budget: Budget with { CeilingUsd = 8.000001m });
    }

    [Fact]
    public void Embedding_cost_cannot_cross_the_shared_ceiling()
    {
        Assert.NotNull(Quote(budget: Budget with { SettledUsd = 7.999840m }));
        Denied("pilot_budget_exhausted", budget: Budget with { SettledUsd = 7.999841m });
    }

    [Fact]
    public void Generation_serialization_retains_the_exact_pre_workload_fingerprint()
    {
        var command = new ReserveCitedGeneration(Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new string('a', 64), Rate with { MaximumOutputUsdPerMillion = .4m }, 8000, 4000);
        // Property order and omission must match the original five-property record.
        var legacy = JsonSerializer.SerializeToUtf8Bytes(new { command.OperationId, command.RequestSha256,
            command.Rate, command.MaximumInputTokens, command.MaximumOutputTokens });
        var current = JsonSerializer.SerializeToUtf8Bytes(command);
        Assert.Equal(legacy, current);
        Assert.Equal(SHA256.HashData(legacy), SHA256.HashData(current));
        var embedding = command with { Workload = CitedSpendWorkload.Embedding, MaximumOutputTokens = 0 };
        Assert.Contains("\"Workload\":1", JsonSerializer.Serialize(embedding));
        Assert.NotEqual(Convert.ToHexString(SHA256.HashData(current)),
            Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(embedding))));
    }

    private static CitedReservationQuote Quote(CitedGenerationRate? rate = null,
        CitedBudgetSnapshot? budget = null, int input = 8000) =>
        CitedGenerationBudgetPolicy.QuoteEmbedding(rate ?? Rate, budget ?? Budget, Now, input, true);

    private static void Denied(string code, CitedGenerationRate? rate = null,
        CitedBudgetSnapshot? budget = null, int input = 8000) =>
        Assert.Equal(code, Assert.Throws<CitedWorkflowException>(() => Quote(rate, budget, input)).Code);
}
