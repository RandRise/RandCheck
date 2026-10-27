# Invoice feature refactor — October 11, 2026

Rand requested professional code organization before further product design work.
This first batch reorganizes the existing invoice page. It does not approve the
proposed dashboard, three-stage flow, or Owner/Staff permissions. Those proposals
remain pending review. Broader frontend/backend refactoring is not complete.

## Structure

The pattern is feature-based separation of UI, state, and services:

| Location under `apps/web/app/invoices` | Responsibility |
| --- | --- |
| `page.tsx` | Route entry, access gate, identity/permission remount boundary |
| `components/invoice-workflow.tsx` | Page composition and explicit component inputs |
| `components/create-invoice-check-form.tsx` | Check creation form |
| `components/invoice-check-list.tsx` | Saved-check list and pagination |
| `components/invoice-document-card.tsx` | Invoice or purchase-order selection |
| `components/review-choice-confirmation.tsx` | Existing inline review confirmation; not a new modal |
| `components/compare-invoice-form.tsx` | Scope selection and deliberate comparison |
| `components/saved-comparisons.tsx` | Links to frozen comparison results |
| `hooks/use-invoice-workflow.ts` | React state, request ownership, navigation, actions and recovery |
| `services/invoice-service.ts` | Authenticated requests and uncertain-write handling |
| `services/invoice-response.ts` | Existing response and version validation |
| `workflow.types.ts` | Invoice workflow data types |
| `workflow.constants.ts` | Document roles, labels and pair-rule version |
| `workflow.selectors.ts` | Saved selections, pair readiness and unsaved-selection checks |

State-dependent action functions stay together in the hook so their ownership and
recovery rules remain visible. Feature components use narrow props. Existing CSS,
review-return context, routes, UI wording, and API contracts are preserved. No new
dependency, state framework, backend change, migration, or permission change.

## Review and fixes

Self-review only; no independent reviewer or subagent was used in this batch.

- The original page combined transport, validation, state and rendering. These
  responsibilities now have separate files, with expanded statements and spacing.
- Initial extracted components received the entire workflow as spread props.
  Replaced this with explicit inputs so each component's dependencies are visible.
- Expanded compound response validation for readability. Checked the exact UUID
  version pattern against the saved original and corrected a transcription error
  before the final tests; the original validation rule is preserved.
- Reviewed identity/permission remounting, request-generation checks, cleanup,
  duplicate-submit locking, scoped creation keys, ETags, uncertain-save recovery,
  and deliberate review/scope selection against the captured source.
- The service performs no automatic write retry. The hook still discards late
  responses and preserves unsaved selections during read-only recovery. Document
  selection never confirms fields or adopts a completed review automatically.
- Result links retain the exact case/run IDs, coverage wording, Arabic isolation,
  and original rule version. This refactor does not repair pre-existing product gaps.

## Verification

Exact current-source snapshots and an inventory of 1,067 pre-existing Git-visible
files are under `outputs/invoice-refactor-20261011`. The snapshot includes uncommitted
work; the diff is against this snapshot, not HEAD. Original test files are unchanged.
Before the continuation note, the only changed pre-existing file was the invoice
page. The final preservation receipt also allows the new continuation checkpoint.

- Original saved page: **29/29 invoice workflow tests pass**, using a temporary
  Vite loader for the captured page without restoring over the refactored source.
- Refactored page: the same **29/29 tests pass** in the initial focused run.
- Final related UI gate: **137/137 pass**, no pending cases, across invoice,
  document, comparison, and navigation tests. This total includes those 29 cases.
- Final TypeScript and affected-file lint pass. **17/17 preview-contract checks
  pass**, with no skips. Results are recorded in
  `outputs/invoice-refactor-20261011/gates.json` and separate logs. These synthetic
  adapter checks do not establish live API/browser integration.

`verify.ps1` in that output directory records the exact final commands. It uses the
project runtime helper and runs gates sequentially. Initial runner failures were
environment/setup failures: blocked PowerShell runtime helper led to old Node;
the temporary baseline configuration resolved setup incorrectly; PowerShell also
treated Vite's existing native-loader advisory as a terminating stderr error.
The corrected runner uses tool exit codes and preserves logs. None of these failed
attempts counts as an application test pass. Existing assertions were not weakened.

No production build, backend suite, retained database, full demo, live identity,
OCR or fresh browser acceptance was run. Existing integration, Arabic/OCR, report,
export, deployment and manual-acceptance gaps remain open. The historical S3 count
is separate from these fresh results.

## Short review checkpoint

VS Code was reused to open the extracted source and the full
`before-feature.txt` / `after-feature.txt` diff. Both are labeled file inventories;
the actual editable source files are open separately.

1. Read the 27-line `page.tsx` and follow `InvoiceWorkflow` to its components.
2. Find a request in `services/invoice-service.ts`, a label in
   `workflow.constants.ts`, and recovery behavior in `hooks/use-invoice-workflow.ts`.
3. Review the captured-baseline diff and judge whether this organization is easy
   to navigate before extending the approach to the larger document/comparison pages.

User acceptance of this structure and browser behavior remains pending.
