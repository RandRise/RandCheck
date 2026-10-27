// Synthetic preview admission only. The .NET application owns the real gate.
export function aedCurrencyIssue(review, label) {
  const fields = review.fields.filter(field => field.fieldKey.trim().toLowerCase() === "currency" && field.occurrenceIndex === 0);
  const field = fields.length === 1 ? fields[0] : undefined;
  const evidence = field?.machine?.evidence?.toSorted((a, b) => a.page - b.page)[0];
  if (!field?.machine?.id || !["Confirmed", "Corrected"].includes(field.decisionStatus) ||
      !field.effective?.isPresent || field.effective.valueType !== "Currency" ||
      !evidence || evidence.page <= 0 || !evidence.quotedText?.trim()) {
    return { code: "aed_currency_unverified", detail: `Check the currency on the ${label}. It must have one checked AED value with source text before you compare. Do not guess or change a foreign amount to AED. Your saved work is kept.` };
  }
  if (field.effective.value?.trim().toUpperCase() !== "AED") {
    return { code: "aed_currency_required", detail: `This check supports AED only. The ${label} uses another or unclear currency. Use a document issued in AED. Do not change a foreign amount to AED. Your saved work is kept.` };
  }
  return null;
}
