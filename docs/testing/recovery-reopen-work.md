# R4-T01 — Find and reopen work

September 30, 2026. Task complete; R4-T02 is next.

Spacing follow-up: grouped All cases and the case Questions link with a 24px
horizontal gap and 20px separation from the case summary. Below 800px the controls
stack with a 16px gap. Verified desktop and 390px browser layouts, keyboard order,
13 existing comparison/navigation tests and independent review. No route, state,
API or permission changes. Preview updated without restarting or resetting data.

## Outcome

Compare documents now lists saved cases, with name/ID search over loaded pages,
source/comparison counts, Refresh cases and Load more cases. Opening or creating
a case updates `?case=<id>`. Refresh and browser Back/Forward restore the case;
All cases returns to the list and refreshes it. Missing or invalid links show an
error without substituting another case. Linked sources offer Open document.

Documents can be searched by filename/ID and selected using `?document=<id>`.
Refresh/Back restore that selection, including documents outside the current list.
An upload updates the URL only if it still owns the selected context. Invalid or
missing document links never silently select the first document. Selecting the
already-open document does not discard its draft. URL query parameters unrelated
to selection are preserved. English/Arabic guidance explains case reopening.

The list API is `GET /api/reconciliation-cases?offset=0`, returning
`{ cases: [{ id, name, status, createdAt, updatedAt, documentCount, runCount }], nextOffset }`.
It uses Read permission, execution-context organization scope, 50 summaries per
page plus a lookahead, offsets 0–10000, and a 15-second query deadline. It projects
counts without tracking/loading the case history graph. Ordering is creation time
descending, then ID descending. No schema migration or mutation contract changed.

## Checks and review

- Independent frontend author: 17 new URL/navigation regressions, including late
  reads and writes, invalid/missing links, upload selection, off-list document
  refresh, pagination and browser Back after case creation.
- Full UI: 150/150 across 14 files, 103.81 seconds, one worker. Command:
  `.node/node.exe apps/web/node_modules/vitest/vitest.mjs run --root apps/web --maxWorkers 1 --no-file-parallelism --testTimeout 15000`.
  An earlier focused run had one existing Arabic correction test exceed its
  five-second timeout under startup load. The final serial run has no failures.
- Later bilingual guidance wording update: focused 20/20 guide tests pass.
- Request/preview adapters: 8/8 using
  `.node/node.exe --test apps/web/app/review-request.test.mjs tools/ui-preview/fixtures.test.mjs`.
  The new fixture test checks pagination, reopening, unchanged ETags and boundaries.
- Backend: 118 API/security tests, 8 application reconciliation tests, and one real
  PostgreSQL persistence test pass, zero skipped. API tests cover collection shape,
  scoped results, offset bounds and enterprise route permissions. Actual PostgreSQL
  verifies tenant isolation, 51 equal-time cases across a page boundary, summary
  counts/status and no tracking. Use project runtimes, `--no-restore -p:NuGetAudit=false`,
  and filters `ReconciliationApiContractTests|EnterpriseSecurityTests`,
  `Reconciliation`, and `Case_list_projects_counts_and_pages_deterministically_without_crossing_tenants`
  for the corresponding test projects.
- Root independently reviewed backend scope, projection, bounds, registration and
  tests. Frontend reviewer identified three bugs: upload URL not updated, an off-list
  selected document replaced by the first document during refresh, and stale case
  list after browser Back. All were fixed and regression-tested; final review clear.
- Focused ESLint and production Next build pass, including TypeScript and 10 static
  pages. Diff whitespace check passes (existing CRLF notices only).

## Browser and integration evidence

Actual-component preview: created `Office supplies — لوازم المكتب`, reloaded its URL,
returned to Saved cases, used Back/Forward and Arabic search, pinned a completed
quotation, opened its document, selected another document, reloaded and went Back.
The intended records remained selected. Mobile case creation/list at 390px had
375px document width and no horizontal overflow. Viewport reset; Dark preference
unchanged. Screenshot: `outputs/ui-preview/saved-cases.png`.

The preview server was restarted once to load its updated sample API; previous
simulated records reset as documented. The new sample case remains available.
The user’s open tab was not navigated or reset through browser automation.

Actual PostgreSQL testing used a fresh disposable native cluster bound only to
127.0.0.1:25435 under `outputs/recovery-case-list-postgres`. The fixture removed only
its disposable test database; the server was stopped and verified. No Docker or
retained application database was used. API/domain authentication policies and
existing write audit behavior are unchanged; the read-only list adds no mutation.

## Boundaries

- Case search filters loaded pages; it is not a global server search. Concurrent
  insertions can shift offset pages; refresh returns to the newest page. The UI
  deduplicates overlapping pages. The API stops pagination at its bounded offset.
- Browser checks use the sample adapter, whose cross-page links reload the page.
  Next production build and component/history tests pass; this is not a live Entra
  or full Next-to-production-API walkthrough. Signed-out return destinations remain R5.
- URLs recover saved records, not unsaved edits. Source links open the document’s
  current review; historic pinned-version navigation remains R4-T03.
- Case categories/references, durable duplicate-request protection and revised
  decisions remain later R4 tasks. History-download testing remains deferred to Rand.
