# Offline source recovery and capacity restore checks

September 14, 2026. Private recovery now inventories and removes promoted source
files that have no current database registration. This closes the gap after a source
was promoted but upload admission failed or the process stopped before registration.
Existing leased quarantine cleanup continues to handle abandoned staged uploads.

This workflow requires all API instances, uploaders, workers and other storage writers
to be stopped. `OfflineMaintenanceConfirmed: true` is an operator acknowledgment, not
a mechanism that stops or detects those processes. Keep writers stopped from inventory
through cleanup. A promoted file awaiting admission can otherwise look unregistered.
This workflow is not exposed through an API, scheduler or organization administrator UI.

## Inventory and review

Use `EnterpriseRecovery inventory-orphans <private-config.json>` with
`PHASE6_RECOVERY_CONNECTION` supplied privately. The tool requires the exact database,
an intact independent deletion journal and a matching authoritative control snapshot.
It refuses retained `phase5_` databases and verbose connection diagnostics.

```json
{
  "JournalDirectory": "C:/enterprise/deletion-journal",
  "JournalId": "00000000-0000-4000-8000-000000000001",
  "UploadDirectory": "C:/enterprise/uploads",
  "QuarantineDirectory": "C:/enterprise/quarantine",
  "ExpectedDatabase": "enterprise_database",
  "ControlFile": "C:/recovery/current-controls.json",
  "OrphanPlanFile": "C:/recovery/orphans-to-review.json",
  "OfflineMaintenanceConfirmed": true,
  "MinimumAgeHours": 24
}
```

These are fictional paths/identities. Controls and plans must be fully qualified,
outside the journal/source/quarantine roots and free of reparse-point ancestors.
Inventory creates a new plan file and never overwrites one. Console output contains
counts only; the private plan contains source keys, organization/document IDs, sizes,
hashes and timestamps. Protect it with private ACLs; it contains no source body.

Only `organizations/{organization:N}/documents/{document:N}/source.{extension}` is
recognized. Unknown directories, files, organizations and reparse points are preserved.
The minimum age defaults to 24 hours, accepts 24–87600 hours, and is measured from
the file's last-write time. This is a maintenance safeguard, not a retention rule.
Files larger than the supported 10 MiB source limit are preserved for investigation.

Inventory preserves any registered document, matching deletion record or journal
intent, current organization/document hold, young file or file it cannot open
exclusively. It never follows unknown directories recursively. Each pass examines
at most 1000 directory entries and selects at most 100 files; `Truncated` reports
partial coverage. A batch dominated by preserved/unknown entries may require a
private operator inventory. There is no durable cursor or automatic retry loop.

Review the plan and investigate why the files are unregistered before executing it.
Absence from the current database does not prove that an untracked file has no value.
Inventory does not remove files, change quota accounting or write cleanup audit events.

## Execution and audit

`EnterpriseRecovery cleanup-orphans <private-config.json>` executes only the supplied
plan. It rechecks offline acknowledgment and binds the plan to its database, journal,
source root and exact control-file digest. Plans expire after 24 hours. Each source
must have a canonical key, bounded size, valid hash and timestamp older than the plan's
cutoff. Duplicate or escaping keys refuse execution.

For each planned source, a Read Committed transaction locks its organization with
`FOR NO KEY UPDATE`, then rechecks registration, deletion history, journal and holds.
A newly registered file is preserved. Changed control state refuses the run before
removal. The source must still match its planned size, timestamp and SHA-256 under an
exclusive read handle. Changed or inaccessible files stop execution.

Before unlinking, the tool independently commits a System
`storage.orphan-removal-authorized` event. It then removes that one validated file
and commits `storage.orphan-removed`. An unlink I/O failure attempts
`storage.orphan-removal-failed`. Events bind the organization/document and plan ID;
safe state contains only byte count and hash. No document rows, jobs, spending rows,
registered source bytes or directory trees are deleted by this workflow.

Filesystem and database changes cannot commit atomically. Authorization failure leaves
the file intact. Completion-audit failure after removal leaves an unresolved authorized
operation and returns failure. Previously removed files cannot be restored by transaction
rollback. Replaying the plan reports `AlreadyAbsent` without inventing a completion
event. Inspect audit and filesystem evidence before retrying a partial failure.

On Windows, the exclusive read handle must close before unlinking. Stopped writers and
private directory ACLs are required through this gap. Row locks, hashes and reparse
checks do not defend against a privileged local process racing filesystem changes.
Network filesystem semantics and coordinated online cleanup are not verified here.

## Capacity policies in restore verification

`export-controls` now produces schema 2, containing all retention policies, active
and released holds, and organization capacity policies, including limits, versions
and update times. It uses one Repeatable Read snapshot and creates a new file.
Exports larger than the verifier's 1 MiB limit are refused.

`verify-restore` compares these values against the restored database and checks the
independent deletion journal as before. Missing capacity policies, changed limits or
versions and schema 1 exports fail. Capture a fresh schema 2 export from the current
authoritative database; do not relabel an old export or produce its replacement from
an unverified restore. Control snapshots are not automatically upgraded.

Keep the current snapshot and journal outside the application backup set, record
capture time and retain them independently. The tool cannot prove that an operator
supplied the newest snapshot: rolling back database, controls and journal together can
defeat detection. No runtime capacity gate or online configuration is changed here.

## Local recovery and future operations

The [local recovery contract](phase-6-local-recovery.md) adds registered-source
verification, an actual same-capture backup/restore drill and a private manual
exact-byte repair procedure. Backups retain an explicit indefinite policy; no expiry
or purge is implied by source cleanup. Automated historical reconciliation, off-host
immutable control retention, physical-volume limits, large-inventory cursors and
deployment verification remain future work. The [deployment preflight](phase-6-deployment-readiness.md)
continues to inspect declared configuration and optional existing resources without
claiming target-environment verification. Shared-key protection is implemented under
its [separate contract](phase-6-source-key-protection.md).
