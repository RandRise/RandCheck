# Local recovery verification snapshot

September 15, 2026. [Supported backup/recovery contract](../../design/phase-6-local-recovery.md).

## Result

Private `EnterpriseRecovery verify-sources` now checks surviving registered files
as well as the existing control/deletion rules. It requires offline acknowledgment,
uses a read-only database snapshot, verifies exact source hashes/sizes and refuses
partial coverage. It prints counts only and performs no repair, database write or
malware-scan bypass. No schema, public HTTP route or browser component changed.

The local policy retains accepted backups indefinitely. There is no backup expiry
or purge command. The supported repair is a private, offline replacement with exact
verified bytes for a currently registered source. Arbitrary historical database
reconciliation, restoration of deleted content and automatic reopening are unsupported.

**501 distinct checks pass: 260 API, 219 Infrastructure and 22 PostgreSQL.**
The backend solution builds with zero warnings/errors.
All 22 PostgreSQL cases pass with zero failures or skips: 11 registered-source/drill
cases and 11 existing lifecycle regressions. The initial 10-case verifier run is
superseded and counted once. Build results are recorded in `verification.json`
alongside the final TRX reports and documentation build log.

The restore snapshot verifies all 41 disposable database table fingerprints and
one 23-byte fictional source. All 34 retained-database fingerprints separately match
the preceding checkpoint; that database was inspected read-only. No retained
migration or restoration occurred.

## Evidence

Artifacts are under `outputs/phase-6/local-recovery-20260915`.

| Artifact / check | Scope |
| --- | --- |
| `recovery-final.trx` | Registered-source checks plus actual dump/restore, deletion, hold, orphan, control and append-only regressions |
| `infrastructure.trx`, `api.trx` | Existing infrastructure and API behavior after the shared control-snapshot adjustment |
| `backup-drill-snapshot.json` | Actual synthetic restore receipt, archive digest/size, before/after complete table fingerprints and registered-file totals |
| `verification.json` | Final counts and build results; initial repeated cases are not counted twice |
| `docs-build.log` | Documentation build result |

Registered-source scenarios exercise missing, same-length corrupt, wrong-length,
locked, foreign-key and escaping source paths. Oversized document sets, byte totals
and individual sources fail without accepting partial coverage. CLI cases use a
non-owner read-only login, require offline acknowledgment, reject stale controls,
refuse silent RLS filtering and check safe output. Wrong/retained database names,
relative roots and cancellation are refused. The existing storage path guard also
rejects reparse-point ancestors.

## Actual backup/restore drill

The drill uses only the disposable `document_intelligence_test` database and a
fictional document. Its API hosts are stopped before capture. It creates a real
PostgreSQL custom-format dump and a source copy, then deliberately damages fixture
policy/file state. The original journal and control file remain outside the restore.

Verification must refuse damaged state and continue refusing after database-only
restore while the source is still corrupt. The test preserves damaged bytes as
private evidence, restores the exact original source copy without changing its
database identity/hash, then compares every public table fingerprint with the
pre-capture state and verifies the registered file. The backup archive hash must
remain unchanged. The snapshot records these results; the temporary database dump
and source copies are disposed with the fixture and are not publication artifacts.

This test explicitly guards the exact disposable database before using pg_restore's
clean options. The operator runbook instead directs restores into a new empty
isolated database. PostgreSQL runners are serial; the tool binaries come from PATH
or `TEST_POSTGRES_BIN`. No retained source database is dumped or restored.

## Acceptance limits

The local P6-T07 contract covers indefinite backup retention, same-capture restore
and exact-byte registered-source repair. Controls do not prove that a backup is the
newest one or preserve membership revocations absent from an old capture. Independent
current authority and operator review remain required before reopening. Source
verification does not inventory unrelated files, quarantine, credentials or grants.
It does not establish off-host immutability, a recovery SLA or production readiness.

P6-T09's final security/secret review and claim-to-evidence acceptance remain next,
followed by the clean-checkout portfolio demonstration. No deployment, paid call,
new schema migration, commit or push is part of this increment. Snapshots here are
backend/CLI evidence, not browser screenshots.
