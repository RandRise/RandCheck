import base from "../phase6-browser-fixture/vite.config.mjs";
import { randomUUID } from "node:crypto";
import { fileURLToPath } from "node:url";
const root = fileURLToPath(new URL(".", import.meta.url));
const organizationId = "11111111-1111-1111-1111-111111111111", actorId = "22222222-2222-2222-2222-222222222222";
const heldId = "33333333-3333-3333-3333-333333333333", eligibleId = "44444444-4444-4444-4444-444444444444", pendingId = "55555555-5555-5555-5555-555555555555";
const policy = { version: randomUUID(), documentDays: 365, quarantineHours: null };
const versions = new Map([heldId, eligibleId, pendingId].map(id => [id, randomUUID()]));
const holds = [{ id: randomUUID(), documentId: heldId, reason: "Investigation", createdAtUtc: "2026-09-11T09:00:00Z", releasedAtUtc: null, version: randomUUID() }];
const records = [{ documentId: pendingId, documentVersion: versions.get(pendingId), policyVersion: policy.version, state: "PendingFileRemoval", requestedAtUtc: "2026-09-13T08:00:00Z", completedAtUtc: null }];
const events = [];
function audit(action, documentId) { events.unshift({ id: randomUUID(), organizationId, actorId, actorKind: "Human", action, targetType: "document", targetId: documentId, outcome: "Succeeded", reasonCode: "committed", requestId: randomUUID(), beforeState: null, afterState: null, recordedAtUtc: new Date().toISOString() }); }
audit("document.database-content-removed", pendingId);
export default { ...base, root, define: { "process.env.NEXT_PUBLIC_API_URL": JSON.stringify("http://127.0.0.1:3020") },
  server: { ...base.server, port: 3020, fs: { allow: [...base.server.fs.allow, root] } },
  plugins: [{ name: "fictional-deletion", configureServer(server) { server.middlewares.use(async (req, res, next) => {
    if (!req.url?.startsWith("/api/")) return next();
    const path = new URL(req.url, "http://localhost").pathname;
    const reply = (value, status = 200) => { res.writeHead(status, { "Content-Type": "application/json", "Cache-Control": "no-store" }); res.end(JSON.stringify(value)); };
    if (req.headers.authorization !== "Bearer fictional-retention-admin") return reply({}, 403);
    if (path === "/api/session") return reply({ organizationId, organizationName: "Al Noor Trading · Demo", actorId, displayName: "Mariam · Fictional administrator", tenantId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", role: "None", canManageMembers: true });
    const prefix = "/api/organization/retention";
    if (path === prefix && req.method === "GET") return reply(policy);
    if (path === `${prefix}/holds` && req.method === "GET") return reply(holds);
    if (path === `${prefix}/preview`) return reply({ documentDeletionEnabled: true, nextOffset: null, documents: [heldId, eligibleId].filter(id => !records.some(r => r.documentId === id)).map(id => ({ documentId: id, createdAtUtc: "2025-06-12T10:00:00Z", disposition: id === heldId ? "Held" : "PastRetentionWindow" })) });
    if (path === `${prefix}/deletions`) return reply(records);
    if (path === "/api/organization/audit") return reply(events);
    const match = path.match(/\/documents\/([a-f0-9-]+)\/deletion$/);
    if (match) {
      const id = match[1]; if (!versions.has(id)) return reply({}, 404);
      if (req.method === "GET") return reply({ documentId: id, documentVersion: versions.get(id), policyVersion: policy.version, enabled: true, blockers: id === heldId ? ["retention_hold_active", "historical_dependencies"] : [], extractionRuns: 1, extractionJobs: 1 });
      let raw = ""; for await (const part of req) raw += part; const body = JSON.parse(raw);
      if (id === heldId || body.expectedDocumentVersion !== versions.get(id) || body.expectedPolicyVersion !== policy.version) return reply({}, 409);
      let record = records.find(r => r.documentId === id);
      if (!record) { record = { documentId: id, documentVersion: versions.get(id), policyVersion: policy.version, state: "PendingFileRemoval", requestedAtUtc: new Date().toISOString(), completedAtUtc: null }; records.unshift(record); audit("document.database-content-removed", id); }
      if (record.state !== "Completed") { audit("document.source-purge-authorized", id); record.state = "Completed"; record.completedAtUtc = new Date().toISOString(); audit("document.deletion-completed", id); }
      return reply(record);
    }
    return reply({}, 409); // Other changes are outside this focused visual fixture.
  }); } }] };
