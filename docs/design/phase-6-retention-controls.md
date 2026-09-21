# Retention controls and quarantine lifecycle

Implemented September 12, 2026. Organization administrators can configure retention,
place and release preservation holds, preview document expiry and explicitly clean
abandoned quarantine uploads. The subsequent [document deletion increment](phase-6-document-deletion.md) adds opt-in stored-content removal and restore verification; deployment defaults remain disabled. These settings
implement application behavior; no statutory retention period is inferred.

## Authority and defaults

All seven routes require active, database-backed Human membership with
`CanManageMembers`. Viewer, Reviewer and Operator roles alone grant no retention
administration. The server derives the organization and actor from authenticated
context. Development identity cannot call these routes. Production requires HTTPS.
Administrators with data role None can see document IDs, upload dates and retention
dispositions for this purpose; they do not gain original-file or document-content access.

An absent policy means documents are retained indefinitely and quarantine cleanup
is disabled. `documentDays` is nullable, otherwise 1–36,500 days measured from the
document's original upload time. It flags eligibility for preview and, when separately enabled, explicit deletion. `quarantineHours` is
nullable, otherwise 1–87,600 hours. These are validation bounds, not recommended
retention periods. Neither setting starts a scheduler. Cleanup needs a separate
explicit request, and the UI requires a preview and checkbox before execution.

| Route | Contract |
| --- | --- |
| `GET /api/organization/retention` | Current policy; absent policy has zero UUID version and null durations |
| `PUT /api/organization/retention` | `expectedVersion`, nullable `documentDays`, nullable `quarantineHours`; zero version only for creation |
| `GET /api/organization/retention/preview?offset=0` | Up to 100 document IDs/dates/dispositions, next offset, and the deployment `documentDeletionEnabled` flag |
| `GET /api/organization/retention/holds?offset=0` | Up to 100 active or released holds in stable UUID order |
| `POST /api/organization/retention/holds` | Nullable `documentId` (null means organization), exact reason `Investigation`, `LegalHold` or `AdministrativeReview` |
| `POST /api/organization/retention/holds/{id}/release` | `expectedVersion`; released holds cannot be reopened or edited |
| `POST /api/organization/quarantine/cleanup` | `expectedPolicyVersion`, `execute` defaults false; result gives eligible/deleted/preserved counts and truncation |

Offsets are bounded to 0–10,000. Unknown and foreign document/hold identifiers return
404. Stale versions return 409. There is no client-selected organization, free-form
hold text, retention delete endpoint or automatic hold expiry.

## Holds, locking and audit

Any active organization or document hold wins over expiry. Releasing one hold does
not override another. Released records remain available. A document hold requires
an existing document in the caller's organization. Organization holds also block
abandoned quarantine cleanup. Ordinary failed upload compensation still removes
that request's temporary bytes; quarantine is not a malware evidence archive.

Mutations and cleanup lock the organization row with `FOR NO KEY UPDATE`, then
recheck active administration. This serializes them with each other and existing
membership changes. A hold committed before cleanup obtains the lock is observed;
a concurrent hold waiting behind cleanup takes effect after that batch. A hold
cannot restore bytes already removed by a previously admitted cleanup.

Policy and hold changes commit with append-only audit in the same transaction:
`retention.policy-changed`, `retention.hold-placed`, `retention.hold-released`.
Safe state contains durations, document UUID/scope, enum reason and released flag.
No file content, filename, token or free-form reason is recorded.

Migration `20260912173517_AddRetentionControls` adds organization policies and holds
with organization/document foreign keys, versions and range constraints. PostgreSQL
triggers prohibit hold identity edits, editing released holds, and deleting or
truncating either table. Downgrade refuses populated retention history. Empty-schema
rollback/reapply is supported. EF adds corresponding history guards. The runtime
grant script permits policy-field updates and hold release/version updates only.
Owner/superuser control remains a trusted deployment boundary.

## Quarantine ownership and cleanup

New Enforced uploads use
`{organization:N}.{document:N}.{upload:N}.upload` with a matching `.lease` file.
The uploader exclusively holds the lease across copying, scanning, closing the
source stream and promoting the source. This closes the old gap between finishing
the scan and moving the file. Normal success and failure remove owned staging and
lease files. Abrupt process death may leave both.

Cleanup works only in Enforced mode with a separate sibling quarantine directory.
It checks root ancestors and candidate paths for reparse points. It considers only
canonical filenames belonging to the authenticated organization. Legacy flat UUID
uploads, malformed names, unknown files and other organizations are preserved.
Both the staging file and lease must be older than the configured cutoff, and the
cleaner must acquire the existing lease exclusively. Missing leases and active
uploads are preserved. Any matching persisted document is preserved regardless of
its retention window. The legacy orphan cleaner is disabled outside Development.

Discovery is bounded to 501 matching directory entries and each request examines
at most 100 valid candidates. Truncation is reported. Large directories, including
a first batch dominated by preserved files, need an operator inventory; this
increment does not implement a durable cleanup cursor or automatic traversal.

For each removable staging file, cleanup writes durable
`quarantine.cleanup-authorized` through an independent audit context before touching
the file. Failure here prevents deletion. The organization lock remains held; its
lock mode permits audit foreign-key checks. After removal it records
`quarantine.cleaned`. Filesystem errors record `quarantine.cleanup-failed` where
possible. Normal completion removes the empty lease marker as well; a competing
reader may leave that marker behind.

The filesystem and database do not share a transaction. An authorization event
without a completion event is unresolved, not proof of removal. A crash or audit
failure after deletion returns no successful batch result and may leave an empty
lease marker. Earlier completed deletions cannot be rolled back. Review audit and
filesystem state before retrying; never infer deletion from authorization alone.
The UI discards cleanup approval after an error and requires a fresh preview.

File leases are cooperative host controls, tested on the current Windows local
filesystem. They do not replace private directory ACLs against a privileged local
attacker. Network filesystems, multi-host filesystem semantics, storage encryption,
real scanner deployment and off-host audit retention remain deployment work.

## Remaining retention work

The subsequent [document deletion increment](phase-6-document-deletion.md) adds explicit removal of eligible unreferenced local content and read-only restore checks. The [local recovery contract](phase-6-local-recovery.md) now defines indefinite backup retention, a private restore rehearsal, registered-source verification and exact-byte repair limits. Automatic backup expiry, historical reconciliation and broader deletion remain unsupported; deployment verification is optional for portfolio delivery. The initial retention verification below covers policy, holds and quarantine behavior.

See [verification](../evidence/phase-6/P6-retention-controls-20260912.md).
