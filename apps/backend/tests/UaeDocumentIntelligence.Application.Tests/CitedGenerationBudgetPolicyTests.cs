using UaeDocumentIntelligence.Application.CitedQuestions;

namespace UaeDocumentIntelligence.Application.Tests;

public sealed class CitedGenerationBudgetPolicyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-08T00:00:00Z");
    // Test-only rates, not a configured provider price or lifecycle.
    private static readonly CitedGenerationRate Rate = new("test", "test-model", "1", "test-rate-1",
        Now.AddDays(-1), Now.AddDays(1), 0.05m, 0.40m);
    private static readonly CitedBudgetSnapshot Budget = new(Guid.NewGuid(), "USD", 8m, 0m, 0m, 0, false);

    [Fact]
    public void Disabled_by_default_without_echoing_configuration()
    {
        var error = Assert.Throws<CitedWorkflowException>(() =>
            CitedGenerationBudgetPolicy.Quote(Rate, Budget, Now, 8000, 4000));
        Assert.Equal("generation_disabled", error.Code);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain(Rate.Model, error.Message);
    }

    [Fact]
    public void Quote_binds_versions_and_counts_all_reserved_output_without_spending()
    {
        var quote = Quote();
        Assert.Equal(0.002m, quote.ReservedUsd);
        Assert.Equal(Budget.BudgetId, quote.BudgetId);
        Assert.Equal(Rate.ModelVersion, quote.ModelVersion);
        Assert.Equal(Rate.RateVersion, quote.RateVersion);
        Assert.Equal(8000, quote.MaximumInputTokens);
        Assert.Equal(4000, quote.MaximumOutputTokens);
        Assert.Equal(0m, Budget.SettledUsd);
        Assert.Equal(quote, Quote()); // A repeated pure quote does not reserve or dispatch.
    }

    [Fact]
    public void Exact_budget_boundary_is_allowed_but_one_microdollar_over_is_denied()
    {
        Assert.Equal(0.002m, Quote(budget: Budget with { SettledUsd = 7.998m }).ReservedUsd);
        Denied("pilot_budget_exhausted", budget: Budget with { SettledUsd = 7.998001m });
        Denied("pilot_budget_exhausted", budget: Budget with { SettledUsd = 8.01m });
    }

    [Fact]
    public void Sub_microdollar_cost_rounds_up()
    {
        var quote = CitedGenerationBudgetPolicy.Quote(Rate, Budget, Now, 1, 1, true);
        Assert.Equal(0.000001m, quote.ReservedUsd);
        var tiny = Rate with { MaximumInputUsdPerMillion = 0.0000000000000000000000000001m,
            MaximumOutputUsdPerMillion = 0.0000000000000000000000000001m };
        Assert.Equal(0.000001m, CitedGenerationBudgetPolicy.Quote(tiny, Budget, Now, 1, 1, true).ReservedUsd);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(8001, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 4001)]
    public void Unbounded_or_invalid_token_reservations_are_rejected(int input, int output)
    {
        Assert.Equal("generation_token_limit", Assert.Throws<CitedWorkflowException>(() =>
            CitedGenerationBudgetPolicy.Quote(Rate, Budget, Now, input, output, true)).Code);
    }

    [Fact]
    public void Pending_or_uncertain_charges_and_in_flight_calls_block_admission()
    {
        Denied("budget_reconciliation_required", budget: Budget with { OutstandingUsd = 0.001m });
        Denied("budget_reconciliation_required", budget: Budget with { RequiresReconciliation = true });
        Denied("generation_in_flight", budget: Budget with { InFlight = 1 });
    }

    [Fact]
    public void Rate_validity_has_inclusive_start_and_exclusive_end()
    {
        Assert.NotNull(Quote(rate: Rate with { ValidFromUtc = Now }));
        Denied("rate_or_model_expired", rate: Rate with { ValidFromUtc = Now.AddTicks(1) });
        Denied("rate_or_model_expired", rate: Rate with { ValidUntilUtc = Now });
    }

    [Fact]
    public void Invalid_ledger_snapshots_fail_closed()
    {
        foreach (var budget in new[] { Budget with { BudgetId = Guid.Empty }, Budget with { Currency = "AED" },
            Budget with { CeilingUsd = 0m }, Budget with { CeilingUsd = 8.000001m },
            Budget with { SettledUsd = -1m }, Budget with { OutstandingUsd = -1m },
            Budget with { InFlight = -1 } })
            Denied("invalid_budget_configuration", budget: budget);
    }

    [Fact]
    public void Unknown_rates_versions_and_overflow_fail_without_leaking_details()
    {
        foreach (var rate in new[] { Rate with { Provider = "" }, Rate with { Model = "private\nmodel" },
            Rate with { ModelVersion = " " }, Rate with { RateVersion = new string('x', 101) },
            Rate with { MaximumInputUsdPerMillion = 0m }, Rate with { MaximumOutputUsdPerMillion = -1m },
            Rate with { ValidUntilUtc = Rate.ValidFromUtc },
            Rate with { MaximumInputUsdPerMillion = decimal.MaxValue } })
            Denied("invalid_budget_configuration", rate: rate);
    }

    private static CitedReservationQuote Quote(CitedGenerationRate? rate = null, CitedBudgetSnapshot? budget = null) =>
        CitedGenerationBudgetPolicy.Quote(rate ?? Rate, budget ?? Budget, Now, 8000, 4000, true);

    private static void Denied(string code, CitedGenerationRate? rate = null, CitedBudgetSnapshot? budget = null)
    {
        var error = Assert.Throws<CitedWorkflowException>(() => Quote(rate, budget));
        Assert.Equal(code, error.Code);
        Assert.Equal("Answer generation admission was denied.", error.Message);
        Assert.Null(error.InnerException);
    }
}
