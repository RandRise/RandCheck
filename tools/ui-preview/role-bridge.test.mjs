import test from "node:test";
import assert from "node:assert/strict";
import { createPreviewRoleFetch } from "./role-bridge.mjs";

const origin = "http://localhost:3410";
for (const admin of [false, true]) test(`legacy Operator ${admin} keeps equivalent rights and blocks membership calls`, async () => {
  let calls = 0;
  const original = async () => { calls++; return Response.json({ role: "Operator", canManageMembers: admin, actorId: "same-actor" }); };
  const fetch = createPreviewRoleFetch(original, origin);
  assert.deepEqual(await (await fetch("/api/session")).json(), { role: admin ? "Admin" : "Staff", actorId: "same-actor" });
  for (const method of ["GET", "POST", "PUT", "DELETE"])
    assert.equal((await fetch("/api/organization/memberships/id", { method })).status, 503);
  assert.equal(calls, 1);
});
for (const session of [
  { role: "Viewer", canManageMembers: false }, { role: "Reviewer", canManageMembers: false },
  { role: "None", canManageMembers: true }, { role: "Operator" },
  { role: "Operator", canManageMembers: "true" }, { role: "Operator", canManageMembers: true, isActive: false },
  { role: "Unknown" }, { role: "Admin", canManageMembers: true },
]) test(`rejects incompatible preview session ${JSON.stringify(session)}`, async () => {
  const fetch = createPreviewRoleFetch(async () => Response.json(session), origin);
  assert.equal((await fetch("/api/session")).status, 403);
  assert.equal((await fetch("/api/organization/memberships")).status, 503);
});
for (const role of ["Staff", "Admin"]) test(`current ${role} session is passed through`, async () => {
  const calls = [];
  const fetch = createPreviewRoleFetch(async (input, init) => { calls.push([input, init]); return Response.json({ role }); }, origin);
  assert.deepEqual(await (await fetch("/api/session")).json(), { role });
  const request = { method: "PUT", body: "original" };
  assert.equal((await fetch("/api/organization/memberships/id", request)).status, 200);
  assert.equal(calls[1][1], request);
});
