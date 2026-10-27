# R5-T01 — Bounded access recovery fixes

October 5, 2026. Rand asked to continue while retaining short acceptance checks
instead of a full demo. This increment fixes three observed source defects in the
actual UI. It does not complete real identity or R5/R6 acceptance.

## Changes and independent review

| Trigger | Result |
| --- | --- |
| Initial, focus, timer or manual session reads overlap; an older Operator/admin response arrives after a newer Viewer response | Only the latest session read can apply grants or invalidate access. Starting a newer read aborts its predecessor; ownership still protects against transports that ignore cancellation. Private drafts reset when the accepted membership changes. |
| `/api/session` token acquisition, response or JSON body stalls | The session read has a 15-second deadline after identity initialization. Failure clears private state and exposes **Check access** and **Sign out**. Retry reloads the page; no write is replayed. The message says access could not be verified, covering both outages and denied access. |
| A membership save is pending and the administrator opens another member or submits twice | A synchronous save guard submits once. Target selection and form controls stay disabled until save, list reload and any self-membership recheck finish. Success, conflict and service failure retain the correct target; existing version/revocation rules remain intact. |

Production files: `apps/web/app/security/enterprise-provider.tsx` and
`apps/web/app/organization/page.tsx`. New regressions:
`apps/web/app/security/session-recovery.test.tsx` and
`apps/web/app/organization/membership-save.test.tsx`.

The independent reviewer identified the three defects and passed the final fixes.
Root reviewed the recovery tests. Root also replaced an initial fieldset wrapper
with individual disabled attributes because the existing stylesheet uses direct
form-child selectors; the independent reviewer passed that correction. The final
DOM structure and CSS remain unchanged. Fresh visual acceptance is still unverified.

## Focused verification

Final frozen-source verification: **59 distinct tests passed**, zero failures or
skips, four files, 30.53 seconds. This comprises 16 new session-recovery checks,
four new membership-save checks, 17 security checks and 22 workspace-navigation
checks. Existing checks cover Operator/Reviewer/Viewer, administrator-only and
combined roles, denied deep links, callback isolation, sign-out, role cleanup and
audit/version behavior. This is component/transport evidence using fictional
identity and HTTP responses, not real authentication or persistence.

New cases cover reverse response order, older network/malformed/401/403 failures,
latest denial, stalled fetch/token/body, late responses after timeout/sign-out/
unmount, and pending save success/conflict/503. Reload recovery models a fresh
provider lifecycle because jsdom cannot execute a browser page reload.

Command from the repository root after selecting its runtime:

```powershell
node apps/web/node_modules/vitest/vitest.mjs run --root apps/web app/security/session-recovery.test.tsx app/security/security.test.tsx app/security/workspace-navigation.test.tsx app/organization/membership-save.test.tsx --maxWorkers 1 --no-file-parallelism --testTimeout 15000 --reporter default --reporter json --outputFile outputs/access-recovery-20261005/final-vitest.json
```

The saved report uses the absolute output path. From `apps/web`,
`node node_modules/typescript/bin/tsc --noEmit --incremental false` passes, and
`node node_modules/eslint/bin/eslint.js app/security/enterprise-provider.tsx
app/security/session-recovery.test.tsx app/organization/page.tsx
app/organization/membership-save.test.tsx --max-warnings=0` passes. Both exit zero.
CRLF-aware `git diff --check` passes on the changed tracked files.

Reports are in `outputs/access-recovery-20261005`: `final-vitest.json`,
`final-vitest.txt`, `static-results.json` and `final-status.json`.
`baseline-and-intermediate-summary.json` records the earlier 39-case baseline and
overlapping runs separately; those are not added to 59. The new regressions were
first executed on the fix; no pre-fix regression execution is claimed. There were
no failing test runs. The existing Vite future-loader notice remains visible.
No broad suite, application/docs build, database runner or full demo was run.

## Browser and identity boundaries

The supported preview launcher restarted the stopped synthetic preview on port
3410, ready in 26.8 seconds. Creating an in-app tab timed out after 41.37 seconds
and reset the kernel. Recovery inventory found that tab, but binding it timed out
after 31.51 seconds on CDP `Emulation.setFocusEmulationEnabled`. No browser inputs,
role/theme changes, screenshots or sample writes were performed. This is a fresh
browser limitation; it does not retract the October 3 sampled passes or establish
a browser pass for the October 5 changes. The preview remains available.

Readiness inspection emitted only presence flags for recognized identity settings
in root `.env`, candidate web local env files and the current process. Web
`AUTH_MODE`/public Entra tenant/client/scope settings and API `Security__Mode`,
tenant/audience/allowed-client settings were absent in those checked locations.
No credential values were printed, registrations inspected or accounts provisioned.
External configuration stores and real tenant/test-account readiness are unknown.

Source review of the installed MSAL redirect bridge clarified that direct sign-in
normally preserves its `ORIGIN_URI`, including the original deep link. Explicit
`/signin` keeps its role-based home/admin fallback. The earlier baseline finding
was corrected; no redundant return-path helper/storage was added. Live callback
and return behavior remain unverified.

Real closure needs matching single-tenant SPA/API registrations, delegated scope/
consent, exact callback, web/API/CORS configuration, a migrated isolated database
and provisioned role memberships. Once available, use separate short actions:
sign in from a saved-work URL; verify the destination; sign out; check denial after
a test membership downgrade/revocation; and inspect the expected audit record as
an authorized administrator. Renewal/expiry and conditional-access behavior also
need target-environment evidence. These require no OCR demo to check access.

API payloads, backend authorization, ETags, append-only audit, evidence, migrations,
Arabic data and retained application data are unchanged by these UI fixes. No
retained database was migrated, real access granted/revoked, provider called or
deployment activated. R5-T01 has passed its local code/review/test gate; fresh
browser and live identity acceptance remain pending, so R5/R6 are not complete.
