import { test } from "node:test";
import assert from "node:assert/strict";
import { createServer } from "node:http";
import { createFixtureApi } from "./fixtures.mjs";

test("review repeats preserve ETag/history and explicit added-field revisions stay valid", async t => {
  const { call } = await setup(t);
  const doc = (await call("/api/documents")).value[0];
  let review = await call(`/api/documents/${doc.id}/reviews`, { method: "POST" });
  const path = `/api/documents/${doc.id}/reviews/${review.value.session.id}/actions`;
  const added = { action: "Added", fieldKey: "due_date", occurrenceIndex: 0, valueType: "Date", value: "2026-10-02", reason: "Checked source" };
  review = await call(path, { method: "POST", etag: review.etag, body: added });
  assert.equal(review.status, 200);
  const saved = review.etag;
  const replay = await call(path, { method: "POST", etag: saved, body: { ...added, reason: " Checked source " } });
  assert.equal(replay.etag, saved);
  assert.equal((await call(`/api/documents/${doc.id}/review-history`)).value.actions.length, 1);
  review = await call(path, { method: "POST", etag: saved, body: { ...added, value: "2026-10-03" } });
  assert.notEqual(review.etag, saved);
  assert.equal((await call(path, { method: "POST", etag: saved, body: added })).status, 412);
  const cleared = { action: "Cleared", fieldKey: "due_date", occurrenceIndex: 0, reason: "Removed date" };
  review = await call(path, { method: "POST", etag: review.etag, body: cleared });
  assert.equal(review.status, 200);
  assert.equal((await call(path, { method: "POST", etag: review.etag, body: cleared })).etag, review.etag);
  assert.equal((await call(path, { method: "POST", etag: review.etag, body: { ...cleared, reason: "Different" } })).status, 400);
  assert.equal((await call(`/api/documents/${doc.id}/review-history`)).value.actions.length, 3);
  const completed = (await call("/api/documents")).value[1];
  assert.equal((await call(`/api/documents/${completed.id}/reviews`, { method: "POST" })).status, 409);
});

test("case references, metadata and filters preserve identity with role and ETag checks", async t => {
  const { call } = await setup(t);
  const created = await call("/api/reconciliation-cases", { method: "POST", body: { name: "  لوازم المكتب  ", category: "OfficeSupplies" } });
  assert.equal(created.status, 201); assert.equal(created.value.name, "لوازم المكتب");
  assert.match(created.value.reference, /^REC-\d{4}-000001$/);
  const path = `/api/reconciliation-cases/${created.value.id}`;
  for (const role of ["unknown", "revoked"]) {
    const result = await call(path, { method: "PUT", role, etag: created.etag, body: { name: "Rejected", category: "Services" } });
    assert.equal(result.status, 403);
  }
  assert.equal((await call(path, { method: "PUT", body: { name: "Name", category: "Services" } })).status, 428);
  const updated = await call(path, { method: "PUT", role: "staff-three", etag: created.etag, body: { name: "استشارات · Consulting", category: "Services" } });
  assert.equal(updated.status, 200); assert.equal(updated.value.id, created.value.id); assert.equal(updated.value.reference, created.value.reference);
  assert.deepEqual(updated.value.documents, created.value.documents); assert.deepEqual(updated.value.runs, created.value.runs);
  assert.notEqual(updated.etag, created.etag);
  const unchanged = await call(path, { method: "PUT", etag: updated.etag, body: { name: updated.value.name, category: "Services" } });
  assert.equal(unchanged.etag, updated.etag);
  assert.equal((await call(path, { method: "PUT", etag: created.etag, body: { name: "Stale", category: "Equipment" } })).status, 412);
  assert.equal((await call(path, { role: "staff-two" })).value.name, updated.value.name);
  const date = created.value.createdAt.slice(0, 10);
  const filters = new URLSearchParams({ q: "استشارات", category: "Services", status: "Draft", createdFrom: date, createdTo: date });
  assert.equal((await call(`/api/reconciliation-cases?${filters}`, { role: "staff-two" })).value.cases[0].reference, created.value.reference);
  assert.equal((await call(`/api/reconciliation-cases?q=${created.value.reference}`)).value.cases.length, 1);
  assert.equal((await call("/api/reconciliation-cases?category=OfficeSupplies")).value.cases.length, 0);
  for (const query of ["category=Unknown", "status=Unknown", "createdFrom=2026-02-30", "createdFrom=2027-01-01&createdTo=2026-01-01"])
    assert.equal((await call(`/api/reconciliation-cases?${query}`)).status, 400);
  const audit = await call("/api/organization/audit", { role: "admin" });
  assert.ok(audit.value.some(event => event.action === "Preview.Case.MetadataChanged" && event.targetId === created.value.id));
  assert.ok(audit.value.every(event => !JSON.stringify(event).includes("استشارات")));
});

test("case creation defaults category and assigns distinct references under concurrent preview requests", async t => {
  const { call } = await setup(t);
  const results = await Promise.all(Array.from({ length: 12 }, (_, index) => call("/api/reconciliation-cases", { method: "POST", body: { name: `Case ${index}` } })));
  assert.ok(results.every(result => result.status === 201 && result.value.category === "Uncategorized"));
  assert.equal(new Set(results.map(result => result.value.reference)).size, 12);
  for (const body of [{ name: " ", category: "Services" }, { name: "x".repeat(201) }, { name: "Title", category: "Custom" }])
    assert.equal((await call("/api/reconciliation-cases", { method: "POST", body })).status, 400);
});

async function setup(t) {
  let api;
  const server = createServer((req, res) => api(req, res).catch(e => { res.writeHead(500).end(e.message); }));
  await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
  const origin = `http://127.0.0.1:${server.address().port}`;
  api = createFixtureApi(origin);
  t.after(() => new Promise(resolve => server.close(resolve)));
  const call = async (path, { method = "GET", body, role = "operator", etag, headers = {} } = {}) => {
    const response = await fetch(origin+path, { method, headers: { Authorization: `Bearer preview-${role}`, Origin: origin,
      "Content-Type": "application/json", ...(etag ? { "If-Match": etag } : {}), ...headers }, body: body === undefined ? undefined : JSON.stringify(body) });
    return { status: response.status, etag: response.headers.get("etag"), value: await response.json() };
  };
  return { call, origin };
}

test("case creation retries reuse one actor-scoped case and reject changed payloads", async t => {
  const { call } = await setup(t);
  const body = { name: "  Retry case — حالة  ", category: "Services", requestKey: "case-intent-1" };
  const attempts = await Promise.all(Array.from({ length: 6 }, () => call("/api/reconciliation-cases", { method: "POST", body })));
  assert.ok(attempts.every(result => result.status === 201));
  assert.equal(new Set(attempts.map(result => result.value.id)).size, 1);
  let current = attempts[0]; const path = `/api/reconciliation-cases/${current.value.id}`;
  current = await call(path, { method: "PUT", etag: current.etag, body: { name: "Renamed", category: "Equipment" } });
  const replay = await call("/api/reconciliation-cases", { method: "POST", body: { ...body, name: body.name.trim() } });
  assert.equal(replay.value.id, current.value.id); assert.equal(replay.value.name, "Renamed"); assert.equal(replay.etag, current.etag);
  assert.equal((await call("/api/reconciliation-cases", { method: "POST", body: { ...body, name: "Different intent" } })).status, 409);
  for (const role of ["unknown", "revoked"]) assert.equal((await call("/api/reconciliation-cases", { method: "POST", role, body })).status, 403);
  const otherActor = await call("/api/reconciliation-cases", { method: "POST", role: "staff-three", body });
  assert.notEqual(otherActor.value.id, current.value.id);
  for (const requestKey of ["", " padded ", "x".repeat(101)]) assert.equal((await call("/api/reconciliation-cases", { method: "POST", body: { ...body, requestKey } })).status, 400);
  const audit = await call("/api/organization/audit", { role: "admin" });
  assert.equal(audit.value.filter(event => event.action === "Preview.Case.Created" && event.targetId === current.value.id).length, 1);
});

test("repeated pins and identical decisions are no-ops; revisions and stale runs are protected", async t => {
  const { call } = await setup(t);
  const docs = (await call("/api/documents")).value;
  let current = await call("/api/reconciliation-cases", { method: "POST", body: { name: "Repeat checks", requestKey: "repeat-checks" } });
  const path = `/api/reconciliation-cases/${current.value.id}`;
  for (const [role, filename] of [["PurchaseOrder", "Completed purchase order.txt"], ["Invoice", "Completed invoice.txt"]]) {
    const doc = docs.find(value => value.originalFilename === filename);
    const review = await call(`/api/documents/${doc.id}/review`);
    const body = { documentId: doc.id, reviewSessionId: review.value.session.id };
    current = await call(`${path}/documents/${role}`, { method: "PUT", etag: current.etag, body });
    const replay = await call(`${path}/documents/${role}`, { method: "PUT", etag: current.etag, body });
    assert.equal(replay.etag, current.etag); assert.deepEqual(replay.value.documents, current.value.documents);
  }
  const beforeRun = current.etag;
  current = await call(`${path}/evaluations`, { method: "POST", etag: beforeRun, body: {} });
  assert.equal((await call(`${path}/evaluations`, { method: "POST", etag: beforeRun, body: {} })).status, 412);
  const run = current.value.runs[0], finding = run.findings[0];
  const decisionPath = `${path}/runs/${run.id}/findings/${finding.id}/resolutions`;
  const decision = { action: "Accepted", reason: "  Checked — راجعت  " };
  current = await call(decisionPath, { method: "POST", etag: current.etag, body: decision });
  const noop = await call(decisionPath, { method: "POST", etag: current.etag, body: decision });
  assert.equal(noop.etag, current.etag); assert.equal(noop.value.runs[0].findings[0].resolutions.length, 1);
  const firstEtag = current.etag;
  current = await call(decisionPath, { method: "POST", etag: current.etag, body: { action: "Resolved", reason: "New evidence" } });
  assert.equal(current.value.runs[0].findings[0].resolutions.length, 2);
  assert.equal((await call(decisionPath, { method: "POST", etag: firstEtag, body: decision })).status, 412);
  const oldRun = current.value.runs[0];
  current = await call(`${path}/evaluations`, { method: "POST", etag: current.etag, body: { documentTotalTolerance: 200 } });
  assert.deepEqual(current.value.runs[1], oldRun); assert.equal(current.value.runs.length, 2);
});

test("saved case summaries paginate, reopen and retain versions without mutation", async t => {
  const { call } = await setup(t);
  assert.deepEqual((await call("/api/reconciliation-cases?offset=0")).value, { cases: [], nextOffset: null });
  let created;
  for (let i = 0; i < 51; i++) created = await call("/api/reconciliation-cases", { method: "POST", body: { name: `Case ${i} — حالة` } });
  const first = await call("/api/reconciliation-cases?offset=0");
  const second = await call(`/api/reconciliation-cases?offset=${first.value.nextOffset}`);
  assert.equal(first.value.cases.length, 50); assert.equal(second.value.cases.length, 1);
  assert.equal(second.value.nextOffset, null);
  assert.equal(new Set([...first.value.cases, ...second.value.cases].map(c => c.id)).size, 51);
  assert.ok(first.value.cases.every(c => c.documentCount === 0 && c.runCount === 0 && c.createdAt && !('documents' in c)));
  const reopened = await call(`/api/reconciliation-cases/${created.value.id}`);
  assert.equal(reopened.value.name, created.value.name); assert.equal(reopened.etag, created.etag);
  assert.equal((await call("/api/reconciliation-cases?offset=-1")).status, 400);
  assert.equal((await call("/api/reconciliation-cases?offset=10001")).status, 400);
  assert.equal((await call("/api/reconciliation-cases", { role: "unknown" })).status, 403);
  const doc = (await call("/api/documents")).value[0];
  assert.equal((await call(`/api/documents/${doc.id}`)).value.originalFilename, doc.originalFilename);
});
test("review corrections persist across requests, reject stale writes, complete and audit", async t => {
  const { call } = await setup(t);
  const docs = (await call("/api/documents")).value, doc = docs[0];
  let result = await call(`/api/documents/${doc.id}/reviews`, { method: "POST" });
  const path = `/api/documents/${doc.id}/reviews/${result.value.session.id}`;
  const originalEtag = result.etag;
  const field = result.value.fields.find(f => f.fieldKey === "total");
  result = await call(path+"/actions", { method: "POST", etag: result.etag, body: { action: "Corrected", machineFieldValueId: field.machine.id, valueType: "Decimal", value: "1200.00", reason: "Preview correction" } });
  assert.equal(result.status, 200);
  assert.equal((await call(path+"/complete", { method: "POST", etag: originalEtag })).status, 412);
  assert.equal((await call(path+"/complete", { method: "POST", etag: result.etag })).status, 409);
  const reloaded = (await call(`/api/documents/${doc.id}/review`)).value.fields.find(f => f.fieldKey === "total");
  assert.equal(reloaded.effective.value, "1200.00"); assert.equal(reloaded.machine.value, "1155.00");
  for (const f of result.value.fields.filter(f => f.decisionStatus === "Unreviewed")) {
    result = await call(path+"/actions", { method: "POST", etag: result.etag, body: { action: "Confirmed", machineFieldValueId: f.machine.id } });
  }
  result = await call(path+"/complete", { method: "POST", etag: result.etag });
  assert.equal(result.value.session.status, "Completed");
  assert.equal((await call(path+"/complete", { method: "POST", etag: result.etag })).status, 409);
  assert.ok((await call("/api/organization/audit", { role: "admin" })).value.some(e => e.action === "Preview.Review.Corrected"));
});
test("preview role boundaries, origin checks, reset, empty and failure scenarios", async t => {
  const { call } = await setup(t);
  assert.equal((await call("/api/session", { role: "revoked" })).status, 403);
  assert.equal((await call("/api/organization/audit", { role: "staff-two" })).status, 403);
  assert.equal((await call("/api/documents", { role: "unknown" })).status, 403);
  const doc = (await call("/api/documents")).value[0];
  assert.equal((await call(`/api/documents/${doc.id}/reviews`, { method: "POST", role: "revoked" })).status, 403);
  assert.equal((await call("/__preview/reset", { method: "POST", headers: { Origin: "https://example.com" } })).status, 403);
  await call("/__preview/scenario", { method: "POST", body: { scenario: "empty" } });
  assert.deepEqual((await call("/api/documents")).value, []);
  await call("/__preview/scenario", { method: "POST", body: { scenario: "unavailable" } });
  assert.equal((await call("/api/documents")).status, 503);
  await call("/__preview/reset", { method: "POST" });
  assert.equal((await call("/api/documents")).value.length, 5);
  assert.equal((await call("/api/unsupported", { method: "POST" })).status, 501);
});
test("comparison, decisions, questions and evidence use selected sample documents", async t => {
  const { call } = await setup(t);
  const docs = (await call("/api/documents")).value;
  let result = await call("/api/reconciliation-cases", { method: "POST", body: { name: "Preview comparison" } });
  const path = `/api/reconciliation-cases/${result.value.id}`;
  for (const [role, doc] of [["Quotation", docs[1]], ["PurchaseOrder", docs[2]], ["Invoice", docs[3]]]) {
    const review = (await call(`/api/documents/${doc.id}/review`)).value;
    result = await call(`${path}/documents/${role}`, { method: "PUT", etag: result.etag, body: { documentId: doc.id, reviewSessionId: review.session.id } });
    assert.equal(result.status, 200);
  }
  result = await call(path+"/evaluations", { method: "POST", etag: result.etag, body: { documentTotalTolerance: .01 } });
  const run = result.value.runs[0], finding = run.findings[0];
  assert.equal(finding.difference, 105);
  result = await call(`${path}/runs/${run.id}/findings/${finding.id}/resolutions`, { method: "POST", etag: result.etag, body: { action: "Accepted", reason: "Test difference" } });
  assert.equal(result.value.runs[0].findings[0].resolutions[0].reason, "Test difference");
  const prepared = (await call(`/api/cited-cases/${result.value.id}/sources`, { method: "POST" })).value;
  assert.equal(prepared.id, result.value.id); assert.equal(prepared.version, result.value.concurrencyToken);
  const sources = prepared.sources;
  const answer = await call("/api/cited-questions/reviewed", { method: "POST", body: { requestKey: "total-question", question: "What is the total?", indexIds: [sources[2].indexId] } });
  assert.equal(answer.value.reviewedAnswer.status, "Answered");
  const hit = answer.value.retrieval.hits[0];
  const evidence = await call(`/api/cited-questions/${answer.value.retrieval.id}/hits/${hit.hitId}/evidence`);
  assert.equal(evidence.value.page.quote, "Total: 1155.00 AED");
  assert.equal((await call(`/api/cited-questions/${answer.value.retrieval.id}`)).value.retrieval.replayed, true);
  const refusal = await call("/api/cited-questions/reviewed", { method: "POST", body: { requestKey: "unsupported-question", question: "Unknown fact?", indexIds: [sources[0].indexId] } });
  assert.equal(refusal.value.reviewedAnswer.status, "InsufficientEvidence");
});
test("membership changes and retention are simulated and recorded", async t => {
  const { call } = await setup(t);
  const members = (await call("/api/organization/memberships", { role: "admin" })).value;
  const member = members.find(m => m.key === "staff-three");
  const options = { method: "PUT", role: "admin", body: { expectedVersion: member.version, role: "Admin", isActive: false } };
  assert.equal((await call(`/api/organization/memberships/${member.id}`, options)).status, 200);
  assert.equal((await call(`/api/organization/memberships/${member.id}`, options)).status, 409);
  assert.equal((await call("/api/session", { role: "staff-three" })).status, 403);
  const hold = await call("/api/organization/retention/holds", { method: "POST", role: "admin", body: { documentId: null, reason: "AdministrativeReview" } });
  assert.ok((await call("/api/organization/retention/preview", { role: "admin" })).value.documents.every(d => d.disposition === "Held"));
  assert.equal((await call(`/api/organization/retention/holds/${hold.value.id}/release`, { method: "POST", role: "admin", body: { expectedVersion: hold.value.version } })).status, 200);
});

async function preparedSources(call) {
  const sources = (await call("/api/cited-sources")).value.sources;
  const indexes = [];
  for (const source of sources.slice(0, 2)) {
    indexes.push((await call("/api/cited-indexes", { method: "POST", body: source })).value.id);
  }
  return indexes;
}

for (const endpoint of ["/api/cited-questions", "/api/cited-questions/reviewed"]) {
  test(`${endpoint} keeps saved results and evidence private to the creating actor`, async t => {
    const { call } = await setup(t);
    const indexIds = await preparedSources(call);
    const body = { question: "What is the total?", requestKey: "private-question", indexIds };
    const created = await call(endpoint, { method: "POST", body });
    assert.equal(created.status, 200);
    assert.equal("actorId" in created.value, false);
    assert.equal("fingerprint" in created.value, false);
    const resultPath = `/api/cited-questions/${created.value.retrieval.id}`;
    const hit = created.value.retrieval.hits[0];
    const evidencePath = `${resultPath}/hits/${hit.hitId}/evidence?spanId=${hit.spans[0].id}`;
    assert.equal((await call(resultPath)).status, 200);
    assert.equal((await call(evidencePath)).status, 200);
    const missing = await call("/api/cited-questions/00000000-0000-4000-8000-999999999999", { role: "staff-two" });
    for (const role of ["staff-three", "staff-two", "admin"]) {
      const denied = await call(resultPath, { role });
      assert.equal(denied.status, 404);
      assert.deepEqual(denied.value, missing.value);
      assert.equal((await call(evidencePath, { role })).status, 404);
    }
    const members = (await call("/api/organization/memberships", { role: "admin" })).value;
    const member = members.find(item => item.key === "operator");
    await call(`/api/organization/memberships/${member.id}`, { method: "PUT", role: "admin",
      body: { expectedVersion: member.version, role: "Admin", isActive: true } });
    assert.equal((await call("/api/session")).value.actorId, member.actorId);
    assert.equal((await call("/api/session")).value.role, "Admin");
    assert.equal((await call(resultPath)).status, 200);
    assert.equal((await call(evidencePath)).status, 200);
    const replay = await call(endpoint, { method: "POST", body });
    assert.equal(replay.value.retrieval.id, created.value.retrieval.id);
    assert.equal(replay.value.retrieval.replayed, true);
  });
}

test("named question history and exact review links stay scoped and read only", async t => {
  const { call } = await setup(t);
  const source = (await call("/api/cited-sources")).value.sources[0];
  const exact = `/api/cited-sources/${source.documentId}/reviews/${source.reviewSessionId}`;
  assert.deepEqual((await call(exact.toUpperCase().replace('/API/CITED-SOURCES/', '/api/cited-sources/').replace('/REVIEWS/', '/reviews/'))).value, source);
  assert.equal((await call(`/api/cited-sources/${source.documentId}/reviews/00000000-0000-4000-8000-999999999999`)).status, 404);
  assert.deepEqual((await call("/api/cited-questions")).value.questions, []);
  const indexIds = await preparedSources(call);
  const created = await call("/api/cited-questions/reviewed", { method: "POST", body: { question: "What is the total?", requestKey: "history", indexIds } });
  const history = (await call("/api/cited-questions")).value.questions;
  assert.equal(history.length, 1); assert.equal(history[0].id, created.value.retrieval.id);
  assert.equal(history[0].question, "What is the total?"); assert.equal(history[0].mode, "reviewed");
  assert.equal(history[0].sourceCount, indexIds.length); assert.equal(typeof history[0].createdAt, "string");
  assert.deepEqual((await call("/api/cited-questions", { role: "staff-two" })).value.questions, []);
  assert.equal((await call("/api/cited-questions", { role: "unknown" })).status, 403);
  assert.equal((await call(`/api/cited-questions/${history[0].id}`)).value.retrieval.replayed, true);
});

test("question retries are scoped by actor and reject a reused key with different inputs", async t => {
  const { call } = await setup(t);
  const indexIds = await preparedSources(call);
  const body = { question: "What is the total?", requestKey: "shared-key", indexIds, topK: 5 };
  const created = await call("/api/cited-questions", { method: "POST", body });
  const replay = await call("/api/cited-questions", { method: "POST", body: { ...body, indexIds: [...indexIds].reverse() } });
  assert.equal(replay.value.retrieval.id, created.value.retrieval.id);
  assert.equal(replay.value.retrieval.replayed, true);
  for (const changed of [{ question: "Different amount?" }, { indexIds: indexIds.slice(0, 1) }, { topK: 2 }]) {
    const rejected = await call("/api/cited-questions", { method: "POST", body: { ...body, ...changed } });
    assert.equal(rejected.status, 409);
    assert.equal("retrieval" in rejected.value, false);
  }
  assert.equal((await call("/api/cited-questions/reviewed", { method: "POST", body })).status, 409);
  const other = await call("/api/cited-questions", { method: "POST", role: "staff-two", body: { ...body, question: "What amount is recorded?" } });
  assert.equal(other.status, 200);
  assert.notEqual(other.value.retrieval.id, created.value.retrieval.id);
  assert.equal(other.value.retrieval.replayed, false);
  const otherReplay = await call("/api/cited-questions", { method: "POST", role: "staff-two", body: { ...body, question: "What amount is recorded?" } });
  assert.equal(otherReplay.value.retrieval.id, other.value.retrieval.id);
  assert.equal(otherReplay.value.retrieval.replayed, true);
  assert.equal((await call(`/api/cited-questions/${other.value.retrieval.id}`)).status, 404);
  assert.equal((await call("/api/cited-questions", { method: "POST", body: { ...body, requestKey: "" } })).status, 400);
});
