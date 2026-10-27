# N05: a simpler Document library

October 20, 2026. Rand approved simplifying the library and removing competing
paths between pages. This is a bounded G02/G03 navigation refinement. Manual
acceptance and full integration remain open.

## Scope and implementation

The library previously repeated the upload form, invoice-check link, review editor,
and Questions link. Those controls are removed from `/documents`. The side menu
now names it Document library. The preview's duplicate page-shortcut menu is also
removed. Home remains the place to start and continue invoice checks.

The library has one searchable list. Selecting a file opens details in place;
visiting the list does not select a file automatically. Original downloads,
read-only saved values, source evidence, quotation documents and review-history
downloads remain available. Values and history use expandable sections.
Processing recovery remains behind its existing permission checks, with routine
reprocessing inside Processing details. Failed-processing retry remains visible
when needed. New upload and review decisions belong to the invoice workflow.

The existing workspace owns retrieval, history, concurrency and recovery. A small
library mode disables review actions and renders a separate `LibraryReview`
component. Scoped CSS keeps the change out of the embedded invoice workspace.
Existing document bookmarks, browser history and legacy entry points remain
compatible; they are not advertised as additional navigation paths.
This changes screen access points, not account permissions or backend endpoints.

Acceptance: no upload, review-edit or competing page links in the library;
search, explicit selection, in-place details, original/history access and quotation
compatibility; truthful missing/unchecked values; preserved retries, stale-response
guards and permissions; readable wide/narrow layouts; focused tests, independent
review, before/after diffs and updated guide.

## Findings and verification

The captured dirty-tree baseline contains 442 files. Evidence lives under
`outputs/document-library-20261020`, including before files, diff, hashes,
test receipts and before/after browser images.

- The initial 49-test selection passed 46 and failed 3 new test assumptions:
  Arabic/numeric text spans multiple bidi elements, the existing download message
  says History is ready, and JSDOM role queries do not model closed-details
  visibility reliably. Assertions now inspect the complete text, actual message
  and disclosure state without weakening the behavior being checked.
- Self-review restored the history download fallback in the new reading view.
  It appears after an export in place of the initial export button.
- The next focused selection passed **124/124**, with no skips. It covers the new
  library, shared document navigation/retries/selection, account/navigation/help,
  and the invoice journey/results. TypeScript and affected ESLint exited 0.
- The separate reviewer found no material source issue and requested extra
  coverage for excluded/missing/added/corrected values, reasons, absent evidence
  and older extraction warnings. That focused display test is added. The final
  focused selection passes **125/125**, with no skips. The reviewer rechecked the
  new test and evidence and found no remaining material issue.
- An outdated Questions help sentence still pointed back to the removed library
  shortcut. English and Arabic help now describe choosing reviewed sources in
  Questions itself. Its guide tests and affected lint were rerun after this
  wording correction; their separate receipts are not added to the 125 count.
- Actual-component browser checks at 1440px and 420px show one library menu link,
  no file input, competing page links or review-edit buttons, preserved quotation
  values/history, and no horizontal overflow, runtime error or application write.
  The browser reads retained synthetic data; it does not verify live identity,
  original source storage or durable history. It does not run the real backend.

No backend change, data deletion, migration, preview restart or reset was made.
No broad build or persistent service was added. The Impeccable context launcher
was unavailable; existing code, saved context and actual rendering guided this
refinement. Other design proposals remain separate.

The guide is edition 14 with a new navigation diagram. Four documents passed
210 local-link checks and the new SVG passed XML validation. A documentation
rebuild was unnecessary for this bounded update. Relevant before/after workspace,
library entry and new read-only component diffs were opened in the existing
VS Code window. Independent preservation review checked all 442 baseline files:
11 intended existing changes, 5 added files, no missing or unexpected changes.
Before and after hashes matched the captured batch. Final diff and hashes are
retained in the evidence folder. Reviewer clearance does not record manual acceptance.
The N04 preview comparison limitation and earlier real integration, Arabic/OCR,
identity, export and deployment gaps remain open.

## Short manual check

1. Refresh and open Document library from the side menu. Expect search and saved
   files, with no upload form or comparison shortcut.
2. Select a file. Expand Saved values and evidence, then Review history. Values
   must be read-only; quotation files and earlier decisions remain accessible.
3. Search for another file and select it. Use browser Back to return. Use Home
   in the side menu when you want to continue an invoice check.
