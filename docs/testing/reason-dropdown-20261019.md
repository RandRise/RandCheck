# Reason dropdown — October 19, 2026

## Scope and behavior

Rand approved replacing the recent normal-form reason buttons with a selection
list. The shared ReasonInput uses a native dropdown and the existing action-specific
reason constants. Choose a reason is initially empty. Other opens and focuses a
custom explanation; choosing a preset hides that box. Existing free-text drafts,
including Arabic, remain supported. Selecting a reason only changes a draft.
Add/Save/Clear still records it through the existing API and audit path.

Styling is small and uses existing theme variables. No UI library, backend change,
data migration, document reset or background process is added. The upcoming redesign
is separate. Guided-view suggestions remain unchanged. G02/G03, supplier identity,
payment-term placement and dashboard/role proposals retain their earlier status.

## Verification and review

Fresh verification passes **93/93 tests** across seven files, with no failures or
skips. Receipts are in `outputs/reason-dropdown-20261019`. The focused command runs Vitest on `page`,
`document-simplification`, `review-experience`, `review/guided-review`,
`document-types`, `document-repeat` and `review-selection`, with `--maxWorkers=1`.
Cases cover preset/custom payloads, explicit saves, empty explanations, Arabic,
ETags, retries, selection races, pending-save disabling and discard resets.

The independent reviewer identified a visible/accessibility label mismatch and
an empty Other explanation surviving discard. Both are fixed; regression cases
check addition and correction discard. The implementation agent owns these fixes.
Initial failures from old label-based test queries are preserved, as is the
intermediate 91/91 pass before reviewer fixes. The same reviewer rechecked both
fixes and found no remaining material issue in this batch.

Post-fix `tsc --noEmit --incremental false` and ESLint on `page.tsx`, ReasonInput
and the five affected test files both exit 0. Receipts: `web-tests.log`,
`typescript.log`, `eslint.log`. Vitest emits an existing advisory about a future
Vite config-loader default; it does not fail these checks. Source-link validation
passes for four Markdown pages and three new links. Read-only delivery of the
updated component from the running sample preview returns HTTP 200; PID 4456
was not restarted. This is not browser interaction or visual acceptance.

The baseline covers 1155 existing files: 11 changed, one new report and none
missing. Exact before copies and the batch-only
diff preserve unrelated uncommitted work. The component diff is opened using
VS Code `--reuse-window --diff`. Browser appearance and user acceptance are pending;
these checks do not establish live backend/database integration, OCR, identity,
report/export or deployment readiness. Guide Markdown and links are checked;
the older static guide is not rebuilt. Existing diagrams do not depict these
normal-form controls and remain accurate.

## Short manual check

1. In All fields, open the Reason list under Add a missing field. Pick a reason;
   it should stay selected without saving or showing another text box.
2. Choose Other — write my own. Write a short explanation. You can switch back
   to a listed reason, or discard the draft to return to Choose a reason.
3. On a test correction or addition, save and check the saved reason. Existing
   documents and earlier decisions should still be available.
