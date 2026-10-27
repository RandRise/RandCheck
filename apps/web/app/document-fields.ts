import { isQuotationDocument } from "./document-types";

// Names and types match the local extraction/review contract; line occurrences start at 1.
export const supportedReviewFields = [
  ["invoice_number", "Invoice number", "String"], ["quotation_number", "Quotation number", "String"],
  ["purchase_order_number", "Purchase order number", "String"], ["invoice_date", "Invoice date", "Date"],
  ["due_date", "Due date", "Date"], ["supplier_trn", "Supplier TRN", "Identifier"],
  ["currency", "Currency", "Currency"], ["subtotal", "Subtotal", "Decimal"], ["vat", "VAT", "Decimal"],
  ["total", "Total", "Decimal"], ["payment_terms", "Payment terms", "String"],
  ["line_item_code", "Line item code", "String"], ["line_description", "Line description", "String"],
  ["line_quantity", "Line quantity", "Decimal"], ["line_unit_price", "Line unit price", "Decimal"],
  ["line_tax", "Line tax", "Decimal"], ["line_total", "Line total", "Decimal"]
] as const;

export const reviewStateLabels: Record<string, string> = { NeedsReview: "Needs review", InProgress: "In progress", Reviewed: "Reviewed" };

export function reviewFieldsForDocument(documentType?: string, hasSavedQuotationField = false) {
  return supportedReviewFields.filter(([key]) =>
    key !== "quotation_number" || isQuotationDocument(documentType) || hasSavedQuotationField,
  );
}
