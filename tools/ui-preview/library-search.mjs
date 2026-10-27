// Synthetic preview only. Production search runs inside the .NET repository.
const identityFields = new Set(["supplier", "supplier_name", "vendor_name", "invoice_number", "purchase_order_number", "po_number", "purchase_order_reference", "document_number"]);
const quoteTypes = new Set(["quotation", "quote", "عرضسعر", "عرضاسعار", "عرضأسعار"]);
const typeKey = value => (value ?? "").trim().toLowerCase().replace(/[\s_-]+/g, "");

export function searchPreviewLibrary(documents, params) {
  const term = (params.get("q") ?? "").trim().toLowerCase();
  const type = params.get("type") ?? "All";
  const include = params.get("includeQuotations") ?? "false";
  const offset = Number(params.get("offset") ?? 0);
  if (term.length > 200 || !["All", "Invoice", "PurchaseOrder"].includes(type) || !["true", "false"].includes(include) ||
      !Number.isInteger(offset) || offset < 0 || offset > 1000000) throw new Error("Invalid library search.");
  const matches = documents.filter(doc => {
    const kind = typeKey(doc.documentType);
    if (include !== "true" && quoteTypes.has(kind)) return false;
    if (type === "Invoice" && kind !== "invoice") return false;
    if (type === "PurchaseOrder" && !["purchaseorder", "po"].includes(kind)) return false;
    return !term || doc.originalFilename.toLowerCase().includes(term) || doc.review?.fields?.some(field =>
      identityFields.has(field.fieldKey) && field.effective?.isPresent && String(field.effective.value ?? "").toLowerCase().includes(term));
  }).sort((a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt) || a.id.localeCompare(b.id));
  const start = matches.length ? Math.min(offset, Math.floor((matches.length - 1) / 10) * 10) : 0;
  return { documents: matches.slice(start, start + 10).map(({ review, actions, fields, ...doc }) => doc), total: matches.length, offset: start, pageSize: 10 };
}

/** Read-only compatibility for the running preview: do not restart/reset its
 * retained fixture state. This is explicitly not server paging verification. */
export function createPreviewLibraryFetch(original, origin) {
  return async (input, init) => {
    const request = new Request(input instanceof Request ? input : new URL(String(input), origin), init);
    const url = new URL(request.url);
    if (url.origin !== origin || request.method !== "GET" || url.pathname !== "/api/documents/library") return original(input, init);
    try { searchPreviewLibrary([], url.searchParams); }
    catch { return Response.json({ detail: "Invalid library search." }, { status: 400 }); }
    const options = { method: "GET", headers: request.headers, signal: request.signal, cache: "no-store" };
    const response = await original(`${origin}/api/documents/work-list`, options);
    if (!response.ok) return response;
    const documents = await response.json();
    if (url.searchParams.get("q")?.trim()) {
      // Small synthetic fixture only; no source downloads, mutations or cached identity data.
      for (const doc of documents) {
        const review = await original(`${origin}/api/documents/${doc.id}/review`, options);
        if (review.ok) doc.review = await review.json();
        else if (review.status !== 404 && review.status !== 409) return review;
      }
    }
    return Response.json(searchPreviewLibrary(documents, url.searchParams), { headers: { "Cache-Control": "no-store" } });
  };
}
