import test from "node:test";
import assert from "node:assert/strict";
import { searchPreviewLibrary, createPreviewLibraryFetch } from "./library-search.mjs";

const docs = Array.from({ length: 23 }, (_, i) => ({ id: `file-${String(i).padStart(2, "0")}`, originalFilename: `File ${i}.pdf`,
  createdAt: "2026-10-23T00:00:00Z", documentType: i === 0 ? "quotation" : "invoice", review: { fields: [
    { fieldKey: "supplier_name", effective: { isPresent: true, value: i === 22 ? "المورد 123" : "Supplier" } },
    { fieldKey: "po_number", effective: { isPresent: false, value: "CLEARED" } },
  ] } }));

test("preview paging/search keeps quotation opt-in and current effective identities", () => {
  const page = searchPreviewLibrary(docs, new URLSearchParams());
  assert.equal(page.total, 22); assert.equal(page.documents.length, 10);
  assert.equal(searchPreviewLibrary(docs, new URLSearchParams("offset=20")).documents.length, 2);
  assert.equal(searchPreviewLibrary(docs, new URLSearchParams("includeQuotations=true")).total, 23);
  assert.equal(searchPreviewLibrary(docs, new URLSearchParams({ q: "المورد" })).documents[0].id, "file-22");
  assert.equal(searchPreviewLibrary(docs, new URLSearchParams({ q: "CLEARED" })).total, 0);
  assert.throws(() => searchPreviewLibrary(docs, new URLSearchParams("offset=-1")));
});

test("retained preview bridge uses only authenticated reads and passes failures through", async () => {
  const calls = [];
  const original = async (input, init) => {
    calls.push({ input, init });
    if (String(input).endsWith("work-list")) return Response.json(docs.slice(22).map(({ review, ...doc }) => doc));
    return Response.json(docs[22].review);
  };
  const fetcher = createPreviewLibraryFetch(original, "http://preview");
  const result = await fetcher("http://preview/api/documents/library?q=123", { headers: { Authorization: "Bearer preview-test" } });
  assert.equal((await result.json()).total, 1);
  assert.ok(calls.every(call => call.init.method === "GET" && call.init.headers.get("Authorization") === "Bearer preview-test"));
  const denied = createPreviewLibraryFetch(async () => new Response("{}", { status: 403 }), "http://preview");
  assert.equal((await denied("http://preview/api/documents/library")).status, 403);
});
