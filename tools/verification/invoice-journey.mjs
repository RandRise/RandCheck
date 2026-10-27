/** Browser acceptance on an owned, disposable demo. Never point this at retained data.
 * Usage: node tools/verification/invoice-journey.mjs DEMO_ROOT OUTPUT upload|review|decision|export|retry|reopen|inspect
 * Only known fictional fixture values are confirmed, after checking frozen source truth.
 */
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { mkdir, readFile, writeFile, readdir } from "node:fs/promises";
import path from "node:path";
import { openBrowser } from "./browser-session.mjs";

const [demoArg, outputArg, stage] = process.argv.slice(2);
assert.ok(demoArg && outputArg && ["upload", "review", "decision", "export", "retry", "failure", "failure-review", "layout", "reopen", "inspect"].includes(stage), "Provide demo root, output and a supported stage.");
const demo = path.resolve(demoArg), output = path.resolve(outputArg);
const owner = JSON.parse(await readFile(path.join(demo, ".demo-owner.json"), "utf8"));
assert.equal(owner.root, demo);
assert.match(owner.project, /^uaedi-demo-[a-f0-9]{12}$/);
const container = JSON.parse(execFileSync("docker", ["inspect", `${owner.project}-backend-1`], { encoding: "utf8", windowsHide: true }))[0];
assert.equal(container.Config.Labels["com.docker.compose.project"], owner.project);
assert.equal(container.State.Running, true);
assert.deepEqual(container.NetworkSettings.Ports["8080/tcp"], [{ HostIp: "127.0.0.1", HostPort: "8308" }]);
assert.ok(container.Config.Env.includes("ConnectionStrings__Database=Host=db;Port=5432;Database=document_intelligence_demo;Username=demo;Password=disposable_demo_only"));
assert.ok(container.Config.Env.includes("Security__Mode=Development"));
const web = JSON.parse(execFileSync("docker", ["inspect", `${owner.project}-web-1`], { encoding: "utf8", windowsHide: true }))[0];
assert.equal(web.Config.Labels["com.docker.compose.project"], owner.project);
assert.equal(web.State.Running, true);
assert.deepEqual(web.NetworkSettings.Ports["3000/tcp"], [{ HostIp: "127.0.0.1", HostPort: "3308" }]);
assert.ok(web.Config.Env.includes("NEXT_PUBLIC_API_URL=http://127.0.0.1:8308"));
assert.ok(web.Config.Env.includes("AUTH_MODE=Development"));
await mkdir(output, { recursive: true });
const receiptPath = path.join(output, "journey.json");
let receipt;
try { receipt = JSON.parse(await readFile(receiptPath, "utf8")); }
catch (error) { if (error.code !== "ENOENT") throw error; }
assert.ok(!receipt || receipt.project === owner.project, "Receipt belongs to another demo.");
receipt ??= { project: owner.project, synthetic: true, realBackend: true, checks: [], documents: [] };
const save = () => writeFile(receiptPath, JSON.stringify(receipt, null, 2));
const api = "http://127.0.0.1:8308";
const attempt = Date.now();
const get = async route => { const response = await fetch(`${api}${route}`); assert.equal(response.status, 200, route); return response.json(); };
const pack = path.join(demo, "samples/supplier-invoice/v0.1.0");
const truthBytes = await readFile(path.join(pack, "source-truth.json"));
const manifest = JSON.parse(await readFile(path.join(pack, "manifest.json"), "utf8"));
assert.equal(createHash("sha256").update(truthBytes).digest("hex"), manifest.truthSha256);
for (const fixture of manifest.files) assert.equal(createHash("sha256").update(await readFile(path.join(pack, fixture.name))).digest("hex"), fixture.sha256);
const truth = JSON.parse(truthBytes);
const fieldLabels = { invoice_number: "Invoice number", purchase_order_number: "Purchase order number", invoice_date: "Invoice date", due_date: "Due date", supplier_trn: "Supplier TRN", currency: "Currency", subtotal: "Subtotal", vat: "VAT", total: "Total", line_description: "Line description", line_quantity: "Line quantity", line_unit_price: "Line unit price" };
const headingFor = field => `${fieldLabels[field.fieldKey]}${field.occurrenceIndex > 0 ? ` · Item ${field.occurrenceIndex}` : ""}`;
const b = await openBrowser(path.join(output, `${stage}-${Date.now()}`));
const shot = async name => writeFile(path.join(output, `${name}.png`), Buffer.from((await b.call("Page.captureScreenshot", { format: "png" })).data, "base64"));
const button = text => `[...document.querySelectorAll('button')].find(e=>e.textContent.trim()===${JSON.stringify(text)}&&!e.disabled)`;
const click = async text => { await b.waitFor(`!!(${button(text)})`); await b.evaluate(`(${button(text)}).click()`); };
const waitLong = async expression => {
  for (let attempt = 0; attempt < 4; attempt++) {
    try { await b.waitFor(expression); return; } catch (error) { if (attempt === 3) throw error; }
  }
};
try {
  await b.call("Emulation.setDeviceMetricsOverride", { width: 1440, height: 1000, deviceScaleFactor: 1, mobile: false });
  await b.call("Page.navigate", { url: `http://127.0.0.1:3308/${receipt.caseId ? `invoices?case=${receipt.caseId}&step=2` : ""}` });
  if (stage === "upload") {
    assert.ok(!receipt.caseId, "Do not repeat a completed upload batch.");
    const existing = await get("/api/documents");
    assert.deepEqual(existing.map(doc => doc.id).sort(), receipt.documents.map(doc => doc.id).sort(), "Resume only this receipt's uploads; refuse unrelated documents.");
    assert.equal((await get("/api/reconciliation-cases")).cases.length, 0);
    await waitLong(`!!(${button("Start with my files")})`);
    // Development identity is local-only and does not call the Entra session endpoint.
    // Wait for hydration: server-rendered buttons may precede the first client request.
    for (let count = 0; count < 100 && !b.requests.some(request => request.url.startsWith(`${api}/api/reconciliation-cases`)); count++) await new Promise(resolve => setTimeout(resolve, 200));
    assert.ok(b.requests.some(request => request.url.startsWith(`${api}/api/reconciliation-cases`)));
    assert.ok(!b.requests.some(request => request.url.includes("/api/") && new URL(request.url).port !== "8308"));
    await shot("home-empty"); await click("Start with my files");
    await waitLong(`document.querySelectorAll('input[type=file]').length===2`);
    for (const [index, role] of ["invoice", "po"].entries()) {
      const file = path.join(pack, `english-${role}.pdf`);
      const retained = receipt.documents.find(doc => doc.role === role);
      if (retained) {
        await waitLong(`!![...document.querySelectorAll('.saved-file-choice select')][${index}]?.querySelector('option[value="${retained.id}"]')`);
        await b.evaluate(`(()=>{const details=document.querySelectorAll('.saved-file-choice')[${index}];details.open=true;const select=details.querySelector('select');select.value='${retained.id}';select.dispatchEvent(new Event('change',{bubbles:true}));})()`);
      } else {
        const { root } = await b.call("DOM.getDocument");
        const { nodeIds } = await b.call("DOM.querySelectorAll", { nodeId: root.nodeId, selector: "input[type=file]" });
        await b.call("DOM.setFileInputFiles", { nodeId: nodeIds[index], files: [file] });
      }
      await waitLong(`document.body.innerText.includes('Selected: english-${role}.pdf')`);
      const documents = await get("/api/documents");
      const document = documents.find(item => item.originalFilename === `english-${role}.pdf`);
      assert.ok(document);
      if (!retained) receipt.documents.push({ role, id: document.id, filename: document.originalFilename, sha256: createHash("sha256").update(await readFile(file)).digest("hex") });
      await save();
      if (index === 0) {
        assert.equal(await b.evaluate(`(${button("Continue to review")})===undefined`), true);
        const cases = await get("/api/reconciliation-cases");
        assert.equal(cases.cases.length, 0);
        receipt.checks.push("One upload cannot create a case or continue to review");
      }
    }
    await shot("pair-uploaded"); await click("Continue to review");
    await waitLong(`new URL(location.href).searchParams.has('case')`);
    receipt.caseId = await b.evaluate(`new URL(location.href).searchParams.get('case')`); await save();
    const item = await get(`/api/reconciliation-cases/${receipt.caseId}`);
    assert.equal(item.draftDocuments.length, 2); assert.equal(item.runs.length, 0); assert.equal(item.documents.length, 0);
    receipt.checks.push("Both uploads create one case only on Continue; no values confirmed or comparisons run");
    await shot("case-created");
  }
  if (stage === "review") {
    assert.ok(receipt.caseId && !receipt.reviewed, "Upload first; do not repeat completed review batch.");
    for (const doc of receipt.documents) {
      await waitLong(`document.querySelector('.embedded-document .review-head h2')?.textContent===${JSON.stringify(doc.filename)}`);
      await waitLong(`!!(${button("Start review")})||!!(${button("Complete review")})||document.body?.innerText.includes('Your decisions are saved and locked')`);
      const workspace = await get(`/api/documents/${doc.id}/review`);
      assert.equal(workspace.documentId, doc.id);
      doc.processing = await get(`/api/documents/${doc.id}/processing`);
      await writeFile(path.join(output, `${doc.role}-processing.json`), JSON.stringify(doc.processing, null, 2));
      const specification = truth.documents.find(item => item.id === `english-${doc.role}`);
      const expected = new Map(Object.entries(specification.expected).map(([key, value]) => [`${key}:0`, value]));
      for (const row of truth.rows) for (const [key, value] of Object.entries({ line_description: row.description, line_quantity: row.quantity, line_unit_price: row.unitPrice })) expected.set(`${key}:${row.occurrence}`, value);
      for (const field of workspace.fields.filter(field => field.machine)) {
        const key = `${field.fieldKey}:${field.occurrenceIndex}`;
        assert.ok(expected.has(key), `Inspect unexpected extracted field before confirmation: ${key}`);
        const raw = field.machine.value.replaceAll(",", ""), wanted = expected.get(key);
        assert.ok(raw === wanted || /^\d+(\.\d+)?$/.test(raw) && Number(raw) === Number(wanted), `Source truth mismatch: ${key}`);
        assert.ok(field.machine.evidence.some(evidence => evidence.page === 1 && evidence.quotedText?.includes(field.machine.value)), `Missing exact source quote: ${key}`);
      }
      await writeFile(path.join(output, `${doc.role}-before-review.json`), JSON.stringify(workspace, null, 2));
      if (await b.evaluate(`!!(${button("Start review")})`)) await click("Start review");
      for (let count = 0; count < 60; count++) {
        await waitLong(`!!(${button("Keep this value")})||!!(${button("Complete review")})||document.body?.innerText.includes('Your decisions are saved and locked')`);
        if (!await b.evaluate(`!!(${button("Keep this value")})`)) break;
        const pending = await b.evaluate(`document.querySelector('.guided-count').textContent`);
        const card = await b.evaluate(`(()=>{const card=document.querySelector('.guided-value');return {heading:card.querySelector('h3').textContent,value:card.querySelector('.guided-suggested-value p').textContent,quotes:[...card.querySelectorAll('.source-evidence blockquote')].map(e=>e.textContent)};})()`);
        const field = workspace.fields.find(field => field.machine && headingFor(field) === card.heading);
        assert.ok(field, `Unexpected guided field/item: ${card.heading}`);
        assert.equal(card.value, field.effective.value ?? field.machine.value);
        assert.deepEqual(card.quotes, field.machine.evidence.filter(evidence => evidence.quotedText).map(evidence => evidence.quotedText));
        if (count === 0) await shot(`${doc.role}-value-evidence`);
        await click("Keep this value");
        await waitLong(`document.querySelector('.guided-count')?.textContent!==${JSON.stringify(pending)}`);
      }
      if (await b.evaluate(`!!(${button("Complete review")})`)) await click("Complete review");
      await waitLong(`document.body?.innerText.includes('Your decisions are saved and locked')`);
      const completed = await get(`/api/documents/${doc.id}/review`);
      assert.equal(completed.session.status, "Completed");
      assert.equal(completed.documentId, doc.id); assert.equal(completed.extractionRunId, workspace.extractionRunId);
      for (const field of workspace.fields.filter(field => field.machine)) {
        const saved = completed.fields.find(saved => saved.fieldKey === field.fieldKey && saved.occurrenceIndex === field.occurrenceIndex);
        assert.equal(saved.machine.id, field.machine.id); assert.equal(saved.latestAction.action, "Confirmed");
        assert.equal(saved.effective.value, field.machine.value);
      }
      doc.reviewSessionId = completed.session.id; doc.extractionRunId = completed.extractionRunId;
      await save(); await shot(`${doc.role}-review-completed`);
      await click(doc.role === "invoice" ? "Continue to purchase order" : "Show results");
    }
    await waitLong(`!!document.querySelector('.result-summary h3')`);
    const item = await get(`/api/reconciliation-cases/${receipt.caseId}`);
    assert.equal(item.runs.length, 1); const run = item.runs[0];
    assert.equal(run.ruleSetVersion, "invoice-po-2.0");
    for (const doc of receipt.documents) assert.ok(run.sources.some(source => source.documentId === doc.id && source.reviewSessionId === doc.reviewSessionId && source.extractionRunId === doc.extractionRunId));
    assert.ok(run.coverage.couldNotCheck > 0); assert.equal(run.coverage.differences, 0);
    assert.equal(await b.evaluate(`document.querySelector('.result-summary h3').textContent`), "Checks incomplete");
    receipt.runId = run.id; receipt.reviewed = true; receipt.coverage = run.coverage;
    receipt.checks.push("Explicit browser review of source-checked fictional values", "Exact saved invoice/PO reviews compared by invoice-po-2.0", "Missing document information stays incomplete despite matching totals");
    await writeFile(path.join(output, "case-after-review.json"), JSON.stringify(item, null, 2)); await shot("results-incomplete");
  }
  if (["decision", "export", "layout", "reopen"].includes(stage)) {
    assert.ok(receipt.reviewed);
    await click("View results"); await waitLong(`!!document.querySelector('.report-download')`);
  }
  if (stage === "decision") {
    assert.ok(!receipt.decision, "Do not repeat a recorded decision.");
    const before = await get(`/api/reconciliation-cases/${receipt.caseId}`);
    const run = before.runs.find(run => run.id === receipt.runId);
    const check = run.checks.find(check => check.fieldKey === "total" && check.status === "CouldNotCheck");
    assert.ok(check?.findingId);
    const card = `[...document.querySelectorAll('.result-check')].find(card=>card.getAttribute('aria-label')==='Document total: Could not be checked')`;
    await b.waitFor(`!!(${card})?.querySelector('select')`);
    await b.evaluate(`(()=>{const select=(${card}).querySelector('select');select.value='Requested the missing information';select.dispatchEvent(new Event('change',{bubbles:true}));})()`);
    await click("Acknowledge missing check");
    await b.waitFor(`!!(${card})?.querySelector('.resolution-history')`);
    const after = await get(`/api/reconciliation-cases/${receipt.caseId}`), saved = after.runs.find(run => run.id === receipt.runId);
    for (const key of ["checks", "fields", "sources", "coverage"]) assert.deepEqual(saved[key], run[key]);
    assert.notEqual(after.concurrencyToken, before.concurrencyToken);
    const decisions = saved.findings.find(finding => finding.id === check.findingId).resolutions;
    assert.equal(decisions.length, 1); assert.equal(decisions[0].action, "Accepted");
    assert.equal(decisions[0].reason, "Requested the missing information");
    assert.equal(await b.evaluate(`document.querySelector('.result-summary h3').textContent`), "Checks incomplete");
    receipt.decision = decisions[0]; receipt.checks.push("Scenario-specific browser decision preserves all performed/incomplete checks and evidence");
    await b.evaluate(`(${card}).scrollIntoView({block:'start'})`); await shot("missing-check-decision");
    await writeFile(path.join(output, "case-after-decision.json"), JSON.stringify(after, null, 2));
  }
  if (stage === "export") {
    assert.ok(receipt.decision && !receipt.exported);
    const downloads = path.join(output, `downloads-${Date.now()}`); await mkdir(downloads);
    await b.call("Browser.setDownloadBehavior", { behavior: "allow", downloadPath: downloads });
    receipt.downloads = [];
    for (const format of ["html", "csv"]) {
      await b.waitFor(`!document.querySelector('.report-download button').disabled`);
      await b.evaluate(`(()=>{const select=document.querySelector('.report-download select');select.value='${format}';select.dispatchEvent(new Event('change',{bubbles:true}));})()`);
      await b.evaluate(`document.querySelector('.report-download button').click()`);
      for (let attempt = 0; attempt < 100; attempt++) {
        const file = (await readdir(downloads)).find(file => file.endsWith(`.${format}`));
        if (file) { receipt.downloads.push(path.join(downloads, file)); break; } await new Promise(resolve => setTimeout(resolve, 100));
      }
    }
    assert.equal(receipt.downloads.length, 2);
    assert.equal(path.parse(receipt.downloads[0]).name, path.parse(receipt.downloads[1]).name);
    const html = await readFile(receipt.downloads[0], "utf8");
    const csv = await readFile(receipt.downloads[1], "utf8");
    const snapshot = html.match(/Snapshot ID<\/bdi><\/td><td><bdi dir="auto">([a-f0-9-]{36})<\/bdi>/)?.[1];
    assert.ok(snapshot, "HTML must contain its full saved snapshot ID.");
    assert.ok(csv.includes(`"Report","Snapshot ID","","${snapshot}"`));
    const current = await get(`/api/reconciliation-cases/${receipt.caseId}`);
    for (const file of receipt.downloads) {
      const content = await readFile(file, "utf8");
      for (const id of [current.concurrencyToken, receipt.runId, ...receipt.documents.map(doc => doc.reviewSessionId)]) assert.ok(content.includes(id));
      assert.ok(content.includes("Checks incomplete"));
      assert.ok(content.includes(receipt.decision.reason));
    }
    receipt.exportSnapshotId = snapshot;
    receipt.exported = true; receipt.checks.push("Real HTML and CSV downloaded from the same fresh-pair snapshot");
    await shot("fresh-export");
  }
  if (stage === "reopen") {
    const expected = JSON.parse(await readFile(path.join(output, "case-after-decision.json"), "utf8"));
    assert.deepEqual(await get(`/api/reconciliation-cases/${receipt.caseId}`), expected);
    assert.equal(await b.evaluate(`document.querySelector('.result-summary h3').textContent`), "Checks incomplete");
    assert.ok(await b.evaluate(`document.body.innerText.includes(${JSON.stringify(receipt.decision.reason)})`));
    receipt.checks.push("Fresh browser reopens unchanged reviews, saved comparison and decision");
    await b.evaluate(`document.querySelector('.result-summary').scrollIntoView({block:'start'})`); await shot("reopened-results");
  }
  if (stage === "retry") {
    assert.ok(receipt.exported && !receipt.retry, "Finish the normal journey first; do not repeat the retry batch.");
    const doc = receipt.documents.find(doc => doc.role === "invoice");
    await waitLong(`document.querySelector('.embedded-document .review-head h2')?.textContent===${JSON.stringify(doc.filename)}`);
    await waitLong(`!!(${button("Reprocess document")})`);
    const before = await get(`/api/reconciliation-cases/${receipt.caseId}`);
    const history = await get(`/api/documents/${doc.id}/review-history`);
    await b.evaluate(`(()=>{document.querySelector('.processing-details').open=true;const real=window.fetch.bind(window);window.acceptanceWrites=[];let lose=true;window.fetch=async(input,init)=>{const response=await real(input,init);if(String(input).endsWith('/${doc.id}/reprocess')&&init?.method==='POST'){window.acceptanceWrites.push({key:new Headers(init.headers).get('X-Operation-Key'),status:response.status,body:await response.clone().json()});if(lose){lose=false;throw new TypeError('Acceptance probe: reply lost after server accepted request');}}return response;};})()`);
    await click("Reprocess document");
    await b.waitFor(`document.body.innerText.includes('Retry to check this processing request without starting another run')`);
    await shot("retry-uncertain-reply");
    await click("Reprocess document");
    await b.waitFor(`document.body.innerText.includes('Existing processing request found')`);
    const writes = await b.evaluate("window.acceptanceWrites");
    assert.equal(writes.length, 2); assert.equal(writes[0].status, 202); assert.equal(writes[1].status, 202);
    assert.equal(writes[0].key, writes[1].key); assert.equal(writes[0].body.extractionRunId, writes[1].body.extractionRunId);
    assert.equal(writes[1].body.replayed, true); assert.equal(writes[1].body.runNumber, 2);
    assert.notEqual(writes[0].body.extractionRunId, doc.extractionRunId);
    let processing;
    for (let count = 0; count < 90; count++) {
      processing = await get(`/api/documents/${doc.id}/processing`);
      if (["Partial", "Ready", "Failed", "Cancelled"].includes(processing.status)) break;
      await new Promise(resolve => setTimeout(resolve, 500));
    }
    assert.equal(processing.status, "Partial"); assert.equal(processing.extractionRunId, writes[0].body.extractionRunId);
    assert.deepEqual(await get(`/api/reconciliation-cases/${receipt.caseId}`), before);
    assert.deepEqual(await get(`/api/documents/${doc.id}/review-history`), history);
    receipt.retry = { fault: "Browser intentionally loses first successful server reply", writes, processing };
    receipt.checks.push("Lost real reprocessing reply safely retried with the same operation key and one new extraction", "Reprocessing preserves exact earlier review history and saved comparison/decisions");
    await shot("retry-recovered");
  }
  if (stage === "failure") {
    assert.ok(receipt.exported && !receipt.failedDocument, "Do not repeat a completed failure batch.");
    const before = await get(`/api/reconciliation-cases/${receipt.caseId}`);
    let doc = receipt.failureProbe;
    if (!doc) {
    const file = path.join(output, `truncated-invoice-${attempt}.pdf`);
    await writeFile(file, "%PDF-1.7\nFictional deliberately truncated PDF for failure acceptance.\n", { flag: "wx" });
    await b.call("Page.navigate", { url: "http://127.0.0.1:3308/invoices/new" });
    await waitLong(`document.querySelectorAll('input[type=file]').length===2&&!document.querySelector('input[type=file]').disabled`);
    const { root } = await b.call("DOM.getDocument");
    const { nodeId } = await b.call("DOM.querySelector", { nodeId: root.nodeId, selector: "input[type=file]" });
    await b.call("DOM.setFileInputFiles", { nodeId, files: [file] });
    await waitLong(`document.body.innerText.includes('Selected: ${path.basename(file)}')`);
    doc = (await get("/api/documents")).find(doc => doc.originalFilename === path.basename(file)); assert.ok(doc);
    receipt.failureProbe = { id: doc.id, originalFilename: doc.originalFilename }; await save();
    }
    let processing;
    for (let count = 0; count < 90; count++) {
      processing = await get(`/api/documents/${doc.id}/processing`);
      if (["Partial", "Ready", "Failed", "Cancelled"].includes(processing.status)) break;
      await new Promise(resolve => setTimeout(resolve, 500));
    }
    assert.equal(processing.status, "Failed"); assert.ok(processing.errorMessage);
    assert.doesNotMatch(processing.errorMessage, /Traceback|\/data\/uploads|Exception at/);
    await b.call("Page.navigate", { url: `http://127.0.0.1:3308/documents?document=${doc.id}` });
    await waitLong(`!!document.querySelector('.terminal-state')`);
    assert.equal(await b.evaluate(`!!(${button("Start review")})||!!(${button("Complete review")})`), false);
    assert.ok(await b.evaluate(`!!(${button("Try processing again")})`));
    assert.deepEqual(await get(`/api/reconciliation-cases/${receipt.caseId}`), before);
    receipt.failedDocument = { id: doc.id, filename: doc.originalFilename, processing };
    receipt.checks.push("Malformed PDF fails clearly, offers recovery and cannot become a completed review or alter the saved comparison");
    await b.evaluate(`document.querySelector('.terminal-state').scrollIntoView({block:'center'})`); await shot("failed-document");
  }
  if (stage === "layout") {
    const before = await get(`/api/reconciliation-cases/${receipt.caseId}`);
    receipt.layouts = [];
    for (const theme of ["dark", "light"]) {
      await b.evaluate(`(()=>{const e=document.querySelector('[aria-label="Appearance"]');e.value='${theme}';e.dispatchEvent(new Event('change',{bubbles:true}));})()`);
      await b.waitFor(`document.querySelector('.app-appearance')?.dataset.theme==='${theme}'`);
      for (const width of [1440, 390]) {
        await b.call("Emulation.setDeviceMetricsOverride", { width, height: 1000, deviceScaleFactor: 1, mobile: width === 390 });
        await b.evaluate(`document.querySelector('.result-summary').scrollIntoView({block:'start'})`);
        assert.equal(await b.evaluate("document.documentElement.scrollWidth<=innerWidth+1"), true);
        const spacing = await b.evaluate(`(()=>{const controls=document.querySelector('.report-download');const a=controls.querySelector('select').getBoundingClientRect(),b=controls.querySelector('button').getBoundingClientRect();return {separate:a.right+7<=b.left||a.bottom+7<=b.top,buttonWidth:b.width,buttonHeight:b.height};})()`);
        assert.ok(spacing.separate); assert.ok(spacing.buttonHeight >= 40);
        receipt.layouts.push({ theme, width, spacing }); await shot(`results-${theme}-${width}`);
      }
    }
    assert.deepEqual(await get(`/api/reconciliation-cases/${receipt.caseId}`), before);
    assert.equal(b.requests.filter(request => request.url.includes('/api/') && !['GET','OPTIONS'].includes(request.method)).length, 0);
    receipt.checks.push("Dark/light results at desktop and phone widths, separate controls and no layout writes");
  }
  if (stage === "failure-review") {
    assert.ok(receipt.failedDocument);
    const id = receipt.failedDocument.id;
    // The root document URL deliberately retains the review-capable legacy workspace.
    await b.call("Page.navigate", { url: `http://127.0.0.1:3308/?document=${id}` });
    await waitLong(`document.querySelector('.review-head h2')?.textContent===${JSON.stringify(receipt.failedDocument.filename)}&&!!document.querySelector('.terminal-state')`);
    assert.equal(await b.evaluate(`!!(${button("Start review")})||!!(${button("Complete review")})`), false);
    assert.equal(await b.evaluate(`!!document.querySelector('.document-library')`), false);
    const response = await fetch(`${api}/api/documents/${id}/review`), problem = await response.json();
    assert.equal(response.status, 409); assert.equal(problem.code, "review_requires_successful_extraction");
    const history = await get(`/api/documents/${id}/review-history`); assert.deepEqual(history.sessions, []);
    receipt.failedDocument.reviewEligibility = { status: response.status, problem, history, reviewCapableRoute: true };
    assert.equal(b.requests.filter(request => request.url.includes('/api/') && !['GET','OPTIONS'].includes(request.method)).length, 0);
    await b.evaluate(`document.querySelector('.terminal-state').scrollIntoView({block:'center'})`); await shot("failed-document-review-capable");
  }
  if (stage === "inspect") { await waitLong(`!!document.querySelector('main')`); await shot("inspect"); }
  assert.deepEqual(b.errors, []); await save();
} finally {
  await writeFile(path.join(output, `${stage}-${attempt}-browser.json`), JSON.stringify({ at: new Date().toISOString(), errors: b.errors, requests: b.requests, networkFailures: b.networkFailures, responses: b.responses, view: await b.evaluate("document.body?.innerText"), url: await b.evaluate("location.href") }, null, 2));
  await b.close();
}
console.log(`${stage}: completed; receipt ${receiptPath}`);
