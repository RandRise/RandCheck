/** Run only through the owned test executable; config contains expiring offline tokens. */
import assert from "node:assert/strict";
import { readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { openBrowser } from "./browser-session.mjs";

const [configPath, output] = process.argv.slice(2);
const config = JSON.parse(await readFile(configPath, "utf8"));
assert.equal(new URL(config.api).hostname, "127.0.0.1");
assert.equal(await (await fetch("http://127.0.0.1:3338/__admin_acceptance_owner")).text(), config.instance);
const b = await openBrowser(output);
const checks = [];
const apiEvidence = [];
async function api(route, { method = "GET", body, token = config.token, status = 200, headers = {} } = {}) {
  const response = await fetch(config.api + route, { method, headers: { Authorization: `Bearer ${token}`, ...(body ? { "Content-Type": "application/json" } : {}), ...headers }, body: body ? JSON.stringify(body) : undefined });
  apiEvidence.push({ route, method, expected: status, actual: response.status });
  assert.equal(response.status, status, `${method} ${route}`);
  return response;
}
const members = async () => (await api("/api/organization/memberships")).json();
const policy = async () => (await api("/api/organization/retention")).json();
const text = value => b.waitFor(`document.body?.innerText.includes(${JSON.stringify(value)})`);
async function click(label) {
  await b.waitFor(`!![...document.querySelectorAll('button')].find(e => (e.textContent.trim()===${JSON.stringify(label)} || e.getAttribute('aria-label')===${JSON.stringify(label)}) && !e.disabled)`);
  await b.evaluate(`[...document.querySelectorAll('button')].find(e => (e.textContent.trim()===${JSON.stringify(label)} || e.getAttribute('aria-label')===${JSON.stringify(label)}) && !e.disabled).click()`);
}
async function set(selector, value) {
  await b.evaluate(`(() => {const e=document.querySelector(${JSON.stringify(selector)}); if(!e || e.disabled) throw Error('Missing enabled input'); Object.getOwnPropertyDescriptor(e.tagName==='SELECT'?HTMLSelectElement.prototype:HTMLInputElement.prototype,'value').set.call(e,${JSON.stringify(value)}); e.dispatchEvent(new Event(e.tagName==='SELECT'?'change':'input',{bubbles:true}));})()`);
}
const screenshot = async name => writeFile(path.join(output, name + ".png"), Buffer.from((await b.call("Page.captureScreenshot", { format: "png" })).data, "base64"));
async function navigate(route, ready) {
  await b.call("Page.navigate", { url: "http://127.0.0.1:3338" + route });
  await text(ready);
}
try {
  // Install before secureTransport captures fetch. Arm only for the named fault.
  await b.call("Page.addScriptToEvaluateOnNewDocument", { source: `(() => {const original=window.fetch.bind(window); window.__acceptedPolicyWrites=0; window.__losePolicyReply=false; window.fetch=async (...args)=>{const response=await original(...args); if(window.__losePolicyReply&&String(args[0]).endsWith('/api/organization/retention')&&args[1]?.method==='PUT'&&response.ok){window.__acceptedPolicyWrites++; if(window.__acceptedPolicyWrites===1) throw new TypeError('Simulated lost response after real commit');} return response;};})()` });
  await b.call("Emulation.setDeviceMetricsOverride", { width: 1440, height: 1000, deviceScaleFactor: 1, mobile: false });
  await b.call("Page.navigate", { url: "http://127.0.0.1:3338/organization" });
  await b.waitFor(`document.body?.innerText.includes('People & access')&&document.body?.innerText.includes('Sensitive display name')`);
  assert.ok(b.responses.some(response => response.url === `${config.api}/api/session` && response.status === 200));
  assert.ok(b.responses.some(response => response.url === `${config.api}/api/organization/memberships` && response.status === 200));
  await writeFile(path.join(output, "people-connected.png"), Buffer.from((await b.call("Page.captureScreenshot", { format: "png" })).data, "base64"));
  await writeFile(path.join(output, "connection.json"), JSON.stringify({ api: config.api, offlineIdentity: true, realHttp: true, responses: b.responses, errors: b.errors }, null, 2));
  assert.deepEqual(b.errors, []);
  checks.push("Real Admin session and membership HTTP reads through normal browser authorization and CORS");

  const initialMembers = await members();
  const admin = initialMembers.find(member => member.id !== config.memberId);
  await click("Edit " + admin.displayName);
  await set(".people-editor select", "Staff");
  await click("Save changes");
  await text("Keep at least one active administrator");
  assert.deepEqual(await members(), initialMembers);
  checks.push("Last active Admin cannot demote themselves; members remain unchanged");
  await click("Close");

  let member = (await members()).find(row => row.id === config.memberId);
  await click("Edit " + member.displayName);
  await set(".people-editor select", "Admin");
  await click("Grant Admin access");
  await text(member.displayName + ": access saved");
  const promoted = (await members()).find(row => row.id === config.memberId);
  assert.equal(promoted.role, "Admin"); assert.notEqual(promoted.version, member.version);
  checks.push("Explicit browser Admin grant updates the real versioned membership");

  await click("Edit " + member.displayName);
  await api(`/api/organization/memberships/${member.id}`, { method: "PUT", body: { expectedVersion: promoted.version, role: "Staff", isActive: true } });
  await set(".people-editor select", "Staff");
  await click("Save changes");
  await text("changed by another administrator");
  assert.equal((await members()).find(row => row.id === member.id).role, "Staff");
  await click("Reload members");
  await b.waitFor("!document.querySelector('.people-editor')");
  checks.push("Stale browser editor receives conflict and reloads without overwriting a concurrent change");

  const grant = await (await api(`/api/documents/${config.documentId}/source-access`, { method: "POST", token: config.staffToken })).json();
  const source = await api(grant.url, { token: config.staffToken, headers: { "X-Document-Access": grant.accessToken } });
  assert.equal(await source.text(), config.expectedSource);
  assert.match(source.headers.get("content-disposition"), /attachment/);
  assert.match(source.headers.get("cache-control"), /no-store/);
  checks.push("Current Staff downloads the exact fictional original using an actor-bound source grant");

  await click("Edit " + member.displayName);
  await b.evaluate("document.querySelector('.people-editor input[type=checkbox]').click()");
  await click("Save changes");
  await text("Their next API request will be denied");
  assert.equal((await members()).find(row => row.id === member.id).isActive, true);
  await click("Confirm removal");
  await text(member.displayName + ": access revoked");
  await api("/api/session", { token: config.staffToken, status: 403 });
  await api(grant.url, { token: config.staffToken, headers: { "X-Document-Access": grant.accessToken }, status: 403 });
  await api("/api/session");
  checks.push("Access removal requires confirmation; existing Staff token and source grant are denied immediately; Admin remains active");

  await navigate("/organization/retention", "How long to keep files");
  const originalPolicy = await policy();
  await set("section[aria-labelledby=storage-settings-title] input[type=number]", "30");
  await click("Save settings");
  await text("Storage settings saved and recorded");
  const savedPolicy = await policy();
  assert.equal(savedPolicy.documentDays, 30); assert.notEqual(savedPolicy.version, originalPolicy.version);
  checks.push("Browser storage settings persist with a new version without removing documents");

  await click("Add protection");
  await text("Protection added");
  let holds = await (await api("/api/organization/retention/holds?offset=0")).json();
  assert.equal(holds.length, 1); assert.equal(holds[0].documentId, null); assert.equal(holds[0].releasedAtUtc, null);
  await click("Release protection");
  await text("Other protections still apply");
  assert.equal((await (await api("/api/organization/retention/holds?offset=0")).json())[0].releasedAtUtc, null);
  await click("Confirm release");
  await text("Protection released and recorded");
  holds = await (await api("/api/organization/retention/holds?offset=0")).json();
  assert.ok(holds[0].releasedAtUtc); assert.equal(holds.length, 1);
  checks.push("Organization protection is stored, requires explicit release, and remains in history");

  // Drop only the reply after a real accepted write. Never mock or replay the API write.
  await b.evaluate("window.__losePolicyReply=true");
  await set("section[aria-labelledby=storage-settings-title] input[type=number]", "31");
  await click("Save settings");
  await text("The result could not be confirmed");
  assert.equal((await policy()).documentDays, 31);
  assert.equal(await b.evaluate("[...document.querySelectorAll('button')].find(e=>e.textContent==='Save settings').disabled"), true);
  await click("Refresh controls");
  await text("Saved settings changed");
  await click("Discard entries and load saved settings");
  assert.equal(await b.evaluate("document.querySelector('section[aria-labelledby=storage-settings-title] input').value"), "31");
  assert.equal(await b.evaluate("window.__acceptedPolicyWrites"), 1);
  checks.push("Lost successful save reply locks writes; explicit refresh recovers the committed state without replay");
  await screenshot("storage-real");

  const audits = await (await api("/api/organization/audit?offset=0")).json();
  for (const action of ["membership.changed", "retention.policy-changed", "retention.hold-placed", "retention.hold-released", "document.source-download-authorized"]) assert.ok(audits.some(row => row.action === action && row.outcome === "Succeeded"), action);
  const beforeAudit = b.requests.length;
  await navigate("/organization/audit", "File protection released");
  await text("Access changed");
  await screenshot("audit-real");
  assert.deepEqual(b.requests.slice(beforeAudit).filter(row => row.url.startsWith(config.api) && row.method !== "GET" && row.method !== "OPTIONS"), []);
  checks.push("Activity screen reads actual successful access/storage/source events without API writes");
  assert.deepEqual(b.errors, []);
  await writeFile(path.join(output, "acceptance.json"), JSON.stringify({ checks, apiEvidence, offlineIdentity: true, realDatabase: "document_intelligence_test", limits: ["Offline JWT metadata, not live Entra", "Vite mounts actual components; Next server routing is covered by the separate demo", "Fictional original and deterministic clean scanner"] }, null, 2));
} finally {
  await writeFile(path.join(output, "assertions-progress.json"), JSON.stringify({ checks, apiEvidence }, null, 2));
  await writeFile(path.join(output, "browser-state.json"), JSON.stringify({ view: await b.evaluate("document.body?.innerText"), requests: b.requests, responses: b.responses, networkFailures: b.networkFailures, errors: b.errors }, null, 2));
  await b.close();
}
console.log(`${checks.length} real Admin browser/API assertions passed.`);
