# Stored-document deletion and restore verification

Implemented September 13, 2026 as a limited extension of
[retention controls](phase-6-retention-controls.md). Deletion is disabled by default.
An administrator must inspect a document and confirm removal explicitly; a retention
window never starts automatic deletion. This is application policy, not a statutory
retention recommendation.

## Eligibility and authority

The server requires current Human membership with `CanManageMembers`, independently
of document roles. Data-only users cannot inspect or delete through these routes.
Organization and actor come from authenticated context; foreign IDs return 404.
Development identity is denied and Production requires HTTPS. An administrator with
role None sees IDs and blockers, not original files or extraction text.

A document must be past its configured retention window, have no organization or
document hold, and have no active processing. Any review session, reconciliation
link/snapshot or cited index blocks deletion. Any non-local extraction provider,
model identity or recorded extraction cost/currency/price basis also blocks deletion.
Unexpected restrictive foreign keys abort the transaction. This increment does not
delete reviewed/cited history or accounting records.

| Route | Behavior |
| --- | --- |
| `GET /api/organization/retention/documents/{id}/deletion` | Current blockers, document/policy versions, run/job counts and deployment flag |
| `POST /api/organization/retention/documents/{id}/deletion` | Requires `expectedDocumentVersion` and `expectedPolicyVersion`; returns Completed or fails without claiming completion |
| `GET /api/organization/retention/deletions?offset=0` | Up to 100 deletion records, including PendingFileRemoval; offset bounded to 10,000 |

The UI requires confirmation for both initial removal and retry. A failed request
directs the administrator to refresh history and inspect the audit trail. HTTP errors
do not expose storage paths, journal entries or exception bodies.

## Commit and retry boundaries

1. Lock the organization, recheck current administration, then lock the document.
   Check versions, policy, holds and dependencies again.
2. Flush an immutable intent to the separate journal before removing any content.
   The intent binds organization/document, source key/hash/size, both versions and
   request time. Time is canonicalized to PostgreSQL microsecond precision before
   writing either copy. Conflicting retries never overwrite an intent.
3. In one database transaction, insert the deletion record and
   `document.database-content-removed` audit event, remove processing jobs and runs,
   cascade owned pages/fields/evidence/warnings, and delete the document.
4. In a new transaction, lock the organization and recheck administration and holds.
   Require exact agreement with the journal; durably audit
   `document.source-purge-authorized`, verify the original file's size/hash under an
   exclusive read handle, remove it, then commit completion and
   `document.deletion-completed` audit together.

Database and filesystem changes are not atomic. A source/audit failure after step 3
leaves PendingFileRemoval. Retry uses the original two versions, checks current holds
again, and safely handles an already absent file. A completed request replays without
new deletion work. A newly restored source under a completed record requires offline
recovery review. A hold placed between the two transactions prevents source removal;
it cannot undo already committed database deletion.

An intent surviving a rolled-back database transaction remains authoritative and
blocks ordinary business access until the exact request can resume. A conflicting
policy/document change requires private operator investigation. There is deliberately
no public override or automatic journal erasure.

Migration `20260912200431_AddDocumentDeletion` adds immutable deletion history.
Only its first completion timestamp may change; delete/truncate and populated
downgrade are rejected. Released holds retain document IDs after content removal.
A scoped insert trigger validates new document holds against live documents, replacing
the live-document FK. Direct document deletion requires a matching deletion record
and no active hold. These guards do not constrain a database owner/superuser who can
alter the schema.

## Deployment configuration

`DocumentDeletion:Enabled` defaults false. Enabling requires Enforced storage, a
fully qualified `DocumentDeletion:JournalDirectory`, and a pinned nonempty UUID in
`DocumentDeletion:JournalId`. Initialize a new empty journal privately; the API never
creates a missing journal. Its directory must be separate from and not contain the
upload/quarantine directories. Missing identity, malformed/unknown entries, reparse
points or conflicting records fail closed. Inspection is bounded to 1,000 intents;
there is no rotation or pruning yet.

Retain the current journal independently of application database/file backups, with
private ACLs and separate durable backup. `Flush(true)` and identity checks do not
prove off-host replication, operator authorization or freshness. Restoring old
configuration/journal copies together can defeat detection. Every API and worker
instance must retain the same enabled configuration and authoritative journal.

The runtime security grant script protects the new table and grants only completion
updates. Full API runtime grants remain a deployment prerequisite: deletion additionally
needs SELECT/row-lock authority over inspected records and DELETE on `documents`,
`extraction_jobs`, `extraction_runs`. Do not use owner credentials to bypass an incomplete
deployment grant review. No live deployment or production deletion was enabled here.

## Offline recovery procedure

The private `apps/backend/tools/EnterpriseRecovery` console provides
`initialize-journal`, `export-controls`, `verify-restore`, and `verify-sources`, plus
[offline orphan inventory/cleanup](phase-6-orphan-recovery.md), each followed by a private
JSON configuration path. Configuration contains `JournalDirectory`, `JournalId`,
`UploadDirectory`, `QuarantineDirectory`, `ExpectedDatabase`, and `ControlFile`.
Database commands read `PHASE6_RECOVERY_CONNECTION`, require an exact database-name
match, forbid verbose parameter/error logging, and refuse `phase5_` databases.
The control file must be fully qualified and outside the journal/source/quarantine
roots. Export creates a new file and never overwrites an earlier export.

1. Keep a current authoritative schema 2 export of retention and capacity policies and active/released holds
   outside the application backup set. Capture it while authoritative writes are
   stopped for recovery, record its capture time, and protect it with the journal.
   An older snapshot cannot prove that more recent holds were preserved.
2. Restore database and files into an isolated deployment with ingress and workers
   stopped. Mount the current journal; do not replace it from the old backup.
3. Run `verify-sources` under the [local recovery contract](phase-6-local-recovery.md)
   to include all registered file hashes/sizes. The narrower `verify-restore` checks
   only the restored database/files against the current control file and journal.
   It compares policy/hold JSON values, including capacity limits and versions, and requires every journal intent to match a
   completed database deletion record, with no matching live document or source file.
4. A failure leaves the deployment offline. Investigate and reconcile restored
   content/controls privately, retaining audit evidence, then verify again. This tool
   is read-only; it supplies neither automatic repair nor an override.
5. Reopen only after verification and deployment checks. Enabled API business routes
   recheck journal/deletion consistency; workers check before opening a queue session.
   Session, audit, membership and retention administration remain accessible for
   investigation. Runtime checks do not replace the offline hold-snapshot comparison.

Schema 1 exports predate capacity policies and are refused. Capture a fresh schema 2
export from the authoritative database; do not manufacture one from an unverified restore.
The independent snapshot's actual freshness remains an operator responsibility.

## Local acceptance and future work

The [local backup/recovery contract](phase-6-local-recovery.md) defines private
inventory, indefinite backup retention, a same-capture restore rehearsal and exact-byte
registered-source repair. Existing backups are not physically erased by document
deletion. Automated expiry, arbitrary historical reconciliation, immutable off-host
authority, large-journal handling and broader dependency-aware historical deletion
remain unsupported. API grants and attempt audit are implemented under their own
contracts; actual deployment verification remains optional for portfolio delivery.
Final Phase 6 security/secret acceptance is still required.
