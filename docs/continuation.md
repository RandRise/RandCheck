# Continuation checkpoint

## RandCheck name approved — October 28, 2026

Rand approved **RandCheck**, with GitHub repository name `randcheck`. Visible app
branding, browser title, README and guide branding now use this name. Keep the
existing folder, package identifiers, .NET namespaces, storage keys and historical
records stable. This is a name change only; the invoice/PO workflow is unchanged.
The prior milestone is committed as `003b45c`. GitHub returned 404 for
`RandRise/randcheck`; upload is still pending repository access and authentication.
Do not restart Docker or heavy services for this rename or upload.

Rename verification: 36 existing UI tests, targeted ESLint, seven content checks
and diff whitespace checks passed; independent reviewer found no material issues.
VS Code before/after diffs shown. No fresh browser/build run; prior screenshots
remain historical. See the October 28 follow-up in the local-delivery task record.

## Current milestone completed; upload requested — October 27, 2026

Rand explicitly closed the current milestone and requested upload to GitHub,
followed by shutting down Docker and heavy project services. This supersedes the
earlier publication deferral and open-ended verification plan. Keep the software
unlicensed. Completion means the current local-development milestone; unrun
checks below remain limitations, not passes. No production readiness is claimed.

Latest Python CI `77e67e82e923` / source `8a6f2dd6979a` passed with Pillow 13:
338 application, one native, five OCR and 153 pytest-native tests, zero skips,
plus tooling checks. Cleanup passed. All seven separate fixture tests passed.
Updated Python and documentation audits report no known vulnerabilities; the
earlier web-app and .NET audits passed. Original failures remain recorded.

Follow-ups: repeat the three Arabic/mixed inputs against the updated packaged
runtime; build/read the final guide after dependency/editorial changes; hosted
CI, real Entra and deployment verification. Earlier Arabic checks used the prior
runtime. These follow-ups are outside the owner's closed checkpoint.

Docker Desktop, web 3310, API 8310, Python 8010 and PostgreSQL 55623 are stopped.
Saved data remains. The small Admin preview is kept to preserve in-memory state.
Do not restart heavy services for upload. GitHub identity: RandRise. No local
remote exists; the project repository is not yet accessible. Existing Git CLI
credentials returned HTTP 401. Prepare the commit, then complete upload using
an accessible repository and authenticated connection; never assume push success.

The following sections preserve earlier checkpoints and their original states.

## Final dependency repairs — October 27, 2026, late evening

Completed web CI: 560 Vitest + three Node cases, lint and Locked build. Completed
Python CI `8eff789f8a96` / source `366ef8024fd9`: 338 application, one native,
five OCR and 153 pytest-native cases, zero skips; tooling tests also pass.
Fixture CI `318574c123b1` passed all seven cases. Their cleanup passed.
Do not repeat completed backend suites, demo restart/reset or the full web suite.

P1 finishes the earlier first Python-organization slice. Models and safe errors
are separate; OpenAPI and 102 error-response comparisons are identical. The same
43 tests pass on host and Linux. Independent review cleared P1. See its report
and `outputs/local-completion-20261027/python-organization/`.

The final audits found documentation dependencies and Pillow need security
updates. .NET and web-app audits pass. Documentation now uses Vite 6.4.5,
Vue 3.5.44 and source-map-js 1.2.2, with a zero-finding audit. Pillow is pinned to
13.0.0 and its strict runtime guard/test updated. Frozen generators stay unchanged.
Updated Python CI is active from source `8a6f2dd6979a`, log
`outputs/local-completion-20261027/dependencies/ci-python.log`. Finish its actual
OCR/runtime evidence, audit, language recheck and docs build, then independent
D1 review. Do not describe the earlier Pillow 12 run as verification of 13.

Credential decisions: seven residual patterns independently reviewed as synthetic
or protocol property names, plus 21 exact historical hash/rule/line matches to
earlier reviews. Decisions are recorded in
`docs/security/local-delivery-secret-review-decisions-20261027.json`.
Initial scan failed on 52 binary Edge-cache files named `.log`; scanner remains
fail-closed. `l3/prepare-scan.py` now stages actual storage/export/journey/L1/L2/L3
logs with hashes. Refresh after CI, add dependency logs, and run final scan with
the exported Locked bundle. Finish inventory/notices, final guide/status records,
VS Code diffs and reviewer. Keep unlicensed; no publication.

The following earlier checkpoints remain historical evidence.

## CI fixture repairs and delivery review — October 27, 2026, evening

Continue delegated local completion; do not restart completed suites. The original
full CI run `uaedi-ci-163d0117ee35` / source `fb0eb85ca2f9` ended after persistence:
Domain 217, Architecture 4, Application 441, Infrastructure 222 and API 320 passed;
persistence passed 472 and failed 24, zero skips. Its project cleanup succeeded.
This is not a full CI pass. Log: `outputs/local-completion-20261027/l2/ci-all.log`.
The narrow Linux recheck `uaedi-ci-433051a18dd3` passed all 32 selected cases, zero
skips, with cleanup confirmed. Evidence: `l2/linux-recheck/` and its log. Those
cases cover all 24 original failures plus related pilot-roundtrip scenarios.
`l2/backend-final.json` records 1,700 covered original backend cases using TRX
execution counts. The older name-deduplicated draft count is preserved/superseded.
Do not repeat the completed five suites or all persistence tests.

Test-only fixes now use explicit pilot registration for prompt safety and synthetic
embedding preparation, while public request tests retain ordinary registration.
The tenant audit-outage fixture now replaces a different reviewed invoice; exact
repeat pinning correctly remains a no-op. Changed files: CitedPromptPreparationTests,
CitedClosureIntegrationTests and ApiTenantMutationTests. Later fixes update the
schema test's dated migration count and the role-migration CLI test's hardcoded
Windows interpreter. Exactly five test files overlay the original CI image.
The independent reviewer found additional stale fixture calls, then cleared the
corrected source and backend/restart/reset evidence checkpoint. Overall L2 is open.
The isolated host build is under `l2/fix-bin/`. Its first focused run passed 15,
but six late-edited cases loaded stale compiled code. After forcing source
recompilation with unchanged captured hashes, those six passed. All 21 distinct
focused cases now pass, zero skips. See `fixture-repairs.trx` and
`late-fixture-repairs.trx`; preserve the stale-build failure log. Production
behavior/registration is unchanged. The original run's two private-server startup
timeouts also passed the quiet Linux recheck without changed limits. Remaining
source snapshot: `c6e4786d8c9d`.

L3 current README, architecture, roadmap and notices are updated. User explicitly
chose to remain unlicensed. The initial L3 documentation build passed (98.14 seconds);
later diagrams/status prose need final build reconciliation. The interim inventory
had 1,397 candidates and zero errors; refresh after final additions. All 1,395 L3
baseline files remain. The tracked
TypeScript cache was untracked, with working bytes preserved. All 16 sample binary
hashes/provenance and four sample raster contents are independently checked. All
32 documentation screenshots were visually reviewed (19 independently, 13 self);
36 candidate raster images have no EXIF. Keep Phase 7's specific redistribution
permission distinct from other packs' CC0. Fresh NuGet metadata has 102 identities;
notices distinguish host Python, lockfiles and actual CI restore. No publication.

Web CI passed in `uaedi-ci-4c7a95026c6b`: 560 Vitest and three Node tests,
zero skips, lint, Locked production build, documentation build and cleanup.
Log: `l2/ci-web.log`, source `c6e4786d8c9d`; browser scan input is exported.
Still required: Python/fixture/audit CI groups, fresh Locked bundle credential
review, final delivery evidence/diffs/reviewer. Python CI is active from the
latest source copy `366ef8024fd9`, log `l2/ci-python.log`.
Do not repeat completed backend suites. Quiet browser reopening
now passes (unchanged S6 expected case, nine API responses, zero errors/writes).
The first quiet attempt lacked its copied expected JSON; that harness gap is fixed
without changing the expected data. Its screenshot is visually checked. The demo
database and uploads are archived under `l2/demo-archive/`; guarded reset succeeded.
Documented empty startup and Empty checks passed: zero documents/reviews/cases/
comparisons/uploads, 26 migrations. The owned demo is now down, with empty volumes
preserved. Retained 3310/8310 and previews are untouched.

## L2 CI and L3 delivery work active — October 27, 2026

Historical checkpoint: the completed backend, restart/browser and reset checks
above supersede the pending statements in this section. Its attempts remain
recorded; do not rerun them merely because this older status says pending.

L1 is independently cleared. L2 fixes the unittest-only discovery gap with a
pytest-native runner (153 cases pass, plus four runner regressions) and replaces
the tests' ignored local geometry dependency with a separately pinned core-font
fixture. Historical PDFs, truth and receipts remain. Source checkpoint reviewer
found no material issue; its low docstring correction is fixed.

Full declared CI is running from `outputs/ci-source/fb0eb85ca2f9`, project
`uaedi-ci-163d0117ee35`; main log is
`outputs/local-completion-20261027/l2/ci-all.log`. It exports the Locked browser
build for the later redacted credential review. Do not rerun completed groups.
The earlier source copy `b9a4bf17c379` was prepared but not used for a CI run.

The documented demo down/up succeeded with migrations and database health. Exact
JSON comparison proves six documents, six review histories and one complete case
survived (`l2/restart-verified.json`); 518 runtime source files match current code.
The browser recheck was delayed by cold route compilation/hydration during the
heavy CI build, with no API responses before its timeout. It is not yet passed.
Only owned demo web 3308 is paused to reduce contention; restore and finish the
read-only browser recheck after CI. Disposable reset/empty restart remain pending.
Retained 3310/8310 and preview services remain untouched.

L3 baseline is 1,395 files under `outputs/local-completion-20261027/l3/`. README
current overview is being corrected; its historical section is preserved.
Rand explicitly chose **keep software unlicensed until he decides**. Record this
decision, retain sample/dependency licenses, and do not publish. Architecture,
roadmap, notices, source/binary inventory and credential review remain underway.

## L1: bounded language and Admin verification cleared - October 27, 2026

S6 is independently cleared. L1 baseline: 1,379 files under
`outputs/local-completion-20261027/l1/`. A test-executable-only Admin browser mode
is being added: existing offline signed JWT fixtures, real loopback Kestrel,
normal browser session/authorization/CORS, real repositories and the exact
reserved `document_intelligence_test` database. It does not change application
sign-in or promote the Development Staff account. The first connection attempt
stopped at test content-root setup; the explicit private manifest fixed it.
`admin-full-2` now passes ten browser/direct-HTTP assertions, including conflicts,
revocation, original download, protection and lost-reply recovery. A late-installed
fault caused the first full harness attempt to fail; its corrected recheck passes.
Focused security regressions pass: 28/28, zero skips. Cleanup confirms the reserved
test database and temporary Admin listener are gone. Three actual OCR inputs and
six Arabic browser assertions pass within their recorded limits. The two mixed
forms lose one diacritic; unknown roles, missing coverage and unchecked fields
remain explicit. Final independent recheck cleared bounded L1 with no material findings.
[Current evidence](testing/local-language-security-20261027.md).

The temporary Docker demo web container on 3308 is restored after language checks;
its API/worker/database and volumes remain. Existing 3310/8310 and preview servers
are untouched. Next check current-source restart persistence and full CI in L2,
then delivery records in L3. A likely clean-source fixture gap is under inspection:
supplier Python geometry tests reference an ignored historical PDF. Preserve old
fixtures/truth and add a redistributable geometry input rather than weaken tests.

## S6: fresh local journey in progress — October 27, 2026

N18 and S5 are independently cleared. S6 now uses a disposable current-source
demo on 3308/8308; the retained 3310/8310 workspace and previews remain. Corrected
the stale demo extraction profile and current walkthrough; preserved historical
probes. Fresh build, reset guards and 12 staged journey assertions pass, including exact reviewed versions, incomplete coverage, decision/export, lost-reply retry, malformed PDF and four layout variants. Final independent review cleared S6.
[Scope, source inspection and evidence](testing/local-journey-20261027.md).
Continue through the delegated completion ledger without a new manual-review pause.

## October 27: delegated local completion

Rand asked the implementation agent to finish the remaining project and own
hands-on browser, design and integration checks. Continue through reviewed batches
without waiting for another user manual checkpoint. Record agent verification
honestly; existing user acceptance and historical evidence remain distinct.

The target is the supported local product and reproducible repository. Real Entra,
cloud hosting and domains remain optional production-track work. Follow the
[current completion ledger](tasks/local-completion-20261027.md): Document storage, coherent report/CSV,
fresh real upload-to-export integration, bounded language/local security checks,
then clean-source delivery and documentation. This supersedes older waiting-for-
user pauses and contradictory current-status summaries below, which remain as
historical records until their overview tables are reconciled. No retained-data
reset, silent permission expansion or publication is authorized.

## S5: coherent report and CSV - October 27, 2026

Implemented under delegated completion: readable HTML and CSV from a single
selected-run/decision snapshot, an authorized version-checked endpoint, exact
string decimals, safe text encoding and account/run/version cleanup. Reports keep
actual Invoice/PO roles, one-based item rows, field review states, old quotation
roles and links between findings and decisions. Five material reviewer findings
are fixed and source-rechecked; final evidence recheck cleared S5, including the narrow report layout.

Fresh: 46 distinct web tests across latest receipts, 10 API/security checks,
2 PostgreSQL snapshot/history tests, TypeScript/scoped ESLint, 4 real browser
export/reopen checks and a final 101-row/19-column CSV verification. Prior failed
export artifacts are explicitly superseded. The retained case is unchanged; zero
API writes in the browser check. The owned API runs the tested isolated build;
its database/Staff identity and disabled workers remain. No user acceptance claim.

[Verification](testing/results-export-20261027.md). Next: fresh invoice/PO upload through worker, explicit
review, factual comparison, decision and export (S6), then bounded language/local
security and clean-source delivery. Keep optional production identity separate.

## N18: Document storage - October 27, 2026

Implemented under delegated local completion: grouped settings/protection, optional
status/removal controls, plain language, safe mutation validation, account-scoped
uncertain-write recovery and preserved settings drafts with original versions.
The reviewer identified three medium findings; fixes and regressions are in place,
and independent final recheck cleared N18. The sample cleanup also now respects
organization protection, covered by a second adapter test. Fresh: 31 web tests, two preview tests,
TypeScript/scoped ESLint and eight browser checks pass. Browser checks use actual
components with isolated fictional storage plus real Staff denial; zero network
API writes. Real Admin/database storage acceptance remains in L1. No retained data
or sample server was reset. Guide, diagrams and sample screenshots are updated.

[Verification](testing/admin-storage-20261027.md). Next: coherent saved-result report/CSV (S5), then the fresh
real upload-to-export journey. Continue without a new user-review pause.

## N17: Activity history - October 26, 2026

Approved: the user accepted the People & access sample review and explicitly
requested Activity history next. N16's real Admin identity/database gates stay
open; sample acceptance does not close them. Document storage remains separate.

Implemented: read-only What happened / Who / When / Result list, native expandable
Details, outcome-aware known labels, honest unknown-code/actor-name fallbacks,
UAE time with English digits and original/UTC details. Exact saved values and
legacy quotation comparison history remain. Existing Admin-only route, 100-row
GET paging, page limit and server order remain. Failed reads never claim empty
history; retries stay on the same page; old-account reads cannot leak into a new
session. No backend/schema, document workflow, permission or retained data edits.

The independent reviewer found no initial material defect; generic Comparison
wording, singular upload cleanup labels and a minor article typo were corrected.
Final independent recheck cleared N17 with no remaining findings. Fresh: 81 focused web tests across latest per-file receipts, 3 preview
adapter tests, TypeScript and scoped ESLint pass. Seven browser checks pass across
the fictional preview and actual local Staff-denial route, with zero API writes.
Existing sample events were retained; no preview/service reset or new server was
needed. VitePress builds, 261 checked links and both rendered diagrams pass. All
1,314 baseline files remain (11 modifications, 15 additions). Guide/screenshots
and diagrams are updated; meaningful VS Code diffs shown.

[Verification and manual list](testing/admin-activity-20261026.md). Preview:
http://127.0.0.1:3430/organization/audit. Four read-only examples are added alongside
the existing sample history. Real Admin/API integration remains open. Previous
backend test counts are historical; no broad application build or backend rerun.
Baseline/evidence: `outputs/admin-activity-20261026/`. Next: user reviews Activity
history, then agrees the separate Document storage batch. Final manual, OCR,
export, identity and deployment acceptance work remains open.

## N16: Administration batch 1 - October 26, 2026

**Manual follow-up, October 26:** the user reviewed and accepted People & access
and explicitly approved proceeding to Activity history. This accepts the reviewed
sample UI/behavior; real Admin identity and database concurrency gates stay open.
The original implementation checkpoint below is retained as historical evidence.

Approved: user accepted N15 and authorized batch 1 with "Yes, Proceed."
Implemented: one Admin-only Administration entry; shared section links on the
existing routes; People & access list/editor and focused modules. The other
sections received navigation/headings only. Activity/storage redesigns, invitations,
member search, export and first-Admin provisioning remain separate. No live
account was promoted. Staff identity, retained data and backend contracts remain.

Safety fixes: lock editor transitions during reads/writes; retain ordinary-refresh
drafts; preserve account-scoped uncertain-save recovery across navigation without
replaying writes. Version checks, removal confirmation and final-Admin protection
remain. The independent reviewer found three refresh/navigation defects; all are
fixed with focused regressions. Final recheck found no material issues. No manual
acceptance is recorded.

Fresh: 107 focused web tests across latest per-file results, TypeScript and scoped
ESLint pass; 115 backend security tests pass with test-host fixtures. Nine sample
browser checks plus a separate real Staff-denial check pass. Sample Dark/Light
screenshots, guide diagram and code diffs are available. VitePress builds and all
255 checked documentation links pass. All 1,294 baseline files remain (15 scoped
modifications, 20 additions). Live Admin authentication
and PostgreSQL membership concurrency were not verified by these tests.

[Verification and short manual checks](testing/admin-people-20261026.md). Sample Admin view:
http://127.0.0.1:3430/organization (fictional API, no retained-data connection).
The existing 3310 app remains Staff; the retained 3410 preview was not reset.
Evidence/baseline: `outputs/admin-people-20261026/`. Next: user manually reviews
People & access, then agree the next small Activity-history batch. Existing final
acceptance and integration gaps remain open.

## N15: administration design review - October 26, 2026

Authorized: inspect existing admin pages and prepare a simple proposal using
fictional company/users. Proposal prepared; implementation and local Admin
provisioning are not approved by this checkpoint. Current Staff identity,
services, saved documents, roles and audit records are unchanged.

Recommended structure: one Administration sidebar entry opens People & access;
Activity history and Document storage use section links on the existing routes.
No extra dashboard landing page. List/editor first, readable activity second,
grouped retention/protection/removal controls third. Existing confirmation,
concurrency, last-Admin and history protections stay mandatory. Invitation emails,
directory lookup and uncapped member search are separate proposals.

[Design brief and labelled sketch](design/admin-workspace-20261026.md).
The sketch is an illustration, not a live Admin screen or evidence of permissions.
Independent review found no remaining issues after adding Active access to the
sketch. Static rendering passes; 284 documentation links resolve. All 1,292 baseline
files remain (3 documentation edits, 2 additions). Diffs/sketch opened in VS Code.
No application tests or builds ran for this documentation-only batch. Next: user review of this layout, then
batch 1 only if accepted. Earlier manual and integration gates remain open.

## N14: web security dependencies - October 26, 2026

Approved: resolve the web dependency findings while preserving the workflow.
Next.js/config 16.4.3, Vitest 4.1.12 and compatible affected dependencies are
installed. Fresh web audit: 8 findings to 0, including dev dependencies. No
application rules, permissions, API contracts or retained data changed.

480 Vitest tests pass across latest per-file results after correcting a stale
retention role fixture (initial 475/480; focused rerun 25/25). All 3 Node contract
tests, TypeScript and full ESLint pass. Webpack and default Turbopack production
builds pass. Only owned web3310 was restarted; API/Python/database stayed running.
Live browser retry passes five workflow views plus Library/Questions and Staff
admin denial; zero API writes and the saved case response unchanged. All 1,291
baseline files remain (7 scoped modifications, 1 addition). Final independent
review found no material issues; all 280 checked documentation links resolve. Manual acceptance
and earlier integration/export/deployment gates remain open. Root AGENTS.md and
generated build-path settings are preserved; Next's nested managed heading is H2.

[Scope, receipts and manual checks](testing/dependency-security-20261026.md).

## N13: sidebar icons and visible phase progress - October 26, 2026

Approved: add sidebar icons and distinct current/saved/upcoming invoice phases;
explain admin access/JSON history and prepare final fresh-document test scenarios.
Implemented: Lucide outline icons keep existing labels and permissions. Current
phase numbers are purple, saved setup green with a check/text, upcoming grey.
Saved setup never claims a match or completes results just by opening them.
The current Staff identity correctly lacks admin links. Admin dashboard design
and S5 report/CSV remain separate; JSON is the existing technical review history.

73 distinct focused tests pass after correcting one new test's expected conflict
message; TypeScript and scoped ESLint pass. Five live browser views pass, with
Staff administration denied, no API writes and the saved case unchanged.
Independent review found no remaining material issues after correcting the
checklist's AED gate and quantity wording. All 1,286 baseline files remain;
277 documentation links resolve. Manual acceptance remains open.
Audit found 8 issues in existing dependencies, including
a critical Next.js advisory; dependency remediation is open before deployment.
Lucide is not identified in that audit. No existing dependency version changed.

[Batch evidence](testing/navigation-progress-20261026.md).
[Final fresh-document checklist](testing/final-user-acceptance.md) is a plan, not
recorded acceptance. Real identity, upload/OCR, report/export and deployment gates
remain open. Preserve current services, retained data and earlier evidence.

## N12: case questions, review search and spacing - October 26, 2026

Approved from the user's screenshots: improve page actions, large review selection
and touching headings while preserving the grey theme and simplified workflow.
Implemented: action links styled as buttons; stacked headings in both History and
embedded results; a case-specific question entry with one explicit action for all
currently pinned reviews; optional filename search with 10-review pages that replace
rather than append. Selected versions, old quotations and saved history remain.

101 focused web tests, 2 real PostgreSQL checks and 14 cited API tests pass. Scoped
ESLint/TypeScript and API build pass. Live Next/API/Python/PostgreSQL preparation
of the synthetic pair and distant-review search pass; the saved case is unchanged.
Eight browser views have no measured horizontal overflow or runtime exceptions.
The reviewer found a saved-source panel issue; it was fixed with regression coverage.
Independent closure review found no remaining material issues. All 1,276 baseline
files remain; 18 scoped files changed and 10 were added. Manual acceptance remains
open; full question-answer generation was not part of this live browser check.

The local manual workspace, API, Python service and test database remain available.
Python was started for case-source preparation; upload workers and paid generation
remain off. Real identity, upload/OCR/export/deployment gaps and other picker scale
work stay separate. [Findings, evidence and manual checks](testing/questions-polish-20261026.md).

## N11: readable test identity - October 23, 2026

Approved: clarify the known development actor in decision history. Implemented:
Recorded by Development user (test identity), with the original ID retained in
its hover title and saved records. Other IDs remain visible; no person's name is
guessed. This is a presentation-only change, with no backend or permission edits.

40 focused tests, scoped ESLint and TypeScript pass. The browser attempt timed
out at Edge startup; the alternate connector has no available browser. Fresh
visual/manual acceptance remains open. Independent review found no material
findings. All 1,273 baseline files remain: 5 expected modifications and 3 scoped
additions. All 264 checked documentation links resolve. The isolated manual app/database started for the user remain running;
N10's earlier shutdown record describes the end of that verification batch.
Other-user name lookup, invoice-picker paging and earlier integration gaps remain
separate. [Scope, evidence and short checks](testing/actor-label-20261023.md).

## N10: scale and live integration - October 23, 2026

Approved: simulated 1,000/10,000-file library checks, real database/API measurement,
and live browser/API workflow verification in isolated data. Preserve preview state
and history. Docker is unavailable; use bounded installed-runtime checks instead
of treating the old quotation/adapter launcher as current evidence.

Verification tools and evidence are implemented. Completed: both library simulations,
126 real API performance requests, live Next/API library paging and supplier search,
and a real saved comparison using the exact seeded reviewed invoice/PO versions.
The result stayed 12 passed + 1 differed after a decision and reload; stale retries
returned 412. Two native PDF HTTP probes returned expected Partial results and
133 focused Python tests passed. These are isolated synthetic data with Development
identity, not real sign-in, uploads or Arabic OCR. The first cold API request took
5.2 seconds; 10,000-file warm scenario medians were 195-406 ms. The invoice picker
still loads the full work list. No production performance claim is made.

Initial independent assessment found no material issue in tooling/evidence. Owned
validation services and database are stopped; no Python service remains. All 1,259
baseline files remain, with only 3 checkpoint/guide files changed and 14 scoped
additions. Generated Next settings match their captured baseline. All 261 checked
document links resolve; verification-script syntax and critical Python lint pass.
The optional desktop screenshot retry timed out at Edge startup; this does not
replace the completed live-library receipt. Final independent recheck found no material findings. User manual acceptance
remains open. Retained preview was not reset; it was unreachable after
conversation interruptions. Live identity, full upload/worker processing, scanned
OCR, original downloads, exports and deployment remain unverified. Other design
proposals and milestones stay separate.

[Scope, findings and evidence](testing/scale-integration-20261023.md).


## N09: results stage - October 23, 2026

Approved: keep the three-stage workflow and fix results accuracy and clarity.
Implemented: both pair policy versions now report separate passed/differing/missing
counts; result cards show saved values/evidence and one review action for a gap.
Decisions use scenario reasons and never alter check outcomes. Legacy findings and
preview simulations remain clearly separate. No retained data was changed.

Fresh checks: 11 application +12 API contracts +3 real PostgreSQL/API scenarios,
166 web tests, TypeScript and scoped ESLint. The API selects the exact older
reviewed invoice even when a newer review exists; decisions and stale retries
preserve frozen results. Twelve read-only browser views replay captured real-API
fixtures for both themes and sizes; this is not live browser-to-backend wiring.
Three independent presentation findings were fixed with regression tests; final
reviewer recheck found no material issues. All 1,248 baseline files remain; 257 doc links
resolve. TypeScript refreshed its generated build cache; other changes are scoped.
Temporary database stopped. Manual acceptance and live identity/OCR/Next
production/export integration remain open. Other proposals remain separate.

[Batch findings, evidence and manual checks](testing/results-stage-20261023.md).


## N08: library search and 10-file pages - October 23, 2026

Rand approved 10 files per page, filename/supplier/invoice-number/PO-number
search across the accessible library, and All/Invoices/Purchase orders filters.
Implemented with a scoped no-store .NET endpoint/repository and separate web
list, service and hook. Quotation opt-in, bookmarks, saved reviews/history and
processing recovery remain. No files/data deleted. Search never confirms values.
Current review corrections replace extracted values; cleared values are excluded.
The library no longer downloads all summaries in the production list path.

Fresh results: 7 PostgreSQL/API, 83 web and 24 preview tests pass (114 distinct).
Final changed web/guide rerun: 37 passed within that total. TypeScript/scoped
ESLint pass. Four read-only browser views and one 23-file intercepted fixture
pass without application writes or runtime exceptions. A temporary isolated
PostgreSQL cluster was stopped after testing. The retained preview was not reset;
its tooling-only adapter simulates search and explicitly labels this limitation.
Independent final assessment found no material issues. All 1,234 baseline files
remain, with 15 expected changes and 14 additions; 253 documentation targets
resolve. Guide edition 23 and library diagram
explain the new search. [Evidence and short checks](testing/library-search-20261023.md).

Manual acceptance and live identity/OCR/Next production integration remain open.
Large-volume performance is not benchmarked; substring searches/counts still cost
work on the server, and pages are not a frozen snapshot of concurrent changes.
Other milestones, including administrator design, Python organization and reviewed
export, remain separate. Earlier N07's 25-file suggestion is superseded by the
approved 10-file N08 implementation; earlier records below are historical.

## N07: quotation visibility in Document library - October 23, 2026

Rand chose to hide quotations while keeping history accessible. Implemented:
the library hides known quotation types by default; Show earlier quotations
includes them in the searchable list. Counts reflect the current view. Selected
or bookmarked quotations retain their reading view, saved reviews and history.
Unknown types, invoices and POs remain visible. No files or data were deleted.
The quotation samples still support legacy tests, demos and historical evidence.

79 focused tests (59 document checks + 20 guide checks), TypeScript, scoped
ESLint and four read-only synthetic browser views pass. Independent review found
no material issues. All 1,231 baseline files remain; 250 documentation targets
resolve. Guide edition 22
and the library diagram explain visibility without claiming deletion.
[Scope, dependency findings and manual checks](testing/library-quotation-filter-20261023.md).

Large-library limitation: the endpoint returns all document summaries; filename/
ID search is local. Proposed next: server-side paging/search, 25 files per page,
and Invoice/PO/All filters. This is not approved or implemented in N07. Existing
N06/manual/integration gaps and administrator-dashboard work remain open.

## N06: choose both files before creation ? October 22, 2026

Approved and implemented: Home Start opens `/invoices/new` without a write.
Choose/upload an invoice and PO, then Continue to review creates the check,
saves both draft links and opens value review. One uploaded file stays in the
library without a case. Creation does not confirm values, pin reviews or compare.
History can only recover a matching older request; it no longer starts fresh
empty cases. Older quotations, cases, permissions and retry payloads are retained.

The APIs still save creation and each link separately. Interrupted preparation
can leave a partial draft; the same request keys and fresh reads recover it.
Changed choices are never overwritten. The reviewer identified a recovery trap
when a selected file disappears/changes type; fixed by allowing recovery of the
original case while blocking invalid draft writes. 194 focused tests pass; TypeScript and scoped ESLint pass. Four read-only
selection views and one browser-intercepted recovery flow pass without retained
data writes. Independent recheck found no material remaining issues. All 1,223
baseline files remain; 247 documentation targets resolve. Evidence: [N06 evidence](testing/pair-first-20261022.md).
Guide edition 21 includes the new timing, recovery explanation and screenshot.
Manual acceptance and live integration are still pending.

Earlier requirements still open: administrator dashboard design; staged Python
organization; S5 reviewed report/CSV; S6 integrated acceptance; real-account role
migration, identity and Arabic/OCR/extraction verification; deployment checks.
Supplier identity checks beyond the name and G02 quantity/price presentation need
separate scope decisions. Earlier refactoring/cleanup batches do not mean every
file is finished. No new specialist, backend service or data migration was added.

## Current: D03 reading-page layouts — October 22, 2026

All three authorized batches are implemented: a wider Document library with
saved-value cards and spaced recovery controls; History with numbered guidance,
grouped filters and saved case cards; Questions with sources beside the question
and answers/saved questions below. Both themes and phone stacking are supported.
The shared numbered guide describes reading order, never check completion.

[D03 scope, findings, evidence and short manual checks](testing/workspace-layout-20261022.md).
182 distinct focused tests pass (60 library/review, 70 history, 52 questions).
Final navigation rerun: 9 passed. TypeScript and scoped ESLint pass. Twenty-four
read-only sample-preview views plus two browser-only saved-answer focus fixtures
pass with no overflow, runtime exceptions or application writes. The retained
preview/data were not reset. VS Code before/after diffs are open. Guide edition 20
adds the new layouts, reading diagram and labelled screenshots.

Independent review cleared library/History and found a saved-answer focus issue.
The fix focuses the answer after an explicit load, with stale-response/frame
guards and focused tests. The reviewer rechecked the fix and final records;
no material findings remain. All 1,214 baseline files remain, with only expected
changes/additions; all 244 local documentation targets resolve. Visual/manual
acceptance and N04/G02/G03 integration gaps remain open. Next: Rand reviews the
three pages, then the administrator dashboard becomes the next design stage.
No temporary specialist agents were created; unapproved proposals stay separate.

## D03 first checkpoint (historical) — October 22, 2026

Rand accepted shared themes and authorized three layout batches: Document library
and button spacing, Earlier cases and history, then Ask about documents. Admin
dashboard work follows later. Keep the existing reviewer only.
The first batch expands the library reading area, shows saved values after an
explicit file choice, groups values in cards and separates recovery buttons from
their explanations. 60 focused tests and four read-only dark/light desktop/phone
views pass; reprocessing was not activated. Remaining two batches and independent
assessment are in progress. Evidence: `outputs/workspace-layout-20261022`.
Saved data, permissions, retry identities and historical quotations stay intact.

## Current: shared dark and light themes — October 22, 2026

Rand accepted the working Home look, requested dark styling and a switchable
light view, and chose to keep only the existing Progress and Quality Reviewer.
D02 now shares the accepted charcoal/purple colours across the workspace and
adds a matching pale-grey/white/purple Light theme. The existing Appearance
dropdown and preference storage are retained. Home-specific duplicate palette
tokens are removed; layouts, permissions, data and comparison rules stay intact.

[D02 scope, verification and short checks](testing/themes-20261022.md).
91 focused tests pass. Twelve read-only browser views cover Home, library and
a saved workflow at 1440/420px in both themes, with no overflow, runtime errors
or application writes. The theme persists across reloads/navigation; the switch
has a visible keyboard focus outline. Measured shared text/status pairs pass
4.5:1 and control borders pass 3:1. The guide is edition 19 with fresh labelled
sample-preview screenshots. Independent review found no material findings and
confirmed all 1210 baseline files remain, with only the six expected existing
changes and four additions. Browser coverage is limited to three sample screens;
full accessibility and Next production rendering were not verified.
D02 manual acceptance and existing N04/G02/G03 integration gaps remain open.
No preview restart/reset or retained-data changes occurred. Home's visual design
is accepted; individual earlier manual checks are not inferred from that feedback.

## Current: approved Home design implemented — October 20, 2026

Rand accepted the Mosaic-inspired Home preview and explicitly approved applying
it to the working Home page. D01 now implements the grey/purple start panel,
ordered step cards and neutral saved-review list. Styling is scoped to the
mounted Home screen and its surrounding frame; document/result styling is not
changed. Light mode, account controls, permissions, legacy routes, loading,
errors, pagination and safe creation retries remain.

[D01 implementation, fresh evidence and manual checks](testing/home-design-20261020.md).
75 focused tests pass; TypeScript and scoped ESLint pass. Read-only browser checks
at 1440/420/360px in dark and light pass without overflow, runtime errors or
application writes. A keyboard check reaches Start with a visible outline;
navigation restores the library's existing palette. A temporary DOM-only long
Arabic-name/reference fixture fits at 360px. Self-review fixed a CSS border
override. Independent assessment found no material code/design issue; its guide
wording correction now distinguishes illustrations from the actual sample-preview
screenshot. It independently verified the six expected changes and four additions
against 1206 baseline source files, with no missing/unexpected changes.
The guide is edition 18. Working-page manual acceptance remains pending.
No retained data or preview server was reset. N05 manual acceptance and the
N04/G02/G03 real-comparison/integration gaps remain open.

## Current design preview: Mosaic-inspired Home — October 20, 2026

Rand rejected the flat grey preview, chose Mosaic as a reference and approved
preparing a dark grey/purple Home preview. The static proposal now has a tinted
start panel, document-pair illustration, three step tiles and a separate saved
review list. Organization context stays visible on mobile.
The [design record](design/home-dark-refinement-20261020.md) explains the scope.
Visual acceptance and implementation in the working app remain pending.
No app files, permissions, saved data or comparison rules changed.
N05 manual acceptance and N04/G02/G03 integration gaps remain open.
Fresh static renders pass at 1440, 768, 420 and 360px with no horizontal overflow,
runtime errors or external resources. A button-outline contrast issue found
during self-review was fixed. Final text contrast is at least 4.74:1 and tested
control outlines at least 3.14:1. All 223 checked local document targets exist.
Independent review found no material issue blocking presentation and confirmed
only the five intended files changed against 1206 captured source files.
Evidence: `outputs/home-mosaic-20261020`. This is static-preview verification,
not interactive accessibility or real application acceptance.

## Design revision: layered greys — October 20, 2026

Rand rejected amber and requested grey shades, invoking frontend-design.
The static Home proposal now keeps charcoal surfaces with a light grey primary
button, raised grey secondary buttons and quieter navigation/dividers. The
accepted layout stays unchanged. Palette review and app implementation remain
pending; no app files or saved data changed. See the
[current design record](design/home-dark-refinement-20261020.md).

## Design accent review: grey accepted, amber proposed — October 20, 2026

Rand approved the dark grey surfaces and requested a better button colour.
The static Home proposal now uses muted amber accents; its layout and grey
palette are unchanged. Accent acceptance and application implementation remain
pending. The teal proposal is retained in `outputs/home-amber-20261020/before`.

## Design colour revision: charcoal and teal — October 20, 2026

Rand liked the Home layout and requested dark grey with some colour. The
[static proposal](design/home-dark-refinement-20261020.md) now uses charcoal/grey
surfaces and teal actions, keeping the layout unchanged. The palette awaits
review; application implementation is still pending. Earlier navy screenshots
remain evidence of the first proposal. No application files or saved data changed.

## Design review started: dark Home refinement — October 20, 2026

Rand asked to start design work and chose to keep the dark workspace and refine
it. The first [Home layout proposal](design/home-dark-refinement-20261020.md)
keeps the current palette and navigation, reduces repeated copy and improves
the start action and saved-work rows. A labelled static mockup is available
from that document. Dark styling is approved; layout acceptance and application
implementation are pending. No application code or saved data changed.
N05 manual acceptance and the N04 real-comparison gap remain open.

## Current: simpler Document library — October 20, 2026

N05 follows Rand's approval to simplify the library and remove competing routes.
The library now searches and opens saved files in place, with read-only values,
originals and history. Upload, review-edit and comparison/question shortcuts are
removed there. Home remains the invoice-workflow entry. Recovery, permissions,
old bookmarks and quotation history remain; no retained data was changed.

[N05 scope, evidence and manual check](testing/document-library-20261020.md).
The guide is edition 14. Final focused checks pass 125 tests, including the extra
reviewer-requested display coverage; TypeScript and lint pass. Wide/narrow
synthetic browser checks pass without application writes. Independent review
found no remaining material issue or unexpected baseline change. Manual
acceptance remains pending. G02/G03 live integration and the
N04 sample-comparison limitation remain open. Other proposals stay separate.

## Current: retained preview evaluation compatibility — October 20, 2026

N04 addresses Rand's Show results error. Both completed reviews were present;
the current v2 request was incompatible with the retained preview's v1 fixture.
A preview-only adapter uses Unknown scope for the older simulation and preserves
its incomplete-check warnings. The production request and workflow stay unchanged.
The preview now visibly explains that these results do not run current backend rules.

[N04 cause, evidence and manual check](testing/evaluation-preview-20261020.md).
Fresh checks pass 52 preview tests and 26 workflow tests; syntax passes.
A read-only browser check sees the new notice without runtime errors or writes.
Independent review found no remaining material issue; its button-label correction
is fixed. The guide is edition 13. No preview restart,
reset or retained-data write occurred. Manual acceptance and G02/G03 integration
remain open, including real v2/AED, identity, OCR, export and deployment checks.
Earlier records and other design proposals remain separate.

## Current: upload/menu/type-selection fixes — October 20, 2026

Rand's manual feedback led to N03: one upload action, overlapping Other tools
links, and a purchase-order type rejection. Choosing a file now uploads it;
safe retries remain explicit. Other tools links use separate rows. The affected
retained preview check had an invoice selected as its PO. The backend rejection
was correct; new choices now filter known wrong types, and retained wrong choices
show an early warning without replacing files or discarding open review drafts.

[N03 cause, review findings, verification and manual check](testing/workflow-fixes-20261020.md).
The workflow guide is edition 12. Fresh checks pass 116 web tests and 22 isolated
synthetic preview tests; TypeScript and lint pass. Final independent review found
no remaining material issue. User acceptance is pending. No retained data was changed or preview
restarted. G02/G03 integration, actual upload/OCR, identity, export and deployment
gaps remain open. The affected saved check still needs an explicit correct PO
selection. Older checkpoints and unrelated proposals remain separate.

## Current: full workflow overview and optional Details — October 20, 2026

Rand rejected the repeated links and hidden phases in N01 and requested this
refinement. N02 source now starts with Start here on Home and keeps all 3 phases
visible on one page. The active work opens beneath its heading. Optional Details
opens a separate read-only view; Return keeps the same phase/document and checked
review intent. Home is the single saved-check destination. Historical tools remain
under Other tools. This refines the navigation, not the backend comparison policy.

[Design and acceptance](design/workflow-overview-20261020.md).
[Changes, findings, fresh evidence and manual checks](testing/workflow-overview-20261020.md).
The guide is edition 11. Manual acceptance and overall G02/G03 integration remain
open; other visual/Owner/export proposals remain separate. Earlier records below
are retained. The existing preview and its data were not restarted or reset.

Independent review identified dirty Details navigation, shared result-location
state and duplicate navigation; fixes were rechecked. A further cancelled-Back
run-URL issue was reproduced and fixed. Final independent review found no remaining
material issue in this bounded batch. The broad selection passed 230 checks;
after the last fix, 126 overlapping affected checks passed again. TypeScript and
lint pass. Final evidence and remaining limits are in the N02 verification report.
Real browser rendering and a limited
Back/Forward/focus check now work, using synthetic data. They do not close the
older preview's backend-policy gap or identity/OCR/export/deployment gaps.

## Current: numbered invoice workflow - October 20, 2026

Rand approved simpler navigation. Source now has Home with saved checks and one
numbered invoice/PO workspace: Add documents, Check details, See results. Both
reviews, evidence and decisions stay in that workspace. The factual v2 backend
policy removes the routine scope question while retaining old policies/results,
AED evidence requirements and missing-check limits. Exact inspected reviews are
pinned explicitly; uncertain saves require read-only recovery.

[Plan and acceptance boundaries](design/one-review-navigation-20261020.md).
[Fresh verification, findings and manual checks](testing/navigation-flow-20261020.md).
G02/G03 source slices are implemented; manual and full live integration acceptance
remain pending. The workflow guide is edition 10. Earlier visual dashboard ideas,
Owner and export remain separate. No retained data or accounts were migrated.
Port 3410 was not restarted/reset; its older simulated backend cannot verify v2.
The temporary database used only disposable test data. Browser rendering failed,
so no screenshot or visual pass is claimed. Earlier identity/OCR/export/deployment
gaps remain. The reviewer found and fixes address navigation, closed-case access,
frozen source navigation and stale/concurrent result-token handling. Final source
review has no remaining material finding. Fresh receipts record 269 web component
checks, 3 retry-contract checks, 69 domain, 67 application, 51 API and 7 database
checks passing; TypeScript and affected lint pass with no lint warnings. Guide
links pass and the documentation build has only a chunk-size advisory.


## Current: Staff and Admin only — October 19, 2026

Rand clarified and approved removing the old roles completely. A02 replaces the
earlier A01 naming-only solution. Source now uses Staff and Admin in the domain,
API, permissions and account screens. Admin includes all Staff document powers.
Active/disabled is account status; there is no separate administration checkbox.
The database migration requires an exact, explicitly reviewed account inventory
before changing a populated database. Historical audit text is retained.

No real accounts were migrated: configured localhost:55432 is unavailable.
No document list was cleared and the running in-memory preview was not restarted.
A preview-only bridge projects equivalent old Operator sessions, rejects limited
legacy sessions and blocks membership operations on the old server. It is not
real identity or migration evidence. New preview instances use the new contract.

[A02 evidence, review findings and manual checks](testing/two-roles-20261019.md)
records verification and remaining limits. Captured before files and batch diffs
are under `outputs/two-roles-20261019`; relevant diffs open in the existing VS Code.
Manual acceptance, real-account inventory/approval and deployment remain pending.
The workflow guide is edition 09. G02/G03, dashboard, Owner and export remain
separate; earlier OCR, identity, integration and deployment gaps remain open.

Fresh checks: 246 web, 115 API, 37 PostgreSQL migration/access/retry cases and
33 synthetic preview cases pass. TypeScript, affected ESLint and critical Python
lint pass. The VitePress build passes with a bundle-size advisory. Independent
review findings were fixed and rechecked, including approval identity binding,
uncertain migration outcomes and isolated security test inputs. Source assessment
has no remaining material finding. This is source verification, not live cutover.

## Earlier checkpoint: one Staff role for document work — October 19, 2026

Rand approved combining Viewer, Reviewer and Operator into one everyday role,
with naming delegated. Staff now covers daily document work in new-account setup.
It uses the existing Operator permission contract. Administration remains separate.
Existing Viewer/Reviewer accounts keep their limits and their original saved role;
an administrator must explicitly choose and grant Staff access to change them.
No account migration, bulk promotion or document/history change was performed.

New accounts require an explicit choice of Staff or No document access. Limited
roles appear only when maintaining an existing account with that role. Account
labels, English/Arabic help and preview test labels reflect the distinction.
The backend still accepts historical role contracts; this batch consolidates the
normal product setup, not the stored role enum or API into one value.

Fresh focused tests pass 93 web cases and 21 backend role-matrix cases. See
[verification, review status and short manual checks](testing/staff-role-20261019.md).
The batch diff is opened in the existing VS Code window; before copies and receipts
are under `outputs/staff-role-20261019`. Manual acceptance is pending. No preview
restart or data reset was performed. The workflow guide source is updated; its
earlier static build is not refreshed. Owner, dashboard and G02/G03 remain separate.
TypeScript and affected lint exit 0; five source documentation pages and four new
links pass. Independent source and documentation recheck found no material issue.

## Current: reason dropdown — October 19, 2026

Rand approved changing the recent reason buttons into a selection list. The normal
review form now starts with Choose a reason; only Other opens Your explanation.
Shared styles stay small for the future redesign, which is not part of this batch.
Nothing is saved by selecting a reason. Existing values, history, permissions,
Arabic text and retry behavior are preserved. Guided-view suggestions are unchanged.

The reviewer identified an accessible-label mismatch and an empty Other editor
that survived discard. Both are fixed, with focused discard regressions. See
[fresh verification and manual check](testing/reason-dropdown-20261019.md).
Before copies and receipts are in `outputs/reason-dropdown-20261019`; the component
diff is opened in the existing VS Code window. Manual/browser acceptance is pending.
Workflow guide source is updated; the earlier static guide is not rebuilt. No
document reset or preview restart was performed. G02/G03 and earlier gaps remain.
Fresh checks pass 93/93 tests, TypeScript and affected ESLint exit 0. Independent
recheck found no remaining material issue; source links pass. The running sample
preview serves the changed component, but browser/manual acceptance is still open.

## Current: reason choices — October 19, 2026

Rand approved short choices for required review reasons. The missing-field form,
normal change/clear controls and added-field clearing now use a shared ReasonInput
component with action-specific constants. Presets fill only the draft; Other
focuses a custom explanation, clearing a preset but preserving already-custom text.
No reason is preselected. Explicit save, existing permissions, ETags, audit history
and source-evidence requirements remain intact. Guided review's earlier reason
buttons are unchanged. Payment-term placement and G02/G03 remain separate.

Fresh focused verification passes 68/68 web tests; TypeScript and affected ESLint
exit 0. Independent source review found no material issue. See
[verification and short manual check](testing/reason-choices-20261019.md) and the
[workflow guide](workflow-review.md#reason-choices-less-typing). Exact before files,
diffs and receipts are under `outputs/reason-choices-20261019`. Manual/browser
acceptance is pending. No preview reset or restart is required; the guide's
Markdown source is updated without rebuilding the earlier static site.

## Current: named fields without custom settings — October 19, 2026

While manually inspecting the review screen, Rand approved removing Custom field
settings. The normal addition form now offers only named fields. It derives value
types from the existing catalog, fixes document fields at occurrence 0 and keeps
the line-number control for item fields. Existing custom values, review history,
backend APIs, permissions and evidence requirements remain unchanged. Payment-term
placement remains a proposal. This UI batch does not complete G02/G03.

See [the change and manual check](testing/named-fields-20261019.md) and
[updated guide](workflow-review.md#adding-a-missing-value-fewer-settings).
The existing dirty tree was captured before changes; source diffs and receipts are
under `outputs/named-fields-20261019`. Manual acceptance is pending. No document
reset or application-preview restart is part of this change.

Fresh focused verification: 63/63 web tests pass; TypeScript and affected ESLint
exit 0. The reviewer caught a type issue in a new test query, fixed before these
static checks. Guide validation covers source links, not a rebuilt static site.

## Current: AED-only invoice checks — October 12, 2026

Rand chose AED only, with no conversion service or scheduled job. This bounded
batch adds an application admission gate for new evaluations of saved
InvoicePurchaseOrder cases. Both exact pinned reviews must contain one explicitly
checked, evidence-backed AED currency. Rejection precedes case mutation and leaves
saved reviews/results accessible. Original values and versioned comparison rules
remain unchanged; legacy workflows retain compatibility. No migration is needed.

The UI and [workflow guide](workflow-review.md#aed-only-one-currency-less-work)
explain the gate. [Verification and limits](testing/aed-only-20261012.md) distinguish
source changes from live behavior. The existing sample preview is not restarted
because its document list must be kept; its simulated backend remains the already
loaded version. G02/G03, supplier identity changes, Python refactoring and earlier
manual/integration acceptance remain open. Dashboard/role proposals stay separate.

Fresh focused evidence: 64 application, 48 HTTP contract, 33 web and 21 synthetic
preview passes; TypeScript and affected lint exit 0. Reviewer findings in new test
fixtures were fixed and rechecked. HTTP tests use fake repositories, not a real
database. Manual acceptance remains pending; exact receipts are in
`outputs/aed-only-20261012`.

## Current: editor diagnostics — October 12, 2026

Rand asked about roughly 459 VS Code Problems and explicitly cancelled clearing the
document list. No document reset/deletion or preview restart was performed. Two
archived before copies were open from our earlier diff workflow. A reproduction
using VS Code's TypeScript 6.0.3 produced 457 diagnostics for those copies; the
actual web project passed a fresh TypeScript check with zero diagnostics.

Workspace settings now treat only `outputs/**/before/**` as reference text and
identify the intended web SDK, .NET solution and Python environment. Generated
Python scan paths are excluded; actual application checks remain enabled. See
[the findings and editor refresh steps](testing/editor-diagnostics-20261012.md).
The live Problems counter after refresh is not independently captured. This
editor-only change does not advance G02/G03 or establish G01 manual acceptance.

## Current: approved simpler review, first implementation batch — October 12, 2026

Rand approved the low-effort review direction in the conversation: suggested values,
simple actions and one invoice/PO flow with backend-managed checks. The bounded
G01 implementation changes value checking first. Saved pair review links open the
one-value view; edits and reasons appear on demand. “I can't tell” is read-only
until explicit “Leave this value out” records the existing Cleared action. Missing
and excluded values remain visible; no confidence-based confirmation or complete
match claim is introduced. Current details and gates: [G01 report](testing/guided-review-20261012.md).

The [batch plan](design/low-effort-review-20261012.md) separates G01 from the next
backend policy/compatible persistence batch and the combined pair workspace. Scope
and Choose/Use reviewed-version controls remain in the current application. The
old scope-question proposal is superseded as future direction, not yet removed
from code. Dashboard and Owner/Staff proposals remain unapproved. Manual acceptance
of G01 and earlier quotation changes is pending. Preserve the earlier records below.

G01's final focused evidence is 99 distinct latest UI passes across runs (15/15 in
the final new guided test file), 17 preview contract passes, and TypeScript/affected
lint exit 0. Independent review found no material production defect; test/type
findings were repaired and rechecked. Browser automation failed to initialize, so
visual/manual and live integration acceptance remain open. Sample UI:
`http://127.0.0.1:3410/`; updated guide: `http://127.0.0.1:5174/workflow-review`.
Exact receipts, source preservation and documentation checks are in
`outputs/guided-review-20261012`. Give the short manual checkpoint before the next
substantial batch; do not mistake this first slice for completion of G02/G03.

## Current: Progress and Quality Reviewer — October 12, 2026

Rand requested a separate assessment agent and lasting reviewer rules in root
`AGENTS.md`. Use the reviewer at meaningful implementation checkpoints to inspect
approved scope, roadmap/acceptance criteria, code/design changes, test evidence,
and progress records. The implementation agent owns fixes; material findings return
to the reviewer for recheck. Ask Rand before creating any recommended specialist.
Temporary specialists do not require editing the contributor guide unless lasting
rules change. Independent review also identified older roadmap wording that could
imply approval of the UI/role proposals; a dated clarification now supersedes that
wording while preserving the historical paragraph. This governance change does not approve product proposals, advance a
product milestone, or establish application/manual acceptance. Exact before files
and this checkpoint's review evidence are under
`outputs/progress-quality-reviewer-20261012`.

## Current: new reviews exclude quotations — October 12, 2026

Rand explicitly requested invoice/PO-only new reviews, with all existing quotation work preserved. The dependency audit and bounded UI/documentation batch are in [the change report](testing/quotation-workflow-20261012.md). The illustrated workflow guide now separates fresh checks from earlier quotation cases. Fresh web case creation uses InvoicePurchaseOrder; interrupted earlier requests retain original payloads for recovery. Legacy APIs, extraction, saved cases, results, audit and permissions are retained. Other dashboard, three-stage and role proposals remain unapproved. Python organization remains separate requested work. Current verification and manual-review status are in the report; earlier results below remain historical.

## Current: illustrated workflow review guide - October 11, 2026

Rand requested a VitePress presentation that explains the current process in plain
language, illustrates it, and records changes for repeated review. The new
[Workflow review room](workflow-review.md) is the primary reading page, linked from
home, top navigation and the Product sidebar. It covers today's exact invoice/PO
steps, review/evidence/coverage limits, saved history, decisions, and the proposed
three-stage flow, home screen, roles, export and code organization including Python.

Five local SVG illustrations use made-up examples and are clearly labelled as
illustrations rather than app screenshots. Proposed screens and unbuilt export
remain marked. A review history and reusable change card separate implementation,
verification and Rand's actual approval. No approval or product behavior is changed.
The older home overview is explicitly labelled as an earlier planning baseline.

Next: Rand reads today's workflow first and identifies unclear or unnecessary
steps, then reviews proposals one at a time. Python organization remains requested
but unimplemented. Keep this guide current after each meaningful batch; preserve
previous records, show before/after code in VS Code, link findings/test evidence,
and record feedback only when Rand actually supplies it. Documentation does not
approve the dashboard, three stages, Owner/Staff, or permission migration.

Documentation-only verification and preserved before/after sources live under
outputs/workflow-review-20261011. No application tests are rerun for this writing
batch. The October 11 baseline and all of its integration gaps remain unchanged.


## Current: baseline recheck and Python scope - October 11, 2026

Rand requested fresh verification before further modifications and added Python
organization to the refactoring scope. No application changes were made this turn.
See [the fresh recheck and findings](testing/project-recheck-20261011.md).

.NET: 1,160 pass across domain, application, API, infrastructure and architecture.
Web: 325/326 on the full run; the one retention failure passed unchanged with all
six cases in its focused rerun. Timing sensitivity remains open. TypeScript,
whole-web lint, three review-request and 17 preview-adapter checks pass.
Python: 499 distinct parent cases pass across appropriate existing runtimes; five
pinned OCR cases remain unexecuted. The initial fixture dependency failure passed
in its separate interpreter; the native PDF gate also passed. CI Python lint
passes; broader lint reports 175 existing findings. The unittest-only group runner
also misses function-style pytest cases; today's broad pytest run includes them.

All 1,120 pre-existing Git-visible files were unchanged after verification. Only
this checkpoint and the new report are changed afterward. Evidence is separate
under outputs/project-recheck-20261011. Browser initialization failed twice; no
preview was started and no visual pass is claimed. No retained database, migration,
full demo, package install, deployment or persistent test service was involved.

Next: review this qualified baseline, then organize Python in bounded batches,
starting with API models/error handling and later extraction responsibilities.
Preserve test coverage and show each meaningful source diff in the existing VS
Code window. No new Python refactor, user acceptance, or dashboard/three-stage/
Owner-Staff approval is implied. Existing integration, OCR, identity, export and
deployment gaps remain open.


## Current: .NET repository organization and cache cleanup — October 11, 2026

Rand requested cleanup of unnecessary files and professional .NET organization
using the repository pattern. The existing pattern is retained: application
handlers use `IReconciliationRepository` / `IDocumentRepository`; Infrastructure
implements them with EF through scoped DI. This first .NET batch moves 40 existing
types into separate repository-contract and reconciliation feature files, removes
three obsolete combined source containers, and adds scoped C# formatting rules.
Other features and the remaining storage/extraction ports are future batches.

Self-review and the SDK/Roslyn audit confirm 40/40 moved declarations retain exact
tokens/namespaces; remaining port declarations also match. Original application
baseline: 44 pass. Final application: 80 pass; API: 66 pass; architecture: 4 pass
(150 final tests, no skips). Formatting passes. Receipts are in
`outputs/net-refactor-cleanup-20261011/gates.json`.
Existing tests, repository implementations, DI, rules, permissions, migrations,
audit behavior and data are unchanged. VS Code was reused for source and the full
captured-baseline diff. No fresh database/browser acceptance is implied.

Cleanup removed 143 disposable cache files (2,161,830 bytes) with verified paths,
contents and hashes. Test source, historical logs/results, snapshots, fixtures,
backups, databases, runtimes and reusable build outputs remain. The root ignore
rules now include `.ruff_cache/`. See [review and cleanup evidence](testing/net-refactor-cleanup-20261011.md).

Next: Rand's short .NET source-organization review before the next substantial
refactoring batch. Dashboard, three-stage workflow and Owner/Staff proposals remain
unapproved; the earlier proposed documents are not authorization to implement them.
All existing integration, Arabic/OCR, live identity, export and deployment gaps remain.

## Current: invoice code organization — October 11, 2026

Rand has not approved the saved dashboard, three-stage workflow, or Owner/Staff
proposals. Treat the design plan and handoff below as proposals even where their
older wording says they govern implementation. Rand's next request is code
refactoring first: clear files for UI components, functions, services and constants,
with readable spacing. Preserve behavior and deliver manageable reviewed batches.

The first batch splits the invoice page into a 27-line access/route entry, a
workflow hook, API/response services, types, constants, selectors and seven UI
components. Existing tests, CSS, review-return context, permissions and backend
remain unchanged. See [the refactor review](testing/invoice-refactor-20261011.md).
This is a bounded first batch, not whole-project refactoring or dashboard acceptance.

Original saved-source and first refactored invoice runs each pass 29 checks.
The final related UI gate passes 137 checks, including those 29. TypeScript and
affected lint pass; 17 preview-contract checks pass with no skips. Exact results
are in `outputs/invoice-refactor-20261011/gates.json`.
Snapshots, preservation hashes, failed setup attempts and a complete organization
diff are retained there. Review was self-review, not an independent agent review.
VS Code was reused for the extracted source and captured-baseline diff. No app or
database service was started, and no broad build or fresh browser check was run.

Next: Rand's short source-organization review before the next substantial batch.
Keep the earlier uncommitted work and all documented product/integration gaps.
No new manual acceptance or role expansion is implied by this refactor.

## Current: simpler workflow planning reset — October 11, 2026

Rand wants fifth-grade-level clarity, a redesigned dashboard, fewer unnecessary
roles, step-by-step delivery, bug and design review, and visible code review in
VS Code or Visual Studio at every implementation step. This changes the future
UI/role plan; it is not manual acceptance of the existing S3 interface.

Read [the new plan](design/simple-invoice-workflow-20261011.md) and
[the copy-ready new-chat message](handoffs/simple-invoice-new-chat-20261011.md).
The proposal is Add documents → Check details → See results, with decisions and
export inside results, one main next action, an invoice-list home and two default
account types (Owner/Staff). Exact reviewed versions, unchecked-field protections,
coverage, history and safe retries must survive simplification. Existing restricted
accounts require a deliberate mapping before any permission migration.

Current source review found mandatory case naming, separate per-document save and
Choose/Use revision controls, visible review UUIDs, technical scope wording, and
peer Documents/Compare/Questions navigation. These are design findings, not newly
demonstrated runtime bugs. VS Code was opened on the project and invoice source.
This turn adds planning/handoff documents and direction notes only; no application,
permission or retained database changes, and no fresh application tests were run.

Next: review a small dashboard/three-stage prototype with empty/error/incomplete
states before dashboard implementation. At each meaningful implementation step,
show a diff against its captured current baseline, review code and design, fix
findings, run focused checks and offer a short manual checkpoint. Preserve the
extensive existing dirty tree. Earlier test counts remain historical; manual
acceptance, customer validation and all documented integration gaps remain open.

## Earlier checkpoint: saved invoice/PO workflow — October 6, 2026

Rand's latest “Continue” authorizes S3, without implying manual S1/S2 acceptance.
The `/invoices` workflow now saves invoice and PO drafts before review completion,
reopens a stable case URL, links directly into each review and offers a validated
return. Each completed review revision is chosen and then explicitly pinned.
Comparison requires both saved pins, explicit scope and `invoice-po-1.0`.
Existing comparison cases and default legacy policy remain accessible.

Backend migration 24 adds immutable workflow identity, draft references and
append-only actor/organization-scoped retry receipts. ETags and per-role versions
protect stale writes and superseded retries; the SQL guard also rejects restoring
an earlier recorded role version. Replacing a draft removes that role's live pin,
while frozen runs retain their sources. Current drafts block document retention.
Uncertain UI writes require read-only reload, preserving unsaved selections and
disarming pending comparison intent.

Independent production source review and final focused gates pass: **324 distinct
automated checks** — domain 25, application 59, API 66, actual PostgreSQL 20,
affected UI 137 and preview adapter 17. TypeScript and affected ESLint pass.
Counts exclude reruns and historical S2 totals. Exact baseline copies and final
production/test hashes are preserved in `outputs/supplier-invoice-S3-20261006`.
Every earlier schema assertion remains, with only migration 24's required additions.
Fixture/matcher and compile failures remain recorded. Final copy-only clarification
of unchanged legacy quotation requirements passed repeated affected UI/page lint.

The bounded S2 repair now loads persisted extraction warnings. Actual fresh-scope
PostgreSQL proves exact original warnings survive evaluation, row coverage stays
unverified and affected checks remain unavailable. Other database gates verify
scoped receipts, ETag races, restricted grants, immutable history and recorded-role
version reuse refusal. A queued draft cannot be deleted; legal deletion after an
explicit clear preserves the immutable receipt. Schema/model and guarded migration
rollback gates pass. The uniquely owned cluster is stopped, its fixture database
is absent and port 25441 is closed. No retained database was changed.

One short browser smoke passes creation, both draft saves, refresh with no automatic
review pins, disabled comparison and exact invoice-review/same-case return. Console
errors are empty. Evidence is `browser-smoke.json` and `browser-saved-pair.jpg`.
For Rand's short manual checkpoint, open
[the prepared sample](http://127.0.0.1:3410/invoices?case=246b5e58-f804-4c73-98b8-ba7396387c9d),
refresh, then follow **Open invoice review → Return to invoice check**. Both saved
filenames should remain. The sample persists while the preview runs; restart and
use [the short creation steps](ui-preview.md#short-invoice-check-checkpoint) if needed.

The lightweight preview imports actual UI components but simulates total checks
and storage. It cannot prove new rule coverage, Next.js routing, durable database
writes, live identity or extraction. Rand's manual S1/S2/S3 feedback remains
unreported. Retained migration 23/24 application and role provisioning remain
pending. S1 defaults and basis/tax-currency gaps remain unchanged; report,
CSV, Arabic, R5/R6 and deployment gaps stay open. S4 has not started. See the
[S3 receipt](testing/supplier-invoice-S3-20261006.md) and
[task ledger](tasks/supplier-invoice-product.md).

## Earlier checkpoint: supplier invoice comparison checkpoint — October 6, 2026

Rand requested “Continue” after the S1 checkpoint, authorizing the bounded S2
comparison batch. This does not imply a reported manual S1 pass. The three existing
agents completed the opt-in `invoice-po-1.0` policy, independent source review and
focused verification, preserving default legacy `1.0` behavior and earlier work.
The coordinator saved exact copies and hashes of 16 backend files before edits,
and one existing schema test before its bounded expectation additions.
Final gates pass 168 distinct checks: domain 100, application 22, API 34 and
actual PostgreSQL 12. These include the pre-change baseline 49; reruns and the
manual one-case command are not additional tests. PostgreSQL uses latest per-case
results from preserved receipts. Earlier fixture/compile failures remain recorded;
production hashes are unchanged by the final test-only repairs.

Completed work includes persisted immutable check coverage and scope, frozen source
warnings/outcome, original raw values and source evidence, explicit compatible
numeric bases, permanent duplicate ambiguity, and signed invoice-minus-PO amounts.
Unchecked/cleared values and unverified identities cannot imply trusted arithmetic.
Actual disposable PostgreSQL proves precision, restricted roles, savepoint atomic
saves, stale/concurrent ETags, audit, scoped keys, history and rollback boundaries.
Review found and fixed later-INSERT appends to completed history: server-assigned
full transaction markers allow only the original atomic child inserts. Independent
source checks confirm unchanged legacy engine/projection and existing S1 files.

One short manual checkpoint is ready:
`./outputs/supplier-invoice-S2-20261006/check-policy.ps1`.
Expect matched 1260 AED, invoice 1261 versus PO 1260 with +1 AED, and unavailable
USD/AED arithmetic. This actual-engine synthetic-input receipt is not a browser,
extractor or complete lifecycle pass. S1/S2 manual results remain pending; S3's
saved invoice/PO intake is next after this meaningful checkpoint. Migration 23 is
unapplied to retained installations, which also need updated role provisioning.
Defaults and UI remain unchanged. S1 lacks explicit basis/tax-currency coverage;
future activation needs truthful evidence and explicit new-policy selection.
Legacy document-number/PO-reference alias restrictions remain documented.
Report/CSV, coherent decision cutoff, live identity, Arabic and R5/R6 gaps stay open.
See [S2 evidence](testing/supplier-invoice-S2-20261006.md) and the
[task ledger](tasks/supplier-invoice-product.md). Existing acceptance gaps remain open.

## Earlier checkpoint: supplier invoice extraction checkpoint — October 6, 2026

The meaningful S1 batch passes independent source review and final frozen checks:
200 distinct tests plus 43 subtests, zero failures/errors/skips, 6.07 seconds.
The actual evaluator passes four text helpers, 28 numeric vectors and two public
English native PDFs, each with two correct rows and exact source quotes/polygons.
Both PDFs correctly remain Partial for missing units, price basis and explicit
gross line-total coverage. A separate original mixed-PDF native geometry bridge
recovers six row fields; public Arabic PDF/OCR acceptance is still unverified.

Only two production files changed: `local_extraction.py` and `arabic_text.py`.
The opt-in `supplier-invoice-v1` profile emits `local-1.6.0`; legacy settings and
adapter identities remain unchanged. English digits are required in this profile,
with Arabic text and original evidence retained. Six unowned baseline files remain
byte-identical. No backend/UI/default configuration, retained migration, paid call,
deployment or browser action was changed or run for this batch.

The earlier failing test run is preserved: 36 failed/164 passed, caused by invalid
synthetic confidence/geometry in public test helpers. Correcting those helpers did
not alter production or frozen source truth. Final source/test/truth/evaluator
hashes match the receipts. [S1 evidence](testing/supplier-invoice-S1-20261006.md)
records commands, actual boundaries and remaining gaps.

Rand's latest instruction supersedes per-edit manual stops. One short extraction
checkpoint is ready: inspect the synthetic PDFs and run
`.venv/Scripts/python.exe samples/supplier-invoice/v0.1.0/evaluate.py`.
Expect invoice/PO roles, 1260.00 AED, quantities 2/1, prices 400.00 each and Partial.
Manual feedback remains pending; S2 has not started. Backend corrected-number/raw
fallback safety and matched adapter activation remain S2 integration dependencies.
The [task ledger](tasks/supplier-invoice-product.md) and existing R5/R6, live
identity, Arabic, history-download and deployment gaps retain their scoped status.

## Earlier checkpoint: supplier invoice product agents started — October 6, 2026

Rand requested a focused supplier invoice checking direction and authorized
planning after discussing solo customer-access constraints. The
[product plan](design/supplier-invoice-product-plan.md) and
[task sequence](tasks/supplier-invoice-product.md) define one invoice and one PO,
explicit coverage, reviewed evidence and decisions, and a faithful CSV export.
Planning is complete; the three existing agents are active on S1's input contract,
independent source audit and frozen fixture baseline. Later tasks remain pending.
Exact copies and hashes of eight existing extractor and test files were saved
under `outputs/supplier-invoice-S1-20261006` before production edits.
Rand's latest October 6 instruction is to continue without stopping on every simple
change. This supersedes the earlier per-edit manual gate. Agents are working through
one bounded S1 extraction batch, with focused tests and independent review before
one useful manual checkpoint. Routine fixes and documentation edits do not require
separate stops. The new
[input-contract draft](design/supplier-invoice-input-contract.md) is now independently
reviewed and ready for Rand's manual review. All eight baseline hashes still match;
the tester's pre-change parser baseline passed 19 tests plus 15 subtests. No new
application behavior, ordinary-table or PDF/OCR pass was established by the draft.
The active batch uses an opt-in supplier-invoice-v1 profile with a new adapter
identity; legacy defaults and historical fixtures stay intact. Record actual
changes and results before presenting S1 manual acceptance. Automated results do
not replace the milestone check.
Rand's October 6 manual feedback narrows the new numeric profile to English
digits (`0` through `9`), with Arabic characters/text retained. Contract, plan and
fixture preparation reflect this scope; Arabic-number forms need explicit review
or correction, with original evidence intact. No parser, legacy Arabic fixture or
application behavior changed for this feedback. Existing Arabic-number capability
must not be removed as part of this scope change.
Customer recruitment is
optional for prototype completion, with demand and benefit claims still unvalidated.
Begin future implementation with the supported fixture and evidence contract;
preserve the existing three-document rules and historical runs.

Existing source, tests and acceptance statuses are preserved. R5/R6, real identity,
representative Arabic line accuracy and actual history-file delivery remain open.
The October 6 short browser attempt connected to the synthetic preview, then was
interrupted after a service-unavailable scenario selection. No new acceptance pass
or screenshot was established; inspect preview state before later browser work.
No migration, deployment or broad verification is authorized by this bounded
increment. Agents must report actual S1 changes and gates before proceeding to S2.
The earlier checkpoints remain the implementation baseline.

## Earlier checkpoint: bounded access recovery — October 5, 2026

Rand asked to continue with short acceptance checks. Independent source review
found and passed fixes for out-of-order session grants, unbounded session reads
and membership-save editor ownership. Latest session reads own access; token/
response/body waits have a 15-second deadline after identity initialization.
Membership controls lock during saves and synchronous duplicate submits are ignored.
59 distinct focused checks, full nonincremental TypeScript and affected-file lint
pass. [Evidence](testing/recovery-access-20261005.md) records commands and artifacts.
No broad build/demo, backend changes, retained migration or real access change.

R5-T01 local gate passes; fresh browser/live identity acceptance remains pending.
Preview3410 restarted and is available. Creating/binding the owned tab timed out;
no browser inputs or fresh screenshots were taken. The checked local env/process
Entra settings are absent, with external tenant/test-account readiness unknown.
Do not treat the October 3 sample passes as October 5 changed-source acceptance.
MSAL's installed bridge already preserves original sign-in URLs; the earlier
baseline was clarified without adding redundant return-path storage. Next remains
the short target-environment access journeys when browser/identity prerequisites
are available; R5-T02/R6 dependencies and unrelated Arabic work stay preserved.

## Earlier checkpoint: short browser verification — October 3, 2026

Rand requested simple browser actions, without a full demo run. The current UI
preview passed sampled role gates across its seven account choices, exact reviewed
source handoff, reviewed-question/default behavior, named saved-result reopening
after refresh, native hidden-Settings validation, one Back navigation and a light
390-pixel Arabic layout check. [Evidence](testing/simplification-20261002.md#october-3-short-browser-acceptance-check-passed-for-sampled-actions)
records the exact scope and screenshots. Operator, Dark and the default viewport
were restored. Preview3410 remains available with one synthetic question and case.
No product code or retained data changed, and no broad build/demo was rerun.
This replaces the browser-connector blocker for these sampled checks; full R5/R6,
real identity/OCR, actual file hashing and exhaustive browser acceptance remain
separate. The October 2 automated results below still apply.

## Earlier checkpoint: coordinated simplification implementation — October 2, 2026

Rand authorized three agents for implementation, independent review/bug fixes,
and testing/integration after the [source audit](reviews/2026-10-02-simplification-audit.md).
The [existing execution ledger](tasks/reviewer-experience-recovery.md) records the
five increments and gates. Startup consistency passed independent review and focused
configuration/provider/preview checks; [fresh evidence](testing/simplification-20261002.md)
records the exact limits. R4-T05b passed independent review, UI/static checks,
application/API checks, and actual disposable PostgreSQL checks. Documents and
comparison Settings and Questions passed their focused gates. Feature organization
and documentation consolidation passed independent source review. Final automated
regressions/builds used frozen source: 1,001 non-persistence backend tests
and 38 selected real PostgreSQL checks passed. The Release solution and both
standalone pilot tools compile without warnings or errors. Full UI verification
has 277 distinct passing cases after two independently reviewed test corrections;
17 request/preview checks also pass. Full TypeScript, ESLint and the Locked production
Next build pass; exact pre-build generated configuration bytes were restored.
The VitePress build passes with dead-link validation enabled; repository-source
references are verified code paths rather than unpublished site routes. All automated
gates for this increment passed. Final browser acceptance is unverified: tab connections
timed out, and the final quiet inventory reported no enabled browser surfaces.
Existing uncommitted changes and Arabic work are preserved. The fresh fixture
database is absent and its owned PostgreSQL cluster is stopped; preview3410 remains
available. Migration22 is required before a real installation uses durable keyed
upload/processing retries; no retained application database was migrated.
Full demo, real identity, and later increment acceptance are not claimed complete.

## Earlier checkpoint: comparison repeat-action protection complete — September 30, 2026

R4-T05a complete. Durable actor/organization-scoped creation keys prevent uncertain
UI retries making another case; same-source pins and identical decisions are no-ops.
Explicit New comparison, Replace pinned review and Revise decision controls preserve
saved history. Stale/uncertain saves require read-only reload and retain drafts.
Backend handles unique-index-before-ETag races as stale recovery after rollback.

237 UI, 15 request/fixture, 141 API/security/audit, 12 application and nine distinct
real PostgreSQL checks passed (initial race found, fixed and affected checks rerun).
Lint, production build/TypeScript, independent reviews, all-role browser checks and
mobile/light/dark checks pass. [Evidence and limits](testing/recovery-comparison-repeat-actions.md).

Next R4-T05b: document review/upload/processing and question repeat-action hardening.
The split preserves per-task gates; these remaining gaps are explicitly inventoried
in [action repeat contract](design/action-repeat-contract.md). R5 waits for T05b.

Preview3410 is running (health PID104728 verified); one fixture restart reset old samples.
Fresh `Repeat protection — مراجعة القرارات` case `0d3b54da-99dd-4bd6-ba64-9cd97e84b0d9`
has two pins, two comparisons, two decisions on the first run. Temporary tab14 closed;
original tabs preserved, dark/default viewport restored. Screenshot:
`outputs/ui-preview/comparison-repeat-protection.png`. Disposable PostgreSQL25435 stopped
and test database dropped; no retained database migration/deployment. Migration21 is
required for real durable creation keys; legacy no-key callers retain old semantics.
Preserve unrelated Arabic/Phase7 work. Live Entra/history-download checks still pending.

## Earlier checkpoint: structured case references/categories complete — September 30, 2026

R4-T04 complete: server-generated `REC-YYYY-000001` references by organization/UTC
year; free-form names, categories, ETag-protected metadata edits and server filters.
Migration backfills legacy cases deterministically without changing UUIDs or history.
217 UI, 13 request/fixture, 134 API/security, one audit-operation, eight application
and seven real PostgreSQL checks pass; no backend skips. Lint, build/TypeScript,
independent reviews and role/mobile/theme browser checks pass.
[Evidence and deployment requirements](testing/recovery-case-metadata.md).

Next R4-T05: action-repeat rules, retry protection and explicit revised decisions.
Retain the per-task test/review/integration gate and all-role requirement. Preview
3410 remains running; fixture restart reset old temporary cases. Fresh sample case
`2ee116e9-028b-4e5b-817c-0dd82de419ea` is `September office supplies — لوازم المكتب`,
reference `REC-2026-000001`, Office supplies, no pins/runs. Original tabs preserved;
temporary tab closed, dark/default viewport restored. Screenshot:
`outputs/ui-preview/case-references-filters.png`.

Disposable PostgreSQL25435 is stopped and test database dropped. No retained database
migration/deployment. Existing real deployments need the migration plus normal private
EnterpriseAdmin API-role configuration to update name/category grants before startup.
Live Entra acceptance and manual history-download check remain pending; preserve
unrelated Arabic/Phase 7 work. Backend metadata/persistence files were added this task.

## Earlier checkpoint: comparison/questions context complete — September 30, 2026

R4-T03 complete. Saved comparison selection uses case/run URLs; saved questions use
result URLs and preserve frozen source versions separately from the composer. Viewer
comparison access now matches backend Read; mutations remain Review-only. Each
document role with/without administration was tested. Account-bound saved result
rules corrected in English/Arabic guides and preview adapter. 200 UI, 118 backend
API/security/contract, 11 request/fixture checks pass; lint, build/TypeScript,
independent review, role-by-role browser checks and mobile/theme checks pass.
[Evidence and limits](testing/recovery-comparison-questions.md).

Next R4-T04: structured case references/categories, contracts, migration/backfill,
creation form and filtering. Rand explicitly requires testing every affected role
for each task; retain test/review/integration gates. Preview 3410 was restarted for
fixture changes, resetting temporary samples. Fresh case `Role checks — مراجعة الأدوار`
has ID `c6abfcba-21a8-4413-8556-71a6d188eb61`, three pins and two comparison runs.
Original user tab untouched; temporary tab closed and dark/default viewport restored.
No Docker, production authentication or schema changes. Real Entra walkthrough and
Rand's history-download check remain pending. Preserve unrelated Arabic/Phase 7 work.

## Earlier checkpoint: focused document review complete — September 30, 2026

R4-T02 passed its test/review/integration gate: source evidence alongside values,
progress and Needs review navigation, clear saved/draft/completed states, sibling
draft preservation and completion guards. Stale refresh can retry without losing
same-context drafts; remotely completed reviews expose unsaved draft recovery.
166 full-suite UI tests pass; final focused 18/18 includes two further retry tests.
Eight request/fixture checks, lint, TypeScript, production build and independent
review pass. Desktop and 390px checks passed; screenshot saved at
`outputs/ui-preview/focused-review.png`. [Evidence](testing/recovery-focused-review.md).

Next R4-T03: comparison/questions shared context, pinned-version correctness and
saved-result recovery. Preserve per-task gates. Preview remains at 3410 without a
restart; one synthetic invoice-number field was confirmed during verification.
No Docker or real data changes. Original user tab/theme unchanged, viewport reset,
temporary tab closed. Real identity walkthrough and Rand's manual history-download
test remain pending. Preserve unrelated Arabic/Phase 7 edits.

## Earlier checkpoint: find and reopen work complete — September 30, 2026

R4-T01 complete: saved-case list with paginated tenant-scoped API, loaded-page search,
document search and `?case=` / `?document=` navigation with refresh and Back recovery.
Late responses cannot replace newer selections. Independent frontend review's three
bugs fixed and tested; root reviewed backend query. 150 UI, eight request/fixture,
118 API/security, eight application and one actual PostgreSQL tests pass. Lint,
TypeScript/production build, desktop/mobile browser checks pass.
[Evidence and boundaries](testing/recovery-reopen-work.md).

Next R4-T02: focused document review, evidence context and clear save/finish states;
preserve concurrency/history and the per-task test/review/integration gate. Preview
3410 restarted for updated sample API; new `Office supplies — لوازم المكتب` case
remains with one pinned quotation. Original user tab untouched; temporary tab closed
and viewport reset. Disposable native PostgreSQL25435 stopped; no Docker or retained
database used. Full real identity walkthrough still pending. Preserve unrelated
Arabic changes and deferred history-download check.

## Earlier checkpoint: bilingual role guides and diagrams — September 30, 2026

Rand requested more detailed English/Arabic guidance and a visual for each role.
R3-T03 now includes responsibilities, specific page steps, access limits and an
HTML/CSS workflow diagram in both languages. Arabic uses RTL; narrow diagrams stack
downward. Guide language switching preserves form drafts. Language persists within
the current mounted guide, not across reloads. Wider UI translation is not implied.
Independent content/code review, 20 guide tests, full 133-test UI suite, lint and
production build pass; desktop/mobile browser checks completed in both themes.
[Evidence](testing/recovery-role-guidance.md). Preview remains at 127.0.0.1:3410;
original Dark preference and viewport restored after testing. Next remains R4-T01.

## Earlier checkpoint: shared navigation and role guidance complete — September 30, 2026

R3-T02 and R3-T03 are complete in the [execution ledger](tasks/reviewer-experience-recovery.md).
One role-aware sidebar connects the actual work and administration screens;
redundant phase headings/tabs are removed. Help with this page provides contextual
role instructions without discarding drafts, and clears on identity/access changes.
Full UI suite 126/126, seven request/fixture checks, focused lint, production build
and independent review pass. Desktop and 390px browser checks passed. Evidence:
[navigation](testing/recovery-shared-navigation.md) and
[role guidance](testing/recovery-role-guidance.md).

Next: R4-T01 document/case list and stable reopen/deep-link flow, including necessary
tenant-scoped API queries. Preserve the per-task test/review/integration gate.
Preview remains at 127.0.0.1:3410 without Docker. Browser viewport and original Light
preference were restored after dark-mode checks; approved design remains A with
blue accents. Real identity/backend proof is still pending; history-download testing
remains deferred to Rand. Preserve unrelated Arabic/Phase 7 edits.

## Earlier checkpoint: approved dark appearance implemented — September 30, 2026

Rand selected **A — blue accents**; dark palette approved, light refinements can
wait. R2-T02 and R3-T01 are complete in the
[execution ledger](tasks/reviewer-experience-recovery.md). Actual screens now use
shared dark/light tokens, fonts/buttons and persistent appearance control; callback
and identity provider remain isolated. Ten independent new appearance tests, full
91 UI tests, seven request/fixture checks, TypeScript, focused lint and production
Next build pass. [Evidence and limits](testing/recovery-shared-appearance.md).
Next task: R3-T02 role-aware shared navigation/account frame and redundant headings.
Keep the task-by-task test/review/integration gate. The fast preview was restarted
and remains running at 127.0.0.1:3410 with sample documents. Existing review/history
and Arabic caveats below still apply; no real identity/backend proof is implied.

## Earlier checkpoint: reviewer experience recovery — September 26, 2026

Rand authorized task-by-task implementation with tests, independent review and
integration impact checks before each next task. Follow the
[recovery execution ledger](tasks/reviewer-experience-recovery.md). R0 fast preview
is available via Start preview.cmd without Docker. R1 baseline and stale selected
document response fix are complete: 81 UI tests, TypeScript and focused lint passed.
R2-T01 isolated design proposal is complete at `/design-preview`: two restrained
styles, light/dark, role help, local review/compare/questions/activity and case form.
Ten proposal tests, seven request/fixture checks, TypeScript and independent review
passed; browser desktop/mobile checks recorded in
[proposal evidence](testing/recovery-design-proposal.md). R2-T02 awaits Rand's visual
choice before production shared-interface work (R3). The preview server remains
available at 127.0.0.1:3410; existing sample scenario restored to empty workspace.
No real identity, backend, audit durability, or history-download proof is inferred.
Preserve existing unrelated Arabic/Phase 7 changes in the working tree.

## History download: manual check deferred to Rand

Rand will test history download manually later. Do not repeat the full workflow
or automatically resume browser verification. Edge control approval timed out;
no Edge navigation or export was performed. A download-only replay helper is
available at `tools/phase7-history-preview.mjs`, using the saved completed review
response from the upload audit. Delivery remains unverified. No OCR, upload,
database or reconciliation tests were repeated.

## Earlier checkpoint: actual uploads and reconciliation RTL — September 22, 2026

Read the [upload workflow report](evidence/phase-7/P7-upload-workflow-20260922.md)
and its JSON audit. Real browser uploads and the durable worker recovered English
8/8 fields and mixed native/scanned 8/9 each; all critical numeric/identifier
fields match, with the existing payment diacritic mismatch. The mixed forms are
one invoice in two representations, not a production accuracy sample.

Fixed a real UI bug: the previous terminal processing status stopped subsequent
uploads from polling. Reset it after a successful new upload; the next quotation
visibly populated fields without a reload. Terminal status text is now accurate.
English total and corrected Arabic payment answers persisted, reopened in the
browser and retained exact original citations. Reconciliation Arabic case/reason
display passed visual checks, and its decision persisted through the real API.

History export now has a persistent explicit save-link fallback and honest ready
feedback, with blob cleanup on replacement/unmount. Both automatic and explicit
download-event checks timed out in the available browser; no expected file was
found in Downloads. Delivery remains unverified. Do not claim this gap is fixed.

34 focused UI tests pass serially (initial concurrent run had one existing-test
timeout under service load). TypeScript, focused lint, audit syntax/basic lint
and documentation build pass. No test-port listeners remain. Scanned
extraction was verified but its review was not completed in this run. Services,
owned browser and disposable DB are stopped; no paid/retained-data changes.
Next: verify file delivery in a browser that exposes downloads, then address the
separate semantic evaluation/representative-coverage gates. Keep scope simple;
do not reopen general table parsing or chase perfect OCR. Phase 7 is in progress.

## Earlier checkpoint: reuse the English labeled-row parser — September 22, 2026

Rand asked to keep the implementation simple and reuse the working English
path. Added Arabic aliases for item code, description, quantity, unit price,
tax and total to the existing labeled-row parser. It accepts `Line`, `البند`
or `السطر` with the same one-based row numbers and field types. Western amounts
and dates remain standard values. Existing Arabic matching-view offsets map
back to original quotes and geometry; the review warning remains in place.

Focused validation: 65 tests and 43 subtests pass; six opt-in runtime tests are
skipped. Basic Ruff and whitespace checks pass. See the
[labeled-row report](evidence/phase-7/P7-labeled-row-reuse-20260922.md).
This is a parser extension, not a new OCR run or ordinary-table reconstruction.
The frozen PDF table gap, history download and reconciliation RTL checks remain
open. Do not resume the previously proposed general table parser without a new
scope decision. Phase 7 remains in progress.

## Earlier checkpoint: real saved browser workflow and line prices — September 22, 2026

Read the [browser/line-price report](evidence/phase-7/P7-browser-and-line-prices-20260922.md).
The real Next frontend, .NET API, private Python chunking and isolated PostgreSQL
workflow passed: Arabic correction, two numeric prices, completed-review reload,
English price answer, Arabic payment answer, reviewer provenance, original page-2
quote and saved-result reopening. The API audit proves exact UTF-8 source bytes.
No paid-spend reservations were recorded. This used frozen actual extraction;
browser upload and worker ingestion were not rerun.

Fixed a practical UI gap: missing fields can now have a selected type and repeated
position. Line items use the existing one-based row numbers; normal singleton
fields remain occurrence zero. Headings distinguish Line 1/Line 2. Mixed reasons
use BidiText. Nine focused UI tests pass; TypeScript and focused lint pass.
An initial zero-based browser attempt was corrected and repeated on a fresh
fixture; it is not the final evidence. New guarded seeder/audit tools reproduce it.

Both frozen PDF forms score 0/6 on the added structured table assessment. The
parser expects `Line N Unit Price: ...`, not ordinary table cells. The earlier
8/9 header-field result is unchanged. Reviewer-entered prices are a fallback,
not an OCR improvement. The history API response is verified; browser download
events timed out, so completed history file delivery remains unverified.

The subsequent direction above narrows line-item work to reuse of the English
parser. Browser history download delivery and reconciliation RTL, then separate
multilingual semantic evaluation remain open. Do not chase perfect synthetic OCR
scores or mark Phase 7 complete.
The owned browser tab, test listeners and disposable database were closed/stopped;
Docker is stopped. Temporary Next type includes were removed. No retained-data
changes, paid calls, commit or push occurred.

## Current direction: reviewable PDFs and saved language workflow — September 22, 2026

Rand agreed that zero real-world OCR failures is not the completion target. Stop
chasing perfect scores on the small synthetic set. Prioritize standard numeric
accuracy, visible review needs, corrections, persisted answers and faithful
citations. Keep the remaining mixed English-payment-label failure documented.

New fictional two-page native/scanned PDFs and frozen extraction receipts are in
`samples/phase-7/pdf-v0.1.0/` (generated PDFs in `output/pdf/phase-7-language-v0.1.0/`).
Both forms recover all eight ID/date/currency/amount fields exactly. Payment text
loses one Arabic diacritic; the exact-match failure is preserved. These are two
representations of one invoice, not a production failure-rate estimate. No OCR
runtime code was changed in this increment. Table line items are not scored.

The new `ArabicPdfWorkflowPersistenceTests` exercise real application handlers
against an isolated, disposable PostgreSQL container, using the actual frozen
extraction receipts and private Python chunking. Both cases pass (2/2); review
corrections, saved Arabic/English answers, page evidence, replay and organization
isolation were verified. The initial test-only timestamp precision failure is
retained separately. See the
[workflow report](evidence/phase-7/P7-pdf-workflow-20260922.md) for final validation
and limitations. Full browser/upload/worker verification, history/reconciliation
RTL and multilingual semantic evaluation remain separate. No paid calls or
retained database changes are authorized by these checks.
Validation completed: 2 persistence tests, Python syntax/basic lint and docs build
pass. The owned disposable database and Docker are stopped. The report preserves
the initial failed test receipt. No commit or push was made in this increment.

## Earlier checkpoint: targeted mixed OCR refinement — September 22, 2026

Read [the new report](evidence/phase-7/P7-mixed-ocr-refinement-20260922.md).
Both original mixed failures are fixed with the opt-in `best` / `eng+ara` /
`lines-v1` profile: 9/9 original cases with unchanged PNG hashes. The expanded
v0.3.0 set is 11/12; English payment wording under an Arabic label still loses
the payment label, while all its ID/currency/amount fields pass. A prior run's
timeout and all failed attempts are retained. Do not claim general OCR acceptance.

New bounded line retries preserve observed numbers and protected Latin tokens,
translate crop boxes to page coordinates, share a page deadline, and retain clean
or overlapping line layouts. Best weights now have an explicit application pin
and opt-in configuration; defaults remain English/fast/block. Metadata identifies
the new adapter/profile. See `.env.example` and the quality plan before enabling.

The new phrase fixture also exposed word-by-word bidi isolation: v3 rendering and
the citation component keep `Net 45 days` together. All nine older images remain
unchanged. The evaluator now scores actual pipeline fields and warnings. The
v0.3.0 source expectations were fixed and passed text checks before OCR.

Verification: 59 Python tests, 43 subtests and 21 citation/display tests pass;
six gated Python skips remain. Canonical text is 12/12. Python basic lint and
whitespace checks pass. TypeScript, focused ESLint and documentation build also
pass. Offline Docker evaluation has been stopped.

Next: script-aware mixed label/value regions, native/multipage and realistic
samples, persisted language journeys, remaining RTL history/reconciliation and
semantic retrieval. No paid calls, retained-data changes, commit or push.

## Earlier checkpoint: standard-numeric OCR fixes and real frontend RTL check — September 21, 2026

Read the [new report](evidence/phase-7/P7-standard-numerics-and-rtl-20260921.md)
first. The v0.2.0 offline comparison is now done. The renderer needed per-line
direction and numeric isolation. Application fixes prevent due-date warnings
referencing absent fields and trim OCR boundary direction marks without changing
canonical evidence. Same-image `best`/`eng+ara` scores 5/5 Arabic, 1/3 mixed, 1/1
English; fast/eng+ara scores 2/5, 1/3, 1/1. The candidate is evaluation-only, with
its own exact model pin; defaults remain unchanged. Failed receipts are preserved.

Actual Next frontend visual checks used `tools/phase7_workspace_preview.mjs`, an
in-memory synthetic adapter. Correction, mixed reason, original evidence and saved
preview-result reopening were checked. The source viewer visibly reversed ISO
date components; `BidiText` now isolates numeric/Latin runs without changing text
or highlighted source slices. Review columns stack at medium widths. Final PNGs
are linked in the report. This does not prove database persistence or QA accuracy.

Latest checks: 48 Python tests and 43 subtests pass, 6 gated skips; 9/9 canonical
fixtures; 20 citation/display component tests; TypeScript, focused lint and docs
build pass. Next work: remaining mixed layout
and payment-wording failures, native/multipage samples, persisted language journey,
broader history/reconciliation RTL coverage, semantic retrieval. Phase 7 is still
in progress. No commit/push, paid call or retained database changes.

The sections below are historical checkpoints; their pending-image/visual claims
are superseded by this increment. Disposable OCR containers completed and Docker
was stopped. The owned 3327/8327 preview listeners were also shut down.

## Earlier clarification: standard numeric fields — September 21, 2026

Rand clarified that dates, totals, prices, VAT amounts and similar fields do not
need Arabic conversion or Arabic numeral extraction. Keep these values in standard
Western digits and Gregorian ISO/decimal formats; Arabic support targets labels
and text. Arabic-Indic source numeral recognition is optional, not a completion
gate. Do not spend effort improving that optional capability at the expense of
the required mixed-language workflow.

New `samples/phase-7/language-v0.2.0/cases.json` retains the nine scenarios with
standard source numerals. Both Phase 7 evaluators default to it and accept an
explicit `--dataset` for historical runs. Its text result is 9/9, recorded in
`docs/evidence/phase-7/P7-standard-numerics-text-20260921.json`. Image OCR for this
version is still pending; no OCR accuracy improvement is claimed. Preserve
v0.1.0, its original expected labels, images and failed receipts unchanged.
No runtime service, model call or database was needed for this scope correction.

## Current: Phase 7 language foundation and failed OCR baseline — September 21, 2026

Rand authorized proceeding with Phase 7. Read the
[foundation report](evidence/phase-7/P7-language-foundation-20260921.md),
[task ledger](tasks/phase-7.md) and [language contract](design/phase-7-language-contract.md).
Phase 7 is in progress, not complete. HEAD `48337e9` contains the earlier saved
implementation; the new Phase 7 work is uncommitted. No new commit/push, paid call
or retained-data migration was performed.

Implemented: nine versioned synthetic Arabic/mixed/English text fixtures; mapped
Arabic label normalization with original UTF-8 evidence; explicit pinned Arabic
OCR modes; RTL values/corrections/reasons/citations; and a bounded whole-question
Arabic grammar using the existing reviewed-fact answer engine. Confirmed normalized
amounts still cite the original Arabic numerals. Unsupported Arabic additions refuse.

Verification: nine text cases pass (5 Arabic, 3 mixed, 1 English); 44 focused Python
tests plus one separate English native-PDF test pass; 94 typed-answer tests plus
two additional Arabic numeral/citation tests pass; 63 browser components pass
serially. TypeScript, ESLint, isolated Python basic lint and the documentation build
pass. The first parallel
browser run had two timeouts and a now-fixed text-node assertion; do not hide it.
The six Python skips are explicitly gated native/OCR tests; one native case was
run separately, and no new pass is claimed for the five older OCR goldens.

The actual offline OCR comparison fails every Arabic/mixed full case: `eng`,
`ara`, `ara+eng` and `eng+ara` each score 0/5 Arabic and 0/3 mixed. Three modes
pass the one English control. Keep the unchanged expected labels and failed
results in `docs/evidence/phase-7/P7-ocr-baseline-20260921.json`; raw synthetic PNGs
and logs remain in `outputs/phase-7/`. Model errors include numerals and date order.
Arabic OCR is explicitly experimental and now always produces a degraded review
warning. Default OCR and the local demo remain English. Do not claim bilingual
OCR acceptance merely because label parsing or citation offsets pass.

Next Phase 7 work: improve/evaluate Arabic numeric OCR and other pinned model
choices, add native/multipage/realistic layouts, visually demonstrate actual RTL
review/citation behavior, exercise persisted language journeys, and separately
evaluate multilingual embeddings/cross-language semantic retrieval. No paid model
experiment is authorized. Keep Phase 8 and the prior completed demo checks separate.
Docker was started only for the disposable offline OCR test and stopped afterward;
no database, listener or retained source data was used. Prefer serial browser
tests on this host. Reproduction is in the Phase 7 quality plan.

## Current: GitHub checkpoint save authorized — September 21, 2026

The user reversed the earlier deferral and explicitly requested saving the current
work with a GitHub commit and push. This authorizes the checkpoint save; it does
not mark the unfinished phases complete or authorize public hosting, paid provider
calls or deployment. A new repository should be private by default. At preparation,
this checkout has no configured remote; resolve the intended GitHub destination
before pushing. Keep existing source history and do not force-push.

The save includes implementation, tests, documentation, curated screenshots and
reviewed synthetic samples. Retained databases, uploads, keys, private environment
files, raw logs, generated outputs and dependencies remain excluded and preserved
locally. The current file inventory has 858 candidates and no excluded-artifact
errors; the credential scan has zero unresolved findings/errors across current
text, history, the existing Locked browser build and selected demo logs. These
are bounded backup checks, not public-release or license clearance. Raw receipts
are `outputs/github-save-inventory.json` and `outputs/github-save-secret-review.json`.

Use a checkpoint commit with `[skip ci]` so saving does not launch another full
suite. No runtime services need starting. Phase 7 remains unimplemented; the
completed local demo/reconciliation evidence remains valid, and the unfinished
dependency audits, license decision, public-release review and hosted CI remain
explicitly pending. Record the actual commit/remote result in the task once saved.

## Current direction: finish local product work before GitHub delivery — September 21, 2026

The user deferred source delivery: finish the remaining work first, then discuss
commits and pushes. Do not treat that future sequence as authorization to commit,
push, publish or run hosted CI now. Keep verification focused on changed behavior;
do not repeat completed Phase 4 or demo acceptance suites.

The next unfinished product phase is [Phase 7 Arabic/mixed-language support](phases/phase-7-arabic-support.md).
Start with a versioned synthetic language dataset and evidence-preservation
contract, then implement and evaluate Arabic extraction, RTL review and cross-language
questions. The current local extractor explicitly accepts only `OCR_LANGUAGE=eng`
and has English field labels. Existing English results do not establish Arabic
support. Phase 7 is not complete or implemented by this sequencing decision.

After the language work, close remaining local usability gaps (including reopening
a reconciliation case after refresh), align the documentation with measured results,
and complete the necessary local Phase 8 checks. License/notices and publication
review belong to the final repository-preparation step. Clean-checkout and hosted
verification follow separately authorized source delivery. Real Entra/cloud
deployment and optional paid model experiments remain outside this local scope.

Docker and demo services were stopped after the completed walkthrough. This planning
update starts no services and changes no runtime implementation or retained data.
Preserve the uncommitted implementation; HEAD is outdated. Use
`tools/use-project-runtimes.ps1` for host .NET/Node commands.

## Current: Phase 8 reconciliation demo walkthrough complete — September 21, 2026

The user authorized demonstrating the existing Phase 4 feature, not rebuilding it.
The [walkthrough report](evidence/phase-8/P8-reconciliation-demo-20260921.md),
[receipt](evidence/phase-8/P8-reconciliation-receipt.json) and two screenshots show
three pinned synthetic reviews, nine exact expected findings, matching fields,
source-page evidence and one retained reasoned decision. A fresh API read matches.
Only this focused journey and the helper's ownership guard were checked; do not
repeat Phase 4 acceptance, the full CI suite or the earlier restart tests.

`tools/prepare-local-demo.ps1` now includes the unchanged Phase 4 sample pack and
`tools/verify-demo-reconciliation.ps1`. Follow the separate reconciliation section
in [the demo guide](local-demo.md). Use an empty copy for its Prepare stage, complete
the browser case, then run Verify with the displayed case UUID. Application code,
reconciliation rules, security controls and synthetic sample PDFs are unchanged.

Final source: `outputs/local-demo/1e45fce6127e`, 304 inventory files all matching the
workspace; raw journey, startup, verification and cleanup logs remain there. An
initial helper empty-array guard failure occurred before upload in `145e430debdb`;
its failed evidence and successful reset are preserved. `b82bd06f6bb0` was never
started. All owned disposable resources are gone, ports 3308/8308 are closed, and
Docker Desktop/WSL are stopped. The native retained database was not accessed.

Known UI limit: refreshing the reconciliation page loses the selected case in the
browser; the case remains stored and accessible through its UUID. Case reopening
UI, cited-answer and broader security walkthroughs are separate future work.
Phase 6 local scope is complete; Phase 7 Arabic/bilingual remains pending. Keep
services stopped until needed. No commit, push or publication is authorized.

## Current: Phase 8 essential priority checks complete — September 19, 2026

The user requested only important verification. The [follow-up report](evidence/phase-8/P8-priority-followup-20260917.md)
and [receipt](evidence/phase-8/P8-priority-receipt.json) close this local increment.
Two fresh demo trials passed setup/migrations, upload, local extraction, append-only
review, exact restart persistence, health observation and guarded reset. Their
runtime source inventories still match the workspace. Do not repeat these checks.

The final isolated CI source `outputs/ci-source/8e7fa1227ac2` passed 1,785 tests,
Release/Locked browser/documentation builds, lint and migration checks, including
the final crash-test harness. The combined run was interrupted during dependency
audits and lacks its runner summary; do not call it all-green. On September 19,
Docker was initially stopped. It was started for targeted cleanup only. The owned
CI project `uaedi-ci-8389844dc608` was removed; separate cleanup evidence is
`outputs/p8-final-ci-cleanup.json`. No suite or audit rerun is needed for this increment.

Final safety receipt `outputs/p8-final-state.json` verifies zero owned resources,
closed demo ports and retained public-table fingerprints unchanged at SHA-256
`06c07f7f05eb9d20a7ceb561d43821414427bd79d0453e170a65ae41132c7549`.
The retained cluster was initially stopped, started on loopback for this read-only
comparison, allowed to recover its prior interruption, then cleanly stopped.
No retained-data reset or migration was performed.

Fresh CI browser/source/history/selected-log credential review has zero unresolved
findings/errors after exact decisions. It precedes final report edits and does not
establish publication clearance. Source/images/evidence and earlier failed attempts
remain preserved. The historical demo database exit-code-2 cause is still unproven;
the new startup health gate detects recovered crashes and permits slow initialization.

Phase 6 remains complete for documented local scope; Phase 7 Arabic/bilingual is
pending. License choice, notices/publication review, clean-HEAD reproduction and
hosted CI remain pending. No commit, push, account, publication or deployment was
performed. Use `tools/use-project-runtimes.ps1` for host .NET/Node commands.

## Current: Phase 8 local CI increment complete — September 17, 2026

The [CI guide](testing/ci.md) and [verification report](evidence/phase-8/P8-ci-local-20260916.md)
record 1,780 passing tests: 1,365 backend, 64 browser and 351 Python/tool checks.
Release/Locked browser/docs builds, lint, migration checks and scoped NuGet/npm/pip
audits pass. GitHub execution, artifact upload and clean-HEAD reproduction remain
unverified. Phase 6 is complete for its documented local scope; Phase 7 Arabic
support remains pending. Do not ask again about phase order.

Implementation is in `.github/workflows/ci.yml`, `tools/ci/` and
`tools/prepare-ci-source.ps1`. Use an isolated copy including uncommitted source;
HEAD is outdated. Host .NET/Node commands still require the project runtime helper.
No project commit, push, publication or deployment is authorized or performed.

Backend/browser and audits used `outputs/ci-source/c18d1e04b907`; final Python and
fixture checks used `outputs/ci-source/81f785689247`. The initial all-stage attempt
failed at missing Python fixtures. Preserve its failed receipt; successful targeted
reruns supersede only those stages. Code/package inputs for earlier passing stages
are unchanged. The report documents packaging, loopback and synthetic-input fixes.
No production security code, expected hash, OCR golden or retained output changed.

All CI containers, networks and named volumes are removed. Source copies, images
and ignored logs remain. Final retained public-table fingerprint SHA-256 matches
the baseline: `06c07f7f05eb9d20a7ceb561d43821414427bd79d0453e170a65ae41132c7549`.
The new fixture helper verifies five existing synthetic files, generates the smoke
report and refuses changed outputs/redirected paths. Four preservation tests pass.
Remaining Phase 8 work is broader workflow screenshots, publication/license
inventory and a later authorized source delivery/hosted CI run.

## Current: Phase 8 local demo increment — September 16, 2026

The authorized local demo increment is implemented. Start with the
[runbook](local-demo.md) and [verification report](evidence/phase-8/P8-local-demo-20260916.md).
Phase 6 remains complete for its documented local scope. Phase 7 Arabic/bilingual
support remains pending; do not ask again about this accepted phase order.

`tools/prepare-local-demo.ps1` snapshots the current uncommitted runtime source,
one fictional English PDF and its license records into a uniquely owned copy.
`tools/local-demo.ps1` builds, migrates, starts, stops and safely resets only that
copy's Docker project. API/web publish only loopback 8308/3308; DB/Python stay
private. Existing Locked/Entra, original-source access and production identity
gates are unchanged. Host .NET/Node commands still require
`tools/use-project-runtimes.ps1`.

The 295-file journey copy is `outputs/local-demo/4b297fab4fb0`. Upload/local native
extraction, eight append-only review actions, completion, full review-response
equality and source-byte persistence after container recreation all pass. Reset
refuses changed Compose, a foreign path and unexpected project volumes; actual
reset removes owned volumes and fresh migrations yield an empty document store.
Four rebuilt security profile checks pass. Retained PostgreSQL public-table
fingerprints are unchanged. Screenshots and a curated JSON receipt are in
`docs/evidence/phase-8/`; raw logs/receipts stay in ignored `outputs/local-demo/`.

The final startup copy `outputs/local-demo/5a61b2aebc85` changes only the Compose
health check to require PostgreSQL TCP readiness instead of its initialization
socket. Its separate inventory records that bounded difference. Final fresh startup,
19 migrations, empty-store verification and the documentation build pass. Both demo
projects' containers/volumes are removed and loopback ports 3308/8308 are closed;
source copies, image cache and evidence remain. Initial builds were
slow on the 8 GB host; earlier superseded database process-recovery events have
an unproven cause and are documented, not treated as production acceptance.

This is current-working-source reproduction, not clean-HEAD acceptance: HEAD is
still outdated. Preserve the existing extensive uncommitted implementation and
retained data. No external account, paid provider, cloud deployment, commit, push
or publication was used. Local CI verification is recorded above. Broader workflow screenshots and
publication/license inventory review remain pending.

## Prior handoff: user created the local demo chat manually

The user wants the local demo in a new chat they will create manually. The mistaken
"Local demo setup" fork (01a0aa98-8e4a-7a60-9d4e-3651aee9a0e0) was interrupted and
archived; do not restart it or create another chat automatically. The user will
paste a handoff prompt into their new chat. That prompt authorizes the local demo
increment of Phase 8; do not ask again about phase order. Phase 7's Arabic/bilingual
support has not been started or cancelled. Phase 6 is complete for its documented
local contract; deployment and publication are not authorized.

The interrupted Phase 8 investigation was read-only: it inspected Compose,
Docker availability, setup instructions and application configuration. No demo
code/configuration was changed, no containers were started and no database was
mutated. Findings to retain if Phase 8 is chosen: the existing quick start needs
manual migration, the production web image correctly rejects Development identity,
and legacy Compose publishes ports on all interfaces. A separate disposable,
loopback-only demo was proposed but has not been implemented. Preserve the existing
Locked/Entra safeguards. The workspace still contains extensive uncommitted work;
continue in this directory rather than assuming HEAD contains the implementation.

## Delivery decision: independent GitHub portfolio

September 15, 2026: Rand does not have a domain and wants an independent project
to be proud of on GitHub. Follow the [portfolio delivery plan](design/github-portfolio-delivery.md).
Local reproducibility, security evidence, screenshots, documentation and CI are the
completion target. Cloud hosting and actual Entra/TLS/deployment verification are
optional future work, not portfolio blockers. Preserve strict enterprise profiles
and honest preflight results. Do not publish or push as part of this scope decision.
Phase 6 is complete for its supported local contract as of September 16.
Next is Phase 8's clean-checkout local demo, then screenshots, CI and publication
inventory. Arabic remains Phase 7; production integration remains optional.

## Current: Phase 6 local acceptance complete

September 16, 2026: read the [acceptance matrix](security/phase-6-local-acceptance.md)
and [final verification](evidence/phase-6/P6-local-acceptance-20260916.md). P6-T09 and
Phase 6 are accepted for the supported local portfolio scope. No domain or cloud
account is needed. The strict enterprise preflight still has DeploymentVerified=false.

597 fresh primary checks pass: 260 API, 220 Infrastructure, 35 PostgreSQL security
walkthrough, 64 browser, 14 Python service and four scanner tests. The Python run
also has 13 subtests, excluded from that total. Backend builds with zero warnings/errors;
production browser and documentation builds pass. Earlier runtime/worker/storage/
restore evidence remains separately linked, not added to the fresh total. No fresh
package audit is implied. Initial zero-selected-test and incorrect docs-script
attempts are retained and superseded by passing final runs.

The Development orphan cleanup logger now suppresses raw exception/state details,
with a regression against the background loop. Git ignores private .env variants,
key containers, tmp/, outputs/ and .vite/; example .env files remain visible. This
preserves local artifacts while preventing ordinary staging of temporary databases,
raw logs, runtime downloads and generated receipts. Nothing was deleted.

The offline scanner reviews current Git text candidates, reachable historical text,
explicit .log/.trx roots and fresh static JS/maps/prerendered HTML/RSC. Three synthetic
server-only build markers do not appear in client output. There are zero unresolved
findings/read errors; 26 reviewed fixture/field-name matches use exact file hashes,
paths, rules and lines in docs/security/phase-6-secret-review-decisions.json. Scanner
reports never contain credential values. It is bounded detection, not a universal
secret/PII guarantee. Raw PostgreSQL logs remain a sensitive operator boundary;
MSAL uses sessionStorage and service credentials are not constrained by tenant RLS.

Artifacts are under outputs/phase-6/acceptance-20260916, including verification.json,
secret-review.json, TRX/logs and the 14-event audit snapshot. A curated synthetic copy
is docs/evidence/phase-6/P6-local-acceptance-audit-20260916.json. All 34 retained
fingerprints match; no migration, paid call, deployment, commit or push occurred.

**Next:** verify the independent local demo from an isolated copy of intended source
files, including these uncommitted changes; plain HEAD does not contain the current
implementation. The workspace has extensive preexisting changes. Do not clean/reset
or publish it. Read the portfolio delivery plan and Phase 8 plan, establish an exact
source/sample inventory, then demonstrate loopback startup/shutdown/reset and the
synthetic document journey without external accounts. Curate screenshots and evidence
into docs rather than force-adding ignored output/raw-log directories. CI,
README/setup, sample provenance and license/publication review follow. Do not weaken
Locked/Entra settings to make the demo work.

All .NET/Node shells use tools/use-project-runtimes.ps1. PostgreSQL tests run one at a
time and may recreate only document_intelligence_test, local port 25434. The final
walkthrough filter is in the acceptance matrix; its partial test class requires
method-name filters. Certificate tests/builds may need approved host access. Tests
use --no-restore -p:NuGetAudit=false; runtimes remain the September 15 baseline.

## Prior: local backup and recovery

September 15, 2026: read the [local recovery contract](design/phase-6-local-recovery.md)
and [verification snapshot](evidence/phase-6/P6-local-recovery-20260915.md). P6-T07 is
accepted for this explicit local scope. Backups and inventory records retain an
indefinite policy; no expiry/purge scheduler is supplied. Exact-byte repair is a
private offline procedure, not an automatic historical reconciliation command.

New private `EnterpriseRecovery verify-sources <private-config.json>` requires
offline acknowledgment, an exact non-retained database and the existing journal/
controls. It checks all registered canonical files by hash/size in one read-only
Repeatable Read database snapshot with controls/deletion history. It refuses silent
RLS filtering, relative/reparse paths, missing/corrupt/locked sources, more than
1,000 documents, over 10 MiB per file or 1 GiB total. It prints counts and changes
no files/rows. The command has a two-minute verification budget. Normal source
scanning remains mandatory after integrity verification.

The actual pg_dump/pg_restore drill stops its fixture API hosts, captures only
`document_intelligence_test` plus one synthetic source, restores the same capture
after deliberate fixture damage and matches all 41 table fingerprints plus exact
source bytes. Current journal/controls remain independent. No valuable database
is dumped/restored; retained checks remain read-only. Machine results and the
backend/CLI snapshot are under `outputs/phase-6/local-recovery-20260915`.

501 distinct checks pass: 260 API, 219 Infrastructure and 22 PostgreSQL (11 new
source/drill cases plus 11 lifecycle regressions), with no failures or skips.
The initial 10-case source run is superseded and not added again. Backend solution
build has zero warnings/errors. All 34 retained fingerprints match the preceding
checkpoint. Final documentation build status is recorded in `verification.json`.

Code: `RegisteredSourceRecovery.cs`, the recovery CLI and an adjustment allowing
the control snapshot to share the verifier's transaction without committing it.
No schema, HTTP route, browser or package version changed. The dump drill needs
PostgreSQL tools on PATH or `TEST_POSTGRES_BIN`; all .NET/Node shells use the runtime
helper. Run PostgreSQL fixtures one at a time. Test builds use `--no-restore
-p:NuGetAudit=false`; no fresh package audit is implied.

**Next:** finish P6-T09's local claim-to-evidence matrix and source/log/client secret
review. Keep real Entra/TLS/deployment checks separate and optional for portfolio
delivery. Then verify the independent clean-checkout demo. An old backup is not
authority to reopen: current memberships/revocations, controls, keys and grants need
review; stale historical reconciliation remains unsupported. No commit, push or
deployment was requested.

## Prior: operational audit coverage

September 15, 2026: the [coverage map](security/phase-6-audit-coverage.md) closes
P6-T05 for the explicit local enterprise contract. Read the
[verification snapshot](evidence/phase-6/P6-audit-coverage-20260915.md).
New admission reasons identify 25 fixed operations. Historical method-only reasons
remain valid. A coverage test cross-checks every authorized route for unique safe
names, omission on ordinary reads and no body access.

The review also found that semantic retrieval opened an isolated database context
without the bound audit identity. It now carries that context, refuses mismatched
bindings and commits `question.saved` atomically with retrieval/hits. Signed-token
vector/hybrid/nonredundant tests cover audit outage rollback, retry and replay;
unbound Development behavior remains supported. No historical events are fabricated.

The map distinguishes Human/System business events, independent file authorization,
private operator receipts, global events and incomplete attempts. It does not claim
exhaustive HTTP/host/DBA access logging, client byte receipt or tamper-proof storage.
No schema or browser change was needed. Current artifacts and synthetic HTTP/CLI
snapshots are under `outputs/phase-6/audit-coverage-20260915`.

497 distinct checks pass: 260 API, 219 Infrastructure and 18 PostgreSQL. The final
API run follows the semantic fix; no failures or skips. Backend solution build has
zero warnings/errors. All 34 retained fingerprints match the preceding checkpoint
when psql display spacing is normalized. No migration, paid call, deployment, commit
or push occurred. See the verification JSON for final documentation build status.

**Next:** resolve the supported local recovery/backup-expiry contract (P6-T07),
including registered-content repair boundaries. Then complete the final security
and secret review/claim-to-evidence acceptance (P6-T09), followed by the independent
clean-checkout demonstration. Real Entra/TLS/cloud deployment remains optional.
Use the project runtime helper for every .NET/Node shell and run PostgreSQL fixtures
one at a time against `document_intelligence_test` only.

## Prior: private audit export and retention

September 15, 2026: `EnterpriseAudit export <private-plan.json>` now captures a
bounded organization/time snapshot with a read-only PostgreSQL transaction.
`verify <archive-directory> <trusted-manifest-sha256>` works offline. Read the
[export/retention contract](design/phase-6-audit-export-retention.md) and
[verification snapshot](evidence/phase-6/P6-audit-export-20260915.md).

225 checks pass: 219 infrastructure (30 archive cases) and six PostgreSQL. The full
backend solution builds with zero warnings/errors. A synthetic CLI export, trusted
receipt and final offline verification are captured under
`outputs/phase-6/audit-export-20260915`. No package version, schema, HTTP route or
browser component changed. Source audit records and accepted archive/receipt pairs
remain indefinitely retained; no delete/purge/expiry command was added.

Export requires private database-operator authority, an explicit database name,
organization, UTC microsecond-aligned window and existing private output directory.
It refuses retained phase5 databases, silent RLS filtering and oversized/truncated
output. Limits: 1–50,000 rows (default 10,000), 64 MiB data, 8 KiB record and 16 KiB
plan/manifest. Publication follows verification; checksums require an independently
trusted receipt. Late commits can be absent despite older timestamps: no safe
incremental cursor is claimed. Global/unresolved events are not exported.

**Next:** finish the operational audit coverage inventory and local acceptance,
explicitly distinguishing database-operator receipts from application actor events.
Then resolve the supported local recovery/backup-expiry contract and complete Phase 6
acceptance/secret review before the clean-checkout demo. Production deployment is
optional. All 34 retained fingerprints match; no commit, push, paid call or deployment
occurred. Continue using the project runtime helper for every .NET/Node shell below.

## Prior: audit admission

September 15, 2026: [audit operations](design/phase-6-audit-operations.md) now
persists `request.admitted` in Entra mode after authorization and request budgets,
before body parsing, for mutations and source downloads. Admission success is not
business success. A failed admission insert returns 503 without reading the body or
executing the endpoint. The local Entra profile also places cited-body buffering
after admission; Development keeps its original response ordering.

All 277 distinct checks pass: 259 API and 18 focused PostgreSQL cases, with the
latest successful rerun replacing the initial membership-outage failure. Read the
[verification snapshot](evidence/phase-6/P6-audit-admission-20260915.md). The broad
PostgreSQL run was intentionally stopped after 56 completed passes; it is not a full
suite pass and is excluded from these totals. Membership/upload outage tests now
fail their business audit action after admission, preserving rollback coverage.
Membership Save now maps only genuine PostgreSQL membership-table uniqueness errors
and concurrency failures to 409; audit/storage outages reach the safe 503 boundary.
Deletion replay expects one new admission and no repeated business events.
Artifacts: `outputs/phase-6/audit-admission-20260915`.

Next implementation: bounded private audit export, explicit archive/retention
handling and final operational coverage review. Audit records currently remain
append-only and retained indefinitely; document deletion does not purge them.
No schema or browser component changed in this increment.
The artifact folder contains a synthetic HTTP audit-viewer JSON snapshot. All 34
retained fingerprints match. No deployment, paid call, commit or push occurred.

## Prior: runtime and dependency security baseline

Updated September 15, 2026 after Rand asked to keep going. Read the
[runtime contract](design/phase-6-runtime-baseline.md) and
[verification snapshot](evidence/phase-6/P6-runtime-20260915.md).

**Use the project runtimes for every subsequent shell:**
`. ./tools/use-project-runtimes.ps1` from the repository root. The reviewed Windows
bootstrap is `./tools/install-project-runtimes.ps1`; it installs into ignored
`.dotnet` and `.node` folders using official archives and committed checksums.
System dotnet remains 8.0.403/8.0.10 and cannot satisfy the new root global.json.
System Node remains old too; do not accidentally test with the machine PATH.
The project SDK is 8.0.425, both API frameworks require 8.0.31, and Node is 24.21.0.

EF/MVC and vulnerable transitive packages were updated; SQLitePCLRaw 3.0.5 resolves
native SQLite 3.53.4 (loaded version verified). NuGetAuditMode=all remains enabled;
fresh restore/audit passes. xUnit's new assertion-overload style rule alone is
suppressed to preserve equivalent existing tests. Vitest is 4.1.11, with a jsdom
FileReader test adaptation. Two documented React effect-style exceptions preserve
existing retention loading/reset behavior; no download/business logic was rewritten.

The Python service image is digest-pinned to 3.11.16; five OCR goldens and 330
application checks pass with networking disabled and source mounted read-only.
The latter use the test-only derivative in tools/runtime-regression.Dockerfile.
Production and Windows application package inventories/audits are captured separately.
Windows Python remains 3.11.9; its environment is not the enterprise interpreter
baseline. Historical fixture dependencies remain isolated and were not regenerated.
Docker was started for isolated image verification; no Compose application was started.

1,338 distinct checks pass: 938 backend (44 PostgreSQL), 64 browser, 336 Python.
The Python application repeat in the patched image is not counted twice. The final
migration test replaces its initial stale 16-migration assertion failure (current: 19).
TRX aggregation uses test ID plus display name because Domain theory rows share IDs.
Builds, lint, TypeScript and whitespace checks pass. Artifacts are under
`outputs/phase-6/runtime-20260915`. All 34 retained fingerprints match; check silently.
No new migration, deployment activation, paid provider call or commit occurred.

**Next:** audit operations: complete operational-attempt coverage and define/implement
safe private audit export plus retention handling. Read the membership/audit contract
and task ledger first; preserve append-only application grants. Backup expiry/restore
repair need an explicit supported local contract. Windows interpreter and historical
fixture-tool maintenance remain tracked limitations. Deployed OS/native/image scans,
edge limits and real Entra/TLS/scanner/key/recovery evidence now belong to the optional
production track. Plan .NET's supported major migration before its November 10, 2026
end of support. Phase 6 is not complete. Earlier deployment-gated next steps below
are historical and superseded by the portfolio decision above.

Communication preference: mention retained data/spending only if something changes
or needs attention.
## Prior: protected source-grant keys

Updated September 15, 2026 after Rand asked to proceed. Enterprise original-file
grants now use an isolated certificate-protected Data Protection provider with an
explicit shared directory and application name. Read the
[key contract](design/phase-6-source-key-protection.md) and
[verification snapshot](evidence/phase-6/P6-source-keys-20260915.md).

Production/other nonlocal profiles refuse source grants until Certificate mode is
usable. Local Development/Testing defaults remain available. The API still starts;
source issuance fails safely with 503, and unreadable grants release no file.
Wrapping certificates import into ephemeral storage. Directory/certificate resources
must already exist; no production certificate or directory was provisioned.
Configuration changes and wrapping-certificate rotation require restart.

Preflight now consumes `SourceKeyProtection`, checks its static profile and optionally
loads existing certificates/decrypts descriptors without creating a key or grant.
The repository candidate has 11 blockers, four passes and seven live requirements.
The fictional configured candidate still exits 2 with DeploymentVerified false.
Artifacts and final test outcomes are in `outputs/phase-6/source-keys-20260915`.
283 distinct tests pass: 253 API and 30 PostgreSQL; the final key-boundary run
supersedes matching API cases. Documentation and whitespace checks pass.

No new schema, browser screen or deployed settings changed. Key-ring ACLs, actual
shared mounts, private certificate delivery/backup, replica restart/rotation and
framework revocation caching require deployment evidence. Key encryption does not
encrypt source files or protect against a compromised API identity holding both
ring and private certificate. Keep old certificates while retained keys need them.

**Next:** verify the supported dependency/runtime security baseline, then complete
actual target-environment preflight and live Entra/TLS, scanner, private-service,
storage/key permissions and recovery evidence before activation. The local .NET
runtime observed in this verification was 8.0.10; updating package references alone
does not update the deployed runtime. No actual deployment target or private
credentials were supplied. Phase 6 remains in progress.

Communication preference: discuss retained data/spending only if something changes
or needs attention. All 34 retained fingerprints match; continue checking silently.
Do not repeat old evidence collection or invent deployment configuration.
## Prior: enterprise deployment preflight

Updated September 14, 2026 after Rand asked to proceed. Private EnterpriseReadiness
`inspect` and `verify-resources` commands now distinguish static configuration validity,
read-only resource observations and live deployment evidence. Reports contain fixed
check IDs/messages without secrets, paths or exception diagnostics. No service host,
worker, provider request, grant repair or migration is started by the tool.

Read the [preflight contract](design/phase-6-deployment-readiness.md) and
[verification report](evidence/phase-6/P6-readiness-20260914.md). 235 distinct tests pass:
230 API and five PostgreSQL. The final static-check run supersedes matching earlier
cases and adds canonical-CORS boundaries. Private CLI builds and documentation/
whitespace checks pass. Artifacts are in `outputs/phase-6/readiness-20260914`.
All 34 retained fingerprints match; check silently. No runtime settings, deployment,
new migration, source cleanup, browser UI, commit or paid call changed.

The checked-in base backend configuration evaluated as Production with browser
production defaults has 10 static blockers, four passes and seven live-evidence
requirements. This is a repository candidate, not discovered effective staging
configuration. A fictional valid candidate returns exit 2 (VerificationRequired),
never deployment acceptance. No real target tenant, endpoints or key-ring evidence
was supplied. The report always keeps DeploymentVerified false.

Optional probes use exact API/worker identities and startup read-only PostgreSQL
sessions, then inspect current grants, policy/actor coverage, existing storage roots
and schema 2 recovery controls. Failed API grants prevent tenant-control reads.
Full-row disposable fingerprints and source bytes are identical before/after direct
and CLI probes. File/DB observations do not prove ACLs, encryption, receiver credential
validity, independent snapshot freshness or cross-instance deployment behavior.

**Next:** explicit protected Data Protection key storage and cross-instance source-grant
verification. The host currently only calls default AddDataProtection(); do not invent
unconsumed shared-key settings. Implement validated deployment configuration and test
key persistence/protection and compatible application isolation, then use preflight
and actual target-environment evidence before activation. Live Entra/TLS, scanner,
Python verifier/rotation, storage permissions, recovery/backup expiry, patched runtime,
edge limits and protected telemetry remain outstanding. Phase 6 is not complete.

Communication preference: discuss retained data/spending only if something changes
or needs attention. Earlier fictional deletion snapshots remain in their evidence folder.

## Prior: offline source recovery and capacity restore verification

Updated September 14, 2026 after Rand approved the next step. Private EnterpriseRecovery
`inventory-orphans` and `cleanup-orphans` commands now cover unregistered promoted
source files. Inventory produces a new reviewable plan; execution binds it to the exact
database, journal, upload root and control-file digest, rechecks registrations/holds,
and verifies each source's size, timestamp and SHA-256. Independent System audit
authorization precedes removal; completion audit follows it. Missing-file replay never
manufactures a completion event. Registered documents and quota accounting are unchanged.

Read the [offline recovery contract](design/phase-6-orphan-recovery.md) and
[verification report](evidence/phase-6/P6-orphan-recovery-20260914.md).
442 distinct checks pass: 200 API, 189 Infrastructure and 53 PostgreSQL, including
16 new recovery cases. The final PostgreSQL run replaces the initial recovery run.
Documentation and whitespace checks pass.
Artifacts are in `outputs/phase-6/orphan-recovery-20260914`. All 34 retained
fingerprints match; keep checking silently. No new migration, live cleanup, deployment,
browser UI change, commit or paid call occurred.

The recovery commands require `OfflineMaintenanceConfirmed: true` and all storage
writers stopped throughout inventory/execution. This flag acknowledges an operator
precondition; it does not detect or stop processes. Inventory is limited to 1000
entries and 100 sources. Plans expire after 24 hours; minimum source age is 24 hours.
Unknown layouts/organizations, registered, held, young or inaccessible sources are
preserved. Partial failures require audit/filesystem investigation. Existing leased
quarantine cleanup continues to handle staged uploads.

Schema 2 `export-controls` snapshots now include capacity policy presence, limits,
versions and update times. `verify-restore` refuses legacy schema 1 and mismatched
capacity state. Capture current controls from the authoritative database, not from
an unverified restore. Keep the journal and controls independently retained; rolling
back all copies together can evade detection. No online recovery gate is changed.

**Next:** deployment-readiness preflight and verification. Check the current non-secret
configuration and prerequisites for Entra/TLS, restricted API/worker database identities,
scanner, storage permissions, journal/control retention and protected keys. Identify
missing live prerequisites before enterprise activation. Backup expiry, registered-content
restore repair, physical volume limits/monitoring, large-inventory handling and distributed
request limits also remain. Phase 6 is not complete.

Communication preference: discuss retained data/spending only if something changes
or needs attention. Earlier fictional deletion snapshots remain in their evidence folder.

## Prior: durable organization queue and source capacity

Updated September 14, 2026 after Rand approved the next step. Upload and reprocess
admission now enforce privately provisioned organization limits for active jobs and
registered source bytes. PostgreSQL Read Committed transactions serialize admissions
across hosts with an organization-first `FOR NO KEY UPDATE` lock. Worker completion
and audit remain compatible with that lock. Retry/leased jobs remain charged; terminal
work releases slots. Pending source deletion stays charged until completion commits.

Read the [capacity contract](design/phase-6-organization-capacity.md) and
[verification report](evidence/phase-6/P6-capacity-20260914.md). 785 distinct checks
pass: 200 API, 337 Application, 189 Infrastructure and 59 PostgreSQL. Final capacity
coverage replaces its earlier regression-run cases; totals use stable TRX test IDs.
Documentation and whitespace checks pass. Raw artifacts and aggregate verification
are in `outputs/phase-6/capacity-20260914`. All 34 retained fingerprints match; keep
checking silently. No browser UI change, deployment, commit or paid call occurred.

Migration 19 (`20260914125645_AddOrganizationCapacity`) was applied only to disposable
databases. No default quotas are seeded. Enterprise profiles force RequirePolicy;
local profiles may omit policies, but an existing policy is always enforced. Private
EnterpriseAdmin `capacity-policy`/`capacity-status` commands bind to an exact database;
updates require the expected version and commit with System audit. Deployment must
migrate, provision policies and reapply the restricted API SELECT grant. API policy
writes, removal/remapping and populated downgrade are refused.

**Next:** operational recovery of untracked upload files and independent capacity-policy
restore verification. Registered-byte accounting excludes staged/orphan files, derived
content and backups; it is not a hard disk quota. Existing retention-control exports
do not yet pin capacity-policy freshness after restore. Physical storage monitoring,
backup expiry/audited restore repair, edge/distributed request limits, API gate activation
and live Entra/TLS/scanner/storage/key verification remain. Phase 6 is not complete.

Communication preference: discuss retained data/spending only if something changes
or needs attention. Earlier fictional deletion snapshots remain in their evidence folder.

## Prior: request and in-flight resource limits

Updated September 14, 2026 after Rand asked to continue. Process/IP admission now
precedes database access and body buffering; actor/organization budgets follow current
authorization. Mutations and original-file downloads have tighter work budgets.
Admission has bounded partition state, no waiting queue and concurrent leases released
on completion/failure/cancellation. Actual-body caps and a 15-second read deadline
cover unknown-length/chunked input. Generic 429 responses include Retry-After.

Read the [request-limit contract](design/phase-6-request-limits.md) and
[verification report](evidence/phase-6/P6-request-limits-20260914.md). 221 unique checks
pass: 196 API and 25 PostgreSQL. Eight final body checks repeat within the API total.
The documentation build and whitespace checks pass.
Raw evidence and deduplicated counts are in `outputs/phase-6/request-limits-20260914`.
No UI changes, schema migration, deployment restart, commit or paid call occurred.

Limits default on outside Development/Testing; disabling them elsewhere fails startup.
Defaults per 60 seconds: global 1200/IP 300/organization 600/actor 180 requests, with
concurrency 64/16/16/6. Work limits are organization 60 and actor 20, with concurrency
4 and 2. State is capped at 8192 partitions. These are startup configuration values,
per process; replicas multiply capacity and restart resets counters. Forwarded headers
are not trusted. Metrics use bounded stage tags without per-rejection database writes.

Real restricted-login tests prove actor/organization separation, revocation before
throttling, upload refusal without file/queue/audit side effects, and source download
throttling before scanning/release. Exact-limit and excess chunked bodies pass/fail on
real Kestrel; timeout handling distinguishes stalled reads from workflow cancellation.
The earlier failed unknown-length fixture is retained and replaced by real streamed
HttpContent. All 34 retained fingerprints match; check silently on future work.

**Next:** durable queue and storage capacity admission, with atomic organization
limits and safe recovery after work completes or content is removed. Current rate
limits bound incoming/in-flight requests, not persistent backlog or stored bytes.
Distributed/edge throttling, production measurement and telemetry export remain.
API database gate activation, live Entra/TLS/scanner/storage/key verification, backup
expiry and audited restore repair also remain open. Phase 6 is not complete.

Communication preference: discuss retained data/spending only if something changes
or needs attention. Earlier fictional deletion snapshots remain in their evidence folder.

## Prior: restricted API database access and tenant mutations

Updated September 14, 2026 after Rand approved the next task. The API now has an
explicit table/column allowlist, private EnterpriseAdmin `api-role` and
`verify-api-role` commands, and an opt-in per-request catalog guard before membership
lookup. Grant configuration and its System audit event are atomic. Excess/missing
privileges return a generic 503. The gate remains off locally; enterprise deployment
must provision a dedicated login and enable it on every instance.

Read the [API database contract](design/phase-6-api-database-access.md) and
[verification](evidence/phase-6/P6-api-database-20260914.md). 407 unique checks pass:
169 API, 189 Infrastructure and 49 PostgreSQL. Documentation and private CLI builds
pass. Raw evidence, final totals and deduplicated names are in
`outputs/phase-6/api-database-20260914`. The final PostgreSQL run passed 23 cases,
superseding earlier failures. No new schema migration or browser UI change was needed.

Restricted-login signed-token checks cover successful workflows and foreign
document/job/review/field/case/run/finding substitutions, saved-question actor privacy,
and prepared semantic actor/organization binding. Reconciliation link replacement now
records an unlink event instead of failing the generic audited-deletion guard. Audit
failure rolls back the replacement; previous evaluation snapshots survive. The older
audit downgrade test now names its target migration rather than assuming it is latest.

**Next:** request and resource limits: bound expensive upload, processing and query
work, partition authenticated limits by server-resolved actor/organization, bound
unauthenticated requests, and verify throttling/recovery with safe response behavior.
The API service role still accesses allowed rows across organizations; no RLS or
compromised-service isolation is claimed. Deployment activation, live Entra/TLS,
scanner/storage/key protection, backup expiry and audited restore repair remain open.
Phase 6 is not complete. No commit, paid call or production enablement occurred.

Communication preference: run preservation checks silently and mention retained data
or spending only if something changes or needs attention. All 34 fingerprints matched.
Earlier fictional deletion snapshots remain in their prior evidence directory.

## Prior: stored-document deletion and recovery verification

Updated September 13, 2026 after Rand approved proceeding. Administrators can inspect
and explicitly delete expired unreferenced local documents, with hold/dependency
blockers, confirmation, pending-file retries and separate audit stages. An independent
pinned journal precedes database removal; immutable deletion records survive source
removal and permit replay. Enabled business routes and worker sessions reject restored
deleted content. The private EnterpriseRecovery console exports retention controls and
verifies a restored database/files against supplied authoritative controls and journal.

Read the [deletion/recovery contract](design/phase-6-document-deletion.md) and
[verification](evidence/phase-6/P6-document-deletion-20260913.md). 481 unique checks pass:
169 API, 182 Infrastructure, 66 PostgreSQL and 64 web. Production Next and recovery CLI
builds pass. Five labeled fictional snapshots and raw evidence are in
`outputs/phase-6/document-deletion-20260912`; the directory retains the start date.
Loopback preview port 3020 uses in-memory data; `preview.json` records its process.
Migration `20260912200431_AddDocumentDeletion` was exercised only on disposable data.
All 34 retained fingerprints match. Initial failed runs are preserved and excluded
from final totals. No commit, paid call or production enablement occurred.

**Next:** complete restricted API database grants and the remaining tenant mutation
matrix, then request/resource limits. Deletion remains disabled by default and excludes
reviewed/cited/accounting dependencies. Backup expiry, independently durable journal
storage, snapshot freshness, automatic audited restore reconciliation and deployment
verification remain open. The recovery CLI verifies and refuses mismatches; it does
not repair a restore. Old journal/config rollback is outside the runtime guard's
assurance. Live Entra/TLS, real ClamAV, protected shared keys and storage ACLs/encryption
also remain open. Phase 6 is not complete.

Communication preference: run preservation checks silently and discuss retained data
or spending only if something changes or needs attention. Focus updates on new work.

## Prior: retention controls and quarantine lifecycle

Updated September 12, 2026 after Rand approved proceeding. The browser now exposes
Retention & holds: versioned organization settings, organization/document holds,
release confirmation, document expiry preview and explicit quarantine cleanup.
Defaults retain documents indefinitely and disable cleanup. Stored-document deletion
remains disabled. New Enforced uploads carry organization/document/upload identifiers
and an exclusive lease held through scan and promotion; the old orphan cleaner is
blocked outside Development. Cleanup rechecks holds/membership under an organization
lock and requires durable authorization audit before deletion, with separate completion.

Read the [retention contract](design/phase-6-retention-controls.md) and
[verification](evidence/phase-6/P6-retention-controls-20260912.md). 434 unique checks
pass: 169 API, 172 Infrastructure, 35 PostgreSQL and 58 web. The production Next build
passes. Four labeled fictional snapshots are in `outputs/phase-6/retention-20260912`.
Loopback fixture port 3019 uses in-memory records; read `preview.json` for its process.
Migration `20260912173517_AddRetentionControls` was applied only to disposable test
data. All 34 retained fingerprints match. Earlier raw failed/repeated runs remain
in the evidence folder and are not added to the final total.

**Next:** dependency-aware stored-document deletion and backup/restore behavior,
including hold-aware recovery and preventing deleted content from reappearing after
restore. Current retention only previews document expiry. The quarantine batch is
bounded without a durable cursor; unknown legacy staging and interrupted empty lease
markers remain preserved. Filesystem deletion plus DB audit is not atomic: a durable
authorization without completion is unresolved. Live Entra/TLS, real ClamAV, shared
protected key deployment, storage ACLs/encryption, broader tenant mutations, full API
runtime grants and rate limits remain open. Phase 6 is not complete.

Communication preference: Rand asked why unchanged Phase 5 data/spending was repeated.
Continue preservation checks silently; mention them only if something changes or needs
attention. Keep updates focused on new work, results and next steps.

## Prior: authorized original-file access

Updated September 12, 2026 after Rand asked to keep going. Enterprise readers can
use Download original with an authenticated five-minute grant. Grants bind actor,
organization, membership version, document, hash and size; their credential travels
in a header, never a URL. Redemption rechecks expiry/membership after scanning and
requires durable audit before releasing a verified memory snapshot as an attachment.
Production source access requires HTTPS and Enforced storage. No schema migration.

Read the [source-access contract](design/phase-6-source-access.md) and
[verification](evidence/phase-6/P6-source-access-20260912.md). 399 unique checks pass:
165 API, 161 Infrastructure, 21 PostgreSQL and 52 browser checks. All 34 retained
fingerprints match; original spending and 15 migrations remain unchanged. No paid
call, retained write, model enablement or original-file cleanup occurred.
Evidence and three labeled fictional snapshots are in
`outputs/phase-6/source-access-20260912`. The local fixture uses port 3018; read
`preview.json` for its process. Native blob file-save completion was not established
by in-app automation; HTTP bytes and client initiation are tested separately.

**Next:** quarantine lifecycle and retention rules, with holds and preservation
tests before any cleanup. Live Entra/TLS, real ClamAV, shared protected key-ring
deployment, storage permissions/encryption, broader tenant mutations, API runtime
grants and rate limits remain open. Phase 6 is not complete. Preserve Phase 5 data,
spending and prior evidence; use only disposable databases for new schema tests.

## Prior: storage admission and malware screening

Updated September 12, 2026 after Rand asked to keep going. Enforced uploads now
stage in a separate quarantine directory, enforce byte bounds, hash and scan before
promotion. Clean-scan audit commits with the document/run/job; failed admission
compensates the source. Enforced workers verify stored identity/size/hash and rescan
before extraction. Scanner outages use bounded retries; detection and integrity
failures terminate with safe System audit reasons. Production storage defaults Locked.

Read the [storage contract](design/phase-6-storage-safety.md) and
[verification](evidence/phase-6/P6-storage-safety-20260912.md). 655 unique checks pass:
337 Application, 157 Infrastructure, 153 API and eight PostgreSQL integration checks.
All 34 retained fingerprints match this increment and the previous checkpoint;
464 spend events, 153 reservations and 15 original migrations remain unchanged.
No new migration, paid call, retained write or browser change occurred.
Evidence is in `outputs/phase-6/storage-safety-20260912`.

**Next:** authorized original-file access/expiring links and quarantine lifecycle,
plus private storage permissions/encryption and real scanner deployment validation.
No real ClamAV daemon/signatures or detection quality was verified: tests use injected
outcomes and a real TCP protocol fixture. Live Entra/TLS, remaining tenant mutation
checks, full API runtime grants, retention and rate limits remain open. Phase 6 is
not complete. Preserve prior browser snapshots and all Phase 5 evidence/spending.

## Prior: worker database authority and durable audit

Updated September 12, 2026 after Rand asked to go on. Enforced extraction workers
now use a dedicated restricted database connection and provisioned System actors.
Claims, completions, retries, terminal failures and final lease expiry produce
append-only audit events in the same transaction as state changes. Lease locks
now also bind document/run/attempt, and provider identity comes from the stored run.
The private CLI provisions role grants and new service actors without migrations.

Read the [worker contract](design/phase-6-worker-security.md) and
[verification](evidence/phase-6/P6-worker-security-20260912.md). Worker authority
remains cross-organization and shares the API process; it is not database row-level
isolation. The original Phase 5 database remains read-only, with no retained
migration or paid calls. Provisioning refuses retained `phase5_*` targets.

306 unique checks pass: 35 PostgreSQL security/queue, 123 Infrastructure and 148
API tests. Final guard/provisioning checks also pass and are counted once. All 34
retained fingerprints match, including 464 spend events and 153 reservations;
migration count remains 15. Test database and roles are removed. Documentation
build and whitespace checks pass. Evidence is in `outputs/phase-6/worker-security-20260912`.

**Next:** protected storage and malware quarantine before processing; remaining
tenant mutation checks, live Entra/TLS verification, complete API runtime grants,
retention and rate limits remain open. Phase 6 is not complete.

## Prior: private-service access controls

Updated September 11, 2026 after Rand asked to keep going. Named, scoped, expiring
service credentials now protect the Python boundary before request parsing and
provider setup. .NET selects separate extraction/retrieval/answer credentials,
requires HTTPS in ServiceKeys mode and disables redirects, proxies, cookies and
framework HTTP logs for private clients. API production defaults remain Locked;
Python enterprise mode rejects legacy routes. Local Phase 5 fixtures are retained.

Read the [private-service contract](design/phase-6-private-service-access.md) and
[verification](evidence/phase-6/P6-private-service-access-20260911.md). Service
access telemetry has safe fields; it is not a durable organization audit trail.
584 checks pass: 330 Python application, 111 Infrastructure and 143 API. All 34
retained table fingerprints match, including spending; migration count stays 15.
No paid calls or retained writes occurred. The documentation build passes.

**Next:** worker database least privilege and service-actor audit, remaining
tenant mutation checks, storage/scanning controls and live Entra/deployed TLS
verification. Whole-Phase-6 acceptance remains open. Browser snapshots from the
prior increment remain in `outputs/phase-6/browser-20260911`.

## Prior: browser access and organization UI

Updated September 11, 2026. Rand approved browser sign-in, member management and
audit viewing and requested snapshots. This increment is implemented: MSAL v5
sign-in/logout and isolated redirect bridge; database-backed `/api/session`;
authenticated transport for all prior workflows and history download; organization
member grant/edit/revoke with version-conflict and last-administrator handling;
and a paged, read-only audit viewer. Production defaults to Locked until configured.

Read [browser contract](design/phase-6-browser-access.md) and
[browser verification](evidence/phase-6/P6-browser-access-20260911.md).
44 web checks, 139 API checks and one new signed-token PostgreSQL session check
pass. The production build passes on patched Next.js 16.3.5. Production npm audit
has zero advisories; two moderate development-tool advisories remain. Browser
snapshots and verification are in `outputs/phase-6/browser-20260911`.
All 34 retained table fingerprints match; original migration count remains 15.
No paid calls or retained spending changes. The disposable test database is gone.

**Next:** configure and verify real Entra tenant/SPA/API registration, then continue
worker/private-tool identity and remaining isolation/storage controls in Phase 6.
The UI preview uses explicitly fictional data and a test identity adapter; live
Entra has not been verified. No whole-Phase-6 completion or production-readiness
claim. Prior Phase 5 evidence and backend membership increment remain below.

## Prior API and membership checkpoint

Updated September 11, 2026 after Rand asked to begin Phase 6 enterprise security
and access control. Phase 5 development remains complete; its historical reports,
retained data and spending records are preserved.

**Phase 6 is in progress.** Rand approved the user-to-organization and audit plan.
The backend now has immutable Entra tenant/object identity, database organization
membership, Viewer/Reviewer/Operator data roles and separate membership administration.
Token roles confer no permissions. One active organization per identity is enforced;
revocation applies to the next request with the same token. Private bootstrap creates
a new organization and first administrator with data role None. Scoped/versioned
membership APIs preserve the last administrator under concurrent removal.

Safe security events commit in the business transaction. Authorization denials and
failed admitted mutations use an independent audit context. PostgreSQL blocks audit
UPDATE/DELETE/TRUNCATE; populated security history blocks downgrade. A deployment
script configures a separate runtime role. All 31 business routes are classified;
local development identity cannot use the four enterprise administration routes.

Read the [current identity contract](design/phase-6-identity-contract.md),
[membership/audit contract](design/phase-6-membership-audit-contract.md),
[task ledger](tasks/phase-6.md) and
[increment verification](evidence/phase-6/P6-membership-audit-20260911.md).
Evidence is in `outputs/phase-6/membership-audit-20260911/verification.json` and
adjacent TRX/logs. **All 921 unique backend checks pass**, zero failures/skips:
663 non-PG (111 Domain, 335 Application, 74 Infrastructure, 4 Architecture, 139 API)
and all 258 PostgreSQL checks. Nine focused security scenarios pass; the final two
strengthened same-organization history/source and membership checks pass again.
The full PostgreSQL run took 32 minutes 51 seconds. CLI, migration model and docs
checks pass. `audit-example.json` demonstrates actual safe events from a fictional
organization. The disposable test DB and temporary runtime-test role were removed.
Existing Phase 5 previews were not restarted.

Migration `20260911162600_AddEnterpriseMembershipAndAudit` adds three tables and
has been applied only in the disposable `document_intelligence_test`. The original
`phase5_e2e_20260908` stays at 15 migrations; all 34 whole-table fingerprints match
the before snapshot. No retained actor remapping, data rewrite, provider call, spending
change, commit, new task, automation or subagent. General model answers and arbitrary
live embeddings remain disabled; unknown holds stay retained. Python/web production
code is unchanged; their Phase 5 acceptance remains the prior result.

**Next product work after verification:** browser OIDC sign-in/logout, live Entra
registration verification and organization administration UI. No real tenant is
configured. Worker/private-tool identity, remaining HTTP isolation variants, storage
links, malware scanning, retention/deletion, rate limits and runtime/dependency
hardening remain open. Audit scope and database-owner limitations are explicit in
the contract. Local .NET/ASP.NET runtime remains 8.0.10; this is not production readiness.

## Preserved Phase 5 development closure

Updated September 11, 2026 after Rand asked to proceed with closure.
**Phase 5 development is complete.** The authoritative report is
[development acceptance](evidence/phase-5/P5-development-acceptance-20260911.md).
Case selection pins completed linked reviews; public vector/hybrid uses already
settled exact query/corpus embeddings, with clear 422 when unavailable. New
canonical sources quarantine on the declared hostile patterns; prompt preparation
screens again before reservations. General model answers remain disabled.

860 unique backend checks have passing latest results (611 non-PG, 249 PG),
329 Python and 27 web checks pass. The initial full PG run's historical clock
failures are preserved; affected fixtures use the frozen rate-window midpoint
and were rerun. Production rate admission and stored spending were unchanged.
Frozen HTTP acceptance: 30/30, 54 exact cited-fact references, all saved-history,
replay and changed-input checks. Fresh extraction/review/evidence and Phase 4
reconciliation flows pass. Five browser snapshots saved. All 34 retained tables
unchanged; semantic HTTP used a copied database and added only retrieval rows.
Zero new paid calls or charges. No migration, commit, task or automation created.

Evidence: outputs/phase-5/closure-20260911/verification.json and SNAPSHOTS.md.
Reproduce Python with tools/run_python_regression.py and docs/testing/python-runtimes.md.
The app dependency installation is repaired; application/native share .venv,
fixture generation intentionally uses Pillow 9.4 and OCR uses the pinned image.

Next planned phase is Phase 6 enterprise identity/security. Do not silently enable
the optional model route or arbitrary live query embeddings. Historical private
29/30, varied development 36/36 and paid 17/30 remain distinct immutable results;
unknown spending holds stay retained. Synthetic results do not establish real-world
accuracy or production readiness. Original retained phase5_e2e_20260908 remains
read-only. Disposable PG fixture DB is document_intelligence_test, one runner only.
Local preview API8088/Python8009/web3006 uses phase5_reviewed_public_20260911;
secondary API8090/Python8010 uses phase5_semantic_public_20260911. Read service
JSON files for current PIDs; never reuse old PIDs blindly.

## Preserved pre-closure checkpoint

Updated September 11, 2026 after Rand agreed to the final acceptance audit.
**The audit is complete; it no longer remains the next task.** See
[acceptance audit](evidence/phase-5/P5-acceptance-audit-20260911.md).
Six new PostgreSQL adversarial checks pass: canonical poisoned source/direct
hostile questions, corrupt chunk/page refusal, real active other-actor history
isolation and separate same-key run, superseded zero-hit replay/history denial,
and forged/swapped hit/span rejection. Production code is unchanged in this audit.
SeedTypedFactCorpus now optionally appends/indexes a poisoned page; default fixture
behavior is unchanged. All 14 shared-fixture regression cases also pass (20 unique
PostgreSQL checks total); documentation build passed, all 34 retained tables match.
Results are recorded separately
in outputs/phase-5/acceptance-audit-20260911/verification.json.

Correction to prior status: the approved P5-T01 contract makes model adapters
optional. Do not impose enabling OpenAI/general model answers as an automatic
closure condition. If that route is delivered, its known support/quality and
security failures still require resolution. It remains disabled; no paid rerun.

**Next implementation:** connect reconciliation-case selection to a frozen
reviewed corpus; then complete the declared RAG security matrix and the final
full regression. Also reconcile the delivered retrieval policy with the approved
contract: public evidence defaults to lexical, bounded answers read all reviewed
facts, and evaluated vector/hybrid remains private. Do not silently drop planned
features or merge the historical 29/30, paid 17/30 and revised 36/36 scores.
Enterprise identity remains Phase 6. Phase 5 remains open for these concrete items.

Evidence folder: outputs/phase-5/acceptance-audit-20260911.
Only disposable document_intelligence_test is reset by these PG tests. Retained
phase5_e2e_20260908 must remain unchanged. No new model calls, charges, commits,
tasks, automations or subagents were requested. Prior services/ports and browser
snapshots remain as recorded below; no new UI behavior was introduced this turn.

## Preserved public reviewed-answer checkpoint

Updated September 11, 2026 after Rand asked to keep going.
**The broader development evaluation and public reviewed-answer integration are
implemented and verified.** The `/questions` page offers evidence search (default)
and Answer from reviewed fields. The new POST `/api/cited-questions/reviewed`
atomically stores scoped runs, reviewed-fact hits, answers, claims and citations.
Saved history reads the stored answer. Same-key retries replay; changed inputs
return 409. Active actor/source eligibility is checked on creation, history,
alternate retrieval GET and evidence navigation. No migration or provider call.

The new 36-case varied synthetic fixture initially passed 31/36; version 1.2
passes 36/36 after buyer-TIN/compound-identifier, maximum and plural-item fixes.
It is development feedback, not an independent holdout or old-model improvement.
591 unique non-PG backend checks, eight new PostgreSQL cases and 24 web checks
passed. Final extraction-default alignment to local-1.3.0 was followed by another
74 infrastructure and 83 API checks (repeats, not additional unique cases).
Web lint/build passed. Real services passed 20 earlier evidence-flow checks,
16 new answer scenarios plus a partial-answer supplement (41 cited fact refs),
and three-document reconciliation with all nine expected findings and one saved
resolution. Browser checks and six screenshots cover date, original quote,
history, maximum, refusal and partial answer. All 34 retained tables unchanged;
zero model calls, new reservations or new cost.

See [public integration verification](evidence/phase-5/P5-reviewed-public-verification.md)
and [contract](design/phase-5-reviewed-public-contract.md).
Evidence: outputs/phase-5/reviewed-public-20260911/{verification.json,SNAPSHOTS.md}.
New isolated synthetic acceptance DB: phase5_reviewed_public_20260911; API 8088,
private Python 8009, web 3006. Startup: tmp/start_reviewed_public.ps1. Services
remain running for the local preview. Do not reset retained phase5_e2e_20260908.
Disposable PG tests still use only document_intelligence_test, one runner at a time.
No Python production changes this increment; preceding 329-check/OCR result remains
the due-date checkpoint, not a newly rerun total.

**Next:** final Phase 5 acceptance audit against the existing criteria and remaining
RAG/adversarial cases. General semantic/model-answer acceptance remains unmet;
public model generation stays disabled. Keep enterprise identity in Phase 6.
The new reviewed-field route is bounded, uses all reviewed selected-corpus facts,
and is separately versioned reviewed-facts-corpus-v1 / reviewed-fact-answer-v1.2 /
bounded-english-fact-intents-v1.2. Do not rewrite the frozen historical 29/30 typed
trial or 17/30 paid result, silently backfill old reviews, or automatically spend.
No new paid run, commit, task, automation or subagent was requested.

## Preserved due-date repair checkpoint

Updated September 11, 2026 after Rand approved the due-date fix.
**The missing-field path is fixed and verified on fresh extraction/review.**
The local extractor previously recognized Due date only as a neighboring-field
boundary, not an output field. It now emits due_date as Date for explicit valid
YYYY-MM-DD labels in local-1.3.0. Conflicting or invalid/ambiguous dates suppress
the field with a degraded review warning. ReviewWorkspace includes the missing
due_date row. No date is inferred from Net 30.

The existing native synthetic invoice now extracts due date 2026-08-31 separately
from issue date 2026-08-01. New disposable-database tests confirm it, correct it to
2026-09-15 while preserving original evidence, and clear it with no answer fallback.
550 non-PostgreSQL backend, nine unique PostgreSQL and 329 unique Python checks
passed, including all five pinned OCR cases and the native due-date PDF case.
One initial PostgreSQL case timed out creating the disposable DB; all three
date scenarios passed on the completed rerun. Initial compile/environment logs
remain preserved. Native extraction repeats exactly; all 34 retained tables are
unchanged. Zero provider calls, reservations or new model cost.

See [due-date verification](evidence/phase-5/P5-due-date-verification.md).
Fresh fixture: samples/phase-5/due-date-v0.1.0/{manifest.json,extraction.json}.
Evidence: outputs/phase-5/due-date-20260911/{verification.json,SNAPSHOTS.md}.
Old local-1.2.0 goldens are preserved; tests now use the versioned local-1.3.0
subdirectory, with unchanged prior fixture field/page expectations.
Runtime split: .venv for application (316), tmp/phase2-fixture-runtime-20260910
for fixture generator (7), existing pinned Docker image for OCR (5), and
tmp/phase5-native-runtime-20260908 for new native due-date (1). Do not combine
the fixture generator's Pillow 9.4.0 requirement with the application's 12.3.0.

**Next:** broader varied-document/wording evaluation and public answer/citation/
history integration with authorization, replay and security acceptance. The
private English fact route remains bounded and public generation stays disabled.
Existing completed reviews are immutable: deliberately add/review a missing
date in a new revision or use fresh extraction/review. No silent backfill.
The historical 29/30 typed result and 17/30 paid source-v4 result stay frozen;
do not call this 30/30 or Phase 5 complete. No new paid run, commit, automation,
task or subagent was requested.

## Preserved typed-fact trial checkpoint

Updated September 11, 2026 after Rand approved trying backend typed facts.
The private candidate is implemented and tested: **29/30** unchanged development
references pass, **68** cited references verified, all **30** answers repeat
exactly, all **34** retained tables unchanged, **zero** provider calls/new cost.
The only unmet reference is q009: due_date is absent from completed review.
TRN/TIN are separate strings, amounts use decimal arithmetic and citations are
generated from exact reviewed facts. This is a bounded English route reading
all reviewed facts in the selected corpus, not an improvement score for the old
top-five/model pipeline or an independent holdout.

See [typed-fact verification](evidence/phase-5/P5-reviewed-fact-verification.md)
and [design](design/phase-5-reviewed-fact-answers.md).
Evidence: outputs/phase-5/typed-facts-v020-20260911/{evaluation.json,
repeat-verification.json,verification.json,SNAPSHOTS.md}. All 545 non-PostgreSQL
backend tests, six new real PostgreSQL cases, seven evaluator checks, and pilot
build passed. Shared full-suite TRX filename was overwritten by the test runner;
use backend-tests.log for per-assembly totals. No paid process is running.

Current code: CitedTypedFacts.cs, CitedFactQuestionPlan.cs, CitedFactAnswerEngine.cs,
CitedFactWorkspace.cs, CitedFactSourceStore.cs and the private SyntheticPilot
--typed-source-case branch. No public route, model call, spend admission or answer
write exists on this route. New source policy reviewed-fields-from-selected-corpus-v1;
answer reviewed-fact-answer-v1.1; language bounded-english-fact-intents-v1.1.
Earlier typed v0.1.0 (25/30) and v0.1.1 (28/30) outputs are preserved separately.
The first parser missed ordinary reading verbs; the intermediate breakdown gave
quantity a currency. Both are corrected and regression-tested.

**Next:** handle page-only/missing reviewed evidence deliberately (q009), then
evaluate varied documents/wording and integrate accepted behavior into public
answer/citation/history contracts with authorization, replay and security tests.
Do not fabricate a review decision, change the frozen due-date label, claim
Phase 5 complete, or automatically start another paid run. Public generation
remains disabled. User authorized the offline experiment; no new paid budget,
commit, task, automation or subagent was requested.

## Preserved source-v4 paid checkpoint

Updated September 11, 2026 after all 30 source-v4 question attempts completed.
The TLS certificate-chain problem was reproduced and fixed using system-trusted
certificates with verification enabled. All 25 subsequent requests returned.
Result: 17/30 end-to-end passes; 18 accepted proposals; nine rejections; three
HeldUnknown transport failures. q022 still selects TIN instead of TRN.
Known batch cost USD 0.012233 + USD 0.006 unknown reservations = USD 0.018233
accounted upper bound, within the original USD 0.06 approval. Shared settled
USD 0.027121 + HeldUnknown USD 0.008001. No pending reservations or budget freeze.
No paid process is running; all original outputs and holds remain immutable.
See [current recovered evaluation](evidence/phase-5/P5-source-recovered-evaluation.md).
Phase 5 remains open because answer-quality and public/security acceptance
requirements are not satisfied. This supersedes the historical checkpoints below.
Do not retry any saved case or automatically schedule another paid batch.

## User direction and project context

- Rand asked to proceed, compact context and consult “Start phase 3” if needed.
  That task was read: `019ffad1-6cd5-7f62-9029-281f98836bed`. It shares this repo.
- Phases 1–3 complete; Phase 4 approved September 7. Phase 5 foundation approved
  September 1. No request for a commit, new task, automation or subagent.
- Paid synthetic embeddings and the cheapest-first nano answer pilot are already
  authorized within the shared USD 8 admission ceiling (outer allowance USD 10).
  Do not silently change model/raise the budget. The user subsequently explicitly
  approved the synthetic 30-case v4 run for up to USD 0.06; automatic review accepted
  that launch. Preserve every saved attempt. The connection/guard recovery is now complete; the blocker is answer quality,
  followed by public integration and remaining security acceptance.
- Rand declined investigating tiny unknown charges and lacks the account password.
  Do not revisit OpenAI Usage/account access for the retained holds. No Arabic
  explanation/side-chat work; the old task explicitly cancelled that idea.
- Large existing dirty worktree contains Phases 3–5. Preserve unrelated changes.
  No root AGENTS; read apps/web/AGENTS.md and its local Next guide before web edits.
  No web code changed in this increment. No subagents authorized.

## Current result and immediate next work

- [Nonredundant retrieval](evidence/phase-5/P5-nonredundant-retrieval-verification.md)
  fixes q024: all 46 deduplicated labels are covered, full coverage for 24/24
  answerable cases and macro recall@5 100%. It suppresses only confirmed unchanged
  facts covered by a higher-ranked selected page's exact UTF-8 span. Corrections,
  review-only facts and other-source evidence remain distinct. Policy
  `pgvector-english-rrf60-c20-nonredundant-v1`, top 5 after a bounded 20 fused candidates.
  Original lexical/vector/hybrid results (95.83/95.83/98.61%) remain unchanged.
- [Prompt-v3/minimal evaluation](evidence/phase-5/P5-answer-followup-verification.md):
  19/30 end-to-end, 30/30 structure, 24/28 supported claims. Thirty paid calls,
  USD 0.004464 additional settled; all replayed exactly, 33 citation references
  navigated through the workspace handler, every table fingerprint unchanged.
- [Low-reasoning evaluation](evidence/phase-5/P5-low-reasoning-verification.md):
  23/30 end-to-end, 29/30 structure, 21/25 supported claims. Thirty completed
  calls, USD 0.008392 additional settled. All replayed, including q009's unchanged
  duplicate-citation rejection; 40 citation references navigated; no DB change.
- Low failures: q002 cites quotation number for a total; q009 duplicate citation;
  q011 adds an irrelevant subtotal citation; q013 lacks quotation support for an
  all-three claim; q024 omits invoice; q028 legal determination; q030 uses an
  authorized quotation total for a foreign invoice request. No foreign data was
  retrieved; the foreign mirror is absent, so no live isolation claim is justified.
- After freezing those results, `CitedScopeClaimPolicy` / `cited-scope-restriction-v1`
  now requires empty InsufficientEvidence/unsupported_scope for detected English
  legal/input-tax, foreign-organization and secret requests. Applied to new and
  replayed proposals. Actual q028/q030 are now rejected; q029's correct refusal
  still replays. All fingerprints unchanged. This rejects, never repairs outputs.
- [V4 source context](evidence/phase-5/P5-source-context-verification.md) is now
  implemented behind `CitedGenerationSettings.SourceAwarePilot` (default false).
  Source schema 1.1 exposes document/index/review/extraction identity, filename,
  chunk kind, review action and exact spans. `cited-answer-prompt-v4` requires
  per-citation `supportText`; validator `citation-excerpt-binding-v2` checks exact
  page/reviewed-content membership. Wrong excerpts/downgrades settle known cost
  then reject; replay never redispatches. Original machine quotes remain distinct
  from corrections. Membership is NOT entailment or comparison completeness.
- Next implementation: typed reviewed facts/evidence IDs, server-resolved source
  excerpts and arithmetic, distinct TRN/TIN fields, and pre-generation scope and
  missing-evidence decisions. Then versioned evaluation and public/security
  acceptance. Do not weaken the gate or repair historical outputs. See the current
  evaluation for the observed failures; no further paid run has been scheduled.
- Keep original failed 4/10 baseline and both new reports immutable. These are
  development-set evaluations by Codex manual source review, not independent
  human ratings, holdouts, statistical improvements or real-world accuracy claims.

## Database and accounting

- Retained DB: `phase5_e2e_20260908` at localhost:25434, PostgreSQL 16 + pgvector.
  Local `psql -w -U document_intelligence` works. Never reset this database.
- 15 migrations; 7 documents, 5 indexes, 78 chunks, 69 vectors, 201 retrieval runs,
  123 reservations, 246 artifacts, 371 spend events. No active pending dispatch or
  reconciliation freeze. Four historical provider-free answers; no published claims.
- USD 0.014888 settled + USD 0.002001 HeldUnknown = USD 0.016889 accounted against
  the unchanged USD 8 ceiling. Unknown costs remain null, not falsely settled zero.
- Original embedding hold: operation `ccaecf4e-d611-5042-aa64-84d054d42346`, reservation
  `8ca633af-24ed-400d-ae02-5791b68354ca`, USD 0.000001. User-accepted policy
  [retained unknown holds](design/phase-5-retained-unknown-pilot-policy.md).
- Low-run local transport failure: operation `3ae994cd-4973-4c28-9619-5e886cb51e29`,
  reservation `1c46a20c-025f-45fd-8268-1ce3af187283`, USD 0.002 retained. Old transport
  rebuilt low requests as minimal and rejected before HTTP. Fixed transport now
  uses declared effort; the failed code snapshot and all artifacts are preserved.
  [Separate narrow policy](design/phase-5-local-preflight-hold-v1.md) retains full
  cost under existing authorization without claiming verified zero billing.
  Combined caps: USD 0.002 generation + USD 0.001 embeddings; every amount counts
  against USD 8. Retained rows cannot be redispatched, cancelled, settled or edited.
- Test fixtures create/drop ONLY `document_intelligence_test`, never the retained
  DB. Set TEST_POSTGRES_CONNECTION to the same server, Database=postgres and local
  development credentials. Fixtures remove their disposable DB on completion.

## Frozen inputs and evidence

- NEW `samples/phase-5/answer-source-v0.4.0/plan.json` SHA256
  `56fda3a2d5317cea14700f00965b4a808dc855e9d1d6934d48b8190cce7c08be`:
  30 fresh runs reuse saved query embeddings; hit content/ranks/scores identical
  to low-fixed except IDs. Questions/references unchanged. v4/source schema1.1,
  same nano/low; max30 attempts/.06 reserved within8. Four attempts now saved;
  see the current partial-run checkpoint for their states and costs.
  Output `outputs/phase-5/answer-source-20260911`: retrievals + ledger-before.
  CLI `--source-case`, `--verify-source-case`, `--replay-source-case` and launcher
  `tools/phase5_run_answer_followup.ps1 -SourceContext -ExecutePaid` are wired.
  Reporter `tools/phase5_answer_followup_report.py --source` is tested; still needs
  actual generation outputs, final ledger, digest-bound manual assessments.
- Historical pre-run authorization record (superseded by explicit approval):
  automatic review blocked the command before process creation, then rejected
  reconsideration with prior real-model authorization retrieved from Start phase3.
  The later explicit user approval was accepted and the paid run began.
  Do not route around it. Evidence/turn IDs and both outcomes saved at
  `outputs/phase-5/source-context-20260911/authorization-review.md`.
  Fixed outbound destination api.openai.com count/Responses; only the same three
  generated synthetic documents. Current output now includes four generation
  files, partial-run.json, stopped diagnostics, test logs and SNAPSHOTS.md.

- Dataset: `samples/phase-5/cited-questions-v0.1.0/question-set.json`, 30 questions.
  Reference overlay `evidence-conditioned-refusals-v1`: q025–027 incomplete_evidence,
  q028–030 unsupported_scope with nonempty evidence. Original labels unchanged.
- Retrieval plan `samples/phase-5/retrieval-evaluation-v0.1.0/plan.json` SHA256
  `d4176993fac3bc00d0bdbaf8f1ee9e8bb4f7cda6c7b7ff7d50c3366e723e8406`.
- Minimal plan `samples/phase-5/answer-followup-v0.2.0/plan.json` SHA256
  `f342359d97a8cb176013d4aed01a1833116b0a1fe510a294162586b494b8b752`.
- Original low plan `samples/phase-5/answer-low-v0.3.0/plan.json` SHA256
  `ffca5a39729d0f5a57f72212afa54618824322e0e000bbed9183ff5d41ab7321`.
  Only its q001 dispatched, failing locally. Preserve it.
- Completed low continuation `samples/phase-5/answer-low-fixed-v0.3.1/plan.json` SHA256
  `9695a66bdc7fc3394525d10731537f8d33c326f9a1cc3b1b00d9479cf9a44632`.
  New q001 run, other 29 original low run IDs; actual evidence/ranks match baseline.
- Outputs: `outputs/phase-5/nonredundant-retrieval-20260910/`,
  `answer-followup-20260910/`, `answer-low-20260910/`, `answer-low-fixed-20260910/`.
  Last directory includes scope-policy replay and before/after fingerprints.
- EmbeddingPilot private CLI handles evaluation retrieval/replay; SyntheticPilot
  handles frozen answer cases. `--replay-followup-case` and `--replay-low-fixed-case`
  use OfflineAnswerClient and navigate accepted citations. Historical scope failures
  now reject under the new policy; do not rewrite original replay/result files.
- `tools/phase5_answer_followup_report.py --low-fixed` requires frozen manifest,
  settled ledger, actual output digests and explicit manual assessments. Exclusive
  output creation means it refuses to overwrite a finished report.
- OpenAI snapshot remains `gpt-5-nano-2025-08-07`. Current rate version valid only
  September 10–11 UTC: input/cached/output USD 0.05/0.005/0.40 per million.
  Reverify official rates before later new calls; do not extend automatically.
  Private worker profiles nano-v1/minimal and nano-low-v1/low, no public selector.
  Key stays in .env/private Python worker only; never print it. No retries/fallback.

## Verification and preserved history

- Latest v4: 565 backend passed (510 non-PG +53 targeted PG +2 low/source PG),
  175 cited Python +6 report checks passed. One new mock-stream fixture error in
  first Python run was corrected; final suite passes. Exact .NET/Python roundtrip,
  accounting-before-rejection, schema downgrade, correction and replay covered.
  Output `outputs/phase-5/source-context-20260911`; no model-quality score added.
- Offline audit `tools/phase5_source_context_audit.py` rebuilt all61 historicalv3
  prompt/count/generation hashes exactly and created30 v4 packet snapshots,
  max17,822 generation bytes. Reused historical IDs are preview-only, NOT the
  executable plan. `packets/audit.json` records hashes and limitations.
- Before/after offline retained fingerprint SHA stayed
  `764a7c90a54a982459a2514cb08243d8a0e3aad60d77a27241fcf801462faef4`.
  Subsequent prepared evaluation adds30 retrieval runs/sources/hits only;
  spending, old answer history, documents/chunks/vectors remain unchanged.

- Latest user asked to test the complete implemented flow, earlier-phase impact,
  integrations and save important snapshots. [Full regression evidence](evidence/phase-5/P5-full-regression-20260910.md)
  and `outputs/phase-5/full-regression-20260910/regression-report.json` record:
  703 backend checks (501 non-PG + all 202 PG), 304 unique Python checks across
  application/fixture/pinned OCR environments, and all 20 frontend checks passed.
  All five pinned native/scan/image/degraded/corrupt extraction cases ran in the
  existing Docker image with network disabled and repository source read-only.
  Backend Release, frontend production/lint, model/migration and docs checks pass.
- Real API Phase 4: three reviews, nine exact findings, retained resolution.
  Phase 5: all 20 full-stack assertions; browser independently verified search,
  exact source bytes, review provenance, refusal, saved history and quarantine.
  Seven screenshots and annotated `SNAPSHOTS.md` are in that output directory.
  This accepts implemented evidence workflows, not generated-answer/model quality.
- Harness issue: earlier full PG attempts stalled; one failed process stop briefly
  caused overlapping disposable runners. Those attempts were stopped/preserved.
  Diagnostic run hung at before-settlement crash probe; native cause unproven.
  Test-only crash probes now kill the exact child (no descendants) instead of
  enumerating a process tree. All 11 crash cases passed, then all 191 other PG
  cases passed. No production behavior/schema change; no test assertions weakened.
- Setup dependencies: `.venv` gained declared reportlab 3.6.12. Fixture tests use
  `tmp/phase2-fixture-runtime-20260910` with Pillow 9.4.0; app stays Pillow 12.3.0.
  That runtime and `tmp/dotnet-diagnostics` (dotnet-dump 8.x) are Git-ignored.
- Fresh `retained-database.dump` restored into `phase5_restore_20260910`; all-table
  fingerprints match before/after/final. All seven uploaded documents match their
  archive bytes. SHA-256 manifest plus separate regression DB/upload archives saved.
  Original DB/ledger counts above are unchanged; no paid call ran this increment.
- New test-only DB `phase5_regression_20260910` and restored copy remain; disposable
  `document_intelligence_test` is absent. API 8086, Python 8007, Next 3004 and Docker
  test runtime are stopped. Native PostgreSQL remains running. The in-app test tab
  is temporary; snapshot gallery was queued in the Codex file panel.
- Prior longer checkpoint archived at
  `outputs/phase-5/answer-low-fixed-20260910/continuation-before-final.md`.
  Earlier evidence pages preserve all historical results, authorizations and hashes.
