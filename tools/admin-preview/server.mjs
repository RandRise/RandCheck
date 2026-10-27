// Isolated, fictional administration preview. No proxy or retained-data connection.
import { createRequire } from "node:module";
import { fileURLToPath, pathToFileURL } from "node:url";
import { dirname, join } from "node:path";
import { mkdirSync, writeFileSync } from "node:fs";
import { randomUUID } from "node:crypto";

const root = fileURLToPath(new URL("../../", import.meta.url));
const preview = fileURLToPath(new URL("./", import.meta.url));
const web = join(root, "apps/web");
const output = join(root, "outputs/admin-people-20261026");
const requireWeb = createRequire(join(web, "package.json"));
const viteRoot = dirname(requireWeb.resolve("vite/package.json"));
const { createServer } = await import(pathToFileURL(join(viteRoot, "dist/node/index.js")).href);
const origin = "http://127.0.0.1:3430";
const tenantId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
const members = [
  ["Maya Hassan", "Admin", true], ["Omar Ali", "Staff", true], ["Lina Salem", "Staff", false],
].map(([displayName, role, isActive], index) => ({ id: `sample-${index}`, actorId: `sample-actor-${index}`,
  tenantId, objectId: `00000000-0000-0000-0000-${String(index + 1).padStart(12, "0")}`,
  displayName, role, isActive, version: randomUUID() }));
const events = [];
function reply(res, status, body) {
  res.writeHead(status, { "Content-Type": "application/json", "Cache-Control": "no-store" });
  res.end(JSON.stringify(body));
}

async function sampleApi(req, res) {
  const path = new URL(req.url, origin).pathname;
  if (req.headers.authorization !== "Bearer fictional-admin-preview") return reply(res, 401, {});
  if (path === "/api/session" && req.method === "GET") return reply(res, 200, {
    organizationId: "sample-company", organizationName: "Example Trading · Sample", actorId: "sample-preview-admin",
    displayName: "Sample administrator", tenantId, role: "Admin",
  });
  if (path === "/api/organization/audit" && req.method === "GET") return reply(res, 200, events);
  const base = "/api/organization/memberships";
  if (path === base && req.method === "GET") return reply(res, 200, members);
  if (path !== base && !path.startsWith(`${base}/`)) return reply(res, 404, { detail: "Not available in this sample." });
  if (!["POST", "PUT"].includes(req.method)) return reply(res, 405, {});
  let text = "";
  for await (const chunk of req) {
    text += chunk;
    if (text.length > 10000) return reply(res, 413, {});
  }
  const body = JSON.parse(text);
  if (!["Staff", "Admin"].includes(body.role)) return reply(res, 400, {});
  let member;
  if (req.method === "POST" && path === base) {
    if (members.some(item => item.objectId === body.objectId)) return reply(res, 409, { code: "membership_already_exists" });
    if (body.tenantId !== tenantId || !body.displayName?.trim() || !/^[0-9a-f-]{36}$/i.test(body.objectId)) return reply(res, 400, {});
    member = { id: randomUUID(), actorId: randomUUID(), ...body, isActive: true, version: randomUUID() };
    members.push(member);
  } else if (req.method === "PUT") {
    member = members.find(item => path === `${base}/${item.id}`);
    if (!member) return reply(res, 404, {});
    if (body.expectedVersion !== member.version) return reply(res, 409, { code: "membership_version_conflict" });
    if (member.role === "Admin" && member.isActive && (body.role !== "Admin" || !body.isActive)
      && members.filter(item => item.role === "Admin" && item.isActive).length === 1) return reply(res, 409, { code: "last_administrator_required" });
    Object.assign(member, { role: body.role, isActive: body.isActive, version: randomUUID() });
  } else return reply(res, 405, {});
  events.unshift({ id: randomUUID(), actorId: "sample-preview-admin", actorKind: "Sample administrator",
    action: "Sample.AccessChanged", targetType: "Sample membership", targetId: member.id, outcome: "Succeeded",
    reasonCode: "Fictional preview only", requestId: randomUUID(), beforeState: null,
    afterState: JSON.stringify(member), recordedAtUtc: new Date().toISOString() });
  return reply(res, 200, member);
}

const server = await createServer({
  configFile: false, envFile: false, root: preview, publicDir: false,
  cacheDir: join(output, "vite-cache"), esbuild: { jsx: "automatic" },
  define: { "process.env.NEXT_PUBLIC_API_URL": JSON.stringify(origin) },
  resolve: { alias: [
    { find: /^next\/(link|navigation)$/, replacement: join(root, "tools/ui-preview/navigation.tsx") },
    ...["react", "react/jsx-runtime", "react/jsx-dev-runtime", "react-dom/client"].map(name => ({ find: new RegExp(`^${name}$`), replacement: requireWeb.resolve(name) })),
    { find: /^@azure\/msal-browser$/, replacement: join(web, "node_modules/@azure/msal-browser/dist/index.mjs") },
  ] },
  server: { host: "127.0.0.1", port: 3430, strictPort: true, cors: false,
    fs: { allow: [preview, join(root, "tools/ui-preview"), join(web, "app"), join(web, "node_modules")] },
    watch: { ignored: ["**/outputs/**", "**/.next*/**", "**/node_modules/**"] } },
  plugins: [{ name: "fictional-admin", configureServer(vite) {
    vite.middlewares.use((req, res, next) => {
      if (req.headers.host !== "127.0.0.1:3430") { res.writeHead(403).end(); return; }
      if (req.headers.origin && req.headers.origin !== origin) { res.writeHead(403).end(); return; }
      if (req.url.startsWith("/api/")) {
        void sampleApi(req, res).catch(() => reply(res, 400, { detail: "Invalid sample request." }));
      } else next();
    });
  } }],
});
await server.listen();
mkdirSync(output, { recursive: true });
writeFileSync(join(output, "preview-runtime.json"), JSON.stringify({ pid: process.pid, startedAt: new Date().toISOString(), origin, kind: "fictional-admin-preview" }, null, 2));
console.log(`Sample Admin view: ${origin}/organization. Fictional in-memory records; restart resets this sample only.`);
process.on("SIGINT", async () => { await server.close(); process.exit(0); });
process.on("SIGTERM", async () => { await server.close(); process.exit(0); });
