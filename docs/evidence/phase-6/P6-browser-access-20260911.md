# Phase 6 browser access verification — September 11, 2026

Rand authorized browser sign-in, organization administration and audit viewing,
with snapshots. The [browser contract](../../design/phase-6-browser-access.md)
describes implementation and remaining live-tenant work.

## Automated verification

- 44 web checks pass: the 27 existing workflow checks plus 17 security checks.
  Coverage includes token destination restrictions, concurrency-header/body
  preservation, no denied-mutation retries, sign-out during token acquisition,
  strict-mode initialization, bridge exclusion, membership gating, Viewer
  restrictions, clearing state on denial/logout, stale member versions,
  revocation confirmation, inert audit content and bounded paging.
- All 139 API checks pass after adding the classified session route.
- One new signed-token PostgreSQL test passes against disposable
  `document_intelligence_test`: trusted session fields, None-role administrator,
  Viewer despite token Operator role, role change using the same token, revocation
  and unprovisioned identity rejection. The fixture database was removed afterward.
- Next.js 16.3.5 production build, TypeScript and changed-file ESLint checks pass.
- VitePress documentation build passes.
- Production npm audit: zero advisories. Full npm audit: two moderate development
  advisories in Vitest/mocker; the earlier critical/high issues were patched.

The previous 921-backend-check result remains historical evidence; this increment
reran the 139 API checks and added one database check, rather than rerunning every
earlier backend/Python test. The cumulative backend inventory is now 922 checks.
Initial NuGet restore could not fetch vulnerability metadata in the restricted
environment. The test rerun used existing packages with command-line
`NuGetAudit=false`; it is not evidence of a clean NuGet vulnerability audit.

## Browser verification

Actual React screens were exercised through the browser with the local fictional
fixture: member grant, Viewer-to-Reviewer selection, Reviewer-to-Operator edit,
revocation confirmation/result, retained revoked row, last-administrator rejection,
audit before/after detail, Viewer upload restrictions, direct administrator-route
denial, and logout returning to sign-in. Desktop and 390px mobile layouts were
inspected. The mobile table scrolls horizontally; its accessibility-label overflow
was fixed. Sign-in has a real production Locked/unconfigured snapshot as well.

These snapshots are UI evidence using fictional identities, not live Entra
acceptance. The actual database-backed API authorization was verified separately.

## Preservation

All 34 retained table fingerprints match before/after. The original
`phase5_e2e_20260908` remains at 15 migrations. No retained actor mapping,
provider calls, spending changes or enabling of general model answers/arbitrary
live embeddings. Existing Phase 5 preview processes were not restarted.
No commit, new task, automation or subagent was created.

Artifacts: `outputs/phase-6/browser-20260911/verification.json`, API/PG TRX,
web test/build logs, npm audit JSON, retained fingerprints and `SNAPSHOTS.md`.
