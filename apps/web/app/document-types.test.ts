import { describe, expect, it } from "vitest";
import { isQuotationDocument } from "./document-types";
import { reviewFieldsForDocument } from "./document-fields";

describe("quotation compatibility outside new invoice reviews", () => {
  it.each(["Quotation", " QUOTE ", "عرض سعر", "عرض اسعار", "عرض أسعار"])("recognizes earlier %s documents", type => {
    expect(isQuotationDocument(type)).toBe(true);
    expect(reviewFieldsForDocument(type).some(([key]) => key === "quotation_number")).toBe(true);
  });

  it.each([undefined, "Invoice", "purchase_order", "Unknown"])("does not offer a quotation number for a new %s review", type => {
    expect(isQuotationDocument(type)).toBe(false);
    expect(reviewFieldsForDocument(type).some(([key]) => key === "quotation_number")).toBe(false);
  });

  it("retains an existing quotation field even when the document type is unknown", () => {
    expect(reviewFieldsForDocument(undefined, true).some(([key]) => key === "quotation_number")).toBe(true);
  });
});
