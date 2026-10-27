# Administration batch 2: Activity history

October 26, 2026. The user accepted the People & access sample review and explicitly
approved proceeding to Activity history. N17 is implemented and checked; its own
manual acceptance remains open. Real Admin sign-in remains unverified.

## What changed

The existing Admin-only `/organization/audit` page now shows **What happened,
Who, When, Result**, with a keyboard-operable Details control. Known action codes
use plain wording. Failed/denied actions do not claim completion. Permission to
download or remove a file is distinguished from a completed operation. Successful
comparison activity does not imply a match or change a finding's decision.

Details retains the exact action, actor/target types and IDs, outcome, reason,
request ID, event ID, original timestamp, and saved Before/After strings. It also
shows UTC time. Arabic remains literal text. The audit API supplies no actor name;
the screen uses the recorded type and ID without inventing one. Unknown action,
actor and outcome codes remain readable in Details with honest fallback labels.

Displayed times use `Asia/Dubai` and English digits. Invalid or zone-less times
are unavailable, not silently interpreted in the browser's local timezone.

Page reads remain GET-only, server ordered, 100 rows per page and offset at most
10,000. Refresh returns to the first page; Try again retries the failed page.
Loading/failure/empty states differ. Superseded reads cannot show another account's
records. The UI explains that incoming events can shift offset pages and that the
page cap does not delete older history. No backend, schema, identity, permission,
invoice workflow, saved history or storage control changed.

## Review and fixes

The separate Progress and Quality Reviewer initially found no material safety
issue. Two wording corrections were implemented:

- Generic reconciliation codes also cover older quotation comparisons. Labels
  now say **Comparison**, without relabelling that history as invoice work.
- `quarantine.cleaned` records one item, so its label uses singular **upload**.

Self-review corrected a test fixture's TypeScript type and explicitly distinguished
unknown results, invalid dates and permission events. Final independent review
confirmed the wording fixes, both diagram checks and no remaining findings.
The reviewer also caught a minor article typo, now corrected. Manual acceptance
and real Admin/API integration remain open.

## Fresh checks

- **81 focused Vitest tests across five files:** 60/60 in `tests-final.log`, then
  21/21 in `tests-bounds.log` replacing the prior 20 Activity tests, plus 20/20 help
  tests. They cover exact evidence, inert script-like text, Arabic, actor fallback,
  UAE/UTC dates, outcomes, failed retries, stale organizations, loading controls,
  malformed responses, Staff denial and the page-101 boundary.
- **3/3 Node preview-adapter tests:** bounded pages, unchanged existing events,
  read-only sample additions, unchanged unrelated requests and failure handling.
- **TypeScript and scoped ESLint:** pass after fixing the fixture type. The
  existing Vitest future-config-loader warning remains; no tests skipped.
- **Documentation:** VitePress build passes, all 261 checked links resolve, and
  both SVG diagrams render without out-of-canvas text. The existing large-chunk
  build warning remains. All 1,314 baseline files are retained (11 scoped changes,
  15 additions); meaningful before/after diffs are open in the existing VS Code.
- **Seven browser checks:** actual components with the isolated fictional API in
  Dark/Light at desktop and 390px; original Details/UTC and native keyboard opening;
  Arabic evidence and no horizontal overflow; real local Staff denied the audit
  route. Zero browser API writes and zero recorded runtime exceptions.

The first browser assertion wrongly assumed the newest sample actor was Human;
it was corrected to inspect all actor rows. The first native-keyboard attempt
omitted the character event from the CDP Enter sequence; the corrected check
passed. The three completed desktop checks were retained while the four remaining
checks ran separately. Screenshots were refreshed after the illustrative fixtures
were placed first. These are harness corrections, not hidden application passes.

Backend tests and broad application builds were not repeated: this batch changes
read-only presentation and client state, with the backend contract inspected.
Earlier N16 backend results remain historical. Evidence and captured baseline:
`outputs/admin-activity-20261026/`.

## Sample boundaries and manual check

Open [Sample Activity history](http://127.0.0.1:3430/organization/audit).
The existing sample server was not restarted or reset. A read-only adapter adds
four fictional scenarios and pages the sample server's full response. It never
saves those examples or connects to the retained API. Earlier sample-only action
codes stay unknown with their original Details intact. This simulation does not
verify live Admin identity, backend paging performance or PostgreSQL integration.

1. Find Successful, Not allowed and Failed rows. Check that their wording does
   not say a blocked action succeeded or a comparison matched.
2. Open Details by mouse and by focusing a row and pressing Enter. Check the
   saved values and UAE/UTC times. The unknown sample includes Arabic text.
3. Switch Dark/Light and narrow the window. Check that labels and buttons stay
   readable. Refresh should return to the newest records.

People & access is manually accepted for the sample review. Activity history
awaits your acceptance. Document storage redesign, real identity/permissions
acceptance, OCR/processing, report/export, deployment and final end-to-end scenarios
remain separate open work.
