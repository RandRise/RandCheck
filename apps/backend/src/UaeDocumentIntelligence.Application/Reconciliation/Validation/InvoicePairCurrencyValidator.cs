using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Reconciliation;

/// <summary>
/// Admission check for new saved invoice/PO evaluations. Does not change the
/// versioned comparison rules, original values, or existing evaluation results.
/// </summary>
internal static class InvoicePairCurrencyValidator
{
    public static void RequireAed(ReconciliationDocumentRole role, Document document, ReviewSession review)
    {
        var extraction = document.ExtractionRuns.Single(run => run.Id == review.ExtractionRunId);
        var fields = extraction.Fields.Where(field => IsCurrency(field.FieldKey, field.OccurrenceIndex)).ToArray();
        var addedCurrency = review.Actions.Where(action => !action.MachineFieldValueId.HasValue)
            .GroupBy(action => new { action.FieldKey, action.OccurrenceIndex })
            .Select(group => group.OrderByDescending(action => action.SequenceNumber).First())
            .Any(action => action.Action == ReviewActionType.Added && IsCurrency(action.FieldKey, action.OccurrenceIndex));
        var label = role == ReconciliationDocumentRole.Invoice ? "invoice" : "purchase order";

        if (fields.Length != 1 || addedCurrency)
            throw Unverified(label);

        var field = fields[0];
        var decision = review.Actions.Where(action => action.MachineFieldValueId == field.Id)
            .OrderByDescending(action => action.SequenceNumber).FirstOrDefault();
        // Use the same first page quote as ProjectPairValues; later quotes cannot
        // make this gate accept evidence that the actual comparison will not use.
        var evidence = field.Evidence.OrderBy(item => item.PageNumber).FirstOrDefault();
        if (decision?.Action is not (ReviewActionType.Confirmed or ReviewActionType.Corrected) ||
            evidence is null || evidence.PageNumber <= 0 || string.IsNullOrWhiteSpace(evidence.QuotedText))
            throw Unverified(label);

        var corrected = decision.Action == ReviewActionType.Corrected;
        var type = corrected ? decision.CorrectedValueType : field.ValueType;
        var value = corrected ? decision.CorrectedValueText : field.ValueText;
        if (type != FieldValueType.Currency || string.IsNullOrWhiteSpace(value))
            throw Unverified(label);

        if (!string.Equals(value.Trim(), "AED", StringComparison.OrdinalIgnoreCase))
            throw new ReconciliationValidationException("aed_currency_required",
                $"This check supports AED only. The {label} uses another or unclear currency. " +
                "Use a document issued in AED. Do not change a foreign amount to AED. Your saved work is kept.");
    }

    private static bool IsCurrency(string key, int occurrence) =>
        occurrence == 0 && key.Trim().Equals("currency", StringComparison.OrdinalIgnoreCase);

    private static ReconciliationValidationException Unverified(string label) => new(
        "aed_currency_unverified",
        $"Check the currency on the {label}. It must have one checked AED value with source text before you compare. " +
        "Do not guess or change a foreign amount to AED. Your saved work is kept.");
}
