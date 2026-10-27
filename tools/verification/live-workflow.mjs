import assert from "node:assert/strict";
import { readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { openBrowser } from "./browser-session.mjs";

const output = path.resolve("outputs/scale-integration-20261023");
const seed = JSON.parse(await readFile(path.join(output, "seed.json"), "utf8"));
assert.equal(seed.kind, "synthetic-local-validation");
const invoice = seed.documents.find(d => d.originalFilename.includes("Invoice"));
const po = seed.documents.find(d => d.originalFilename.includes("PurchaseOrder"));
const api = "http://127.0.0.1:8310";
const browser = await openBrowser(output);
const { call, evaluate, waitFor } = browser;
const click = async label => {
  await waitFor(`[...document.querySelectorAll('button')].some(b=>b.textContent===${JSON.stringify(label)}&&!b.disabled)`);
  await evaluate(`[...document.querySelectorAll('button')].find(b=>b.textContent===${JSON.stringify(label)}&&!b.disabled).click()`);
};
try {
  await call("Emulation.setDeviceMetricsOverride", { width: 1440, height: 1100, deviceScaleFactor: 1, mobile: false });
  await call("Page.navigate", { url: "http://127.0.0.1:3310/invoices/new" });
  await waitFor(`!!document.querySelector('.saved-file-choice select option[value="${invoice.id}"]')`);
  assert.equal(await evaluate("document.body.textContent.includes('Development identity')"), true);
  for (const [index, doc] of [[0, invoice], [1, po]]) {
    await evaluate(`(()=>{const el=document.querySelectorAll('.saved-file-choice select')[${index}];el.closest('details').open=true;el.value=${JSON.stringify(doc.id)};el.dispatchEvent(new Event('change',{bubbles:true}));})()`);
  }
  await click("Continue to review");
  await click("Continue to purchase order");
  await click("Show results");
  await waitFor("document.querySelector('.result-summary h3')?.textContent==='Differences found'");
  const caseId = await evaluate("new URLSearchParams(location.search).get('case')");
  assert.match(caseId, /^[0-9a-f-]{36}$/i);
  const beforeResponse = await fetch(`${api}/api/reconciliation-cases/${caseId}`);
  const before = await beforeResponse.json();
  const run = before.runs[0];
  assert.equal(run.ruleSetVersion, "invoice-po-2.0");
  assert.equal(run.coverage.outcome, "DifferencesFound");
  for (const [role, doc] of [["Invoice", invoice], ["PurchaseOrder", po]]) {
    const source = run.sources.find(s => s.role === role);
    assert.equal(source.documentId, doc.id);
    assert.equal(source.reviewSessionId, doc.reviewSessionId);
    assert.equal(source.extractionRunId, doc.extractionRunId);
  }
  const totals = await evaluate("document.querySelector('.result-summary ul').textContent");
  assert.equal(totals, `${run.coverage.passedChecks} Passed${run.coverage.differences} Differed${run.coverage.couldNotCheck} Could not be checked${run.coverage.totalChecks} Total checks`);
  await evaluate("(()=>{const el=document.querySelector('select[aria-label=\"Reason for Document total choices\"]');el.value='Asked the supplier to explain the difference';el.dispatchEvent(new Event('change',{bubbles:true}));})()");
  await click("Accept difference");
  await waitFor("!![...document.querySelectorAll('button')].find(b=>b.textContent==='Revise decision')");
  const after = await (await fetch(`${api}/api/reconciliation-cases/${caseId}`)).json();
  const sort = values => [...values].sort((a, b) => a.id.localeCompare(b.id));
  for (const field of ["checks", "sources", "fields"]) assert.deepEqual(sort(run[field]), sort(after.runs[0][field]));
  assert.deepEqual(run.coverage, after.runs[0].coverage);
  assert.equal(after.runs[0].findings[0].resolutions.at(-1).reason, "Asked the supplier to explain the difference");
  const stale = await fetch(`${api}/api/reconciliation-cases/${caseId}/evaluations`, { method: "POST",
    headers: { "Content-Type": "application/json", "If-Match": beforeResponse.headers.get("etag") }, body: JSON.stringify({ ruleSetVersion: "invoice-po-2.0" }) });
  assert.equal(stale.status, 412);
  await call("Page.reload");
  await waitFor("document.querySelector('.result-summary h3')?.textContent==='Differences found'");
  assert.equal(await evaluate("document.querySelector('.result-summary ul').textContent"), totals);
  assert.equal(await evaluate("document.body.textContent.includes('Asked the supplier to explain the difference')"), true);
  assert.equal(new URL(await evaluate("location.href")).pathname, "/invoices");
  await evaluate("document.querySelector('.result-summary').scrollIntoView()");
  await writeFile(path.join(output, "live-workflow.png"), Buffer.from((await call("Page.captureScreenshot", { format: "png" })).data, "base64"));
  assert.deepEqual(browser.errors, []);
  const requests = browser.requests.filter(r => new URL(r.url).port === "8310");
  assert.ok(requests.some(r => r.method === "POST" && r.url.endsWith("/evaluations")));
  assert.ok(requests.some(r => r.method === "PUT" && r.url.endsWith("/documents/Invoice")));
  assert.ok(requests.some(r => r.method === "PUT" && r.url.endsWith("/documents/PurchaseOrder")));
  await writeFile(path.join(output, "live-workflow.json"), JSON.stringify({ identity: "Development; not live sign-in", source: "Known synthetic reviewed fixture; not OCR/upload/download verification",
    caseId, runId: run.id, coverage: run.coverage, exactReviewedSources: run.sources.map(s => ({ role: s.role, documentId: s.documentId, reviewSessionId: s.reviewSessionId, extractionRunId: s.extractionRunId })),
    decisionPreservedChecks: true, staleRetryStatus: stale.status, reloadPreservedResults: true, runtimeErrors: browser.errors, requests }, null, 2));
  console.log("Live Next.js -> .NET -> PostgreSQL pair/decision/reload checks passed.");
} catch (error) {
  await writeFile(path.join(output, "live-workflow-failure.json"), JSON.stringify({ error: String(error), errors: browser.errors,
    view: await evaluate("({headings:[...document.querySelectorAll('h1,h2,h3')].map(e=>e.textContent),alerts:[...document.querySelectorAll('[role=alert]')].map(e=>e.textContent),buttons:[...document.querySelectorAll('button')].map(e=>({text:e.textContent,disabled:e.disabled})),url:location.href})") }, null, 2));
  throw error;
} finally { await browser.close(); }
