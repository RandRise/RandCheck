# Phase 5 integration and earlier-phase regression

**Date:** September 10, 2026  
**Scope:** Implemented Phase 5 paths and Phases 1–4 regression; synthetic local data.

The real-service upload, extraction, completed review, reconciliation, indexing,
evidence search, source navigation, saved history, no-hit refusal and quarantine
checks passed. Phase 5 remains incomplete because generated-answer quality and
the public generation workflow are still open. Passing regression does not
change the frozen 19/30 and 23/30 answer evaluations.

## Automated verification

Final machine-readable results and logs are under
`outputs/phase-5/full-regression-20260910/`.

- Backend non-PostgreSQL: 501 checks passed: 111 domain, 234 application,
  74 infrastructure, 78 API and 4 architecture checks.
- PostgreSQL: all 202 cases passed: 11 separate-process generation/recovery crash
  cases after the test-harness adjustment below, followed by the complementary
  191-case run. Their union matches the full discovered inventory exactly.
  Together with the non-PostgreSQL suites, 703 backend checks passed with no
  failures or skips in the final runs. `regression-report.json` validates the
  completed TRX records, unique test names and full database-inventory coverage.
- Python application/evaluation/contract suites: 297 discovered, 292 passed
  and five runtime-gated cases executed separately in the pinned image.
- All seven Phase 2 fixture-generator tests passed in their own environment
  with Pillow 9.4.0 and reportlab 3.6.12. The application retains Pillow 12.3.0.
- All five pinned runtime tests passed in existing Docker image
  `sha256:8225cc6015de6cf0895a355c5d47fe8923a6a9f87a2fc65946e1f7fa52ffc987`:
  native PDF, scanned PDF, PNG/JPEG, degraded rotation, and corrupt/encrypted
  input handling. Repository source was mounted read-only and network disabled.
  This yields 304 unique Python checks across the required environments,
  with no remaining unexecuted runtime case. An additional native golden run
  on Windows passed but is not counted twice.
- All 20 frontend tests passed, along with lint and the production build.
- All 15 migrations applied to a fresh isolated database. EF found no pending
  model changes.
- Backend Release build passed with zero warnings/errors. Documentation build
  and whitespace verification are recorded with this checkpoint.

These checks include earlier-phase extraction contracts, review sequencing and
concurrency, evaluation/report tooling, reconciliation rules, and current
retrieval, accounting, recovery, immutable artifact and source-access boundaries.

## Real-service and browser acceptance

The acceptance API was ASP.NET on localhost:8086, the private Python service on
8007, and the Next.js browser on 3004. Both services used the separate
`phase5_regression_20260910` database and synthetic upload directory. No API key
was loaded and no paid model call ran.

`phase4-full-stack.json` records three uploaded/extracted/completed reviews,
all nine expected finding identities, compared-field snapshots and an append-only
resolution. `phase5-full-stack.json` records all 20 assertions for native extraction,
completed-review catalog, private indexing, selected-document retrieval, exact
source quote, idempotent POST replay, saved GET, no-store headers and a no-hit
refusal.

`browser-verification.json` records the independent UI actions and IDs. The browser
created another reconciliation case, pinned three reviews, obtained nine findings,
and saved an accepted AMT-001 decision. In Phase 5 it pinned the invoice, searched,
opened page 1 with stored UTF-8 bytes 0–970, inspected review provenance, obtained
a no-hit refusal, and reloaded the original retrieval ID.

The exact regression index was then quarantined through scoped test-only SQL.
Source opening was rejected; history reload cleared stale hits and displayed a
safe error. This does not implement a public quarantine administration workflow.

Seven actual screenshots are retained in the output `screenshots/` directory;
`SNAPSHOTS.md` is the annotated gallery. The viewport was 749 × 1200. No new
responsive-breakpoint coverage is claimed. The source viewer displays canonical
extracted text, not a rendered PDF image.

## Failures found and handled

Initial frontend execution failed at Node's parent-directory sandbox access,
before tests ran. The required local access allowed all tests/build/lint to pass.
Initial Python discovery lacked the declared reportlab dependency; after install,
fixture generation correctly rejected the application's newer Pillow version.
Running the fixture generator in its separate pinned environment resolved the
setup mismatch without weakening its dependency checks.

The first broad database attempt stalled. An attempted PowerShell process stop
failed, briefly allowing two runners to collide over the disposable test database.
Both runners were stopped; those attempts are preserved and excluded from final
pass counts. A clean diagnostic attempt then timed out at the before-settlement
generation crash case. Its sequence, mini dumps and available managed stacks
are retained; the dumps did not establish a definitive native hang cause.

The test-only crash probe starts no descendants. Its three termination sites now
kill the exact child instead of requesting Windows process-tree enumeration.
All 11 crash checkpoints passed afterward, with existing durable accounting,
rollback, tenant checks and restart/non-redispatch assertions unchanged. No
production application behavior or database schema changed in this increment.
The two temporary fixture/diagnostic runtime directories are ignored by Git.

## Recovery snapshots and preservation

`retained-database.dump` is a fresh backup of `phase5_e2e_20260908`. It was actually
restored into `phase5_restore_20260910`. Every table fingerprint matches the
original, and the original fingerprints remain unchanged after this regression.
The retained upload archive contains all seven document storage keys; every
archived file was compared byte-for-byte with its source. The snapshot manifest
records SHA-256 digests for both databases, upload archives and screenshots.

The separate regression database and its synthetic uploads were also archived.
This proves database restoration and document archive integrity, not a complete
application boot against the restored copy or off-site disaster recovery.
The retained pilot ledger remains USD 0.014888 settled plus USD 0.002001 held
unknown, totaling USD 0.016889 against the unchanged USD 8 ceiling.

The final post-suite fingerprint still matches the original. The disposable
`document_intelligence_test` database was removed by its fixture. Temporary
Next.js, ASP.NET and Python services are stopped. The OCR container removed
itself; Docker Desktop, started only for that test, was stopped after normal
shutdown timed out and the supported force-stop command succeeded. The native
PostgreSQL server and retained/restore/regression databases remain available.

## Remaining Phase 5 gates

Regression coverage of implemented paths does not finish Phase 5. Explicit
document identity in model evidence, claim/citation support, complete comparisons,
and duplicate prevention still need work followed by a new frozen evaluation.
Public query embedding/generation orchestration, generated-answer UI acceptance,
live adversarial model evaluation and deployment privacy/authentication gates
remain open. No general entailment, real-world accuracy or production readiness
claim follows from these synthetic tests.
