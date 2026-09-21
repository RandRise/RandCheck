# Local backup and recovery contract

September 15, 2026. This is the supported recovery scope for the independent GitHub
project. It combines the [deletion journal](phase-6-document-deletion.md),
[control snapshots and orphan recovery](phase-6-orphan-recovery.md),
[audit archives](phase-6-audit-export-retention.md) and offline registered-file
verification. It does not provision a production backup service or promise an RPO/RTO.

## Retention and expiry

| Material | Local policy |
| --- | --- |
| Accepted database/source backups and their private inventory records | Retain indefinitely. No scheduler, automatic expiry or purge command is supplied. Monitor available space before capture. |
| Current deletion journal and authoritative control snapshots | Retain independently of the application backup set. Never replace current authority with an old backup's copy. |
| Security audit rows and accepted archive/receipt pairs | Retain indefinitely under the audit runbook. Document expiry/deletion does not purge them. |
| Live registered documents | Explicit administrator deletion only when current policy, holds and dependency checks permit it. Expiry alone does not delete anything. |
| Abandoned quarantine and unregistered promoted sources | Existing bounded, explicitly reviewed cleanup workflows; these are not backup-expiry tools. |
| Failed captures, damaged files and unverified restores | Keep private and offline for investigation; do not label them accepted backups or delete evidence automatically. |

Deleting a live document does not erase it from older backups. Backups therefore
remain private and potentially contain deleted material indefinitely. Reclamation
requires a separately reviewed finite-retention design, inventory of every copy,
hold handling and destruction evidence. The application makes no statutory retention
or compliance claim. Unlimited retention is a defined local policy, not unlimited
disk capacity: a failed or incomplete capture must leave the last accepted backup intact.

## Capture and inventory

The supported drill captures a quiescent database and exact source files, then
restores that same capture. Stop every API, worker, uploader, private mutation tool
and other writer first. Offline acknowledgment in the CLI does not stop processes
or prove they are stopped. Keep them stopped throughout capture and verification.

1. Use an existing private backup root outside the source, quarantine and journal
   roots. Create a new uniquely named capture directory; never overwrite an earlier
   capture. Restrict it and all copies to authorized operators. Git ignore rules do
   not provide encryption or filesystem permissions.
2. Record a backup UUID, UTC capture start/end, expected database, application
   revision, migration IDs, tool versions and source root identity in a private
   inventory record. Record the file list, sizes and SHA-256 hashes for the database
   archive and source copy, plus verification results and operator review. These are
   operator records, not an automatically signed application manifest.
3. Keep the current journal independently. Export a fresh schema 2 control file
   from the authoritative database while writers are stopped; record its hash and
   capture time independently. It includes retention, holds and capacity. Also
   retain current membership/revocation and required access configuration evidence
   privately: schema 2 does not compare identity history or validate secret freshness.
4. Use PostgreSQL client tools compatible with the server. For a local PostgreSQL
   16 database, create a custom-format dump and copy all canonical registered source
   files under the same quiescent interval. `pg_dump` gives a database snapshot; it
   does not coordinate the filesystem. Preserve source layout and contents. Keep
   quarantine separate and inactive; it is never promoted merely because it was copied.
5. Verify archive and source-copy hashes, capture the current audit archive/receipt
   where needed, and perform a restore rehearsal before marking the set accepted.
   Keep credentials and private keys in their protected delivery/backup mechanism;
   this workflow does not export or rotate them into the application backup.

Use private PostgreSQL connection environment variables or a protected passfile;
do not put passwords in command arguments. Example commands assume those variables
select the explicitly reviewed database and `$captureDirectory` is a newly created
private capture directory. They are operator instructions, not an automatic script.

```powershell
pg_dump --format=custom --no-owner --no-acl --file "$captureDirectory/database.dump"
dotnet apps/backend/tools/EnterpriseRecovery/bin/Debug/net8.0/EnterpriseRecovery.dll export-controls "$privateConfig"
```

The control file is independently retained, not bundled as a replacement for current
authority. PostgreSQL dumps do not include cluster roles, host configuration, key
permissions or external services. Re-provision dedicated runtime grants from the
current reviewed policies before any future enterprise activation; do not run the
API or worker as the restore owner.

## Restore and registered source verification

1. Restore into an isolated empty database and separate private filesystem roots,
   with ingress and workers stopped. Explicitly select its database name before
   using the restore command below. Do not use clean/drop options against an
   existing valuable database.
2. Verify the archive/source-copy hashes against the independently retained inventory.
   Restore the original relative source paths. Mount the current authoritative
   journal and controls; do not copy old authority over them. Prepare a private
   configuration selecting the restored database and roots.
3. Run `verify-sources`. It includes the existing `verify-restore` control/journal
   checks, then checks every registered file in a read-only database snapshot.
   A failure leaves the deployment offline and changes no rows or files.
4. Investigate unregistered files separately with the orphan inventory while writers
   remain stopped. A successful registered-file check says nothing about unrelated
   files, quarantine, free space or an incomplete filesystem inventory.
5. Review current memberships/revocations, schema/application compatibility, service
   credentials, key material and grants before reopening. Apply the existing strict
   enterprise preflight if activating that profile. A CLI integrity pass is not an
   authorization to start services or evidence of production readiness.

Set `$restoreDatabase` to the explicitly reviewed empty restore database:

```powershell
pg_restore --exit-on-error --no-owner --no-acl --dbname "$restoreDatabase" "$captureDirectory/database.dump"
```

```json
{
  "JournalDirectory": "C:/private/current-deletion-journal",
  "JournalId": "00000000-0000-4000-8000-000000000001",
  "UploadDirectory": "C:/private/isolated-restore/uploads",
  "QuarantineDirectory": "C:/private/isolated-restore/quarantine",
  "ExpectedDatabase": "portfolio_restore",
  "ControlFile": "C:/private/current-controls.json",
  "OfflineMaintenanceConfirmed": true
}
```

These are fictional paths and identities. Supply `PHASE6_RECOVERY_CONNECTION`
privately and run:

```powershell
dotnet apps/backend/tools/EnterpriseRecovery/bin/Debug/net8.0/EnterpriseRecovery.dll verify-sources "$privateConfig"
```

Successful output contains only `Documents` and `Bytes`. Failure uses a fixed safe
message and exit 1; usage errors use exit 2. No document content, source path,
credential, identity or exception details are printed. This is a private operator
read with separately retained execution evidence, not a Human application audit event.

The command requires an exact database match, an existing absolute source root,
Enforced storage and offline acknowledgment. It refuses retained `phase5_` targets.
Controls, deletion records and document metadata share one PostgreSQL Repeatable Read
read-only transaction. `row_security=off` refuses silent RLS filtering rather than
reporting a partial success. Files require canonical organization/document keys,
no reparse-point ancestors, exclusive reads, exact byte lengths and SHA-256 matches.

Coverage is bounded to 1,000 registered documents, 10 MiB per file and 1 GiB total;
exceeding a bound fails without reporting a partial pass. There is a two-minute
verification budget and 30-second SQL statement limit. No per-organization filter
or pagination can accidentally hide unchecked rows. The implementation does not
defend against a privileged process changing paths after inspection; private local
filesystems and stopped writers are required. Network filesystems are unverified.

## Repair decisions and limits

| Condition | Supported response |
| --- | --- |
| Registered source is missing or damaged, and an independently verified copy matches the current database's organization/document/key/hash/size | Keep writers stopped, preserve damaged material as private evidence, verify the replacement copy against that exact metadata, then restore only those bytes to the canonical path and rerun verification. No metadata/hash rewrite is allowed to make different bytes fit. The tool itself performs no repair. |
| Deleted content reappears, journal/tombstone disagree, or a deletion is pending | Keep offline. Inspect the existing deletion journal and retry contract. Do not clear intents, restore deleted content into service, or bypass triggers. Broad automatic registered-content reconciliation is unsupported. |
| Current holds, capacity or retention differ from the restore | Keep offline and use a compatible authoritative capture or investigate privately. Never export fresh controls from an unverified restore to force a pass. |
| Backup predates later work, membership revocation or audit history | Point-in-time reconciliation and proving missing-record completeness are unsupported. Retain the backup privately; compare current authority and history before any recovery decision. Hash matches alone cannot establish recency or authorization. |
| Required independent journal, current controls or trusted inventory are lost | There is no automatic trust reset. Keep the restore unaccepted and offline. |

The local acceptance covers the same-capture database/file rehearsal and exact-byte
repair, plus refusal of stale controls, damaged/missing/unsafe files and restored
deletions. It does not claim automatic disaster recovery after loss of current
authority, historical dependency deletion, backup destruction, large-inventory
support or a deployed recovery SLA. Those require separate implementation and evidence.

## Reproduce the drill

The PostgreSQL fixture recreates only `document_intelligence_test`. Run one database
runner at a time, use the project runtime helper, and provide PostgreSQL client tools
on PATH or through `TEST_POSTGRES_BIN`. The test uses a fictional source, stops its
API hosts, makes an actual custom dump, corrupts fixture state, restores only that
disposable database and exact source bytes, then compares all table fingerprints.
No private source database is dumped, restored or migrated.

```powershell
. ./tools/use-project-runtimes.ps1
# Supply TEST_POSTGRES_CONNECTION privately for the disposable fixture server.
# Set TEST_POSTGRES_BIN if pg_dump, pg_restore and psql are not on PATH.
dotnet test apps/backend/tests/UaeDocumentIntelligence.Persistence.Tests --filter "FullyQualifiedName~Phase6_local_backup_drill"
```

The [verification snapshot](../evidence/phase-6/P6-local-recovery-20260915.md) records
executed results and limits. Local lifecycle acceptance is separate from P6-T09's
final security/secret review and the later clean-checkout demonstration.
