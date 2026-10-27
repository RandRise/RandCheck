import { isQuotationDocument } from "../document-types";
import type { Role } from "./workflow.types";

/** Unknown/processing types remain selectable; only the backend can validate a completed source. */
export function documentRoleIssue(documentType: string | undefined, role: Role): string | null {
  const normalized = documentType?.trim().toLowerCase().replace(/[\s_-]+/g, "");
  const actual = isQuotationDocument(documentType) ? "a quotation"
    : normalized === "invoice" ? "an invoice"
    : ["purchaseorder", "po"].includes(normalized ?? "") ? "a purchase order" : null;
  const matches = role === "Invoice" ? normalized === "invoice" : ["purchaseorder", "po"].includes(normalized ?? "");
  return actual && !matches
    ? `This file is identified as ${actual}. Choose ${role === "Invoice" ? "an invoice" : "a purchase order"} in Add documents. Your saved file and earlier results are kept.`
    : null;
}

export function documentChoiceLabel(filename: string, documentType: string | undefined, role: Role) {
  return documentRoleIssue(documentType, role)
    ? `${filename} — not ${role === "Invoice" ? "an invoice" : "a purchase order"}`
    : filename;
}
