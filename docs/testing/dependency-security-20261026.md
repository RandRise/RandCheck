# N14: web dependency security update

October 26, 2026. Approved: fix the dependency findings without changing the
invoice workflow. No product redesign, permission change, data migration or
export feature is included.

## Changes

| Dependency | Before | After |
| --- | --- | --- |
| Next.js and matching ESLint config/plugin | 16.3.5 | 16.4.3 |
| Vitest and its internal packages | 4.1.11 | 4.1.12 |
| Vite used by web tests | 8.3.0 | 8.3.5 |
| Sharp | 0.35.4 | 0.35.5 |
| source-map-js | 1.2.1 | 1.2.2 |
| brace-expansion | 1.1.18 / 5.0.9 | 1.1.21 / 5.0.12 |
| Zod | 4.4.3 | 4.6.7 |

The lockfile also updates the matching native binaries and required dependency
trees. React, MSAL and Lucide versions stay unchanged. No application source,
business rules, role permissions or API contracts changed. Installation scripts
were disabled; no forced audit fix or major-version upgrade was used.

The only test-source fix updates the retention fixture from the retired
`canManageMembers` flag to the existing Admin/Staff role contract. Its five
administration tests had rendered nothing because the mock omitted `role`.
The captured pre-update application already required `session.role === "Admin"`.
Assertions for confirmation, version checks, errors and Staff denial remain.
No retention operation was performed against real data.

Next.js regenerated the heading inside `apps/web/AGENTS.md` from H1 to H2. The
instruction text and bundled-documentation path remain unchanged. Root AGENTS.md
is untouched. Isolated-build additions to tsconfig and next-env were checked and
restored to their captured baseline; earlier uncommitted settings were preserved.

## Verification

Evidence folder: `outputs/dependency-security-20261026/`.

- Fresh `npm audit` before: 8 findings (2 moderate, 5 high, 1 critical).
  After installing: **0 known findings**, including development dependencies.
  `npm ls --depth=0` passes. These results cover `apps/web`, not a new audit of
  Python, .NET or the independent VitePress package.
- Initial full Vitest run: **475 passed, 5 failed**, all failures from the stale
  retention fixture above. After fixing only the mock, retention plus navigation
  rerun: **25 passed**. Across the latest results for all 43 files, **480 tests
  pass**, with no skips. This is not a single clean 480-test invocation.
- The **3 Node review-request tests pass**, covering retry/concurrency contracts.
- TypeScript and full web ESLint pass. The changed retention test also passes
  focused ESLint after the fixture correction.
- Next.js **16.4.3 production builds pass with both Webpack and the unchanged
  default `npm run build` (Turbopack)**. Both used `.next-security-20261026` and
  locked authentication. This verifies compilation/prerendering, not real sign-in
  or production deployment. The default build was added after independent review
  correctly distinguished it from the Webpack check.
- Live browser retry passed: five workflow views in Dark/Light and narrow layouts,
  plus Library/Questions route checks and Staff denial at `/organization`. No
  measured horizontal overflow, captured runtime exception or API write. The
  complete retained synthetic case response was identical before/after. This
  uses Development identity and existing synthetic reviews, not new uploads.
- All 1,291 baseline files remain: 7 scoped modifications and 1 new evidence
  document. Root AGENTS, tsconfig and next-env match their captured baseline.
  Diffs were opened in the existing VS Code window. The Progress and Quality
  Reviewer independently verified the dependency scope, fixture correction and
  evidence, then reported no remaining material findings. All 280 checked
  documentation links resolve. User manual acceptance remains open.

The existing Vite configuration warning remains: a future version will require
an explicit ESM config format. It was not suppressed and does not fail this run.
Initial failures/logs are retained alongside successful receipts.

Key commands (web commands from `apps/web`, installed Node 24.21.0 on PATH):

```powershell
npm audit --json
npm ls --depth=0
node node_modules/vitest/vitest.mjs run --maxWorkers=2 --reporter=json
node node_modules/vitest/vitest.mjs run app/organization/retention/page.test.tsx app/security/workspace-navigation.test.tsx --maxWorkers=2
node --test app/review-request.test.mjs
node node_modules/typescript/bin/tsc --noEmit --incremental false
node node_modules/eslint/bin/eslint.js .
$env:NEXT_DIST_DIR = '.next-security-20261026'
$env:AUTH_MODE = 'Locked'
npm run build
```

## Local workspace and limits

Only the verified owned web process was restarted. The API, Python service and
PostgreSQL cluster stayed running. PID/path/start-time checks prevented acting on
unverified processes; the initial guard also encountered the Windows console host
and was corrected to leave it out of server-process handling. The web workspace
is restored at `http://127.0.0.1:3310`, using the same API and retained database.

The first automated browser navigation timed out after restart; a subsequent HTTP
probe returned 200 in 188 ms and the complete browser retry passed. Manual
acceptance and the earlier live-identity, upload/worker, Arabic OCR, report/export
and deployment gates remain open. A clean dependency audit does not certify the
whole system as secure.

## Short manual check

1. Refresh the local app and reopen your saved invoice check. Confirm the same
   two documents, saved result and decision remain.
2. Open Library and Questions, then switch Dark/Light. Navigation and phase badges
   should look and behave as before. Staff still has no administration access.
3. During your next fresh-file test, continue invoice review to PO review and
   results using the [final acceptance checklist](final-user-acceptance.md).

References: [Next.js release](https://github.com/vercel/next.js/releases/tag/v16.4.3)
and [Vitest release](https://github.com/vitest-dev/vitest/releases/tag/v4.1.12).
The saved npm audit JSON contains the individual advisory URLs and affected ranges.
