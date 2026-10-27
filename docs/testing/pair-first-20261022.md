# N06: choose the pair before creating a check

October 22, 2026. Implementation approved; manual acceptance and real API/browser
integration remain open. This is separate from administrator dashboard design,
Python refactoring, supplier-policy decisions and report/export work.

## Result and acceptance criteria

- Home **Start with my files** opens `/invoices/new`, with all 3 phases visible.
  It does not create a case. One selected/uploaded file is insufficient.
- **Continue to review** requires two different, available documents with no
  known role mismatch. It creates an invoice/PO check, saves both draft links and
  opens invoice value review. Unknown types still need human review.
- Creation never confirms fields, pins completed reviews or evaluates a pair.
  Existing checks and their reviews/results remain accessible.
- History **Recover an earlier request** requires the matching account-scoped
  browser intent. It keeps the exact request key, name, category and original
  workflow payload, including omitted legacy workflow kind. Missing/mismatched
  intent makes no write and does not replace the stored request.
- Current permissions, historical quotations, Arabic text, English digits and
  AED comparison policy remain unchanged.

## Implementation and recovery limits

The new feature separates its component, hook, storage and preparation service.
It reuses existing upload and backend APIs. Creation and the two draft-link saves
are separate requests, not a database transaction. A failure after Continue may
leave an empty or partially linked draft; that is recoverable saved work.

Before sending, the browser stores the pair and stable creation/draft request
keys. Retry first resolves that same request/case, reads its current version,
skips matching saved links and saves only missing links. It refuses changed
selections or already-reviewed work. A lost reply stops further writes until an
explicit retry. Storage is account-scoped and contains identifiers, not source
text. Without session storage, recovery is limited to the mounted page's memory.

The independent reviewer found that unavailable or reclassified files could
disable Retry while also locking selection. Fixed: an existing request remains
recoverable, but incompatible draft writes are blocked. **Open saved check**
provides a way to inspect the recovered case. Two regressions cover missing and
wrong-type files. History's former fresh-empty-case form was also restricted to
recovery, with zero-write missing/mismatched-intent tests and preserved legacy
retry/navigation/Arabic title coverage.

## Verification evidence

Evidence is in `outputs/pair-first-20261022/` and is specific to this batch's
captured baseline. Earlier dirty changes are not counted as N06 implementation.

- Initial focused run: 41 passed. The first expanded run found a test-only
  argument-count assumption; fixed to assert no writes. A later run passed 193.
- TypeScript identified a test mock parameter mismatch; corrected to accept the
  same request types as the shared helper. Final TypeScript and scoped ESLint
  exit codes are 0 in `static-checks.json`.
- A concurrent run had 192 passes and 2 failures: an existing navigation test
  edited before its mock review finished loading, and a metadata test timed out
  while still opening the case. The edit test now waits for the review receipt
  without weakening assertions. A single-worker rerun avoids browser/compiler
  resource contention: **194 passed across 15 files**, recorded in `focused-tests.json`.
  Non-failing warnings remain: Vite configuration migration advice and an older
  History browser-Back test act warning. Neither is a production integration pass.
- Four read-only browser views cover dark/light at 1440/420px, Home routing,
  one-file blocking, two-file readiness, all 3 phases and overflow. No application
  writes or runtime exceptions. Screenshots are sample-preview evidence.
- A fifth browser check intercepts creation and draft saves with synthetic
  responses, simulates a lost PO acknowledgement, then retries into phase 2.
  Exactly one creation and two draft saves were intercepted; recovery performs
  reads, with no pins, evaluations or writes to the retained server.
- No backend changes, database migration, preview restart, full demo or broad
  production build. Real identity, extraction/OCR, Next production routing and
  end-to-end backend acceptance are not established by these checks.

Final independent recheck: no material findings remain. All 1,223 baseline files
remain, with exactly 15 expected existing-file changes and 8 additions (6 web
files, the screenshot and this report). All 247 local documentation targets
resolve; the updated SVG parses. VS Code shows before/after Home, History and
guide diffs plus empty-before diffs for the new component and service. The final
guide wording rerun passes 20 tests (already included in the 194 distinct total).
Reviewer clearance does not mean manual acceptance.

## Short manual check

1. On Home, choose **Start with my files**. Check that all 3 phases are visible.
   Select only an invoice: **Continue to review** must stay disabled.
2. Select its PO and continue. Invoice value review should open. A check is now
   saved, but no values should become checked and no result should be created.
3. Return Home and reopen that same check. Confirm both filenames remain. Open
   an older saved case/result to confirm historical access remains available.

Use permitted test files. These are instructions for Rand, not recorded passes.
