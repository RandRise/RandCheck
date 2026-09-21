// Isolated synthetic HTTP/UI fixture. Uses no database, directory identity or provider.
import { randomUUID } from "node:crypto";
import { fileURLToPath } from "node:url";
import base from "../phase6-browser-fixture/vite.config.mjs";
const root = fileURLToPath(new URL(".", import.meta.url));
const id = "12345678-1234-1234-1234-123456789abc", actorId = "22222222-2222-2222-2222-222222222222";
const organizationId = "11111111-1111-1111-1111-111111111111";
const document = { id, originalFilename: "Fictional sample invoice.txt", sizeBytes: 92, status: "Ready", documentType: "Invoice",
  language: "English", createdAt: "2026-09-12T04:00:00Z", extractor: "fixture-only", processingMs: 0, fields: [] };
const events = [], grants = new Map(); let outcome = "clean";
function audit(action, reasonCode, result = "Succeeded") { events.unshift({ id: randomUUID(), organizationId, actorId,
  actorKind: "Human", action, targetType: "document", targetId: id, outcome: result, reasonCode, requestId: randomUUID(),
  beforeState: null, afterState: null, recordedAtUtc: new Date().toISOString() }); }
export default { ...base, root, define: { "process.env.NEXT_PUBLIC_API_URL": JSON.stringify("http://127.0.0.1:3018") },
  server: { ...base.server, port: 3018, fs: { allow: [...base.server.fs.allow, root] } },
  plugins: [{ name: "fictional-source-access", configureServer(server) {
    server.middlewares.use(async (req, res, next) => {
      if (!req.url?.startsWith("/api/")) return next();
      const reply = (body, status = 200) => { res.writeHead(status, { "Content-Type": "application/json", "Cache-Control": "no-store" }); res.end(JSON.stringify(body)); };
      if (req.headers.authorization !== "Bearer fictional-source-user") return reply({}, 403);
      if (req.url === "/api/session") return reply({ organizationId, organizationName: "Al Noor Trading · Demo", actorId,
        displayName: "Leila Farah · Fictional", tenantId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", role: "Viewer", canManageMembers: true });
      if (req.url === "/api/documents") return reply([document]);
      if (req.url === `/api/documents/${id}/review`) return reply({ documentId: id, fields: [] });
      if (req.url.startsWith("/api/organization/audit")) return reply(events);
      if (req.url === "/api/fixture/scan-mode" && req.method === "POST") {
        let body = ""; for await (const part of req) body += part;
        outcome = JSON.parse(body).outcome === "unavailable" ? "unavailable" : "clean"; return reply({ outcome });
      }
      if (req.url === `/api/documents/${id}/source-access` && req.method === "POST") {
        const token = randomUUID(), expiry = Date.now() + 300000; grants.set(token, expiry);
        audit("document.source-access-issued", "five-minute-grant");
        return reply({ url: `/api/documents/${id}/source`, accessToken: token, expiresAtUtc: new Date(expiry).toISOString() });
      }
      if (req.url === `/api/documents/${id}/source` && req.method === "GET") {
        if ((grants.get(req.headers["x-document-access"]) ?? 0) <= Date.now()) return reply({}, 404);
        if (outcome === "unavailable") { audit("document.source-access-denied", "malware_scan_unavailable", "Denied"); return reply({}, 503); }
        audit("document.source-download-authorized", "scan-clean");
        res.writeHead(200, { "Content-Type": "application/octet-stream", "Cache-Control": "no-store",
          "Content-Disposition": `attachment; filename=source-${id.replaceAll("-", "")}.txt`, "X-Content-Type-Options": "nosniff" });
        return res.end("FICTIONAL TEST DOCUMENT\nPreview data only. No real invoice or customer information.\n");
      }
      return reply({}, 404);
    });
  } }] };
