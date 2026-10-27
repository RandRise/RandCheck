import assert from "node:assert/strict";
import { writeFile } from "node:fs/promises";
import path from "node:path";
import { openBrowser } from "./browser-session.mjs";

const output = path.resolve("outputs/scale-integration-20261023");
const browser = await openBrowser(output);
const { call, evaluate, waitFor } = browser;
try {
  await call("Emulation.setDeviceMetricsOverride", { width: 1440, height: 1100, deviceScaleFactor: 1, mobile: false });
  await call("Page.navigate", { url: "http://127.0.0.1:3310/documents" });
  await waitFor("document.querySelector('.library-count')?.textContent==='Showing 1–10 of 10000'");
  assert.equal(await evaluate("document.querySelectorAll('.document-list button').length"), 10);
  await evaluate("[...document.querySelectorAll('.library-pages button')].find(b=>b.textContent==='Next').click()");
  await waitFor("document.querySelector('.library-count')?.textContent==='Showing 11–20 of 10000'");
  await evaluate("(()=>{const el=document.querySelector('input[type=search]');Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(el,'Validation supplier 09999');el.dispatchEvent(new Event('input',{bubbles:true}));})()");
  await evaluate("document.querySelector('.library-search').requestSubmit()");
  await waitFor("document.querySelector('.library-count')?.textContent==='Showing 1–1 of 1'");
  assert.equal(await evaluate("document.querySelector('.document-list').textContent.includes('09999')"), true);
  const requests = browser.requests.filter(r => new URL(r.url).port === "8310");
  assert.ok(requests.length >= 3);
  assert.ok(requests.every(r => r.method === "GET" || r.method === "OPTIONS"));
  assert.ok(requests.filter(r => r.method === "GET").every(r => new URL(r.url).pathname === "/api/documents/library"));
  assert.deepEqual(browser.errors, []);
  await writeFile(path.join(output, "live-library.png"), Buffer.from((await call("Page.captureScreenshot", { format: "png" })).data, "base64"));
  await writeFile(path.join(output, "live-library.json"), JSON.stringify({ size: 10000, tenVisibleRows: true, nextPage: true,
    supplierBeyondFirstPage: true, noFullWorkListRequest: true, applicationWrites: 0, runtimeErrors: browser.errors, requests }, null, 2));
  console.log("Live Next.js library uses paged API reads; 10,000-file paging and supplier search passed.");
} finally { await browser.close(); }
