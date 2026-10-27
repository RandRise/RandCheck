// Download-only replay of a saved real review response. No database or OCR.
// Run the existing Next frontend on 3328 with API origin http://127.0.0.1:5327.
import { createServer } from "node:http";
import { readFileSync } from "node:fs";
import { createHash } from "node:crypto";

const receipt = JSON.parse(readFileSync(new URL("../docs/evidence/phase-7/P7-upload-workflow-20260922.json", import.meta.url), "utf8"));
const filename = "mixed-native-two-page.pdf";
const history = receipt.histories[filename];
const invoice = receipt.invoices.find(item => item.filename === filename);
if (!invoice || history.documentId !== invoice.documentId || history.sessions[0].status !== "Completed")
  throw new Error("Expected the saved completed mixed-invoice review");
const payload = Buffer.from(JSON.stringify(history), "utf8");
const document = { id: invoice.documentId, originalFilename: "DOWNLOAD CHECK — " + filename,
  sizeBytes: 50858, status: "Ready", documentType: "invoice", language: "ar-en",
  extractor: "saved-review-response", createdAt: "2026-09-22T17:03:03Z", fields: [] };
const review = { documentId: invoice.documentId, session: invoice.review, fields: [] };
const server = createServer((req, res) => {
  res.setHeader("Access-Control-Allow-Origin", "http://127.0.0.1:3328");
  res.setHeader("Access-Control-Allow-Methods", "GET,OPTIONS");
  res.setHeader("Cache-Control", "no-store");
  res.setHeader("Content-Type", "application/json; charset=utf-8");
  if (req.method === "OPTIONS") { res.writeHead(204).end(); return; }
  if (req.method !== "GET") { res.writeHead(405).end(); return; }
  if (req.url === "/api/documents") { res.end(JSON.stringify([document])); return; }
  if (req.url === `/api/documents/${invoice.documentId}/review`) { res.end(JSON.stringify(review)); return; }
  if (req.url === `/api/documents/${invoice.documentId}/review-history`) {
    console.log("History response delivered:", payload.length, "bytes");
    res.end(payload); return;
  }
  res.writeHead(404).end();
});
server.listen(5327, "127.0.0.1", () => console.log(JSON.stringify({
  scope: "download-only replay; no extraction/review mutations",
  filename: `review-history-${invoice.documentId}.json`, bytes: payload.length,
  sha256: createHash("sha256").update(payload).digest("hex")
})));
