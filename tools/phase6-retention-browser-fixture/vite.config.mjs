import base from "../phase6-browser-fixture/vite.config.mjs";
import { randomUUID } from "node:crypto";
import { fileURLToPath } from "node:url";
const root = fileURLToPath(new URL(".", import.meta.url));
const organizationId = "11111111-1111-1111-1111-111111111111", actorId = "22222222-2222-2222-2222-222222222222";
const documentId = "33333333-3333-3333-3333-333333333333";
let policy = { version: randomUUID(), documentDays: 365, quarantineHours: 24 };
let remaining = 3;
const holds = [{ id: randomUUID(), documentId, reason: "Investigation", createdAtUtc: "2026-09-11T09:00:00Z", releasedAtUtc: null, version: randomUUID() }];
const events = [];
function audit(action, targetId, afterState = null, targetType = "retention-hold") {
  events.unshift({ id: randomUUID(), organizationId, actorId, actorKind: "Human", action, targetType, targetId, outcome: "Succeeded",
    reasonCode: "committed", requestId: randomUUID(), beforeState: null, afterState, recordedAtUtc: new Date().toISOString() });
}
audit("retention.hold-placed", holds[0].id, `document=${documentId};reason=Investigation;released=False`);
audit("retention.policy-changed", organizationId, "documentDays=365;quarantineHours=24", "retention-policy");
export default { ...base, root, define: { "process.env.NEXT_PUBLIC_API_URL": JSON.stringify("http://127.0.0.1:3019") },
  server: { ...base.server, port: 3019, fs: { allow: [...base.server.fs.allow, root] } },
  plugins: [{ name: "fictional-retention", configureServer(server) {
    server.middlewares.use(async (req, res, next) => {
      if (!req.url?.startsWith("/api/")) return next();
      const path = new URL(req.url, "http://localhost").pathname;
      const reply = (value, status = 200) => { res.writeHead(status, { "Content-Type": "application/json", "Cache-Control": "no-store" }); res.end(JSON.stringify(value)); };
      if (req.headers.authorization !== "Bearer fictional-retention-admin") return reply({}, 403);
      if (path === "/api/session") return reply({ organizationId, organizationName: "Al Noor Trading · Demo", actorId, displayName: "Mariam · Fictional administrator",
        tenantId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", role: "None", canManageMembers: true });
      let data = {}; if (req.method !== "GET") { let raw = ""; for await (const part of req) raw += part; data = raw ? JSON.parse(raw) : {}; }
      const prefix = "/api/organization/retention";
      if (path === "/api/organization/audit") return reply(events);
      if (path === prefix && req.method === "GET") return reply(policy);
      if (path === prefix && req.method === "PUT") {
        if (data.expectedVersion !== policy.version) return reply({}, 409);
        policy = { version: randomUUID(), documentDays: data.documentDays, quarantineHours: data.quarantineHours };
        audit("retention.policy-changed", organizationId, `documentDays=${policy.documentDays ?? "indefinite"};quarantineHours=${policy.quarantineHours ?? "disabled"}`, "retention-policy"); return reply(policy);
      }
      if (path === `${prefix}/preview`) return reply({ documentDeletionEnabled: false, nextOffset: null, documents: [
        { documentId, createdAtUtc: "2025-08-01T10:00:00Z", disposition: holds.some(h => !h.releasedAtUtc && (!h.documentId || h.documentId === documentId)) ? "Held" : "PastRetentionWindow" },
        { documentId: "44444444-4444-4444-4444-444444444444", createdAtUtc: "2025-06-12T10:00:00Z", disposition: holds.some(h => !h.releasedAtUtc && !h.documentId) ? "Held" : "PastRetentionWindow" },
        { documentId: "55555555-5555-5555-5555-555555555555", createdAtUtc: "2026-09-01T10:00:00Z", disposition: holds.some(h => !h.releasedAtUtc && !h.documentId) ? "Held" : "Retained" }] });
      if (path === `${prefix}/holds` && req.method === "GET") return reply(holds);
      if (path === `${prefix}/holds` && req.method === "POST") {
        const h = { id: randomUUID(), documentId: data.documentId, reason: data.reason, createdAtUtc: new Date().toISOString(), releasedAtUtc: null, version: randomUUID() };
        holds.push(h); audit("retention.hold-placed", h.id, `document=${h.documentId ?? "organization"};reason=${h.reason};released=False`); return reply(h, 201);
      }
      if (path.endsWith("/release")) {
        const h = holds.find(h => path.includes(h.id)); if (!h) return reply({}, 404); if (h.version !== data.expectedVersion || h.releasedAtUtc) return reply({}, 409);
        h.releasedAtUtc = new Date().toISOString(); h.version = randomUUID(); audit("retention.hold-released", h.id, `released=True`); return reply(h);
      }
      if (path === "/api/organization/quarantine/cleanup") {
        if (data.expectedPolicyVersion !== policy.version || policy.quarantineHours == null) return reply({}, 409);
        const eligible = holds.some(h => !h.releasedAtUtc && !h.documentId) ? 0 : remaining;
        if (data.execute && eligible) { for (let i = 0; i < eligible; i++) { const upload = randomUUID(); audit("quarantine.cleanup-authorized", upload, null, "quarantine-upload"); audit("quarantine.cleaned", upload, null, "quarantine-upload"); } remaining = 0; }
        return reply({ executed: data.execute, eligible, deleted: data.execute ? eligible : 0, preserved: eligible ? 2 : 2 + remaining, truncated: false });
      }
      return reply({}, 404);
    });
  } }] };
