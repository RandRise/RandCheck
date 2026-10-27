# N02: one workflow page with optional details

October 20, 2026. Implementation requested by Rand; manual acceptance is pending.

## Scope and roadmap

Rand rejected the repeated links and hidden phases in N01. This batch implements
the [approved refinement](../design/workflow-overview-20261020.md): Start here,
upload first, all 3 phases visible together, active work in place, and optional
read-only Details with one Return button. Home is the canonical saved-check
destination. Documents and historical tools remain available in the side menu.

This is a navigation slice of G02/G03, not completion of those milestones. No
backend policy, permission, database, document, review or historical result was
changed. No reset, deployment, account migration or new service was performed.
The larger visual redesign, Owner, richer list summaries and export remain separate.

## Implementation

- Home introduces the job and has one Start with my files action. Saved work
  keeps Continue. Duplicate library/history links have moved to the side menu.
- `WorkflowPhase` keeps every heading and honest saved summary visible. Upload
  is the default; saved files are an expandable alternative. The primary next
  action opens the following phase on the same page.
- `WorkflowDetails` is a separate navigable page view within the saved-check
  route. Its query keeps case, step and document role. It reads file status,
  current saved reviews or frozen result evidence without advancing the work.
- The main workspace stays mounted but hidden during Details. Return restores
  focus. A fresh browser reload still requires deliberate review reinspection;
  browsing Details never creates a checked-invoice acknowledgement.
- Read-only result selection stays local to Details. The main result selector
  tells its parent when it changes the URL so cancelled Back restores the exact
  selected comparison.

English/Arabic in-app help and workflow guide edition 11 reflect this flow.
The new diagram is illustrative; older diagrams and records are retained.

## Review findings and fixes

The separate Progress and Quality Reviewer assessed this batch against the
captured dirty-tree baseline, rather than attributing earlier work to this change.

1. **High:** dirty browser Back into Details could hide the draft and prevent
   Return. Details entry now blocks while dirty; returning itself cannot trap work.
2. **Medium:** choosing a result in Details could alter the main run URL, and
   detail-only browser history could reload hidden results. Details now uses local
   selection; the main component ignores history that leaves case/run unchanged.
3. **Medium:** brand and Home duplicated the same destination. The brand is now
   non-navigating; Home also replaces the redundant All checks entry.
4. **Medium, follow-up self-review confirmed by reviewer:** cancelling Back after
   selecting an older run restored a stale parent URL. A synchronous location
   callback updates only the remembered address. The regression failed before
   the fix with a missing run ID; it checks URL, selection, draft and request counts.

The independent reviewer rechecked all fixes, the failing-before/passing-after
regression, final static/test receipts and roadmap records. No remaining material
finding was reported in this bounded batch. The reviewer independently verified
430 captured baseline files: no missing or unexpected changes; 13 intended
existing files changed and 5 files were added. Review clearance does not replace
manual acceptance or the outstanding integration checks.

## Fresh verification

Receipts and before files: `outputs/workflow-overview-20261020/`.

- The focused invoice/reconciliation/navigation run initially passed 164/165.
  Its new Details test counted unrelated library/list GETs as result reloads.
  It now checks actual case reads plus the unchanged results DOM element.
- The wider invoice/reconciliation/security/document-navigation selection passed
  **230/230** after fixes. A preceding run passed 229/230: a stub exposed its edit
  button before publishing the loaded-review receipt. The cross-case test now
  waits for that receipt before editing and also asserts the confirmation prompt.
- Initial TypeScript caught use of a case/document-only helper for the run query;
  the run now uses `URLSearchParams`. Initial ESLint caught a JSX apostrophe.
  Both checks then exited 0; ESLint reported no warnings.
- After the final older-run history fix, **126/126 invoice/reconciliation tests**
  passed. These overlap the wider selection; do not add them as unique checks.
  The regression preserves the selected run, URL and reason with no additional
  reads or writes after cancelling Back. TypeScript and affected ESLint again
  exited 0, with no lint warnings.
- The installed Vitest/Vite configuration emits an existing CommonJS/ESM advisory.
  No application production build, backend tests or Python tests were needed for
  this web/documentation-only change.

The VitePress build passed with the existing large-chunk advisory. Its first
PowerShell wrapper returned 1 despite logging build completion; the build was
rerun through a subprocess receipt to capture the actual tool exit code.
No application build or publication was performed. Baseline and final source
hashes, a scoped diff and retained earlier failure receipts are in the output folder.
Home/workflow before/after diffs were opened in the existing VS Code window.

An isolated headless Edge process rendered the existing synthetic preview at
1440px and a real 420px viewport. Home, workflow and file Details were visually
inspected. No page-width overflow was measured. An earlier command-line capture
cropped a wider viewport; it was replaced by explicit device emulation.

Real browser Return, Back, Forward and keyboard Tab passed. Returning focused
the active phase heading; Tab reached its Details action. The check observed
zero writes to the application origin. Windows browser integrations made three
unrelated requests, counted separately. These checks did not upload documents,
record decisions or reset the preview. They are not a full end-to-end comparison.

## Limits and manual check

Port 3410 was not restarted. Its older simulated backend cannot verify the v2
comparison policy or real role migration. Live identity, extraction/OCR, Arabic
quality, report/export, deployment and full integration gaps remain open.
Rendering is synthetic evidence, not user acceptance.

1. On Home, choose Start with my files or Continue existing work. Expect all 3
   phases and an obvious upload area; expand saved files only if needed.
2. Continue to check values. Open available Details, then Return to workflow.
   The same phase/document should remain; viewing must not confirm anything.
3. From saved results, read another result in Details and return. The main
   selection should stay unchanged. Home and historical tools should remain easy
   to find in the side menu.

Do not reset the retained preview. These manual checks are still pending.
