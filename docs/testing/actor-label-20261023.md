# N11: readable development-user attribution

October 23, 2026. Approved after explaining the `22222222-...` test identity:
make its history label readable while preserving the saved ID and audit records.
Scope is presentation in decision history, including the embedded invoice results.
Real-user name lookup, authentication, permissions and other scale work are separate.

## Finding and change

Saved resolutions contain `actorId`, not a person-name snapshot. The .NET fixture
defines `22222222-2222-2222-2222-222222222222` as its development identity. The
running `/api/session` identified it as Development identity with the Staff role.
The page previously printed `Actor` followed by the raw ID.

It now says **Recorded by Development user (test identity)** for that exact ID.
The original ID is available in the label's hover title and remains in the saved
record. Other IDs remain visible as **Recorded by user ID ...**. Missing IDs
remain unattributed. The current user's name is never substituted for another
person's historical action. Real names for other users still require backend data.

The fixed ID is documented in a separate constant file. A small result component
owns the label. Neither is used to authorize access. No API, database, role,
decision payload, retry or comparison logic changed.

## Focused verification

From `apps/web`, using the installed project Node runtime:

```powershell
../../.node/node.exe node_modules/vitest/vitest.mjs run app/reconciliation/workflow-context.test.tsx app/reconciliation/action-repeat.test.tsx app/reconciliation/results/results-decision.test.tsx --reporter=default --reporter=json --outputFile=../../outputs/actor-label-20261023/vitest.json
../../.node/node.exe node_modules/eslint/bin/eslint.js app/security/development-identity.ts app/reconciliation/results/decision-actor.tsx app/reconciliation/reconciliation-workspace.tsx app/reconciliation/workflow-context.test.tsx
../../.node/node.exe node_modules/typescript/bin/tsc --noEmit --incremental false
```

- **40 tests passed** in 3 files, including 3 new attribution scenarios: known
  development ID, another actor and missing attribution. They check preserved
  fixture data and read-only requests. Existing history/retry/decision tests pass.
- Scoped ESLint and TypeScript passed. No broad rebuild was run.
- The run emitted a Vite future configuration-loader warning and React `act`
  warnings in the existing browser-Back test. These are recorded, not suppressed;
  they were not test failures or proof of a clean browser console.
- An attempted live desktop/phone check failed before navigation because Edge
  startup timed out after 20 seconds. The alternate browser connector listed no
  available browsers. **No fresh visual pass, screenshots or live unchanged-data
  assertion is claimed for N11.** N10's earlier evidence remains historical.

Evidence: `outputs/actor-label-20261023/vitest.json`, attempted
`check-history.mjs`, and `n11-before-after.diff`. Diffs were opened in the existing
VS Code window. The independent Progress and Quality Reviewer found no material
findings, checked the fixed ID against the backend and verified the 40-test receipt.
All 1,273 baseline files remain, with 5 expected modifications and 3 scoped additions;
264 documentation links resolve. The reviewer independently confirmed preservation.
The isolated manual workspace on 3310/8310 and its test database remain running
for the user, as started in the preceding manual-check turn. They were not reset.

## Short manual check

1. Refresh the saved test comparison on port 3310. Open **Decision history**.
   Expect **Recorded by Development user (test identity)**.
2. Read the saved reason and result: **12 passed, 1 differed, 0 could not be
   checked**. Refresh again; both the reason and difference should remain.

Manual acceptance is pending. The workflow is still Add invoice + PO → Check
details → See results. This change does not close the live identity, upload-worker,
OCR, export or production-performance gaps.
