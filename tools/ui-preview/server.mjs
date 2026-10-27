import { createRequire } from "node:module";
import { fileURLToPath, pathToFileURL } from "node:url";
import { dirname, join, resolve } from "node:path";
import { randomBytes, createHash } from "node:crypto";
import { mkdirSync, writeFileSync, readFileSync, unlinkSync } from "node:fs";
import { createFixtureApi } from "./fixtures.mjs";

const root = fileURLToPath(new URL("../../", import.meta.url));
const previewRoot = fileURLToPath(new URL("./", import.meta.url));
const web = join(root, "apps/web");
const output = join(root, "outputs/ui-preview");
const metadata = join(output, "runtime.json");
const origin = "http://127.0.0.1:3410";
const workspace = createHash("sha256").update(resolve(root).toLowerCase()).digest("hex");
const controlToken = randomBytes(32).toString("hex");
const started = performance.now();
// Vite is already a declared docs dependency. No extra install or downloaded tool.
const requireDocs = createRequire(join(root, "docs/package.json"));
const viteRoot = dirname(requireDocs.resolve("vite/package.json"));
const { createServer } = await import(pathToFileURL(join(viteRoot, "dist/node/index.js")).href);
let server, stopping = false, ready = false, startupMs = 0;
async function stop() {
  if (stopping) return;
  stopping = true;
  await server?.close();
  try { if (JSON.parse(readFileSync(metadata, "utf8")).controlToken === controlToken) unlinkSync(metadata); } catch {}
  process.exit(0);
}
const fixture = createFixtureApi(origin);
server = await createServer({
  configFile: false, envFile: false, root: previewRoot,
  cacheDir: join(output, "vite-cache"), publicDir: false,
  esbuild: { jsx: "automatic" },
  resolve: { alias: [
    { find: /^next\/(link|navigation)$/, replacement: join(previewRoot, "navigation.tsx") },
    { find: /^react$/, replacement: join(web, "node_modules/react/index.js") },
    { find: /^react\/jsx-runtime$/, replacement: join(web, "node_modules/react/jsx-runtime.js") },
    { find: /^react\/jsx-dev-runtime$/, replacement: join(web, "node_modules/react/jsx-dev-runtime.js") },
    { find: /^react-dom\/client$/, replacement: join(web, "node_modules/react-dom/client.js") },
    { find: /^@azure\/msal-browser$/, replacement: join(web, "node_modules/@azure/msal-browser/dist/index.mjs") }
  ] },
  define: { "process.env.NEXT_PUBLIC_API_URL": JSON.stringify(origin) },
  server: { host: "127.0.0.1", port: 3410, strictPort: true, cors: false,
    fs: { allow: [previewRoot, join(web, "app"), join(web, "node_modules"), viteRoot] },
    watch: { ignored: ["**/outputs/**", "**/.next*/**", "**/node_modules/**"] } },
  plugins: [{ name: "local-ui-preview", configureServer(vite) {
    vite.middlewares.use((req, res, next) => {
      if (req.headers.host !== "127.0.0.1:3410") { res.writeHead(403).end("Preview host required"); return; }
      if (req.url === "/__preview/health" && req.method === "GET") {
        res.writeHead(ready ? 200 : 503, { "Content-Type": "application/json", "Cache-Control": "no-store" });
        res.end(JSON.stringify({ kind: "uaedi-ui-preview", workspace, pid: process.pid, startupMs, memoryMb: Math.round(process.memoryUsage().rss/1048576) })); return;
      }
      if (req.url === "/__preview/stop" && req.method === "POST") {
        if (req.headers.authorization !== `Bearer ${controlToken}`) { res.writeHead(403).end(); return; }
        res.writeHead(200).end("Preview stopped"); setTimeout(() => void stop(), 50); return;
      }
      void fixture(req, res, next).catch(error => {
        console.error("Preview handler failed:", error.message);
        if (!res.headersSent) res.writeHead(500, { "Content-Type": "application/json" });
        res.end(JSON.stringify({ detail: "Preview handler failed; inspect the preview log." }));
      });
    });
  }, handleHotUpdate(context) {
    // CSS uses Vite HMR. TSX uses a full browser reload without another server build.
    if (/\.[cm]?[jt]sx?$/.test(context.file)) {
      context.server.ws.send({ type: "full-reload" }); return [];
    }
  } }]
});
try {
  await server.listen();
  await server.transformRequest("/main.tsx");
  await server.waitForRequestsIdle();
}
catch (error) { console.error(`Cannot start preview: ${error.message}. Port 3410 must be free.`); await server.close(); process.exit(1); }
mkdirSync(output, { recursive: true });
writeFileSync(metadata, JSON.stringify({ workspace, controlToken, pid: process.pid, origin }));
startupMs = Math.round(performance.now()-started); ready = true;
console.log(`UI preview ready at ${origin} in ${startupMs} ms. Synthetic state resets on stop.`);
process.on("SIGINT", stop); process.on("SIGTERM", stop);
