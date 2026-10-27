# Simplification verification — October 2, 2026

This record covers fresh checks for the simplification increments. Earlier baseline
results remain in the [audit](../reviews/2026-10-02-simplification-audit.md); those
counts are not fresh results for this implementation.

## Verification matrix

| Increment | Focused checks | Integration and role checks |
| --- | --- | --- |
| Startup/configuration | English adapter parity across root/demo Compose and Python; Compose syntax; backend provider-identity rejection; demo database-health classification | No root Compose launch or retained database use. Supported preview/demo entry points; configuration cannot weaken version validation. Real Docker/OCR acceptance is separate. |
| Upload/processing/review/question retries | Rapid duplicate submission; stable intent after uncertain outcome/remount; changed payload conflicts; acknowledged success creates fresh intent; explicit saved-field revisions; no-op identical decisions; draft retention | Real disposable PostgreSQL atomicity, restart/replay, tenant/actor isolation and current authorization; request keys, ETags, terminal completion, immutable evidence, migration apply/model drift. UI tests for Operator, Reviewer, Viewer, admin-only and combined roles; denied and role-change cleanup. Preview parity is simulated. |
| Document work status | Processing and review states remain independent; list summary status, previous completed review during reprocessing, selected next action | Real tenant-scoped summary projection; no per-document evidence load; permitted/denied roles; refresh/Back; late responses; Arabic names. |
| Questions simplification | Reviewed answers default; named source/context handoff; prepared-source selection; recent results; saved links; advanced retrieval choices | Preparation requires existing Reviewer/Operator permission; Viewer reads allowed prepared sources only. Real actor/tenant-scoped history query; immutable result snapshots, role downgrade, refresh/Back and uncertain request-key retries. |
| Touched workflow/pilot refactors | Existing behavioral tests; default registrations exclude optional pilots without enabling calls | Existing service graph/security boundaries; Arabic evidence; migrations/history retained; paid calls and real identity services remain disabled. Python reader/parser split is follow-up audit scope; extraction remains unchanged unless an implementation finding requires it. |

Each dependent increment waits for focused tests and independent review. Heavy jobs
run sequentially, using `tools/use-project-runtimes.ps1`; UI runs use one worker.
Only failures or changed code justify reruns. Counts are reported by distinct tests,
not summed across overlapping reruns.

## Environment readiness

- Verified project Node `24.21.0`, .NET SDK `8.0.425`, and native PostgreSQL `16.4`.
- Existing `outputs/recovery-case-list-postgres` is stopped (`pg_ctl status`). It will
  not be reused. Backend integration will use a fresh uniquely named cluster under
  `outputs`, bound only to loopback on a verified unused port. The fixture recreates
  and drops only `document_intelligence_test`; its explicit test connection will
  never reference the retained application database.
- Docker engine access currently fails inside the restricted Windows token. This
  is an environment limitation, not a passed Docker/OCR integration check. Narrow
  elevation can be requested if a changed guarantee requires that environment.
- No real identity service, membership change, paid model call or retained data
  operation is part of these checks.

## Fresh increment results

### Startup/configuration — gate passed

- `docker compose -f docker-compose.yml config --quiet` and
  `docker compose -f tools/demo/compose.yml --project-directory . config --quiet`
  passed. These are configuration parses; neither profile was launched. An empty
  task-local `DOCKER_CONFIG` under `outputs` avoids opening the restricted personal
  Docker configuration and does not grant engine access.
- A source parity assertion passed for root Compose, demo Compose, `.env.example`
  and the Python English `ADAPTER_VERSION`: all expect `local-1.3.0`.
- `./tools/demo/test-database-health.ps1`: **5/5 passed**.
- With project runtimes loaded, `dotnet test
  apps/backend/tests/UaeDocumentIntelligence.Infrastructure.Tests/UaeDocumentIntelligence.Infrastructure.Tests.csproj
  --no-restore --filter FullyQualifiedName~PythonExtractionClientTests --verbosity
  minimal`: **18/18 passed**, no skips. Provider identity mismatch rejection remains
  enforced; the startup fix does not weaken validation.
- `node --test tools/ui-preview/fixtures.test.mjs`: **12/12 passed**, no skips.
  This is simulated adapter evidence, not real API/persistence.
- Initial preview HTTP probe failed because no server was running. Starting the
  supported `./tools/ui-preview.ps1 start` entry point succeeded in 2.5 seconds.
  `GET http://127.0.0.1:3410/main.tsx` returned transformed TSX containing the
  archived design link under secondary preview controls. Preview remains running.
- An attempted root ESLint invocation could not find a root config. It was not a
  product failure or passed lint check. The changed tooling entry point was instead
  verified through Vite's actual transform and startup. No full product build was
  required for config/docs plus this tooling-only link move.

Independent startup source review passed. No permission, API payload, migration,
source-evidence or retained-data contract changed. Real Docker/OCR and identity
acceptance remain unverified. No historical broad suite was rerun just to repeat
the audit baseline.

### R4-T05b — gate passed

Real PostgreSQL: **11 distinct tests pass across focused runs**, no skips: nine
independently authored `Document_repeat` tests plus migration apply/rollback/reapply
and model/snapshot drift. The initial 7/9 and next 2/4 failures were test setup/
contract errors and were repaired without changing production behavior: the API
returns 503 for unavailable business audit, purge requires a matching deletion
receipt plus explicit job/run removal, and HttpClient omits empty/whitespace-only
headers (legacy no-key behavior). Final affected reruns passed; no failure remains.

Checks prove six same-key store contenders commit one document/run/job/business
audit; fresh-provider replay after extraction completion preserves it; actor/tenant
keys remain separate and changed hashes conflict; failed transactions leave intent
available; deletion keeps the receipt and blocks recreation; concurrent reprocess
and terminal replay preserve original IDs/status; and restricted real API roles
deny replay after downgrade or revocation. Operator+administrator retains Operator
rights; administrator-only has none. Sequential API replay avoids another scan/file.
Two real API hosts force both scans before commit at exact capacity: both recover
one accepted source, the losing file is compensated, same-key replay still succeeds
at full quota, and a deliberate new key is denied. Scanner, capacity and business-
audit failures leave no receipt/source/job/file and permit later same-key success.
Embedded spaces, 101 characters, multiple-value separators and Arabic keys fail
before scanning or business mutation. Empty strings are validation-unit scope;
the HTTP test does not pretend an omitted header is a transmitted empty key.

The fresh native cluster is `outputs/simplification-postgres-e0a92c6edfd2`, verified
`listen_addresses=127.0.0.1`, port `25439`, synthetic user `simplification_test`.
Restricted-token initdb failed with Windows error 87; narrow escalation succeeded.
Tests explicitly set `TEST_POSTGRES_CONNECTION` to this loopback server/database
`postgres`; the fixture creates/drops only `document_intelligence_test`. No retained
database or previous native cluster is touched. Shutdown is pending final gates.

Commands load `tools/use-project-runtimes.ps1` and use `dotnet test <project>
--no-restore -m:1 -p:BuildInParallel=false --verbosity minimal`:

| Project | FullyQualifiedName filter / result |
| --- | --- |
| Persistence.Tests | Initial `Document_repeat OR Migration_apply_is_idempotent OR Model_matches_the_latest`; affected reruns `Document_repeat_upload_concurrency OR Document_repeat_api_failed_scan OR Document_repeat_api_invalid_operation OR Document_repeat_api_concurrent_uploads`, then final two corrections. 11 distinct pass. |
| Application.Tests | `UploadDocumentHandlerTests OR ProcessingHandlerTests OR ReviewHandlerTests`: 36/36 pass, no skips. |
| Api.Tests | `EnterpriseSecurityTests OR AuditOperationCoverageTests OR ReviewApiContractTests`: 124/124 pass, no skips. |

Initial UI new files: 13/13 pass. Six affected existing UI files: 79/80, exposing
background-upload form reset ownership. The fix resets only the submitted File,
preserving a newly chosen file and current review. Final UI includes that regression,
durable hashing/remount, honest terminal replay and explicit new-intent recovery;
final result is **97/97 across eight affected files**, no skips (17 new checks).
The deterministic digest seam verifies exact FileReader bytes feed the fingerprint,
same file/key recovers across remount and different bytes with identical metadata
rotate the key. This is component evidence, not a real browser's WebCrypto journey.

Final commands:

```powershell
. ./tools/use-project-runtimes.ps1
node apps/web/node_modules/vitest/vitest.mjs run --root apps/web app/page.test.tsx app/review-selection.test.tsx app/review-experience.test.tsx app/document-navigation.test.tsx app/document-repeat.test.tsx app/questions/page.test.tsx app/questions/workflow-context.test.tsx app/questions/repeat-actions.test.tsx --maxWorkers 1 --no-file-parallelism --testTimeout 15000
```

From `apps/web`, with project runtimes loaded: `node
node_modules/typescript/bin/tsc --noEmit` and focused ESLint of `app/page.tsx`,
`app/questions/page.tsx`, `app/document-repeat.test.tsx` and
`app/questions/repeat-actions.test.tsx` passed. `node --test
app/review-request.test.mjs ../../tools/ui-preview/fixtures.test.mjs`: **16/16
passed** (three request tests and 13 simulated fixtures), no skips.

Independent frontend/backend source reviews pass. Production build and final
cross-feature/browser/docs gates will run after the visible simplification settles.
Current tests establish real PostgreSQL/HTTP guarantees above; OCR and real Microsoft
sign-in were not run, paid providers remain disabled, and retained data is unchanged.

### Comparison Settings — automated/source gate passed

**60 distinct UI tests pass** across settings, action repetition, page, workflow
context and metadata files. Initial 59/60 exposed a jsdom limitation: native closed
`details` children still appear in its accessibility queries. The corrected test
asserts the Quantity input belongs to the closed native disclosure; final settings
rerun is 4/4. Actual browser visibility remains a separate final manual check.

The checks preserve default tolerances/If-Match, customized saved inputs, pending
locks and deliberate repeats. A negative tolerance collapsed into Settings causes
native invalid handling to reopen the disclosure and prevents a comparison POST.
Focused ESLint of `app/reconciliation/page.tsx`, `comparison-settings.test.tsx` and
`action-repeat.test.tsx` passes. Root independently reviewed the source. This change
has no API/schema/default-tolerance or historic snapshot effect; static full build
and browser gates will follow settled Documents/Questions changes.

Commands use project Node Vitest with `--root apps/web --maxWorkers 1
--no-file-parallelism --testTimeout 15000`, initial file selections:
`app/reconciliation/comparison-settings.test.tsx`, `action-repeat.test.tsx`,
`page.test.tsx`, `workflow-context.test.tsx`, `case-metadata.test.tsx`.
Only the corrected settings file was rerun.

### Documents work list and review controls — automated/source gate passed

**Three new PostgreSQL tests pass**. The work list performs one detached scalar
query without machine values, field evidence, extraction pages or review actions;
ordering and tenant isolation are checked. Review context remains accurate when an
aggregate stays Ready during a queued/failed replacement, a completed older review
is retained, or an active older review is followed by a new successful extraction.
The real API is no-store and admits Operator, Reviewer and Viewer independently of
admin privileges; admin-only and revoked members are denied. Reads leave document
and processing counts unchanged. Initial 2/3 exposed an invalid unchanged-role
fixture request; the corrected role sequence passes 1/1, preserving eight role
states. These are three distinct checks, not four.

**66 distinct UI tests pass** across the new eight-case simplification file and
Home, review-selection, review-experience, document-navigation and document-repeat.
The retained terminal processing response does not stop polling a newly active
summary. Supported field presets preserve keys/types/line positions, custom invalid
settings reopen, and current review/processing state drives the next action.
Initial 65/66 found an old raw-key display expectation. The narrow rerun passes
9/9 after asserting the intentional `Line unit price` label for both retained line
1 and added line 2; the unchanged occurrence-2 mutation assertion also passes.

API `EnterpriseSecurityTests` and `AuditOperationCoverageTests`: **114/114 pass**,
no skips. Review request and preview fixtures: **16/16 pass** (3 + 13); those
fixtures remain simulated browser/backend evidence. TypeScript no-emit and focused
ESLint of Home, field definitions and the two affected test files pass. Independent
root/reviewer source gates pass. Browser/production/docs gates remain reserved for
the settled final feature changes.

Commands load `tools/use-project-runtimes.ps1`. PostgreSQL uses the fresh loopback
cluster and `dotnet test ...Persistence.Tests.csproj --no-restore -m:1
-p:BuildInParallel=false --filter 'FullyQualifiedName~Document_work_list' --verbosity
minimal`; only the corrected real-API role test is rerun. UI uses project Vitest
`--root apps/web --maxWorkers 1 --no-file-parallelism --testTimeout 15000` selecting
`app/document-simplification.test.tsx`, `page.test.tsx`, `review-selection.test.tsx`,
`review-experience.test.tsx`, `document-navigation.test.tsx`, `document-repeat.test.tsx`;
only corrected `app/page.test.tsx` is rerun. API uses the same serial .NET flags and
filter `FullyQualifiedName~EnterpriseSecurityTests|FullyQualifiedName~AuditOperationCoverageTests`.
Node: `node --test apps/web/app/review-request.test.mjs tools/ui-preview/fixtures.test.mjs`.
From `apps/web`: `node node_modules/typescript/bin/tsc --noEmit` then
`node node_modules/eslint/bin/eslint.js app/page.tsx app/document-fields.ts
app/document-simplification.test.tsx app/page.test.tsx`.

### Questions discovery — automated/source gate passed

**Four distinct new PostgreSQL tests pass**. Exact source lookup restores a pinned
completed revision older than the first 50 sources, preserves its review/extraction
identity, accepts Partial extraction and rejects foreign/mismatched/incomplete
sources. Recent history selects 20 results by date descending then ID ascending,
scoped to the current active actor/organization. A single detached SQL query
projects metadata and checks eligibility with EXISTS; it does not fetch document
values, page evidence, actions or complete answer graphs. Eligible zero-hit
Retrieved, reviewed Completed and deterministic no-evidence Completed results are
included. Real cited Answered pilot history and malformed no-evidence shapes are
excluded. Each offered shape is separately reopened in the test through the
unchanged saved-result reader, preserving business counts.

A two-source zero-hit result disappears if either frozen index is superseded or
quarantined, its review is no longer completed, or its extraction fails. Partial
extraction restores eligibility; inactive actors see no history. Real HTTP checks
verify no-store, current Operator/Reviewer/Viewer access, all combined admin roles,
admin-only/revoked denial, other actors' private history, foreign sources and no
business mutation. Offline synthetic identity metadata is used; no real identity
service or model is contacted. Initial 3/4 exposed a fixture filename assumption;
the assertion now uses the frozen index filename. Corrected exact lookup and
strengthened pilot/reopen case pass 2/2. Counts remain four distinct tests.

Initial **55/55 UI tests pass** across Questions simplification, page, workflow
context, repeat actions and Home simplification. Existing POST body/count tests
now explicitly select POST because the new named-history GET shares the route.
Independent review then identified a bounded case-pagination/history-refresh race;
the fix and three new regression cases pass **50/50** in four affected Questions
files, for **58 distinct checks** including the unchanged eight Home checks.
TypeScript passes. Initial focused lint blocked release because the case-fetch
effect synchronously set loading state (`react-hooks/set-state-in-effect`). Removing
that redundant state update retains initially-true loading and the explicit retry's
synchronous state/ref guard; final new-file rerun passes **11/11**, and focused lint
passes with no warnings. That file has 11 new checks; interim estimates of 9/12 were
not runner counts. Independent backend and frontend source gates pass.
API logging/permissions/audit checks pass **128/128**, no skips. Node request/preview
fixtures pass **17/17** (three requests, 14 simulated fixtures).

PostgreSQL command loads project runtimes and the fresh loopback test connection,
then `dotnet test apps/backend/tests/UaeDocumentIntelligence.Persistence.Tests/UaeDocumentIntelligence.Persistence.Tests.csproj
--no-restore -m:1 -p:BuildInParallel=false --filter
'FullyQualifiedName~Questions_exact_source|FullyQualifiedName~Questions_recent_results|FullyQualifiedName~Questions_discovery_real_api'
--verbosity minimal`. Only corrected exact-source and strengthened bounded recent
cases are rerun. UI uses `node apps/web/node_modules/vitest/vitest.mjs run --root
apps/web app/questions/simplification.test.tsx app/questions/page.test.tsx
app/questions/workflow-context.test.tsx app/questions/repeat-actions.test.tsx
app/document-simplification.test.tsx --maxWorkers 1 --no-file-parallelism
--testTimeout 15000`.
The affected rerun omits unchanged Home. API uses serial .NET flags and filter
`FullyQualifiedName~EnterpriseSecurityTests|FullyQualifiedName~AuditOperationCoverageTests|FullyQualifiedName~CitedWorkflowLoggingTests`.
Node uses the Documents command above. Static uses TypeScript no-emit then ESLint
of `app/questions/page.tsx`, `simplification.test.tsx`, `page.test.tsx`,
`workflow-context.test.tsx`, `repeat-actions.test.tsx` from `apps/web`.
The final lint fix reruns only `app/questions/simplification.test.tsx` and the same
focused lint selection. Production/browser/docs and final graph regression gates
remain pending until workflow/pilot refactoring settles.

## Final regression — automated gates passed

The final suites below are fresh results. They are kept separate from overlapping
increment tests and the earlier audit baseline. Domain source is unchanged by the
workflow/registration refactor, so its independent check ran while that work was
in progress with root approval; it will only be repeated if changed or implicated
by a finding.

| Suite | Fresh result | Artifact |
| --- | --- | --- |
| Domain | 111/111 passed, no skips | `outputs/simplification-final-20261002/trx/Domain.trx` |
| Architecture | 4/4 passed, no skips | `outputs/simplification-final-20261002/trx/Architecture.trx` |
| Application | 378/378 passed, no skips | `outputs/simplification-final-20261002/trx/Application.trx` |
| Infrastructure | 222/222 passed, no skips | `outputs/simplification-final-20261002/trx/Infrastructure.trx` |
| API excluding source-key certificate class | 263/263 passed, no skips | `outputs/simplification-final-20261002/trx/Api.trx` |
| API source-key certificate class | 23/23 passed, no skips | `outputs/simplification-final-20261002/trx/ApiSourceKeys.trx` |

Command: dot-source project runtimes, then `dotnet test
apps/backend/tests/UaeDocumentIntelligence.Domain.Tests/UaeDocumentIntelligence.Domain.Tests.csproj
--no-restore -m:1 -p:BuildInParallel=false --logger 'trx;LogFileName=Domain.trx'
--results-directory outputs/simplification-final-20261002/trx --verbosity minimal`.
After backend graph freeze the same serial command ran Architecture, Application
and Infrastructure, each with its project and TRX filename. These four final suites
contain **715 distinct passed tests**, separate from focused increment counts.
The API split selects `FullyQualifiedName!~SourceKeyProtectionTests`, then the
complementary `FullyQualifiedName~SourceKeyProtectionTests` with `--no-build` and
its separate TRX filename. The latter ran with approved narrow escalation because
prior evidence establishes restricted Windows-token PFX import failures. Only
disposable self-signed certificates are used. The two API selections are disjoint;
all five final non-persistence suites total **1,001 distinct passed tests**, with
no skips. This total excludes overlapping focused tests and PostgreSQL.
The saved `backend-unit-counts.json` verifies each final case by the TRX test-ID and
display-name pair: theory IDs can be shared, while display names can be truncated.

Initial final PostgreSQL selection: **26/37 passed, 11 failed, no skips**, 8m29s,
saved as `Persistence.trx`. All failures were generation/recovery crash variants
reaching the shared schema inventory, which omitted migration 22's new journal
table after their recovery assertions. The test-only correction preserves every
prior expectation and adds the journal table, four checks, primary index, exact
scoped composite primary key, both RESTRICT actor/org foreign keys and exactly two
foreign keys (no source FK, preserving tombstones). Root independently reviewed it
against migration/configuration. No timeout or product behavior was changed.
The first narrow rerun then exposed the next masked table omission: the preserved
`reconciliation_reference_counters` from migration 20. It was interrupted with
Ctrl-C after eight observed failures, all at that inventory comparison; no complete
TRX exists for the interrupted job. `persistence-interrupted-schema.json` records
that diagnostic state, without acceptance or inferred skipped/total counts.
No dotnet/testhost processes or fixture DB sessions remained after cancellation.
Full current table comparison found only those two omissions. The test now also
checks the retained counter's scoped year primary key, organization RESTRICT FK,
exact year/positive-count constraints and existing case metadata checks/indexes,
preserving all earlier expectations. The direct schema check then passed **1/1**;
the final unchanged-timeout crash rerun passed **11/11**, with no skips. Together
with the original 26 passing cases, final PostgreSQL acceptance is **38 distinct
passed tests**, no skips. `PersistenceSchemaDirect.trx`, `PersistenceCrashFinal.trx`
and `persistence-counts.json` preserve the result and case-pair aggregation; reruns
are not added twice. Root independently reviewed the schema corrections and saved
TRX counters. No production schema or behavior change was needed for these fixes.

The final PostgreSQL selection covers `Document_repeat_`, `Document_work_list_`,
`Questions_exact_source`, `Questions_recent_results`, `Questions_discovery_real_api`,
all three `Semantic_retrieval_persists_scores_policy_evidence_and_replays_without_spend`
modes, `Ledger_reservation_survives_new_service_provider_and_replay_never_revives_it`,
`Embedding_ledger_settles_in_same_budget_and_restarts_with_exact_journal_replay`,
`Embedding_ingestion_dispatches_once_stores_and_replays_with_provider_offline`,
`Recovery_execute_preserves_original_actor_and_attributes_operator_with_stable_replay`,
`Pilot_unknown_hold_retains_cost_replays_and_admits_new_work_without_redispatch`,
`Generation_process_crash_preserves_commits_and_restart_never_redispatches`,
`Recovery_process_crash_keeps_accounting_audit_and_replay_atomic`,
`Migration_apply_is_idempotent_and_rollback_reapply_succeeds` and
`Model_matches_the_latest_migration_snapshot`. Each name is selected with
`FullyQualifiedName~`, joined by `|`, using the serial .NET command above and the
Persistence test project. The direct check adds
`Expected_schema_constraints_and_indexes_exist`; the final rerun selects only the
two crash method names. Migration 22 apply/rollback/reapply and the pending-model
check passed on the frozen graph. Pilot dispatches are synthetic fixtures; the
reopened semantic read uses the default core graph without provider spending.

The owned fresh cluster at `outputs/simplification-postgres-e0a92c6edfd2` was verified
on `127.0.0.1:25439`, with the fixture database absent, then stopped using its exact
resolved path. A subsequent `pg_ctl status` confirmed no server running. Logs,
synthetic cluster files and evidence remain preserved; retained databases and the
local preview remain untouched. `postgres-cleanup.json` records the checks.

Release solution build passed with **zero warnings and errors**, using
`dotnet build apps/backend/UaeDocumentIntelligence.sln -c Release --no-restore
-m:1 -p:BuildInParallel=false --verbosity minimal` and saved
`backend-release-build.txt`. Both changed standalone pilot tools outside the
solution also compile in Release with zero warnings and errors. Initial
SyntheticPilot compile failed CS1705 because its generated assets retained older
EF/Npgsql versions than the current Infrastructure dependency graph. An approved
offline restore of each tool using `--source C:/Users/Rand-/.nuget/packages
-p:NuGetAudit=false` refreshed only local cached assets, followed by the same serial
Release build flags. Package declarations/versions are unchanged; no provider was
executed. Initial failure, offline restore and final tool build logs are retained.
Disabling audit for that offline refresh is not a vulnerability audit pass.

Final full UI initially passed **275/277**, with two failures and no skips, preserved
in `web-tests.json`. One upload assertion expected its POST before the extracted
async fingerprint helper had completed; the ownership guard still runs before the
await. The test now waits for exactly one POST after two synchronous submits and
again asserts exactly one after acknowledgement, retaining selected-document,
new-file and no-reset assertions. The other test expected a disabled upload button
for Viewer, while the accepted simplified screen hides uploads. It now verifies the
Documents workspace, absent upload button/file input, no POST and absent admin link.
Root independently reviewed both test-only corrections; no timeout or product
source was changed. The affected two files passed **30/30**. Final acceptance is
**277 distinct passed UI tests**, no skips, using file/full-name identity and treating
the renamed Viewer assertion as the same contract case. `web-tests-corrected.json`
and `web-counts.json` preserve that calculation; overlapping reruns are not added.

Command: `node apps/web/node_modules/vitest/vitest.mjs run --root apps/web
--maxWorkers 1 --no-file-parallelism --testTimeout 15000 --reporter default
--reporter json --outputFile <absolute-output>/web-tests.json`. The corrected run
adds only `app/document-repeat.test.tsx app/security/security.test.tsx` and writes
its separate report. The existing Vite future-loader notice and one existing
comparison Back-navigation React act warning remain visible; neither causes a
failed assertion. No warnings were suppressed.

Final Node request/preview tests pass **17/17**, no skips, with
`node --test apps/web/app/review-request.test.mjs tools/ui-preview/fixtures.test.mjs`,
saved in `node-tests.txt`. These 14 preview fixtures remain simulated evidence.
From `apps/web`, `node node_modules/typescript/bin/tsc --noEmit` and
`node node_modules/eslint/bin/eslint.js . --max-warnings=0` both exit zero on the
final source and corrected tests; `web-static-status.json` records their silent
successful completion.

The final production UI build passed using `node node_modules/next/dist/bin/next
build` from `apps/web`, with `AUTH_MODE=Locked`, `NEXT_DIST_DIR=.next-simplification-20261002`,
`NEXT_PUBLIC_API_URL=http://127.0.0.1:1`, blank Entra settings and telemetry disabled
in that process. Compilation took 52 seconds, TypeScript 41 seconds, and all ten
static pages generated successfully. No identity/provider execution occurred.
The separate ignored output preserves the older production cache. Exact current
`tsconfig.json` and `next-env.d.ts` bytes were saved before Next added its generated
include paths, restored in `finally`, and independently compared byte-for-byte.
`web-locked-build.txt` and `web-locked-build-status.json` preserve evidence. A
metadata-only wrapper serialization typo after successful build/restoration was
corrected and recorded without repeating the build. CRLF-aware
`git -c core.whitespace=cr-at-eol diff --check` exits zero.

The first final documentation build stopped at esbuild config loading because the
restricted Windows token denied parent-directory traversal (`docs-build.txt`).
Approved narrow escalation of the unchanged local command reached Markdown/link
validation and failed **42 unique dead links** (`docs-build-final.txt`). Architecture
and audit references point outside the published docs tree to repository source;
an independent existence scan verifies all 44 raw source references exist, while
VitePress cannot publish their routes. The audit's root README reference is also
outside the site. Root corrected the references into labeled existing repository
paths with an editor-opening explanation, preserving guide links and audit facts.
Dead-link validation remains enabled; no VitePress configuration was changed.
The corrected final build passed in **47.38 seconds**, exit zero, with
`node node_modules/vitepress/bin/vitepress.js build` from `docs`, using the same
approved narrow token context. `docs-build-corrected.txt` preserves the result.
The large-chunk advisory remains visible; there are no dead-link or render errors.

Final fresh acceptance is **1,001 backend**, **38 PostgreSQL**, **277 UI** and
**17 Node request/preview** tests, each counted distinctly within its own suite,
with no skips. Release solution and both standalone tools, full TypeScript/lint,
Locked production UI and documentation builds pass. Overlapping increment checks
and the prior audit baseline are excluded from these fresh totals. The test-only
schema inventory and UI expectation corrections are independently reviewed;
initial failure reports remain preserved. Source/configuration/data boundaries and
the browser limitation below remain explicit. The database cleanup record confirms
the owned fixture database absent and fresh cluster stopped; evidence and preview
are preserved.

### October 2 browser boundary — historical

The reviewer could not complete browser acceptance through the enabled computer-use
surface. Initial inventory exposed IAB/MCP apps, but creating a hidden local preview
tab timed out after 50.9 seconds and required a reset (a tab was nevertheless created).
A quiet inventory found that owned tab at localhost:3410/about:blank; binding it
with `getTab(1, browser 2)` timed out after 29.6 seconds and required a reset. On the
final frozen source, quiet inventory finished after 16.57 seconds with
`apps=[]` and `browsers=[]`; no alternate supported surface was available.

No browser UI actions, role/theme/viewport changes or screenshots were taken. Actual
browser acceptance for all roles, themes, mobile layout, Arabic, native disclosure
visibility/invalid-input reopening and WebCrypto remains pending. Passed PostgreSQL,
HTTP, preview fixtures and jsdom tests do not replace that browser evidence. No
headless substitute or unsupported native control was used.

### October 3 short browser acceptance check passed for sampled actions

Rand requested simple browser actions instead of a full demo run. The in-app
browser became available, and the current-component preview was started using
`tools/ui-preview.ps1 start -NoBrowser` (ready in 2.6 seconds). This check used
synthetic responses on loopback port 3410; it did not launch Docker, extract new
files, rebuild applications, or use real identity/provider services.

| Sampled action | Observed browser result |
| --- | --- |
| Documents and role changes | Operator has upload/start-review controls; Reviewer has start-review without upload; Viewer has neither. Administrator-only redirects to administration with no Work navigation. Administrator + operator exposes both Work and Administration. Revoked access and Signed out gate the workspace. These are the seven exposed preview account choices, not every permission combination in production. |
| Exact Questions handoff | An existing completed invoice opens Questions with its document/review IDs and named source. Add source pins that exact review; reviewed answers are selected by default. |
| Saved question | `What is the total?` returns the synthetic invoice total of 1155.00 AED. Refresh reopens the same result ID, one frozen source and saved-history indicator; the question also reopens by name. The composer remains independent of that saved source. |
| Native Settings validation | Settings initially hides its five tolerance inputs. After Quantity is set to -1 and Settings closed, Run comparison reopens it, focuses Quantity and displays the browser's minimum-zero validation message. Quantity was restored to 0 and Settings closed. No valid comparison was submitted. |
| Light/phone/Arabic sample | At 390 × 844, Arabic payment terms and their original evidence are readable. Measured document/body width is 375 pixels within the 390-pixel viewport; no page-level horizontal overflow is observed. Dark appearance was also inspected on Questions/Compare at the default viewport. |
| Back navigation and restoration | A fresh Documents → Compare navigation followed by browser Back returns to Documents. Operator, Dark and the default viewport are restored; the preview tab remains available. An earlier Back through role-switch history stayed on Documents and was not counted as a case-reopen proof. |

Saved screenshots: `outputs/ui-preview/acceptance-20261003-question.png`,
`acceptance-20261003-validation.png` and `acceptance-20261003-mobile-light.png`.
The preview-only question is `6493931d-0b6d-4010-a73c-b8f6f10582df`; the case
`8830142b-6916-4a7c-b859-278b5348fb14` is named
`Quick browser check — مراجعة`, with one existing quotation review pinned and no
comparison runs. Both remain in disposable preview memory; no reset was performed.

No blocker was observed in these sampled actions. This supersedes the earlier
connector-unavailable result for this bounded browser check. Full R5/R6 acceptance,
real sign-in/OCR, every role on every workflow, keyboard/accessibility coverage,
actual upload hashing/WebCrypto and browser retry races remain separate unverified
checks. The earlier automated counts are unchanged; no broad suite was rerun for
this browser-only verification and result documentation.
