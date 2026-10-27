# Clean-source delivery checks — October 27, 2026

L2 in progress under the [completion ledger](../tasks/local-completion-20261027.md).
Baseline: 1,388 files. Evidence: `outputs/local-completion-20261027/l2/`.

## Findings and focused fixes

The older CI Python runner used unittest discovery, which omitted newer plain
pytest functions. The added pytest-native runner selects those cases separately,
records node IDs and fails on failed collection, test failures, skips or an empty
selection. Existing runtime-specific unittest groups remain unchanged.

The supplier geometry tests also referenced an ignored historical mixed-language
PDF. A separately pinned one-page fictional table fixture now supplies the same
English table character positions using core Helvetica and no embedded font
program. Its expected rows remain the original frozen S1 truth. Original source
PDFs, truth hashes, receipts and Arabic/OCR measurements are preserved. This is
a new regression input, not a rewritten historical source or weakened assertion.

Fresh focused checks: 153 pytest-native cases passed, with 351 unittest cases
deselected for their existing groups. Four runner regressions verify real
parameterized discovery, no unittest duplication, skip/failure rejection and
empty-selection rejection. The first runner test harness used system temporary
paths and stalled during configuration discovery; moving its synthetic tests
under the repository's temporary directory fixed that harness. Earlier attempts
remain recorded. Warnings are dependency deprecations and a pre-existing helper
class that is not a pytest test class.

The source checkpoint review found no material issue. Its low-priority runner
docstring correction is fixed. The first full CI attempt used source copy
`fb0eb85ca2f9` and `uaedi-ci-163d0117ee35`; its final findings and targeted repairs
are recorded below.

The documented demo stop/start succeeds, reapplies migrations and passes database
health. Exact JSON comparison confirms all six documents, six review histories
and the complete saved case survived unchanged. All 518 runtime source files in
the demo copy match the working implementation. An initial ten-second cold API
read timed out; its later complete comparison passed.

The post-restart browser check timed out first during navigation, then while the
client was still showing Opening invoice checks, with no API responses yet.
This occurred during cold compilation and the heavy CI build. At that earlier
checkpoint the owned demo web was paused, and quiet browser verification and reset
were still pending. The subsequent successful checks below supersede that pending
status; the failed attempts remain evidence. No hosted CI or publication claim is made.

## Full-run findings being repaired

The clean Linux source run built all .NET projects with zero warnings/errors and
passed Domain 217, Architecture 4, Application 441, Infrastructure 222 and API 320
tests, with zero skips. The persistence suite then exposed older fixtures that
focused recent batches did not cover. The full run is not a pass.

- Prompt-preparation tests requested the pilot ledger through ordinary application
  registration, which deliberately excludes that service. Use the existing
  explicit pilot test fixture; preserve all safety/accounting assertions and
  keep production registration unchanged. Independent review identified one more
  affected unsafe-source prompt case in the closure integration tests.
- The restricted-login audit test repeated the exact same document/review pin.
  That request is now intentionally idempotent, so it cannot trigger an unlink
  audit outage. Keep an explicit no-op ETag assertion, then use a different
  completed fictional invoice to test actual replacement, audit failure rollback
  and preservation of the earlier comparison snapshots.

The reviewer found two more embedding-seed calls using ordinary registration.
They now use short-lived pilot providers only to prepare synthetic embeddings;
actual public search/replay and HTTP requests retain ordinary registration.

The first host rerun passed 15 cases but used stale compiled code for six late
edits. Those failures remain in `fixture-repairs.log` and its TRX. A compilation
started while the edits landed and finished afterward; a later incremental build
incorrectly considered that output current. Refreshing only the affected source
timestamps forced recompilation without changing their captured hashes. The
subsequent six-case rerun passed with zero skips (`late-fixture-repairs.trx`).
Across the two focused runs, all 21 distinct related cases passed. Final source
review found no material remaining issue in these repairs.

The original Linux persistence run finished with 472 passed, 24 failed and zero
skips. Two further fixes replace a dated migration-count assertion with the exact
declared/applied chain (including both newer role/policy migrations), and use the
existing CI interpreter contract instead of a Windows-only Python path in the
role-migration CLI test. Schema, permissions, rollback and explicit approval
assertions remain intact. No production code or registration changed.

The targeted Linux recheck overlaid exactly those five changed test files on the
original built image, with source hashes checked before and after. Its 32 cases
cover all 24 original failures and the related pilot-roundtrip scenarios. All 32
passed, zero skips; both temporary database projects cleaned up successfully.
The two private-server startup timeouts passed in this quiet run without changing
their limits. Contention remains a relevant limitation on this small local host.

The five original passing suites and repaired persistence results cover **1,700
original backend cases**. Counts come from TRX executions/counters, not display
names: three different cases share names, and some parameterized cases share IDs.
`backend-final.json` supersedes the retained draft display-name count. The original
all-groups command still failed and stopped before web/Python; this is aggregated
verification with targeted rechecks, not a claim that one full invocation passed.

The quiet post-restart browser reopens the unchanged saved comparison and decision,
with nine API responses, zero browser errors and zero browser API writes. Its first
quiet attempt reached the results but lacked the copied expected-case JSON. The
final attempt uses the unchanged original S6 baseline and passes. The screenshot
was visually checked. Earlier timeout and missing-fixture attempts remain.

The owned demo's fictional database and uploads were archived with SHA-256 receipts;
archive indexes are readable (this is not a restore test). Guarded reset succeeded
for only that demo. Documented startup and the Empty check pass. Direct inspection
confirms zero documents, reviews, cases, comparisons and uploaded files, with all
26 migrations applied (`demo-empty-store.json`). The empty demo is stopped after
verification to avoid idle services. The retained workspace and previews remain
untouched. Remaining Python/fixture/audit CI groups and final independent
evidence review are still pending.

## Web source verification

Source copy `c6e4786d8c9d`, project `uaedi-ci-4c7a95026c6b`: all 560 Vitest
cases and three Node request cases passed, zero skips. ESLint with zero warnings,
TypeScript in the Locked production build, all 13 static routes and the snapshot
documentation build passed. Its temporary project cleanup succeeded. The bundle
is exported for the credential review. Later documentation-only additions need
their own final guide build; they do not invalidate the unchanged web runtime
tests. Evidence: `l2/ci-web.log` and that source copy's `outputs/ci/4c7a95026c6b/`.
