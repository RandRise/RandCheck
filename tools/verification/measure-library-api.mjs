import assert from "node:assert/strict";
import { writeFile } from "node:fs/promises";
import path from "node:path";

const count = Number(process.argv[2]);
if (![1000, 10000].includes(count)) throw new Error("Specify 1000 or 10000.");
const api = "http://127.0.0.1:8310";
const rows = [];
const last = String(count - 1).padStart(5, "0");
const cases = [
  ["first-page", "", count], ["second-page", "offset=10", count],
  ["deep-page", `offset=${count - 10}`, count],
  ["supplier", `q=Validation%20supplier%20${last}`, 1],
  ["printed-number", `q=PO-${last}`, 1],
  ["no-match", "q=nonexistent-validation-supplier", 0],
  ["invoice-filter", "type=Invoice", count / 2],
];
for (const [scenario, query, total] of cases) {
  const samples = [], sizes = [];
  for (let attempt = 0; attempt < 9; attempt++) {
    const start = performance.now();
    const response = await fetch(`${api}/api/documents/library?${query}`, { signal: AbortSignal.timeout(30000) });
    const text = await response.text();
    assert.equal(response.status, 200, `${scenario}: ${text}`);
    assert.ok(response.headers.get("cache-control")?.includes("no-store"));
    const page = JSON.parse(text);
    assert.equal(page.total, total, scenario);
    assert.ok(page.documents.length <= 10);
    if (scenario === "supplier" || scenario === "printed-number") assert.ok(page.documents[0].originalFilename.includes(last));
    samples.push(performance.now() - start); sizes.push(Buffer.byteLength(text));
  }
  const warm = samples.slice(1).sort((a, b) => a - b);
  rows.push({ scenario, firstMs: samples[0], warmMedianMs: (warm[3] + warm[4]) / 2, warmMaxMs: warm.at(-1), samplesMs: samples, maxBytes: Math.max(...sizes) });
}
await writeFile(path.resolve(`outputs/scale-integration-20261023/api-library-${count}.json`), JSON.stringify({ documents: count, identity: "Development", databaseRole: "restricted", concurrency: 1, rows }, null, 2));
console.log(JSON.stringify(rows.map(({ scenario, firstMs, warmMedianMs, warmMaxMs }) => ({ scenario, firstMs: Math.round(firstMs), warmMedianMs: Math.round(warmMedianMs), warmMaxMs: Math.round(warmMaxMs) }))));
