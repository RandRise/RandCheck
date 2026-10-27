/** Loopback-only frontend for the explicit real-API browser acceptance probe.
 * No API mocks, proxy or response rewriting. Private offline token expires with
 * the probe; only source components and temporary fixture configuration are served.
 */
import assert from "node:assert/strict";
import { readFileSync, realpathSync } from "node:fs";
import { createRequire } from "node:module";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const root = fileURLToPath(new URL("../../", import.meta.url));
const fixture = fileURLToPath(new URL("./", import.meta.url));
const web = path.join(root, "apps/web");
const configFile = realpathSync(process.argv[2]);
assert.ok(configFile.startsWith(path.join(realpathSync(root), "outputs") + path.sep));
const config = JSON.parse(readFileSync(configFile, "utf8"));
const api = new URL(config.api);
assert.equal(api.hostname, "127.0.0.1"); assert.equal(api.protocol, "http:");
assert.ok(config.token && config.tenantId && config.clientId && /^[a-f0-9]{32}$/.test(config.instance));
const requireWeb = createRequire(path.join(web, "package.json"));
const viteRoot = path.dirname(requireWeb.resolve("vite/package.json"));
const { createServer } = await import(pathToFileURL(path.join(viteRoot, "dist/node/index.js")).href);
const { token, tenantId, clientId, scope } = config;
const server = await createServer({
  configFile: false, root: fixture, esbuild: { jsx: "automatic" },
  plugins: [{ name: "owned-acceptance-instance", configureServer(server) {
    server.middlewares.use("/__admin_acceptance_owner", (_request, response) => {
      response.setHeader("Cache-Control", "no-store");
      response.end(config.instance);
    });
  } }],
  resolve: { alias: [
    { find: /^next\/(link|navigation)$/, replacement: path.join(fixture, "navigation.tsx") },
    ...["react", "react/jsx-runtime", "react/jsx-dev-runtime", "react-dom/client", "@azure/msal-browser"].map(name => ({ find: new RegExp(`^${name}$`), replacement: requireWeb.resolve(name) })),
  ] },
  define: { "process.env.NEXT_PUBLIC_API_URL": JSON.stringify(config.api), __ADMIN_ACCEPTANCE__: JSON.stringify({ api: config.api, token, tenantId, clientId, scope }) },
  server: { host: "127.0.0.1", port: 3338, strictPort: true, fs: { allow: [fixture, web] } },
});
await server.listen();
console.log("Owned Admin integration frontend listening on loopback 3338.");
for (const signal of ["SIGINT", "SIGTERM"]) process.on(signal, async () => { await server.close(); process.exit(0); });
