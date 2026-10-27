import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const root = dirname(fileURLToPath(import.meta.url));
const web = fileURLToPath(new URL("../../apps/web/", import.meta.url));
const requireWeb = createRequire(join(web, "package.json"));

export default {
  root,
  resolve: { alias: [
    { find: /^react$/, replacement: requireWeb.resolve("react") },
    { find: /^react\/jsx-runtime$/, replacement: requireWeb.resolve("react/jsx-runtime") },
    { find: /^react\/jsx-dev-runtime$/, replacement: requireWeb.resolve("react/jsx-dev-runtime") },
    { find: /^@testing-library\/react$/, replacement: requireWeb.resolve("@testing-library/react") },
    { find: /^vitest$/, replacement: join(dirname(requireWeb.resolve("vitest/package.json")), "dist/index.js") },
  ] },
  test: {
    environment: "jsdom",
    include: ["design-proposal.test.tsx"],
    maxWorkers: 1,
    fileParallelism: false,
  },
};
