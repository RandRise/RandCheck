# N07: keep earlier quotations out of the everyday list

October 23, 2026. Rand explicitly chose **Hide quotations; keep history
accessible** after dependency inspection. No deletion is authorized by this
choice. This follows N06; earlier manual and integration checkpoints stay open.

## Dependency inspection and scope

`Completed quotation.txt` is a synthetic seed in `tools/ui-preview/fixtures.mjs`.
Its completed review is used by legacy comparison and cited-answer tests in
`fixtures.test.mjs`. The PDF quotation sample is referenced by phase-4 truth
data, phase-5 question fixtures, CI preparation, demo setup and historical
evidence. These are useful compatibility fixtures, not disposable files.
No sample, test, source file, upload, review, result or audit record was deleted.

Acceptance: hide known quotation types from the default Document library list;
provide an explicit way to show/search them; keep unknown types, invoices and
POs visible; preserve selected details, bookmarks, browser Back, review/history
downloads and permissions. The embedded invoice workflow and legacy review
screen keep their existing lists. No new dependencies or background services.

## Implementation

The library uses the existing `isQuotationDocument` recognizer. A separate
`LibraryFilter` component contains **Show earlier quotations** and the visible/
total count. Visibility and filename/ID search combine without changing stored
documents or selection. A quotation-only list explains how to show the files.
A bookmarked or already-open quotation stays readable when hidden from the list.
The default returns to hidden on a fresh mount; there is no stored preference.

The current work-list endpoint returns all organization-scoped document summaries.
The browser filters and renders them locally; there is no paging in this path.
The .NET repository uses a summary projection rather than all extracted fields,
but loading remains unbounded by page size. Suggested server-side paging/search
and Invoice/PO/All filters are proposals, not implemented or approved here.

## Verification and review

Evidence: `outputs/library-quotation-filter-20261023/`, with before snapshots and
a hash baseline of the existing dirty tree. Changes are assessed against that
baseline, not against all uncommitted work.

- 59 focused tests pass: library, document-type recognition, navigation,
  processing retries and review simplification. New/updated checks cover default
  hiding, search, counts, quotation-only lists, unknown/PO visibility, selected
  quotation retention, bookmarks/Back and review-history access with no writes.
- TypeScript `--noEmit --incremental false` and scoped ESLint return 0.
- Four read-only browser checks pass: dark/light at 1440/420px. Show/hide works,
  an open quotation remains readable, reload restores its saved link, and no
  horizontal overflow, runtime exceptions or application writes were observed.
  These use the retained synthetic preview; they are not live API acceptance.
- No backend change, full build, preview reset or database operation was needed.

Independent review found no material issues. All 1,231 baseline files remain,
with 8 expected existing-file changes and 3 additions. All 250 local documentation
targets resolve; the diagram parses as SVG. Twenty guide tests also pass, giving
79 distinct focused checks. Scoped ESLint was repeated after the guide edit and
passed. Vite prints its existing non-failing configuration migration notice.
VS Code before/after diffs are open. Visual/manual acceptance and live integration
remain pending; no unrun checks are described as passes.

## Short manual check

1. Open Document library. The default list should hide known quotations.
2. Select **Show earlier quotations**, search for a quotation and open it.
3. Uncheck the option. Its list entry should disappear while its saved values
   and review history remain readable. Refresh its link to confirm access.

These instructions are not recorded as manual passes.
