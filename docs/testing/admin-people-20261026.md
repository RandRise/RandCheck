# Administration batch 1 verification

**Manual follow-up, October 26:** the user accepted the reviewed People & access
sample and approved Activity history next. Real Admin sign-in and PostgreSQL
membership concurrency remain unverified. The original batch report follows.

October 26, 2026. User approved the N15 layout and batch 1 with "Yes, Proceed."
Implemented and checked; manual acceptance remains open. The live local account
is still Staff. No account was provisioned or promoted in the retained system.

## What changed

- One Admin-only **Administration** sidebar entry, active on all three existing
  routes. Native section links open People & access, Activity history and Document
  storage. Old bookmarks still work. Redundant audit footer links were removed.
- People list and side editor, with names, roles and access status. Account IDs
  are expandable. On narrow screens the editor follows the list; selection moves
  focus and scrolls to it. Both themes use the existing appearance controls.
- Components, types, API calls, save/recovery state and styles have focused files.
  No backend contract, permission or persistence implementation changed.
- Staff/Admin choices, explicit Admin grant, removal confirmation, expected
  versions, audit writes and final-Admin protection are preserved. No default
  role is assigned when adding someone. Entra account IDs remain required.
- Uncertain writes require inspection before a new choice; they are never
  automatically replayed. A small organization/actor-scoped flag survives browser
  refresh when session storage is available. Same-SPA returns wait for the known
  pending promise. Failed recovery reads retain the draft and lock. If storage is
  blocked, the in-memory guard remains; the page tells the user to stay until they
  have checked. Reading current access is not proof an interrupted request ended.

The existing list is capped at 500 and says how many people are shown. There is
no claim of a company-wide total or a search that finds every member. Activity
and storage received only the shared navigation and section headings; their
detailed redesigns remain later work. The invoice workflow is unchanged.

## Findings and fixes

Independent Progress and Quality Reviewer findings:

1. A delayed refresh could close a newly opened draft or hide an uncertain save.
   Synchronous read ownership now blocks edits/submits during the refresh.
2. Back/Forward could abandon an in-flight save and lose the recovery notice.
   Account-scoped unresolved state and pending-promise tracking now survive
   remount without replay. Reload requires a fresh read and explicit new choice.
3. An ordinary successful refresh closed an unsaved editor. Normal refresh now
   keeps its draft and original version; only explicit recovery clears it.

Self-review fixed a global header CSS collision, tightened malformed successful
save-response handling, and updated the in-page help labels while retaining Arabic.
Final independent recheck found no material findings remaining. All three findings
above are resolved. All 1,294 baseline files remain: 15 scoped modifications and
20 additions. The meaningful before/after diffs and full batch diff were opened
in the existing VS Code window. Manual acceptance remains open.

## Fresh verification

| Check | Result and limits |
| --- | --- |
| Focused web tests | 107 passing across latest results for 10 files: 105 in `tests-verified.log`, then 13/13 in `tests-recovery-final.log` replacing the earlier 11 tests in those two files. Covers roles, confirmations, conflicts, retries, failed/successful refresh, pending remount, malformed replies, section links and help. |
| TypeScript | `tsc --noEmit` passes after correcting a tuple type in the new navigation test. |
| ESLint | Changed organization/security files pass after fixing JSX apostrophes and initial-read effect scheduling. |
| Backend security | 115/115 `EnterpriseSecurityTests` pass, no skips, using test-host fixtures. These are not a real Microsoft sign-in or a live Admin browser session. |
| Sample browser | Nine checks pass: one sidebar entry, section navigation, editor focus, Dark/Light desktop and 390px layouts without horizontal overflow, last-Admin rejection and versioned save through the fictional API, Back/refresh, and API isolation. |
| Documentation | VitePress build passes; changed-document links resolve. Initial esbuild configuration lookup was sandbox-blocked; the permitted retry passed. Existing large-chunk warning remains. |
| Real local browser | Staff denied `/organization`; no Administration link, editor or API writes. Passed separately after correcting a DOM-ready guard in the harness. |

The first browser attempt timed out on sandboxed CDP; the permitted retry worked.
The initial final Staff step accessed `body` before it existed; only that step was
rerun. The first backend invocation used the system SDK after PowerShell blocked
dot-sourcing; the rerun used the installed `.dotnet` SDK and passed. Vitest emits
an existing future-config-loader warning; no test skips or failures remain.
No full application rebuild, invoice demo, retained database reset or broad test
rerun was needed. Evidence is in `outputs/admin-people-20261026/`.

## Preview and manual check

Open [Sample Admin view](http://127.0.0.1:3430/organization). This separate server
uses fictional people and an in-memory API. It has no proxy to the retained API.
Its banner identifies the simulation; restarting it resets only its own sample.
The earlier preview on port 3410 was not reset. The actual Staff app is on 3310.

1. Open Omar with **Edit access**. Try Dark and Light, then narrow the window.
   Check readable spacing, visible labels and easy access to the editor.
2. Choose Admin and read **Grant Admin access** before saving. Check the message
   and list; then change Omar back to Staff. These are sample changes only.
3. Uncheck **Active access** and press Save. Confirm that removal asks again.
   Close without confirming if you want to keep the sample person's access.
4. Open **Add person**. Check the account-ID explanation and unselected role.
   Visit Activity history and return with the section link or Back.

Actual Admin integration needs a deliberately prepared test identity/environment.
Real identity, PostgreSQL membership concurrency, full processing/OCR/Arabic
extraction, report/export, deployment and the final user-acceptance scenarios are
not closed by this batch. Existing historical evidence remains historical.
