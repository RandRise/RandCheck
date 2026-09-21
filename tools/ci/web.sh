#!/bin/sh
set -eu
if [ "${1:-test}" = audit ]; then
  cd /app
  npm audit --audit-level=low --json > /results/audit-web.json
  cd /docs
  npm audit --audit-level=low --json > /results/audit-docs.json
  exit 0
fi
cd /app
npm run lint -- --max-warnings=0
npx --no-install vitest run --maxWorkers=2 --reporter=json --outputFile=/results/web-tests.json
node /checks/verify-vitest.mjs
node --test app/review-request.test.mjs
npm run build
if [ "${CI_EXPORT_BROWSER_REVIEW:-0}" = 1 ]; then
  # Opt-in private scan input; excluded from hosted artifact uploads.
  mkdir -p /results/browser-review/server
  cp .next/BUILD_ID /results/browser-review/
  cp -R .next/static /results/browser-review/
  cp -R .next/server/app /results/browser-review/server/
fi
cd /docs
npm run build
printf '{"lint":true,"browserTests":true,"productionLockedBuild":true,"documentationBuild":true}\n' > /results/web-checks.json
