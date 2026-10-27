// Disposable fixture state only. This does not execute the product comparison engine.
import test from "node:test";
import assert from "node:assert/strict";
import { createServer } from "node:http";
import { createFixtureApi } from "./fixtures.mjs";
import { createPreviewComparisonFetch } from "./comparison-bridge.mjs";

test("the current request opens a conservative synthetic result without weakening guards or rewriting history", async t => {
  let api;
  const server = createServer((req, res) => api(req, res).catch(error => res.writeHead(500).end(error.message)));
  await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
  t.after(() => new Promise(resolve => server.close(resolve)));
  const origin = `http://127.0.0.1:${server.address().port}`;
  api = createFixtureApi(origin);
  let evaluationRequests = 0;
  const transport = async (...args) => {
    const request = args[0];
    if ((request instanceof Request ? request.url : String(request)).endsWith("/evaluations")) evaluationRequests++;
    return fetch(...args);
  };
  const bridge = createPreviewComparisonFetch(transport, origin);
  async function call(path, { method = "GET", body, etag, actor = "operator", raw = false } = {}) {
    const response = await (raw ? transport : bridge)(origin + path, {
      method, headers: { "Content-Type": "application/json", Origin: origin, Authorization: `Bearer preview-${actor}`,
        ...(etag ? { "If-Match": etag } : {}) },
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    });
    return { status: response.status, etag: response.headers.get("etag"), value: await response.json() };
  }
  const request = { ruleSetVersion: "invoice-po-2.0" };
  const docs = (await call("/api/documents")).value;
  let current = await call("/api/reconciliation-cases", {
    method: "POST", body: { name: "Isolated bridge check", workflowKind: "InvoicePurchaseOrder", requestKey: "new-check" },
  });
  const path = `/api/reconciliation-cases/${current.value.id}`;
  const evaluate = (extra = {}) => call(path + "/evaluations", { method: "POST", body: request, etag: current.etag, ...extra });

  assert.equal((await evaluate()).status, 400); // No saved reviewed sources.
  assert.deepEqual((await call(path)).value, current.value);
  const reviews = [];
  for (const [role, type] of [["Invoice", "invoice"], ["PurchaseOrder", "purchase_order"]]) {
    const document = docs.find(doc => doc.documentType === type && doc.originalFilename.startsWith("Completed"));
    const review = (await call(`/api/documents/${document.id}/review`)).value;
    reviews.push(review);
    current = await call(path + "/draft-documents/" + role, {
      method: "PUT", etag: current.etag, body: { documentId: document.id, requestKey: role },
    });
    current = await call(path + "/documents/" + role, {
      method: "PUT", etag: current.etag, body: { documentId: document.id, reviewSessionId: review.session.id },
    });
    assert.equal(current.status, 200);
    if (role === "Invoice") assert.equal((await evaluate()).status, 400); // One pin is insufficient.
  }

  const beforeFailures = (await call("/api/organization/audit", { actor: "admin" })).value;
  const incompatible = await evaluate({ raw: true });
  assert.equal(incompatible.status, 400);
  assert.match(incompatible.value.detail, /Choose both saved reviewed revisions and an invoice scope/);
  assert.equal((await evaluate({ actor: "revoked" })).status, 403);
  assert.equal((await evaluate({ etag: '"00000000-0000-4000-8000-000000000099"' })).status, 412);
  assert.deepEqual((await call(path)).value, current.value);
  assert.deepEqual((await call("/api/organization/audit", { actor: "admin" })).value, beforeFailures);

  // Retain a deliberately created older result as the historical preservation check.
  current = await evaluate({ body: { ruleSetVersion: "invoice-po-1.0", orderScope: "FullOrder" } });
  assert.equal(current.status, 200);
  const earlier = structuredClone(current.value.runs[0]);
  const auditBefore = (await call("/api/organization/audit", { actor: "admin" })).value;
  const requestsBefore = evaluationRequests;
  const result = await evaluate();
  assert.equal(result.status, 200);
  assert.equal(evaluationRequests, requestsBefore + 1);
  assert.equal(result.value.runs.length, 2);
  const latest = result.value.runs[0];
  assert.equal(latest.ruleSetVersion, "synthetic-total-only");
  assert.equal(latest.orderScope, "Unknown");
  assert.equal(latest.coverage.status, "NotRecorded");
  assert.match(latest.coverage.conclusion, /Synthetic/);
  assert.equal(latest.findings[0].ruleCode, "Preview.ScopeUnavailable");
  assert.deepEqual(result.value.runs[1], earlier);
  for (const review of reviews)
    assert.deepEqual((await call(`/api/documents/${review.documentId}/review`)).value, review);
  const auditAfter = (await call("/api/organization/audit", { actor: "admin" })).value;
  assert.equal(auditAfter.length, auditBefore.length + 1);
  assert.deepEqual(auditAfter.slice(1), auditBefore);
});
