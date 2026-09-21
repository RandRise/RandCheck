// Isolated, synthetic UI fixture server. NOT the application API or a retrieval evaluator.
// Run explicitly: node tools/phase5_workspace_preview.mjs
// Start Next separately with NEXT_PUBLIC_API_URL=http://127.0.0.1:8085 on port 3004.
import { createServer } from "node:http";
import { readFileSync } from "node:fs";
const fixture = JSON.parse(readFileSync(new URL("../samples/phase-5/lexical-v1/evaluation-input.json", import.meta.url), "utf8"));
const sourceText = fixture.sources.find(x => x.id === "invoice-primary").pages[0].canonicalText;
const id = n => `00000000-0000-4000-8000-${String(n).padStart(12, "0")}`;
const source = { documentId: id(1), filename: "UI PREVIEW · SYNTHETIC INVOICE.pdf", reviewSessionId: id(2), extractionRunId: id(3), revisionNumber: 1 };
const hit = { ...source, indexId: id(4), chunkId: id(5), hitId: id(6), rank: 1, kind: "PageText", content: sourceText,
  reviewActionId: null, spans: [{ id: id(7), pageNumber: 1, quote: sourceText }] };
let lastResult;
createServer(async (req, res) => {
  res.setHeader("Access-Control-Allow-Origin", "http://127.0.0.1:3004");
  res.setHeader("Access-Control-Allow-Methods", "GET,POST,OPTIONS");
  res.setHeader("Access-Control-Allow-Headers", "Content-Type");
  res.setHeader("Cache-Control", "no-store");
  res.setHeader("Content-Type", "application/json");
  if (req.method === "OPTIONS") { res.writeHead(204).end(); return; }
  const url = new URL(req.url, "http://127.0.0.1:8085");
  const send = value => res.end(JSON.stringify(value));
  if (url.pathname === "/api/cited-sources") return send({ sources: [source], nextOffset: null });
  if (url.pathname === "/api/cited-indexes" && req.method === "POST") return send({ id: id(4), status: "Ready" });
  if (url.pathname === "/api/cited-questions" && req.method === "POST") {
    let body = "";
    for await (const part of req) { body += part; if (body.length > 32768) { res.writeHead(413).end(); return; } }
    let input;
    try { input = JSON.parse(body); } catch { res.writeHead(400).end(); return; }
    const noHits = String(input.question).toLowerCase().includes("unknown");
    lastResult = { stage: noHits ? "InsufficientEvidence" : "EvidenceReady", answerGenerationEnabled: false,
      question: input.question, indexIds: [id(4)], retrieval: { id: id(8), status: noHits ? "Completed" : "Retrieved",
        policyVersion: "synthetic-ui-preview-not-evaluation", replayed: false, hits: noHits ? [] : [hit], failureCode: null },
      answer: noHits ? { status: "InsufficientEvidence", reasonCode: "no_relevant_evidence",
        message: "Synthetic preview: no matching evidence was retrieved." } : null };
    return send(lastResult);
  }
  if (url.pathname === `/api/cited-questions/${id(8)}` && lastResult) return send({ ...lastResult, retrieval: { ...lastResult.retrieval, replayed: true } });
  if (url.pathname === `/api/cited-questions/${id(8)}/hits/${id(6)}/evidence` && url.searchParams.get("spanId") === id(7))
    return send({ ...source, retrievalRunId: id(8), hitId: id(6), indexId: id(4), kind: "PageText", reviewedFact: null, review: null,
      page: { pageNumber: 1, byteStart: 0, byteEnd: Buffer.byteLength(sourceText), before: "", quote: sourceText, after: "" } });
  res.writeHead(404).end(JSON.stringify({ detail: "Synthetic preview route not found." }));
}).listen(8085, "127.0.0.1", () => console.log("Synthetic-only workspace preview API: http://127.0.0.1:8085"));
