import test from "node:test";
import assert from "node:assert/strict";
import { createPreviewComparisonFetch, installPreviewFetchBridge } from "./comparison-bridge.mjs";

const origin = "http://127.0.0.1:3410";
const path = "/api/reconciliation-cases/00000000-0000-4000-8000-000000000001/evaluations";
const options = body => ({
  method: "POST", headers: { "Content-Type": "application/json", Authorization: "Bearer preview-operator", "If-Match": '"saved-token"' },
  body: JSON.stringify(body),
});

test("adapts only the preview contract, preserving headers, fields, cancellation and response identity", async () => {
  const controller = new AbortController();
  const response = Response.json({ ruleSetVersion: "synthetic-total-only", coverage: { status: "NotRecorded" } });
  const calls = [];
  const fetch = createPreviewComparisonFetch(async request => { calls.push(request); return response; }, origin);
  const init = { ...options({ ruleSetVersion: "invoice-po-2.0", tolerances: { quantity: 0 } }), signal: controller.signal, credentials: "include" };
  assert.equal(await fetch(path, init), response);
  assert.equal(calls.length, 1);
  const request = calls[0];
  assert.deepEqual(await request.clone().json(), { ruleSetVersion: "invoice-po-1.0", tolerances: { quantity: 0 }, orderScope: "Unknown" });
  assert.equal(request.headers.get("authorization"), "Bearer preview-operator");
  assert.equal(request.headers.get("if-match"), '"saved-token"');
  assert.equal(request.credentials, "include");
  controller.abort();
  assert.equal(request.signal.aborted, true);
  assert.deepEqual(JSON.parse(init.body), { ruleSetVersion: "invoice-po-2.0", tolerances: { quantity: 0 } });
});

test("Request+init overrides take precedence without consuming the caller's body", async () => {
  const original = new Request(origin + path, options({ ruleSetVersion: "invoice-po-1.0", orderScope: "Partial" }));
  let sent;
  const fetch = createPreviewComparisonFetch(async request => { sent = request; return Response.json({}); }, origin);
  await fetch(original, options({ ruleSetVersion: "invoice-po-2.0" }));
  assert.equal((await sent.json()).orderScope, "Unknown");
  assert.equal((await original.json()).orderScope, "Partial");
});

for (const [name, input, init] of [
  ["other origin", "http://localhost:8080" + path, options({ ruleSetVersion: "invoice-po-2.0" })],
  ["other route", path + "/extra", options({ ruleSetVersion: "invoice-po-2.0" })],
  ["other method", path, { ...options({ ruleSetVersion: "invoice-po-2.0" }), method: "PUT" }],
  ["legacy comparison", path, options({ ruleSetVersion: "invoice-po-1.0", orderScope: "FullOrder" })],
  ["explicit scope", path, options({ ruleSetVersion: "invoice-po-2.0", orderScope: "Partial" })],
  ["unknown policy", path, options({ ruleSetVersion: "future" })],
  ["malformed JSON", path, { ...options({}), body: "{" }],
  ["missing policy", path, options({})],
  ["nonobject JSON", path, options(null)],
  ["array JSON", path, options([{ ruleSetVersion: "invoice-po-2.0" }])],
  ["non-JSON content", path, { ...options({ ruleSetVersion: "invoice-po-2.0" }), headers: { "Content-Type": "text/plain" } }],
]) test(`passes ${name} through unchanged with one transport call`, async () => {
  const calls = [];
  const response = Response.json({ error: "unchanged" }, { status: 400 });
  const fetch = createPreviewComparisonFetch(async (...args) => { calls.push(args); return response; }, origin);
  assert.equal(await fetch(input, init), response);
  assert.deepEqual(calls, [[input, init]]);
});

test("an unsupported Request body remains readable by the transport", async () => {
  const request = new Request(origin + path, { ...options({}), body: "{" });
  const fetch = createPreviewComparisonFetch(async input => {
    assert.equal(input, request);
    assert.equal(await input.text(), "{");
    return Response.json({});
  }, origin);
  await fetch(request);
});

test("a lost reply or failure response is never retried or converted to success", async () => {
  const failure = new Error("Lost reply");
  let calls = 0;
  const fetch = createPreviewComparisonFetch(async () => { calls++; throw failure; }, origin);
  await assert.rejects(fetch(path, options({ ruleSetVersion: "invoice-po-2.0" })), error => error === failure);
  assert.equal(calls, 1);
  for (const status of [400, 403, 412, 500]) {
    const response = Response.json({ detail: "unchanged" }, { status });
    const bridge = createPreviewComparisonFetch(async () => response, origin);
    assert.equal(await bridge(path, options({ ruleSetVersion: "invoice-po-2.0" })), response);
  }
});

test("repeated installation preserves one comparison adapter and legacy role restrictions", async () => {
  const requests = [];
  const target = {
    location: { origin }, document: { getElementById: () => null },
    fetch: async (input, init) => {
      requests.push([input, init]);
      return Response.json(String(input).endsWith("/api/session")
        ? { role: "Operator", canManageMembers: false, actorId: "same-user" } : {});
    },
  };
  installPreviewFetchBridge(target);
  installPreviewFetchBridge(target);
  assert.equal((await (await target.fetch("/api/session")).json()).role, "Staff");
  assert.equal((await target.fetch("/api/organization/memberships")).status, 503);
  await target.fetch(path, options({ ruleSetVersion: "invoice-po-2.0" }));
  assert.equal(requests.length, 2);
  assert.equal((await requests[1][0].json()).orderScope, "Unknown");
});
