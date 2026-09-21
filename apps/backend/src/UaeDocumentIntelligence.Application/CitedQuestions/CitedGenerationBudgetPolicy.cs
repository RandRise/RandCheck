using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.CitedQuestions;

/// <summary>Server-controlled, versioned maximum rates including applicable surcharges.</summary>
public sealed record CitedGenerationRate(string Provider, string Model, string ModelVersion,
    string RateVersion, DateTimeOffset ValidFromUtc, DateTimeOffset ValidUntilUtc,
    decimal MaximumInputUsdPerMillion, decimal MaximumOutputUsdPerMillion);

/// <summary>A ledger snapshot, not proof that a reservation was made atomically.</summary>
public sealed record CitedBudgetSnapshot(Guid BudgetId, string Currency, decimal CeilingUsd,
    decimal SettledUsd, decimal OutstandingUsd, int InFlight, bool RequiresReconciliation,
    decimal RetainedUnknownUsd = 0m);

public sealed record CitedReservationQuote(Guid BudgetId, string PolicyVersion,
    string Provider, string Model, string ModelVersion, string RateVersion,
    int MaximumInputTokens, int MaximumOutputTokens, decimal ReservedUsd,
    CitedSpendWorkload Workload = CitedSpendWorkload.Generation);

/// <summary>
/// Pure admission calculation only. Never dispatch from this quote alone: the
/// durable shared ledger rechecks and reserves in the same transaction before dispatch.
/// </summary>
public static class CitedGenerationBudgetPolicy
{
    public const string Version = "pilot-budget-preflight-v2";
    public const string EmbeddingVersion = "pilot-embedding-budget-preflight-v2";
    public const decimal MaximumPilotAdmissionUsd = 8m;
    public const int MaximumInputTokens = 8000;
    public const int MaximumOutputTokens = 4000;

    public static CitedReservationQuote Quote(CitedGenerationRate rate, CitedBudgetSnapshot budget,
        DateTimeOffset now, int maximumInputTokens, int maximumOutputTokens, bool enabled = false)
        => QuoteCore(rate, budget, now, maximumInputTokens, maximumOutputTokens, enabled, false);

    // Embeddings have no output-token charge. Require an explicitly zero output rate,
    // not a synthetic positive rate or dummy output token. The batch total uses the
    // same conservative 8,000 input-token admission ceiling as the shared pilot.
    public static CitedReservationQuote QuoteEmbedding(CitedGenerationRate rate, CitedBudgetSnapshot budget,
        DateTimeOffset now, int maximumInputTokens, bool enabled = false)
        => QuoteCore(rate, budget, now, maximumInputTokens, 0, enabled, true);

    private static CitedReservationQuote QuoteCore(CitedGenerationRate rate, CitedBudgetSnapshot budget,
        DateTimeOffset now, int maximumInputTokens, int maximumOutputTokens, bool enabled, bool embedding)
    {
        if (!enabled) throw Failure(embedding ? "embedding_disabled" : "generation_disabled");
        if (rate is null || budget is null || budget.BudgetId == Guid.Empty ||
            budget.Currency != "USD" || budget.CeilingUsd is <= 0 or > MaximumPilotAdmissionUsd ||
            budget.SettledUsd < 0 || budget.OutstandingUsd < 0 || budget.InFlight < 0 || budget.RetainedUnknownUsd < 0 ||
            !Identifier(rate.Provider) || !Identifier(rate.Model) || !Identifier(rate.ModelVersion) ||
            !Identifier(rate.RateVersion) || rate.MaximumInputUsdPerMillion <= 0 ||
            (embedding ? rate.MaximumOutputUsdPerMillion != 0 : rate.MaximumOutputUsdPerMillion <= 0) ||
            rate.ValidUntilUtc <= rate.ValidFromUtc)
            throw Failure("invalid_budget_configuration");
        if (now < rate.ValidFromUtc || now >= rate.ValidUntilUtc)
            throw Failure("rate_or_model_expired");
        if (maximumInputTokens is <= 0 or > MaximumInputTokens ||
            (!embedding && (maximumOutputTokens is <= 0 or > MaximumOutputTokens)))
            throw Failure(embedding ? "embedding_token_limit" : "generation_token_limit");
        if (budget.RequiresReconciliation || budget.OutstandingUsd > 0)
            throw Failure("budget_reconciliation_required");
        if (budget.InFlight != 0) throw Failure("generation_in_flight");

        try
        {
            var microdollars = checked(maximumInputTokens * rate.MaximumInputUsdPerMillion +
                maximumOutputTokens * rate.MaximumOutputUsdPerMillion);
            // Round before dividing so even tiny positive rates cannot underflow
            // to a free reservation at the proposed one-microdollar ledger scale.
            var cost = decimal.Ceiling(microdollars) / 1_000_000m;
            if (checked(budget.SettledUsd + budget.OutstandingUsd + budget.RetainedUnknownUsd + cost) > budget.CeilingUsd)
                throw Failure("pilot_budget_exhausted");
            return new(budget.BudgetId, embedding ? EmbeddingVersion : Version, rate.Provider, rate.Model, rate.ModelVersion,
                rate.RateVersion, maximumInputTokens, maximumOutputTokens, cost,
                embedding ? CitedSpendWorkload.Embedding : CitedSpendWorkload.Generation);
        }
        catch (OverflowException)
        {
            throw Failure("invalid_budget_configuration");
        }
    }

    private static bool Identifier(string value) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 100 && value == value.Trim() && !value.Any(char.IsControl);

    private static CitedWorkflowException Failure(string code) =>
        new(code, "Answer generation admission was denied.");
}
