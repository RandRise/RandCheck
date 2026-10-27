import { supportedReviewFields } from "../document-fields";
import type { ReviewField } from "./review.types";

export const labels: Record<string, string> = {
  ...Object.fromEntries(supportedReviewFields.map(([key, label]) => [key, label])),
  invoice_number: "Invoice number", invoice_date: "Invoice date", supplier_trn: "Supplier TRN",
  currency: "Currency", subtotal: "Subtotal", vat: "VAT", total: "Total", payment_terms: "Payment terms",
  supplier: "Supplier", supplier_ar: "Supplier (Arabic)", trn: "TRN"
};
export const cueLabels: Record<string, string> = {
  MissingMachineValue: "Missing extraction",
  LowConfidence: "Low confidence",
  ConfidenceUnavailable: "Confidence unavailable",
};
export const fieldIdentity = (field: ReviewField) => `${field.fieldKey}:${field.occurrenceIndex}`;
export const fieldValue = (field: ReviewField) => field.effective.value ?? field.machine?.value ?? "";
