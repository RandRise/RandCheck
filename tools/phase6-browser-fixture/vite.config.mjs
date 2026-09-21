// Local in-memory HTTP fixture for browser QA. No retained database, identity provider or paid service.
import { randomUUID } from "node:crypto";
import { fileURLToPath } from "node:url";
const root = fileURLToPath(new URL(".", import.meta.url));
const web = fileURLToPath(new URL("../../apps/web/", import.meta.url));
const tenant = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
const organizationId = "11111111-1111-1111-1111-111111111111";
const member = (displayName, role, manage, active = true) => ({ id: randomUUID(), actorId: randomUUID(), tenantId: tenant,
  objectId: randomUUID(), displayName, role, canManageMembers: manage, isActive: active, version: randomUUID() });
const members = [member("Mariam Al Mansoori", "None", true), member("Omar Hassan", "Reviewer", false), member("Leila Farah", "Viewer", false), member("Daniel Clarke", "Operator", false), member("Nadia Saleh", "Viewer", false, false)];
const audit = (action, target, beforeState, afterState, outcome = "Succeeded") => ({ id: randomUUID(), organizationId,
  actorId: members[0].actorId, actorKind: "Human", action, targetType: "OrganizationMembership", targetId: target,
  outcome, reasonCode: "committed", requestId: randomUUID(), beforeState, afterState, recordedAtUtc: new Date().toISOString() });
const events = [audit("Membership.Changed", members[4].id, "role:Viewer;admin:False;active:True", "role:Viewer;admin:False;active:False"),
  audit("Membership.Added", members[3].id, null, "role:Operator;admin:False;active:True"),
  audit("Membership.Changed", members[1].id, "role:Viewer;admin:False;active:True", "role:Reviewer;admin:False;active:True"),
  audit("Membership.Added", members[2].id, null, "role:Viewer;admin:False;active:True")];
const state = m => `role:${m.role};admin:${m.canManageMembers};active:${m.isActive}`;
export default {
  root, esbuild: { jsx: "automatic" },
  resolve: { alias: [ { find: /^next\/(link|navigation)$/, replacement: root + "navigation.tsx" },
    { find: /^react$/, replacement: web + "node_modules/react/index.js" },
    { find: /^react\/jsx-runtime$/, replacement: web + "node_modules/react/jsx-runtime.js" },
    { find: /^react\/jsx-dev-runtime$/, replacement: web + "node_modules/react/jsx-dev-runtime.js" },
    { find: /^react-dom\/client$/, replacement: web + "node_modules/react-dom/client.js" },
    { find: /^@azure\/msal-browser$/, replacement: web + "node_modules/@azure/msal-browser/dist/index.mjs" } ] },
  define: { "process.env.NEXT_PUBLIC_API_URL": JSON.stringify("http://127.0.0.1:3017") },
  server: { host: "127.0.0.1", port: 3017, strictPort: true, fs: { allow: [root, web] } },
  plugins: [{ name: "fictional-security-api", configureServer(server) {
    server.middlewares.use(async (req, res, next) => {
      if (!req.url?.startsWith("/api/")) return next();
      const reply = (value, status = 200) => { res.writeHead(status, { "Content-Type": "application/json", "Cache-Control": "no-store" }); res.end(JSON.stringify(value)); };
      const caller = req.headers.authorization === "Bearer fictional-admin" ? members[0] : req.headers.authorization === "Bearer fictional-viewer" ? members[2] : null;
      if (!caller?.isActive) return reply({}, 403);
      if (req.url === "/api/session") return reply({ organizationId, organizationName: "Al Noor Trading · Demo", actorId: caller.actorId, displayName: caller.displayName, tenantId: tenant, role: caller.role, canManageMembers: caller.canManageMembers });
      if (req.url === "/api/documents") return reply([]);
      if (!caller.canManageMembers) return reply({}, 403);
      if (req.url.startsWith("/api/organization/audit")) { const offset = Number(new URL(req.url, "http://localhost").searchParams.get("offset") ?? 0); return reply(events.slice(offset, offset + 100)); }
      if (req.method === "GET" && req.url === "/api/organization/memberships") return reply(members);
      let raw = ""; for await (const chunk of req) raw += chunk;
      let body; try { body = JSON.parse(raw); } catch { return reply({}, 400); }
      if (req.method === "POST" && req.url === "/api/organization/memberships") {
        if (members.some(m => m.objectId === body.objectId)) return reply({ code: "membership_already_exists" }, 409);
        const added = { ...member(body.displayName, body.role, body.canManageMembers), objectId: body.objectId };
        members.push(added); events.unshift(audit("Membership.Added", added.id, null, state(added))); return reply(added, 201);
      }
      const current = members.find(m => req.url === `/api/organization/memberships/${m.id}`);
      if (req.method === "PUT" && current) {
        if (current.version !== body.expectedVersion) return reply({ code: "membership_version_conflict" }, 409);
        if (current.canManageMembers && (!body.canManageMembers || !body.isActive) && members.filter(m => m.canManageMembers && m.isActive).length <= 1) return reply({ code: "last_administrator_required" }, 409);
        const before = state(current); Object.assign(current, { role: body.role, canManageMembers: body.canManageMembers, isActive: body.isActive, version: randomUUID() });
        events.unshift(audit("Membership.Changed", current.id, before, state(current))); return reply(current);
      }
      return reply({}, 404);
    });
  } }]
};
