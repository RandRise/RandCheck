// Synthetic UI adapter for the real Next pages. No database, extraction or QA claim.
// NEXT_PUBLIC_API_URL=http://127.0.0.1:8327; Next dev on 127.0.0.1:3327.
import { createServer } from "node:http";
const id = n => `00000000-0000-4000-8000-${String(n).padStart(12, "0")}`;
const text = "فاتورة تجريبية\nرقم الفاتورة: INV-007\nتاريخ الفاتورة: 2026-09-21\nالإجمالي: 210.50\nشروط الدفع: خلال 30 يوماً\n";
const source = { documentId: id(1), filename: "UI PREVIEW — فاتورة تجريبية INV-007.png",
  reviewSessionId: id(2), extractionRunId: id(3), revisionNumber: 1 };
const doc = { id: id(1), originalFilename: source.filename, sizeBytes: 1024, status: "Ready",
  documentType: "invoice", language: "ar-en", extractor: "synthetic-ui-preview",
  processingMs: 0, createdAt: "2026-09-21T12:00:00Z", fields: [] };
let revision = 1;
const fields = [["invoice_number", "INV-007", "String"], ["invoice_date", "2026-09-21", "Date"],
  ["total", "210.50", "Decimal"], ["payment_terms", "خلال 30 يوماً", "String"]].map(([fieldKey, value, valueType], n) => ({
    fieldKey, occurrenceIndex: 0, decisionStatus: "Unreviewed", cues: [],
    machine: { id: id(20+n), value, valueType, confidence: null, evidence: [{ page: 1, quotedText: value }] },
    effective: { isPresent: true, value, valueType, source: "Machine" }
  }));
const workspace = { documentId: id(1), selectedExtractionRunId: id(3), extractionRunId: id(3),
  session: { id: id(2), revisionNumber: 1, status: "InProgress", concurrencyToken: "preview-1" }, fields };
const hit = { ...source, indexId: id(4), chunkId: id(5), hitId: id(6), rank: 1, kind: "ReviewedFact",
  content: "payment_terms[0]: خلال 30 يوماً", reviewActionId: id(9),
  spans: [{ id: id(7), pageNumber: 1, quote: "خلال 30 يوماً" }] };
let lastResult;
const server = createServer(async (req, res) => {
  res.setHeader("Access-Control-Allow-Origin", "http://127.0.0.1:3327");
  res.setHeader("Access-Control-Allow-Methods", "GET,POST,OPTIONS");
  res.setHeader("Access-Control-Allow-Headers", "Content-Type,If-Match");
  res.setHeader("Access-Control-Expose-Headers", "ETag");
  res.setHeader("Cache-Control", "no-store");
  res.setHeader("Content-Type", "application/json; charset=utf-8");
  if (req.method === "OPTIONS") { res.writeHead(204).end(); return; }
  const url = new URL(req.url, "http://127.0.0.1:8327");
  const send = (value, status=200) => { res.statusCode=status; res.end(JSON.stringify(value)); };
  let input = {};
  if (req.method === "POST") {
    if (req.headers.origin !== "http://127.0.0.1:3327") return send({ detail: "Preview origin required" }, 403);
    let body = "";
    for await (const part of req) { body += part; if (body.length > 32768) return send({}, 413); }
    try { input = JSON.parse(body || "{}"); } catch { return send({}, 400); }
  }
  if (url.pathname === "/api/documents" && req.method === "GET") return send([doc]);
  if (url.pathname === `/api/documents/${id(1)}/review` && req.method === "GET") {
    res.setHeader("ETag", `"preview-${revision}"`); return send(workspace);
  }
  if (url.pathname === `/api/documents/${id(1)}/reviews/${id(2)}/actions` && req.method === "POST") {
    if (req.headers["if-match"] !== `"preview-${revision}"`) return send({ detail: "Stale preview" }, 412);
    const field = fields.find(f => f.machine.id === input.machineFieldValueId);
    if (!field || !["Corrected", "Confirmed"].includes(input.action)) return send({}, 400);
    if (input.action === "Corrected" && (!input.value?.trim() || !input.reason?.trim())) return send({}, 400);
    revision += 1;
    field.decisionStatus = input.action;
    field.latestAction = { id: id(100+revision), action: input.action, sequenceNumber: revision,
      ...(input.action === "Corrected" ? { value: input.value, reason: input.reason } : {}) };
    field.effective = { ...field.effective, value: input.action === "Corrected" ? input.value : field.machine.value, source: "Review" };
    workspace.session.concurrencyToken = `preview-${revision}`;
    console.log(JSON.stringify({ action: input.action, fieldKey: field.fieldKey, value: field.effective.value, reason: input.reason ?? null }));
    res.setHeader("ETag", `"preview-${revision}"`); return send(workspace);
  }
  if (url.pathname === "/api/cited-sources") return send({ sources: [source], nextOffset: null });
  if (url.pathname === "/api/cited-indexes" && req.method === "POST") return send({ id: id(4), status: "Ready" });
  if (url.pathname === "/api/cited-questions/reviewed" && req.method === "POST") {
    const accepted = input.question === "ما شروط دفع الفاتورة؟";
    lastResult = { stage: "ReviewedAnswer", answerGenerationEnabled: false, question: input.question, indexIds: [id(4)],
      retrieval: { id: id(8), status: "Completed", policyVersion: "synthetic-ui-preview-not-qa-evaluation", replayed: false,
        hits: accepted ? [hit] : [], failureCode: null }, answer: null,
      reviewedAnswer: { status: accepted ? "Answered" : "InsufficientEvidence", reasonCode: accepted ? null : "unsupported_scope",
        missingParts: [], claims: accepted ? [{ text: "Invoice payment terms: خلال 30 يوماً.", citations: [{ hitId: id(6), spanId: id(7) }] }] : [] } };
    return send(lastResult);
  }
  if (url.pathname === `/api/cited-questions/${id(8)}` && lastResult) return send(lastResult);
  if (url.pathname === `/api/cited-questions/${id(8)}/hits/${id(6)}/evidence`) {
    const quote = "خلال 30 يوماً";
    const start = text.indexOf(quote);
    return send({ ...source, retrievalRunId: id(8), hitId: id(6), indexId: id(4), kind: "ReviewedFact",
      reviewedFact: hit.content, review: { actionId: id(9), action: "Confirmed", fieldKey: "payment_terms", occurrence: 0, reason: null },
      page: { pageNumber: 1, byteStart: Buffer.byteLength(text.slice(0,start)), byteEnd: Buffer.byteLength(text.slice(0,start+quote.length)),
        before: text.slice(0,start), quote, after: text.slice(start+quote.length) } });
  }
  return send({ detail: "Synthetic preview route not found" }, 404);
});
server.listen(8327, "127.0.0.1", () => console.log("Phase 7 synthetic UI adapter: http://127.0.0.1:8327"));
