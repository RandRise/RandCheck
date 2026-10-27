# N04: Show results in the retained sample preview

October 20, 2026. A bounded repair following Rand's evaluation error report.
Manual acceptance and overall G02/G03 integration remain open.

## Cause and change

The current page sends `invoice-po-2.0` without an invoice-scope question. The
retained port 3410 fixture accepts only `invoice-po-1.0` with an explicit scope.
Read-only inspection found both completed review pins in the affected check.
The message “Choose both saved reviewed revisions and an invoice scope” therefore
misidentified a request-version mismatch as missing user input.

A preview-only fetch adapter translates that request to the older simulation
with `Unknown` scope. It does not ask the user another question, confirm values,
replace documents, or change the production backend request. The response stays
unchanged: `synthetic-total-only`, coverage `NotRecorded`, and a missing-scope
warning. A visible preview notice explains that missing checks are not a match.
This unblocks sample UI testing; it does not implement or verify v2 calculations.

Only a same-origin POST to a case's evaluations endpoint, containing v2 JSON
without an explicit scope, is adapted. Other requests pass through. Headers,
save token, cancellation and additional body fields are preserved. Errors pass
through without automatic replay. Reinstallation preserves existing role and
membership restrictions.

## Evidence and preservation

Evidence is under `outputs/evaluation-preview-20261020`: the 438-file dirty-tree
baseline, scoped diff, source hashes, receipts and browser screenshot.

- **51/51 Node checks passed**, covering the adapter, retained role bridge and
  fixture contracts. An isolated fixture reproduces the exact error, then accepts
  the adapted request. Missing pins, a disabled account and a stale save token
  still fail. Prior results, reviews and audit entries remain unchanged; one
  explicit successful comparison adds one result and its audit entry.
- **26/26 Vitest workflow checks passed** in `journey.test.tsx` and
  `journey-results.test.tsx`. The production request remains v2 without scope.
- `node --check tools/ui-preview/comparison-bridge.mjs` exited 0.
- A read-only headless browser loaded the actual retained preview and its new
  warning, with no runtime exception, Vite error overlay or application write.
  The screenshot is `preview-warning.png`. Comparison writes were tested only
  against disposable fixtures, not the user's retained cases.

Commands and exit codes are in `receipts.json`; test details are in
`preview-tests.log` and `web-request.json`. The known Vite configuration advisory
is not a failing check. No application build, deployment, persistent service,
preview restart or data reset was performed. Existing integration, Arabic/OCR,
live identity, report/export and deployment gaps remain. The retained server's
current AED gate and real v2 behavior are not established by this repair.

The separate Progress and Quality Reviewer found no remaining material issue.
It independently verified all 438 baseline files: 5 intended existing changes,
no missing or unexpected changes, and matching hashes for all 9 final files.
Its minor guide button-label correction is fixed. Older preview instructions are
now explicitly marked historical. Five changed documents
pass 194 local-link checks. The initial link checker omitted VitePress public
asset paths; correcting its resolver verified the existing diagram without a
source change. This small guide update did not require a VitePress rebuild.
Before/after diffs for the preview entry and adapter were opened in the existing
VS Code window. Earlier N03 and S3 results remain historical evidence.

## Short manual check

1. Refresh the preview. Expect the notice saying comparisons use sample rules.
2. Reopen your check, inspect the invoice, continue to the purchase order and
   inspect it, then choose **Show results**. Refreshing keeps saved reviews but
   requires you to inspect them again before comparison.
3. Expect a sample result with incomplete-check warnings, without a scope question
   or the reported error. Earlier results should remain accessible.

The normal workflow and diagrams are unchanged. Other design proposals remain
separate; this repair is not manual acceptance of the wider workflow.
