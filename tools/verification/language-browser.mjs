/** Inspect and explicitly correct one known fictional Arabic value. Other fields
 * remain unchecked. This exercises the retained full-field review surface, not
 * a new comparison or a claim of general Arabic OCR accuracy. */
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { openBrowser } from "./browser-session.mjs";

const [demoArg, outputArg] = process.argv.slice(2);
const demo = path.resolve(demoArg), output = path.resolve(outputArg);
const owner = JSON.parse(await readFile(path.join(demo, ".demo-owner.json"), "utf8"));
assert.equal(owner.root, demo); assert.match(owner.project, /^uaedi-demo-[a-f0-9]{12}$/);
for (const [service, port, host] of [["web", "3000/tcp", "3308"], ["backend", "8080/tcp", "8308"]]) {
  const container = JSON.parse(execFileSync("docker", ["inspect", `${owner.project}-${service}-1`], { encoding: "utf8", windowsHide: true }))[0];
  assert.equal(container.Config.Labels["com.docker.compose.project"], owner.project);
  assert.equal(container.State.Running, true);
  assert.deepEqual(container.NetworkSettings.Ports[port], [{ HostIp: "127.0.0.1", HostPort: host }]);
  if (service === "backend") {
    assert.ok(container.Config.Env.includes("ConnectionStrings__Database=Host=db;Port=5432;Database=document_intelligence_demo;Username=demo;Password=disposable_demo_only"));
    assert.ok(container.Config.Env.includes("Security__Mode=Development"));
  } else {
    assert.ok(container.Config.Env.includes("NEXT_PUBLIC_API_URL=http://127.0.0.1:8308"));
    assert.ok(container.Config.Env.includes("AUTH_MODE=Development"));
  }
}
const receipt = JSON.parse(await readFile(path.join(output, "receipt.json"), "utf8"));
assert.equal(receipt.project, owner.project); assert.equal(receipt.completed, true);
const record = receipt.documents.find(row => row.name === "mixed-native-two-page.pdf");
const expected = record.accuracy.find(row => row.key === "payment_terms").expected;
const original = record.workspace.fields.find(field => field.fieldKey === "payment_terms");
const api = "http://127.0.0.1:8308";
const get = async route => { const response = await fetch(api + route); assert.equal(response.status, 200); return response.json(); };
const workspace = () => get(`/api/documents/${record.id}/review`);
const casesBefore = await get("/api/reconciliation-cases");
const b = await openBrowser(path.join(output, `browser-${Date.now()}`));
const card = "document.querySelector('article[aria-label=\"Payment terms\"]')";
const button = label => `[...document.querySelectorAll('button')].find(e=>e.textContent.trim()===${JSON.stringify(label)}&&!e.disabled)`;
const click = async label => { await b.waitFor(`!!(${button(label)})`); await b.evaluate(`(${button(label)}).click()`); };
const checks = [];
try {
  await b.call("Page.navigate", { url: `http://127.0.0.1:3308/?document=${record.id}` });
  await b.waitFor(`!!(${card})`);
  const before = await workspace();
  if (!before.session) await click("Start review");
  await click("All fields");
  await b.waitFor(`!!(${card})`);
  const existing = (await workspace()).fields.find(field => field.fieldKey === "payment_terms");
  if (!existing.latestAction) {
    await b.waitFor(`!!(${card}).querySelector('input:not([disabled])')`);
    await b.evaluate(`(() => {const card=${card}; const input=card.querySelector('input');Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(input,${JSON.stringify(expected)});input.dispatchEvent(new Event('input',{bubbles:true}));const select=card.querySelector('select');select.value='Read incorrectly by the system.';select.dispatchEvent(new Event('change',{bubbles:true}));})()`);
    await b.evaluate(`[...(${card}).querySelectorAll('button')].find(e=>e.textContent==='Save correction'&&!e.disabled).click()`);
    await b.waitFor(`(${card}).querySelector('.effective-value p')?.textContent===${JSON.stringify(expected)}&&(${card}).querySelector('.field-title i')?.textContent==='Corrected'`);
  }
  const after = await workspace();
  const saved = after.fields.find(field => field.fieldKey === "payment_terms");
  assert.equal(saved.effective.value, expected); assert.equal(saved.decisionStatus, "Corrected");
  assert.equal(saved.latestAction.reason, "Read incorrectly by the system.");
  assert.deepEqual(saved.machine, original.machine);
  assert.equal(after.session.status, "InProgress");
  assert.ok(after.fields.filter(field => field.fieldKey !== "payment_terms").every(field => ["Unreviewed", "Missing"].includes(field.decisionStatus)));
  assert.equal(await b.evaluate("[...document.querySelectorAll('button')].find(e=>e.textContent==='Complete review')?.disabled"), true);
  checks.push("Explicit Arabic diacritic correction saves the exact value and reason; original OCR evidence remains unchanged; other fields remain unchecked and completion is blocked");
  for (const theme of ["dark", "light"]) for (const width of [1440, 390]) {
    await b.call("Emulation.setDeviceMetricsOverride", { width, height: 1000, deviceScaleFactor: 1, mobile: false });
    await b.evaluate(`(()=>{const select=document.querySelector('select[aria-label=Appearance]');select.value='${theme}';select.dispatchEvent(new Event('change',{bubbles:true}));(${card}).scrollIntoView({block:'start'});})()`);
    await b.waitFor(`document.documentElement.dataset.theme==='${theme}' || document.querySelector('[data-theme="${theme}"]')!==null`);
    const layout = await b.evaluate(`(()=>{const card=${card}; const quote=card.querySelector('blockquote');return {overflow:document.documentElement.scrollWidth>innerWidth+1,direction:getComputedStyle(quote).direction,quote:quote.textContent,numeric:[...quote.querySelectorAll('bdi')].map(e=>({text:e.textContent,dir:e.dir})),saved:card.querySelector('.effective-value p').textContent};})()`);
    assert.equal(layout.overflow, false); assert.equal(layout.direction, "rtl"); assert.equal(layout.quote, original.machine.evidence[0].quotedText);
    assert.ok(layout.numeric.some(run => run.text === "30" && run.dir === "ltr")); assert.equal(layout.saved, expected);
    await writeFile(path.join(output, `arabic-${theme}-${width}.png`), Buffer.from((await b.call("Page.captureScreenshot", { format: "png" })).data, "base64"));
    checks.push(`${theme} ${width}px: Arabic RTL, isolated English digits, preserved evidence and no page overflow`);
  }
  await b.call("Page.reload");
  await b.waitFor(`!!(${card})&&(${card}).querySelector('.effective-value p')?.textContent===${JSON.stringify(expected)}`);
  assert.deepEqual((await workspace()).fields, after.fields);
  assert.deepEqual(await get("/api/reconciliation-cases"), casesBefore);
  assert.deepEqual(b.errors, []);
  checks.push("Reload preserves the correction and case-list summaries are unchanged");
  await writeFile(path.join(output, "browser-acceptance.json"), JSON.stringify({ checks, before, after, errors: b.errors, requests: b.requests, responses: b.responses, networkFailures: b.networkFailures }, null, 2));
} finally {
  await writeFile(path.join(output, `browser-state-${Date.now()}.json`), JSON.stringify({ checks, view: await b.evaluate("document.body?.innerText"), errors: b.errors, responses: b.responses }, null, 2));
  await b.close();
}
console.log(`${checks.length} Arabic browser assertions passed.`);
