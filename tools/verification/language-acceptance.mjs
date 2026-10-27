/** Bounded OCR acceptance on the explicitly owned fictional Docker demo.
 * Inputs and per-file operation keys are journaled before upload. Resume preserves
 * completed work; no values are confirmed and no comparison is created.
 * Usage: node tools/verification/language-acceptance.mjs DEMO_ROOT OUTPUT
 */
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { createHash, randomUUID } from "node:crypto";
import { readFile, writeFile } from "node:fs/promises";
import path from "node:path";

const [demoArg, outputArg] = process.argv.slice(2);
const demo = path.resolve(demoArg), output = path.resolve(outputArg);
const owner = JSON.parse(await readFile(path.join(demo, ".demo-owner.json"), "utf8"));
assert.equal(owner.root, demo); assert.match(owner.project, /^uaedi-demo-[a-f0-9]{12}$/);
const inspect = name => JSON.parse(execFileSync("docker", ["inspect", `${owner.project}-${name}-1`], { encoding: "utf8", windowsHide: true }))[0];
const backend = inspect("backend"), database = inspect("db");
for (const container of [backend, database]) { assert.equal(container.Config.Labels["com.docker.compose.project"], owner.project); assert.equal(container.State.Running, true); }
assert.deepEqual(backend.NetworkSettings.Ports["8080/tcp"], [{ HostIp: "127.0.0.1", HostPort: "8308" }]);
assert.ok(backend.Config.Env.includes("ConnectionStrings__Database=Host=db;Port=5432;Database=document_intelligence_demo;Username=demo;Password=disposable_demo_only"));
assert.ok(backend.Config.Env.includes("Security__Mode=Development"));
const api = "http://127.0.0.1:8308";
const get = async route => { const response = await fetch(api + route); assert.equal(response.status, 200, route); return response.json(); };
const inputs = JSON.parse(await readFile(path.join(output, "inputs.json"), "utf8"));
const receiptPath = path.join(output, "receipt.json");
let receipt;
try { receipt = JSON.parse(await readFile(receiptPath, "utf8")); } catch (error) { if (error.code !== "ENOENT") throw error; }
receipt ??= { project: owner.project, fictional: true, documents: [], casesBefore: await get("/api/reconciliation-cases") };
assert.equal(receipt.project, owner.project);
const save = () => writeFile(receiptPath, JSON.stringify(receipt, null, 2));
const sql = query => JSON.parse(execFileSync("docker", ["exec", `${owner.project}-db-1`, "psql", "-U", "demo", "-d", "document_intelligence_demo", "-At", "-c", query], { encoding: "utf8", windowsHide: true }));
for (const input of inputs) {
  const bytes = await readFile(input.path);
  assert.equal(createHash("sha256").update(bytes).digest("hex"), input.sha256);
  let record = receipt.documents.find(item => item.name === input.name);
  if (!record) { record = { name: input.name, sha256: input.sha256, operationKey: randomUUID() }; receipt.documents.push(record); await save(); }
  assert.equal(record.sha256, input.sha256);
  if (record.verified) continue;
  if (!record.id) {
    const form = new FormData(); form.set("file", new Blob([bytes], { type: input.mime }), input.name);
    const response = await fetch(api + "/api/documents", { method: "POST", headers: { "X-Operation-Key": record.operationKey }, body: form });
    assert.equal(response.status, 202); const uploaded = await response.json();
    record.id = uploaded.id; await save();
  }
  assert.match(record.id, /^[a-f0-9-]{36}$/);
  for (let attempt = 0; attempt < 180; attempt++) {
    record.processing = await get(`/api/documents/${record.id}/processing`);
    if (["Ready", "Partial", "Failed", "Cancelled"].includes(record.processing.status)) break;
    await new Promise(resolve => setTimeout(resolve, 1000));
  }
  await save();
  assert.equal(record.processing.status, "Partial", "Incomplete fixture must remain partial");
  record.document = await get(`/api/documents/${record.id}`);
  assert.equal(record.document.sha256, input.sha256);
  record.workspace = await get(`/api/documents/${record.id}/review`);
  assert.equal(record.workspace.session, null);
  assert.ok(record.workspace.fields.every(field => ["Unreviewed", "Missing"].includes(field.decisionStatus)));
  record.accuracy = Object.entries(input.expected).map(([key, expected]) => {
    const observed = record.workspace.fields.find(field => field.fieldKey === key && field.occurrenceIndex === 0)?.machine?.value ?? null;
    const cleaned = observed?.replaceAll(",", "");
    return { key, expected, observed, matches: cleaned === expected || /^\d+(\.\d+)?$/.test(cleaned ?? "") && /^\d+(\.\d+)?$/.test(expected) && Number(cleaned) === Number(expected) };
  });
  // SQL is read-only and identifiers above are strictly validated. Capture only
  // this fictional run's text/evidence; never inspect the retained workspace.
  const run = record.processing.extractionRunId;
  assert.match(run, /^[a-f0-9-]{36}$/);
  record.provider = sql(`SELECT row_to_json(r) FROM (SELECT provider_name,provider_version,model_name,model_version,status,language,page_count FROM extraction_runs WHERE id='${run}') r`);
  assert.equal(record.provider.provider_version, "local-1.6.0");
  record.pages = sql(`SELECT coalesce(json_agg(r),'[]') FROM (SELECT page_number,canonical_text FROM extraction_pages WHERE extraction_run_id='${run}' ORDER BY page_number) r`);
  record.evidence = sql(`SELECT coalesce(json_agg(r),'[]') FROM (SELECT page_number,quoted_text,byte_start,byte_end,bounding_polygon,coordinate_unit FROM field_evidence WHERE extraction_run_id='${run}') r`);
  record.warnings = sql(`SELECT coalesce(json_agg(r),'[]') FROM (SELECT code,safe_summary,page_number,field_key FROM extraction_warnings WHERE extraction_run_id='${run}') r`);
  assert.ok(record.evidence.length > 0);
  for (const evidence of record.evidence) {
    const page = record.pages.find(page => page.page_number === evidence.page_number);
    assert.equal(Buffer.from(page.canonical_text, "utf8").subarray(evidence.byte_start, evidence.byte_end).toString("utf8"), evidence.quoted_text);
    assert.equal(evidence.coordinate_unit, "normalized");
    assert.equal(evidence.bounding_polygon.length, 4);
    assert.ok(evidence.bounding_polygon.every(point => point.every(value => Number.isFinite(value) && value >= 0 && value <= 1)));
  }
  if (input.name.startsWith("mixed-")) assert.ok(record.pages.some(page => /[\u0600-\u06ff]/.test(page.canonical_text)));
  record.verified = true; await save();
  console.log(`${input.name}: ${record.accuracy.filter(row => row.matches).length}/${record.accuracy.length} expected headers; ${record.evidence.length} exact quotes; Partial and unreviewed.`);
}
assert.deepEqual(await get("/api/reconciliation-cases"), receipt.casesBefore);
receipt.completed = true; await save();
