import assert from "node:assert/strict";
import { test } from "node:test";
import { createStoragePreviewFetch } from "./storage-fetch.mjs";

test("sample storage preserves its own state, versions and other preview traffic", async () => {
  const values = new Map();
  const storage = { getItem: key => values.get(key), setItem: (key, value) => values.set(key, value) };
  const calls = [];
  const original = async (url, init) => { calls.push([url, init]); return Response.json([]); };
  const base = "http://127.0.0.1:3430";
  let fetcher = createStoragePreviewFetch(original, base, storage);
  const url = `${base}/api/organization/retention`;
  const policy = await (await fetcher(url)).json();
  const put = { method: "PUT", body: JSON.stringify({ expectedVersion: policy.version, documentDays: 30, quarantineHours: 24 }) };
  const saved = await (await fetcher(url, put)).json();
  assert.notEqual(saved.version, policy.version);
  assert.equal((await fetcher(url, put)).status, 409);
  fetcher = createStoragePreviewFetch(original, base, storage);
  assert.deepEqual(await (await fetcher(url)).json(), saved);
  assert.equal(calls.length, 0);
  await fetcher(`${base}/api/organization/memberships`);
  assert.equal(calls.length, 1);
  assert.equal((await (await fetcher(`${base}/api/organization/audit`)).json()).length, 1);
});

test("sample cleanup preserves uploads under an organization hold until explicit release", async () => {
  const values = new Map();
  const storage = { getItem: key => values.get(key), setItem: (key, value) => values.set(key, value) };
  const origin = "http://127.0.0.1:3430", base = `${origin}/api/organization/retention`;
  const fetcher = createStoragePreviewFetch(() => { throw Error("Must not reach a real API"); }, origin, storage);
  const request = async (url, method, value) => (await fetcher(url, { method, body: JSON.stringify(value) })).json();
  let policy = await (await fetcher(base)).json();
  policy = await request(base, "PUT", { expectedVersion: policy.version, documentDays: null, quarantineHours: 24 });
  const hold = await request(`${base}/holds`, "POST", { documentId: null, reason: "LegalHold" });
  const cleanup = execute => request(`${origin}/api/organization/quarantine/cleanup`, "POST", { expectedPolicyVersion: policy.version, execute });
  for (const execute of [false, true]) assert.deepEqual(await cleanup(execute), { executed: execute, eligible: 0, deleted: 0, preserved: 3, truncated: false });
  await request(`${base}/holds/${hold.id}/release`, "POST", { expectedVersion: hold.version });
  assert.equal((await cleanup(false)).eligible, 2);
  assert.equal((await cleanup(true)).deleted, 2);
  assert.equal((await cleanup(false)).eligible, 0);
});
