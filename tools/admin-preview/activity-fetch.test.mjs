import test from "node:test";
import assert from "node:assert/strict";
import { createActivityPreviewFetch } from "./activity-fetch.mjs";

const origin = "http://127.0.0.1:3430";
const rows = Array.from({ length: 105 }, (_, n) => ({ id: String(n).padStart(3, "0"), recordedAtUtc: "2026-10-26T12:00:00Z" }));
test("sample pages retain existing events without mutating or resetting them", async () => {
  const before = JSON.stringify(rows);
  const calls = [];
  const fetcher = createActivityPreviewFetch(async (...args) => { calls.push(args); return Response.json(rows); }, origin, []);
  const first = await (await fetcher(`${origin}/api/organization/audit?offset=0`)).json();
  const next = await (await fetcher(`${origin}/api/organization/audit?offset=100`)).json();
  assert.equal(first.length, 100); assert.equal(next.length, 5);
  assert.equal(new Set([...first, ...next].map(row => row.id)).size, 105);
  assert.equal(JSON.stringify(rows), before);
  assert.equal(calls.length, 2);
});
test("only adapts same-origin audit GET; never manufactures failed-read success", async () => {
  const calls = [];
  const fetcher = createActivityPreviewFetch(async (...args) => { calls.push(args); return Response.json({}, { status: 503 }); }, origin);
  for (const [url, init] of [[`${origin}/api/organization/audit`, undefined], [`${origin}/api/organization/memberships`, undefined],
    ["http://127.0.0.1:8310/api/organization/audit", undefined], [`${origin}/api/organization/audit`, { method: "POST", body: "unchanged" }]]) {
    assert.equal((await fetcher(url, init)).status, 503);
    assert.deepEqual(calls.at(-1), [url, init]);
  }
});
test("blocks invalid offsets without a request and keeps fixture additions read-only", async () => {
  let calls = 0;
  const fetcher = createActivityPreviewFetch(async () => { calls++; return Response.json([]); }, origin);
  assert.equal((await fetcher(`${origin}/api/organization/audit?offset=10100`)).status, 400);
  assert.equal(calls, 0);
  const first = await (await fetcher(`${origin}/api/organization/audit`)).json();
  assert.equal(first.length, 4);
  assert.equal(calls, 1);
});
