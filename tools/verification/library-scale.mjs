import assert from "node:assert/strict";
import { writeFile } from "node:fs/promises";
import path from "node:path";
import { openBrowser } from "./browser-session.mjs";
import { createScaleLibrary } from "./library-scale-fixture.mjs";

const output = path.resolve("outputs/scale-integration-20261023");
const browser = await openBrowser(output);
const { call, evaluate, waitFor } = browser;
const samples = [];
try {
  for (const size of [1000, 10000]) {
    await call("Emulation.setDeviceMetricsOverride", { width: 1440, height: 1100, deviceScaleFactor: 1, mobile: false });
    await call("Page.navigate", { url: "http://127.0.0.1:3410/documents" });
    await waitFor("!!document.querySelector('.library-count')");
    const apiModule = browser.requests.find(r => new URL(r.url).pathname.endsWith("/security/api-fetch.ts"))?.url;
    assert.ok(apiModule, "Locate the actual preview transport module");
    // Install outside the retained preview bridge after it loads. Only this temporary tab changes.
    await evaluate(`(() => {
      const search = (${createScaleLibrary.toString()})(${size});
      const original = window.fetch.bind(window);
      window.scaleReceipts = [];
      window.fetch = async (input, init) => {
        const request = new Request(input instanceof Request ? input : new URL(String(input), location.origin), init);
        const url = new URL(request.url);
        if (url.origin === location.origin && !['GET','HEAD','OPTIONS'].includes(request.method)) throw new Error('Read-only scale preview blocked a write');
        if (url.origin === location.origin && url.pathname === '/api/documents/library') {
          const start = performance.now(); const page = search(url.searchParams); const json = JSON.stringify(page);
          window.scaleReceipts.push({elapsedMs:performance.now()-start,bytes:new TextEncoder().encode(json).length,returned:page.documents.length,total:page.total});
          return new Response(json,{headers:{'Content-Type':'application/json','Cache-Control':'no-store'}});
        }
        return original(input, init);
      };
      const label=document.createElement('p');label.textContent='READ-ONLY SCALE SIMULATION: ${size} fictional files. List/search only; do not open these files. No database performance is measured here.';
      label.style.cssText='padding:16px;background:#fff3bf;color:#222;font-weight:bold';document.body.prepend(label);
    })()`);
    await evaluate(`import(${JSON.stringify(apiModule)}).then(module => module.installTransport(window.fetch))`);
    const timings = [];
    for (let repetition = 0; repetition < 5; repetition++) {
      const start = performance.now();
      await evaluate("[...document.querySelectorAll('button')].find(b=>b.textContent==='Clear search').click()");
      await waitFor(`document.querySelector('.library-count')?.textContent==='Showing 1–10 of ${size}'`);
      await evaluate("[...document.querySelectorAll('.library-pages button')].find(b=>b.textContent==='Next').click()");
      await waitFor(`document.querySelector('.library-count')?.textContent==='Showing 11–20 of ${size}'`);
      timings.push(performance.now() - start);
    }
    const start = performance.now();
    await evaluate(`(()=>{const el=document.querySelector('input[type="search"]');Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(el,'Last supplier');el.dispatchEvent(new Event('input',{bubbles:true}));})()`);
    await evaluate("document.querySelector('.library-search').requestSubmit()");
    await waitFor("document.querySelector('.library-count')?.textContent==='Showing 1–1 of 1'");
    const searchMs = performance.now() - start;
    assert.equal(await evaluate(`document.querySelector('.document-list').textContent.includes('${String(size).padStart(5, "0")}')`), true);
    await evaluate("(()=>{const el=document.querySelector('.library-search select');el.value='Invoice';el.dispatchEvent(new Event('change',{bubbles:true}));})()");
    await waitFor("document.querySelector('.library-count')?.textContent==='No matching files'");
    await evaluate("[...document.querySelectorAll('button')].find(b=>b.textContent==='Clear search').click()");
    await waitFor(`document.querySelector('.library-count')?.textContent==='Showing 1–10 of ${size / 2}'`);
    const receipts = await evaluate("window.scaleReceipts");
    assert.ok(receipts.every(r => r.returned <= 10));
    for (const width of [1440, 420]) {
      await call("Emulation.setDeviceMetricsOverride", { width, height: 1100, deviceScaleFactor: 1, mobile: false });
      assert.ok(await evaluate("document.documentElement.scrollWidth <= innerWidth"));
      await writeFile(path.join(output, `library-${size}-${width}.png`), Buffer.from((await call("Page.captureScreenshot", { format: "png" })).data, "base64"));
    }
    samples.push({ size, label: "Browser simulation only; UI timings include CDP polling overhead", firstAndNextPageMs: timings, supplierSearchMs: searchMs,
      receipts, metrics: (await call("Performance.getMetrics")).metrics.filter(m => ["JSHeapUsedSize", "Nodes", "TaskDuration"].includes(m.name)) });
  }
  assert.deepEqual(browser.errors, []);
  assert.deepEqual(browser.requests.filter(r => new URL(r.url).port === "3410" && !["GET", "HEAD", "OPTIONS"].includes(r.method)), []);
  await writeFile(path.join(output, "library-scale.json"), JSON.stringify({ samples, runtimeErrors: browser.errors, applicationWrites: 0 }, null, 2));
  console.log("1,000 and 10,000 entry browser simulations passed. Evidence: " + output);
} finally { await browser.close(); }
