# Operational audit coverage

September 15, 2026. This is the supported local enterprise audit contract and the
P6-T05 acceptance map. It covers the Entra API profile, configured enterprise
workers and the supplied private tools. Development fixtures deliberately bypass
some enterprise boundaries. Real identity-provider, host and database-operator
activity logging require future deployment verification.

## Request attempts

`SecurityAuditAdmission` runs after authorization and budgets, before request-body
binding. Each classified mutation and original-file download gets an independently
committed `request.admitted`. A failed insert returns 503 without executing the
operation. Its success means admission only. Business commit events remain separate.

New admissions use the following fixed operation codes in `reasonCode`, prefixed
with `authorized_`. Names come from exact server-owned templates, never client
paths, bodies, query strings or route parameter text. Target categories and parsed
UUIDs retain their existing meaning. Older `authorized_post`, `authorized_put` and
`authorized_get` records remain valid method-only history; no backfill is performed.

| Operation codes | Routes covered |
| --- | --- |
| `membership-grant`, `membership-change` | POST membership collection; PUT membership UUID |
| `retention-change`, `hold-place`, `hold-release` | PUT retention policy; POST hold; POST hold release |
| `document-delete`, `quarantine-cleanup` | POST document deletion; POST quarantine cleanup |
| `source-grant`, `source-download` | POST source-access; GET original source |
| `document-upload`, `document-reprocess`, `processing-cancel` | POST documents, reprocess and job cancel |
| `review-start`, `review-action`, `review-complete` | POST review, action and completion |
| `case-create`, `case-link`, `case-evaluate`, `finding-resolve` | POST case, PUT role link, POST evaluation and finding resolution |
| `evidence-index`, `evidence-prepare`, `evidence-retrieve` | POST cited index, case sources and retrieval |
| `question-ask`, `question-reviewed`, `question-semantic` | POST the three question modes |

The permission inventory currently has 25 admitted operations. The API coverage
test iterates the authorization inventory independently: each admitted operation
must produce a distinct safe code and reach the next delegate without reading its
body. Ordinary GETs must create no admission. Adding an authorized mutation without
an audit classification fails closed and fails this coverage test.

Authentication and role denials produce `access.denied`. Filter-observed mutation
errors produce `request.failed` with a bounded HTTP status reason. A malformed body,
client disconnect, cancellation or process death can leave admission without a
terminal event: its outcome is unknown. Client-supplied request IDs can repeat and
are correlation aids, not unique attempts or verified identity.

## Committed changes and external side effects

Paths below are relative to `apps/backend/src`. Evidence links describe executed
tests; their historical suite totals are not summed into this increment's results.

| Operation / writer | Actor and durable evidence | Commit boundary and evidence |
| --- | --- | --- |
| Organization bootstrap and membership administration; Infrastructure `Persistence/EnterpriseMembershipService.cs`, `SecurityAuditPersistence.cs` | Private bootstrap binds a System actor; HTTP uses the resolved Human actor. `organization.created`, `membership.granted`, `membership.changed`; role/admin/active states only | Business and audit share the transaction. Audit failure rolls back actor, identity and membership. [Membership evidence](../evidence/phase-6/P6-membership-audit-20260911.md), [admission outage correction](../evidence/phase-6/P6-audit-admission-20260915.md) |
| Upload, reprocess, cancel; `Persistence/UploadQueueStore.cs`, `SecurityAuditPersistence.cs` | Human; `document.uploaded`, `document.changed`, `processing.queued`, `processing.changed`; clean scan adds `document.scan-clean` with hash/size | Database admission and audit commit together; failed admission compensates promoted files. Rejected uploads have request admission and an observed failure, not a committed clean-scan event. [Storage evidence](../evidence/phase-6/P6-storage-safety-20260912.md), [admission evidence](../evidence/phase-6/P6-audit-admission-20260915.md) |
| Review and reconciliation; `Persistence/SecurityAuditPersistence.cs` | Human; `review.started`, `review.changed`, `review.action-recorded`, `reconciliation.case-changed`, `.document-linked`, `.document-unlinked`, `.evaluated`, `.finding-resolved` | Same SaveChanges/transaction as the change. Safe enum states exclude corrected field text and notes. Live link replacement rolls back if unlink audit fails. [Membership evidence](../evidence/phase-6/P6-membership-audit-20260911.md), [restricted API evidence](../evidence/phase-6/P6-api-database-20260914.md) |
| Index, retrieval and question persistence; `Persistence/SecurityAuditPersistence.cs` | Human; `evidence.index-changed`, `question.saved` with workflow status; admission distinguishes question modes | Tracked index/run changes and their audit share SaveChanges. Child evidence/answer rows are represented by the parent operation, not separate per-field security events. Existing accounting/recovery ledgers remain separate. [Restricted API evidence](../evidence/phase-6/P6-api-database-20260914.md) |
| Worker claims, expiry, completion, retry and failure; `Persistence/ExtractionJobQueue.cs` | Active organization System actor; `worker.claimed`, `.lease-expired`, `.completed`, `.retry-scheduled`, `.failed`; status/attempt and allowlisted scan failure reasons | Audit and persisted transition/output share the transaction. Missing/inactive actor or audit failure rolls back the transition. Heartbeats and rejected leases do not create transition events. [Worker evidence](../evidence/phase-6/P6-worker-security-20260912.md) |
| Original-file access; API `DocumentSourceEndpoints.cs` | Human; `document.source-access-issued`, `.source-download-authorized`, `.source-access-denied` | Independent durable authorization precedes grant/byte release. Hash/size may appear; paths, filenames and credentials do not. An authorized download does not prove the client received every byte. [Source evidence](../evidence/phase-6/P6-source-access-20260912.md) |
| Retention and holds; `Persistence/SecurityAuditPersistence.cs` | Human administrator; `retention.policy-changed`, `.hold-placed`, `.hold-released`; bounded policy/hold states | Same transaction as policy/hold changes; immutable hold history. [Retention evidence](../evidence/phase-6/P6-retention-controls-20260912.md) |
| Quarantine removal; `Persistence/EnterpriseRetentionService.cs` | Human administrator; `quarantine.cleanup-authorized`, `.cleanup-failed`, `.cleaned` | Independent authorization before removal; completion afterward. Filesystem effects cannot roll back with a database failure. A missing completion remains unresolved. [Retention evidence](../evidence/phase-6/P6-retention-controls-20260912.md) |
| Document deletion; `Persistence/EnterpriseDocumentDeletion.cs` | Human administrator; `document.database-content-removed`, `.source-purge-authorized`, `.deletion-completed` | Independent journal precedes deletion; tombstone/audit and raw SQL content removal share a transaction. Separate authorization precedes physical removal; completion is resumable. [Deletion evidence](../evidence/phase-6/P6-document-deletion-20260913.md) |
| Private offline orphan removal; `Persistence/OrphanSourceRecovery.cs` | System classification under private operator authority; `storage.orphan-removal-authorized`, `.orphan-removal-failed`, `.orphan-removed` | Independent inserts bracket only reviewed file removal. No application Human identity is asserted; absent-file replay does not invent completion. [Orphan evidence](../evidence/phase-6/P6-orphan-recovery-20260914.md) |
| Private API/worker grants, service actor and capacity provisioning; `Persistence/ApiDatabasePolicy.cs`, `WorkerDatabasePolicy.cs`, `WorkerActorProvisioning.cs`, `OrganizationCapacity.cs` | System; `api.database-role-configured`, `worker.database-role-configured`, `worker.actor-provisioned`, `capacity.policy-changed` | Configuration changes and events share transactions. Role events are global (null organization); actor/capacity events have organization scope. [API evidence](../evidence/phase-6/P6-api-database-20260914.md), [worker evidence](../evidence/phase-6/P6-worker-security-20260912.md), [capacity evidence](../evidence/phase-6/P6-capacity-20260914.md) |

## Read authority and explicit exclusions

The coverage review found that semantic retrieval opened an isolated context without
the request audit identity. It now carries the bound context, rejects mismatched
actor/organization bindings and commits `question.saved` with the retrieval and its
hits. Signed-token vector, hybrid and nonredundant fixtures exercise audit-outage
rollback, successful retry and replay without another committed-change event.
Unbound Development contexts retain their prior behavior. This is forward coverage;
older missing events cannot be reconstructed and are not fabricated.

- The organization audit viewer requires the current membership administration
  grant and filters by the resolved organization. Viewing it is an ordinary read;
  it does not recursively append an audit-view event. It uses bounded offset pages,
  which are not a consistent export during concurrent inserts.
- `EnterpriseAudit export` uses private database-operator credentials, a read-only
  repeatable-read snapshot and a separately retained receipt. It inserts no audit
  row and asserts no application Human identity. Offline verification uses only
  the archive and trusted receipt. Both are covered by the
  [private export contract](../design/phase-6-audit-export-retention.md) and
  [executed CLI/archive evidence](../evidence/phase-6/P6-audit-export-20260915.md).
- Unresolved authentication denials and database-role events may have null
  organization. Organization viewers and exports exclude them. Global forensic
  review requires private database authority; no global HTTP viewer/export is supplied.
- Read-only grant checks, capacity status, preflight, recovery inventories and
  control-snapshot checks do not create business audit events. Private plans and
  reports must remain protected. OS access, certificate rotation, file copies,
  database dumps/restores and arbitrary DBA SQL require operator/host evidence;
  this application does not identify or log every private operator action.
- Ingress/budget rejections use bounded metrics rather than audit inserts. Routine
  reads, liveness probes, worker heartbeats and private-service HTTP traffic are not
  an exhaustive security access log. Private-service credential rejection has safe
  service telemetry; it is not an organization Human event.
- Process death or an audit outage before insertion can leave no admission. A
  service or filesystem side effect cannot be made transactional by writing an
  audit row. Application database grants are deliberately restricted, but there is
  no tenant RLS guarantee against a compromised application/database owner.

## Retention, integrity and acceptance

Application audit rows are append-only: EF and PostgreSQL reject rewriting,
deletion and truncation, and populated migration downgrade is refused. These
controls do not protect against a database owner deliberately disabling triggers.
Archive checksums detect changes relative to an independently trusted receipt;
they are not signatures or proof against replacement of both archive and receipt.

The local policy retains source audit rows and accepted archive/receipt pairs
indefinitely. Document deletion does not expire audit events. The tested archive
restore procedure verifies copied files offline without importing or replacing
database history. The [local recovery contract](../design/phase-6-local-recovery.md)
now defines indefinite backup retention, registered-file verification and exact-byte
repair limits. These read-only private checks produce operator evidence, not Human
application events or authority to reopen an old restore.

P6-T05 is accepted for this explicit local contract after the
[coverage verification](../evidence/phase-6/P6-audit-coverage-20260915.md).
This does not complete Phase 6: final security/secret review remains required.
Cloud identity, immutable remote archives,
host logging and deployed operational controls remain future production work.
