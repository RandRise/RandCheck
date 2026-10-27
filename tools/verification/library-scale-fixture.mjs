/** Browser-only list simulation. It never uploads, opens, confirms or persists a document. */
export function createScaleLibrary(size) {
  if (![1000, 10000].includes(size)) throw new Error("Use 1000 or 10000 synthetic entries.");
  const entries = Array.from({ length: size }, (_, index) => {
    const number = String(index + 1).padStart(5, "0");
    const documentType = index % 2 ? "purchase_order" : "invoice";
    return {
      id: `90000000-0000-4000-8000-${String(index + 1).padStart(12, "0")}`,
      originalFilename: `Synthetic ${documentType} ${number}.pdf`, documentType,
      sizeBytes: 2048, status: "Ready", reviewState: "NeedsReview", createdAt: "2026-10-23T00:00:00Z",
      supplier: index === size - 1 ? "Last supplier المورد الأخير" : `Supplier ${index % 100}`,
      printedNumber: `${documentType === "invoice" ? "INV" : "PO"}-${number}`,
    };
  });
  return params => {
    const search = (params.get("q") ?? "").trim().toLowerCase();
    const type = params.get("type") ?? "All";
    const matches = entries.filter(d => (type === "All" || d.documentType === (type === "Invoice" ? "invoice" : "purchase_order")) &&
      [d.originalFilename, d.supplier, d.printedNumber].some(value => value.toLowerCase().includes(search)));
    const offset = Math.max(0, Math.min(Number(params.get("offset") ?? 0), Math.max(0, Math.floor((matches.length - 1) / 10) * 10)));
    return { total: matches.length, offset, pageSize: 10,
      documents: matches.slice(offset, offset + 10).map(({ supplier, printedNumber, ...document }) => document) };
  };
}
