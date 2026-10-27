# N08: ten-file library pages and familiar search

October 23, 2026. Rand approved 10 files per page, full-library search by filename,
supplier name, printed invoice number or PO number, and All/Invoices/Purchase
orders filters. N07 quotation visibility and existing historical access remain.
This does not complete earlier manual acceptance, administrator design or export.

## Behavior and boundaries

Search runs on **Search** or Enter; typing alone sends no request. Changing type
or quotation visibility applies the current search words and returns to page 1.
**Clear search** keeps those filters. **Previous/Next** shows 10 matching summaries
at a time, newest first with ID as a stable tie-breaker. Next is disabled when
there are no more matches. Unknown types remain under All. The long internal ID
is unnecessary. Search is a case-normalized literal substring, not fuzzy matching.

Supplier/number search follows current review values: an active review's run,
otherwise a completed review of the selected extraction, otherwise the selected
extraction. Corrections replace machine values; latest cleared values are absent;
added identities are searchable until cleared. Unchecked extraction values may
match, with a visible warning. Search never confirms a field or creates a review.
Missing identities cannot match; filename search remains available.

Changing search/page does not close the selected file. Bookmarks outside the page,
quotation history, downloads, stale-response guards and processing retries remain.
Permissions and organization scoping are unchanged. No source/test/data deletion.

## Organization and implementation

- `IDocumentLibraryRepository` and `DocumentLibraryQueries` keep database work in
  Infrastructure. `GET /api/documents/library` requires Read access, validates
  search/type/offset and returns no-store responses. The old work-list contract
  stays available for the invoice workflow and older clients.
- `DocumentLibraryRepository` uses parameterized PostgreSQL SQL for effective
  identities, then fetches only the selected page's summaries. Three reads cover
  count, IDs and summaries; no per-document API reads in the production list.
  Lateral reads select the latest action without loading all history into memory.
- The web library has separate list/filter components, request service, types
  and search hook. It no longer fetches the full work-list. Detail/recovery reads
  target the selected document. Aborted/disposed searches cannot replace a newer
  response. Search errors show Retry, without claiming an empty successful result.
- The retained preview has a tooling-only, read-only search adapter so its sample
  state need not be reset. It loads sample summaries/reviews locally and simulates
  paging. Its banner states this limit. New fixture instances also support the
  library route. Neither proves production server performance.

No new dependency, migration, index, scheduled job or permanent process was added.
The server still scans matching text/counts; large-volume performance is not
benchmarked. Paging is not a frozen snapshot across concurrent uploads/changes.
Count, ID and summary reads may also observe concurrent changes; refresh/search
again obtains current results. This is a reading aid, not a reviewed report.

## Findings, fixes and fresh evidence

Evidence lives in `outputs/library-search-20261023/`, relative to this batch's
captured baseline of the dirty tree.

1. Initial API build missed the new query namespace; corrected the import.
2. PostgreSQL execution exposed an EF expression-translation error in effective
   field search: 3 failed, 4 passed. Replaced that expression with parameterized
   repository SQL; the same 7 tests now pass, with no skipped cases. Failed code,
   log and TRX are retained as evidence.
3. Existing UI assertions needed async search submission, updated counts and a
   scoped source-page query because pagination also says Page 1. Tests preserve
   their old read-only/history assertions. Type checking caught unsupported test
   query options; removed them without weakening name matching.

Fresh verification:

- **7 PostgreSQL/API tests passed**: 4 new library tests plus 3 existing work-list
  tests. Covers 23 documents over 3 pages, matches beyond page 1, literal `%_`,
  corrected/added/cleared/unchecked values, Arabic, active older reviews, types,
  account isolation, no-store, invalid inputs and revoked access. Ran through
  `dotnet test ...Persistence.Tests --filter 'FullyQualifiedName~Document_library|FullyQualifiedName~Document_work_list'`.
  Only the disposable fixture database in a new temporary cluster on loopback
  port 55623 was used. It was stopped after testing; retained databases untouched.
- **83 web tests passed** in library, types, navigation, retries, simplification
  and guides. Final changed-file/guide rerun: 37 passed, a subset of those 83.
  Includes request races/errors, page/filter reset, retained selection/history,
  unknown types and processing recovery without the full work-list request.
- **24 Node preview tests passed**: library adapter, existing fixtures and
  supplier-invoice workflow. All synthetic, not real comparison/OCR verification.
- TypeScript `--noEmit --incremental false` and scoped ESLint passed. Vite emits
  its existing non-failing configuration migration notice.
- Four read-only browser views cover dark/light at 1440/420px, quotation toggling,
  saved-link reload and layout. One additional browser-only 23-file fixture
  checks Next, supplier search beyond page 1, type filtering and Clear search.
  All show zero application writes/runtime exceptions. Preview not reset/restarted.

Independent final review found no material issues. All 1,234 baseline files remain,
with exactly 15 expected existing-file changes and 14 additions. All 253 local
documentation targets resolve; the updated diagram parses as SVG. VS Code shows
before/after workspace, repository, new list/query and workflow-guide diffs.
The temporary PostgreSQL cluster reports no server running.
Manual acceptance, Next production rendering, live identity/OCR and large-volume
benchmarking remain open. Passing these focused checks does not close them.

## Short manual check

1. On Document library, enter part of a filename or known supplier/printed number
   and press Search. Try a number from a document outside the first page.
2. Switch between All, Invoices and Purchase orders. With more than 10 matching
   files, use Next then Previous. With 10 or fewer, Next should be disabled.
3. Open a file, change the search, then show an earlier quotation. Details and
   saved history should remain readable; nothing should become confirmed.

These are instructions for Rand, not recorded manual passes.
