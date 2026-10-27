// Keep recognition for old documents without offering them for a new invoice pair.
export function isQuotationDocument(documentType?: string | null) {
  const type = documentType?.trim().toLowerCase().replace(/[\s_-]+/g, "");
  return ["quotation", "quote", "عرضسعر", "عرضاسعار", "عرضأسعار"].includes(type ?? "");
}
