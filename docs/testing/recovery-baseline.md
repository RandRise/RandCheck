# R1-T01 — Recovery baseline and integration inventory

September 26, 2026. Read-only product audit plus focused baseline checks; findings
are from current source, not a claim of a new live Entra or database walkthrough.
Independent workflow/API and experience/identity agents reviewed the same baseline.

## Baseline checks

- `./.node/node.exe apps/web/node_modules/vitest/vitest.mjs run --root apps/web --maxWorkers 1`:
  8 files, 71 tests passed, 60.05 seconds. One non-failing Vite config-loader warning.
- `./.node/node.exe --test apps/web/app/review-request.test.mjs tools/ui-preview/fixtures.test.mjs`:
  7 checks passed. Three verify request/ETag behavior; four cover synthetic adapters.
- The lightweight preview responds on port 3410. Existing browser verification is
  recorded in [its report](ui-preview-verification.md); not all flows were rerun here.

These passing tests do not cover all gaps below. Do not call a known gap verified
merely because the existing suite is green.

## Prioritized findings and planned ownership

| Priority | Finding and source evidence | Task / required verification |
| --- | --- | --- |
| P1 | Document review loading and mutation completions in `apps/web/app/page.tsx` can apply an older document's response after the user selects another. Processing updates also lack selected-context guards. | New R1-T02 before styling: delayed A/B read and write tests, safe messages/busy/history state, no cancellation/replay of committed mutations. |
| P2 | Document reload selects the first document. Reconciliation has only page-local case state; backend has GET by ID but no case list route. | R4-T01: document and case deep links, navigation/refresh/reopen, scoped list APIs. |
| P2 | Reconciliation errors do not refresh stale ETags, and Ready source choices include incomplete/wrong-type reviews. API validation is stricter. | R4-T03: conflict recovery preserving drafts and correct eligible source choices. |
| P2 | API supports replacing pinned sources and returns earlier runs/decisions; current UI hides these paths/history. | R4-T03/T05: explicit replacement/revision, unchanged historic snapshots and evidence. |
| P2 | Create case has no request key; each accepted request creates a new ID. Sequential comparison runs and resolutions append history by design. | R4-T05: distinguish duplicate intent from legitimate revision; real retry/atomicity checks. |
| P2 | Viewer can open Questions/saved results, but UI disables both source-preparation paths and has no prepared-index selection. | R3-T03 guidance must describe actual abilities; R4-T03 needs an authorized read/prepared-source contract before advertising new Viewer questions. |
| P2 | Explicit `/signin` falls back to home/admin. October 5 source review clarified that the installed MSAL redirect bridge preserves the original request URL for direct deep-link sign-in; live return proof remains unverified. The original exact reconciliation route gate also needed explicit route permissions. | R3-T02/R5-T01: explicit route permissions and real Next/callback journey proof; do not add redundant return-path storage without a demonstrated failing journey. |
| P2 | Case-to-Questions link populates an ID input but needs a second action to prepare it. Initial failures tell users to start developer services. | R4-T03: contextual source handoff and useful recovery messages. |
| P2 | Reference/category absent from case domain, DTO and database. | R4-T04: explicit migration/backfill, uniqueness, tenant filtering, no renumbering or evidence mutation. |
| P2 | Global main/header/aside CSS and hardcoded colors/fonts can leak into new frame/help. Preview does not render Next root layout. | R3-T01/T02: scoped styles, common tokens, actual Next first render and navigation in both themes. |

## Contracts to preserve

- Review start resumes an existing active session; database enforces one active
  review per document. Completed extraction reviews are not silently reopened.
- Strong ETags/If-Match protect review and comparison mutations. Completion requires
  decisions on all machine fields. Corrections/decisions remain append-only.
- Linked sources preserve document, review, extraction and content hash identity;
  old comparison runs use immutable snapshots. Replacing a link cannot rewrite them.
- Reprocessing refuses conflicting active jobs. Question retries retain the same
  request key for the same question/mode/source selection.
- Read, Review, Operate and independent administration remain separate. New endpoints
  must be registered in permission and audit maps, with tenant checks and safe errors.
- Role help belongs in the authorized workspace reset boundary; guide dismissal keys
  must include organization, actor and guide version. Signed-out help is separate.
- Arabic/mixed-direction fields, values and reasons are preserved through every step.

## Preview and external integration limits

The fixture does not validate document-role type and requires PO + invoice for its
sample total comparison. The real API validates role/type and can evaluate incomplete
chains with missing-source findings. Use backend contract tests for those behaviors.
The fixture's navigation shim, global sample state, and fictional identity do not
prove Next client navigation, tenant isolation, durable audit or authentication.

The checked repository documents live Entra configuration/testing as unverified.
No tenant/test-account readiness claim is made from public configuration examples.
Production configuration or secrets were not opened. R5-T03 remains dependent on
actual configuration and test identities. No external service was enabled.

## Review and gate

Both independent audits completed without modifying files or starting services.
Their findings are included above, with the response race promoted ahead of visual
work. Baseline tests pass; affected integrations are unchanged because this task
only adds planning/evidence. The next task must demonstrate the race with regression
tests and pass an independent code review before advancing to visual proposals.
