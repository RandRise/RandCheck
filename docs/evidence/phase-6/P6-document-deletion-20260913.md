# Document deletion and recovery verification

September 13, 2026. Rand authorized proceeding with the next Phase 6 increment.
The [deletion contract](../../design/phase-6-document-deletion.md) describes the
implemented scope and deployment limits. Deletion remains disabled by default.

## Results

481 unique checks pass: 169 API, 182 Infrastructure, 66 PostgreSQL and 64 web
(61 Vitest + 3 Node). Production Next build, private recovery console build and
documentation build pass. PostgreSQL runs are serial against the disposable
`document_intelligence_test` database on loopback port 25434.

| Evidence | Scope |
| --- | --- |
| `api.trx` | Full API authentication, permission matrix and existing contract suite |
| `infra.trx` | Full infrastructure suite including 10 new journal/configuration/integrity checks |
| `deletion-pg.trx` | 17 deletion/recovery scenarios |
| `compatibility-pg.trx` | 47 retention/quarantine/source/migration/FK checks; one strengthened deletion test repeats |
| `recovery-cli-pg.trx` | 4 executable/hold/worker/grant checks; one restore test repeats |
| `postgres-unique-tests.txt` | 66 distinct PostgreSQL test names after removing repeats |
| `web-tests.txt`, `web-build.txt` | Confirmation, blockers, retry, safe errors and full web regression/build |
| `cli-checks.json` | Usage refusal, new initialization, repeated initialization refusal and retained-target refusal |
| `preservation.json` | All 34 retained fingerprints match |

Raw files are in `outputs/phase-6/document-deletion-20260912` (work began before
midnight). `verification.json` contains the final totals. Earlier failures are kept
as `infra-initial-worker-registration.trx` and `deletion-pg-initial.trx`; they are not
counted as passing evidence.

The first PostgreSQL run exposed sub-microsecond journal timestamp differences after
database reload and a textual JSON comparison sensitive to escaping. The implementation
now canonicalizes intent timestamps before both durable writes and compares parsed
control JSON values. All affected cases passed after the fixes. A worker unit-fixture
registration failure was fixed by requiring the guard only when deletion is enabled.

## Verified boundaries

- Expired unreferenced local content is removed with its original source, jobs, runs,
  canonical pages, fields, evidence and warnings. Released-hold history and audit survive.
- Holds, active work, retention age, reviewed dependencies, cost metadata and foreign
  providers block removal. Policy changes and new holds invalidate earlier eligibility.
- Foreign organization IDs and data-only members cannot authorize deletion. The
  disabled profile cannot remove files. Replays and concurrent requests complete once.
- Each of three audit-failure boundaries is exercised. Database rollback, pending source
  removal and absent-file retry have distinct expected outcomes; none claims completion
  before the completion transaction commits. Changed source content is preserved.
- A new organization hold blocks pending file removal until explicitly released.
- Restored live documents, restored completed-deletion source files and a missing journal
  block business access. Worker session opening also rejects restored content.
- The real recovery executable exports controls, refuses overwrite, verifies a clean
  state, and fails read-only on restored source files or changed holds. Control comparison
  separately exercises hold additions/releases.
- Deletion history rejects mutation/delete/truncate and populated downgrade. Empty
  downgrade/reapply succeeds; runtime grants permit only the completion column update.
- Existing retention, quarantine and source-access regressions pass under migration 18.

## Visual evidence and limits

Five snapshots use the actual React retention/audit pages with a clearly labeled
fictional identity and in-memory API fixture on port 3020. They show blocked and
eligible deletion, completion, pending retry and expanded audit details. The fixture
cannot delete stored files. `SNAPSHOTS.md` is the gallery; `preview.json` records its PID.

These checks establish the limited local implementation, not deployed backup safety
or full Phase 6 acceptance. Current independent journal retention and hold-snapshot
freshness require operator controls. Backup expiry, automatic audited reconciliation,
complete API runtime grants, live identity/TLS/storage/scanner deployment and the
remaining tenant/rate-limit matrix are still open. No production deletion, retained
migration or paid provider dispatch was enabled.
