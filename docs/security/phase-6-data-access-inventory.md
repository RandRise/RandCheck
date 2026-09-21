# Phase 6 data-access inventory

September 15 update: source grants use [dedicated certificate-protected keys](../design/phase-6-source-key-protection.md)
with a shared directory and application name. Restart/cross-host checks retain the
current database membership and download-audit boundary. Key generation is a private
filesystem operation; operator rotation records remain outside the business audit table.

Started September 11, 2026; updated September 14 for restricted API mutations. This is a map
for enterprise isolation acceptance; the tested subset is recorded below. The new
public API boundary supplies the server-resolved organization and actor IDs to
existing handlers. There is no global EF tenant filter or PostgreSQL row-level
security policy in this increment; correctness still depends on the explicit
scoping at each application/repository boundary.

| Public operation | Identity propagation and existing data boundary | Enterprise follow-up |
| --- | --- | --- |
| Browser session | `GET /api/session` resolves active membership and organization/actor names from the database; None-role members may inspect their own session | Signed-token role change/revocation test passes; live Entra verification remains |
| Document list/detail and metrics | `DocumentQueries` / `MetricsQuery` pass `execution.OrganizationId`; `DocumentRepository` filters documents, runs and review statistics by that organization | Signed-token two-organization lists, guessed IDs and aggregate non-disclosure |
| Upload | `UploadDocumentHandler` creates the Document with execution organization/actor; staged storage bounds bytes and scans before promotion; enforced queue admission binds scan hash/size to document and audit identity | Atomic clean-scan/upload/job audit and failed-upload compensation implemented; real daemon, storage ACLs and cleanup remain |
| Original-file access | Five-minute protected header grant binds organization/actor/membership version/document/hash/size; both routes require fresh DB permission; reader verifies and scans an immutable snapshot; audit must succeed before release | Signed-token isolation, revocation/expiry during scan and audit-outage checks pass; real scanner/TLS/shared keys and native browser delivery remain |
| Review and review history | Review handlers load with execution organization; mutations record execution actor and use existing concurrency/review rules | Role matrix and audit checks; restricted-login start/action/completion plus foreign review/field substitution pass |
| Processing status, cancellation and reprocess | Processing handlers pass execution organization; queue status lookup, cancel SQL and document locking constrain organization as well as document/job ID | Restricted-login owner cancel/reprocess and foreign document/job refusal pass; existing security audit retained |
| Reconciliation | Handlers scope case and document reads to execution organization; repository scopes the case aggregate and includes its links/runs/findings/resolutions | Restricted-login owner mutations and foreign case/document/run/finding swaps pass; unlink audit rollback and old snapshots checked |
| Reviewed source/case preparation | `CitedCaseSourceStore` checks active actor, case organization, linked documents and completed review source; index/workspace reads retain organization predicates | Restricted-login case-source/index preparation and foreign-target denials pass |
| Lexical/vector/hybrid retrieval | Existing stores constrain organization and selected index IDs, including parameterized SQL. Request-key lookup is organization + actor + key | Signed-token lexical history checks; restricted-login vector/hybrid/nonredundant owner replay and actor/organization outcome substitution checks pass |
| Reviewed answers and saved history | Store locks the active actor and eligible selected indexes; request identity is organization + actor + key. History and evidence handlers also compare run actor with execution actor | Signed-token peer history/citation denial; restricted-login reviewed answer creation and saved-question actor/organization privacy pass |
| Organization membership and audit | Server-resolved organization, separate administrator grant and fresh active-Human check; membership mutations lock that organization; audit listing filters its UUID | Admin UI implemented; larger membership listing and cursor/export policy remain |

Concrete source files include `DocumentRepository.cs`, `ExtractionJobQueue.cs`,
`ReconciliationRepository.cs`, `CitedCaseSourceStore.cs`, `CitedRetrievalStore.cs`,
`CitedSemanticRetrievalStore.cs`, `CitedReviewedQuestionStore.cs` and
`CitedWorkspaceStore.cs` in Infrastructure/Persistence. Application counterparts
include `ProcessingHandlers.cs`, `ReconciliationHandlers.cs`,
`CitedRetrievalWorkflow.cs`, `CitedQuestionWorkflow.cs`, `CitedReviewedQuestion.cs`
and `CitedWorkspace.cs`. Some lower-level reads scope only by organization; their
public handler adds the actor check. Exposing a repository method directly would
therefore bypass a material part of the existing policy.

## Trusted paths that require separate controls

- `ExtractionJobQueue.ClaimAsync` and expired-lease terminalization select work
  across organizations. The selected job supplies the organization used to load
  its document/run. This is a trusted service capability, not a human-request
  permission. The [worker contract](../design/phase-6-worker-security.md) now adds
  a dedicated restricted database connection, organization System actors, atomic
  transition audit and document/run/attempt lease binding. Cross-organization
  authority remains trusted; process separation and stronger database enforcement
  against a compromised worker remain open.
- Private Python now authenticates named, scoped, expiring service credentials
  before request parsing, with HTTPS required in ServiceKeys mode. Extraction
  still validates deadline/schema and scoped storage requests. Enterprise modes
  deny `/extract` even if the legacy flag is enabled. Local development retains
  its shared token. See the [service contract](../design/phase-6-private-service-access.md).
  Private network/TLS deployment and worker database authority are separate;
  the public JWT boundary does not protect a separately exposed Python port.
- `LocalDocumentStorage` uses organization/document keys, containment and reparse
  checks. Enforced admission scans in a separate staging directory and workers
  verify stored identity/hash and rescan before extraction. See the
  [storage contract](../design/phase-6-storage-safety.md). Direct filesystem access,
  private permissions/encryption and real scanner deployment
  remain separate concerns that organization filters do not supply.
- Internal infrastructure and standalone synthetic tools retain their previous
  development execution context. They must remain trusted local tools until a
  service/operator authorization contract replaces that assumption.
- Database administration, migrations, retention and recovery can bypass normal
  request handlers. Phase 5 immutability/spending checks are preserved; they do
  not replace least-privilege database roles, audited administration or a retention
  policy. No retained schema/data migration was performed for this increment.

The initial bearer tests prove route admission, permission checks and server identity;
the membership increment joins them to actual PostgreSQL repositories. Signed-token
HTTP scenarios cover document lists/detail, review/history, processing status,
aggregate metrics, reconciliation detail, reviewed source lists, lexical saved
question/retrieval history and citation evidence. Positive owner reads and source lists accompany
foreign-ID denial checks. A same-organization peer can read the shared document
but cannot read another actor's saved question/retrieval or citation evidence.
Additional scenarios cover membership/audit scoping,
revocation with the same token, escalation denial, concurrent administrator removal,
and upload/review/index/question/case audit events. Earlier-phase PostgreSQL tests
continue exercising repository/domain isolation and prepared semantic behavior.

The September 14 increment extends this coverage with foreign-ID mutations and
semantic HTTP variants under a restricted API login, as detailed below. Deployed
service identity, storage permissions and live Entra remain open; these tests do not
establish universal isolation or production readiness.

## Retention administration (September 12)

The seven retention/quarantine routes require the separate administrator grant,
active Human membership and server-derived organization/actor. Administration includes
minimal document ID/date/disposition metadata, including for data role None. It does
not grant source-content access. Foreign hold/document targets return 404; holds,
policy changes and cleanup serialize on the organization row and recheck membership.
Development identity is denied; Production requires HTTPS. Expiry is preview-only.
See the [retention contract](../design/phase-6-retention-controls.md).

## Stored-document deletion and recovery (September 13)

Three additional administrator-only routes inspect deletion, execute a version-bound purge and list deletion history. Current authority and holds are rechecked under the organization lock; source removal is separately authorized and audited. Ordinary business routes and worker queue sessions consult the independent journal when deletion is enabled. See the [deletion/recovery contract](../design/phase-6-document-deletion.md) for exclusions, restore checks and deployment limits.

## Restricted API login and tenant mutations (September 14)

The [API database contract](../design/phase-6-api-database-access.md) restricts the
service login to explicit tables and update columns. An enabled gate verifies its
effective permissions before each API request's membership lookup. It supplies no
RLS: allowed rows across organizations remain accessible to the trusted service.

Signed-token tests with that login exercise successful upload/cancel/reprocess,
review start/action/completion, case link/replacement/evaluation/resolution, index
and case-source preparation, reviewed answers, source download, membership changes
and eligible deletion. Swapped document/job/review/field/case/run/finding IDs are
rejected; denied mutations leave the checked records unchanged. Saved questions
are private to their actor even within one organization. Prepared vector, hybrid
and nonredundant query outcomes cannot be reused by another actor or organization.
Owner replay succeeds without new spending rows.

Live reconciliation link replacement now records an unlink audit event. Failure to
persist that event rolls back the replacement, and prior evaluation snapshots remain.
The gate defaults off locally; activation with dedicated credentials is required on
each enterprise instance. Deployment verification remains; request limits are described below.

## Request admission (September 14)

The [request-limit middleware](../design/phase-6-request-limits.md) admits process/IP
traffic before catalog verification and authentication, then checks actor/organization
budgets after authorization binds identity. It does not replace membership lookup or
tenant predicates. Authorized admission precedes body binding and workflow/recovery
filters. Throttled requests do not scan, upload, enqueue, dispatch a provider or insert
workflow audit events. Ordinary reads have capacity separate from the tighter work
budget, subject to the common process/IP/actor/organization limits. No database rows
are used to store rate counters.

## Durable capacity policy (September 14)

The [capacity policy](../design/phase-6-organization-capacity.md) stores organization,
job/byte limits, version and update time. Private EnterpriseAdmin commands create or
change it with an atomic System audit event; status reads current limits and usage.
The API may only read policy rows; workers have no policy-table grant. Upload and
reprocess admissions use the authenticated organization and serialize quota checks
with their database mutation. Pending source deletion remains charged until completion.
Limits contain no document content. Existing generic failed-request audit captures
capacity refusal. Schema 2 recovery snapshots now compare capacity policies as well as
retention and holds. Their current capture and independent retention remain operational
responsibilities.

## Private orphan recovery (September 14)

The [offline recovery tool](../design/phase-6-orphan-recovery.md) reads canonical source
metadata/content to hash candidates, and reads current organizations, documents,
deletion records, policies and holds. It writes a private inventory plan, then can remove
only reviewed unregistered files while all writers are stopped. It has no public API.
Independent System audit inserts bracket removal; database content and quota accounting
are unchanged. Console output contains counts; private plans contain source keys and
hashes. Unknown layouts, registered sources, holds and deletion intents are preserved.

## Private deployment preflight (September 14)

[Configuration inspection](../design/phase-6-deployment-readiness.md) reads the explicit
input only and reports fixed check IDs/messages. Optional resource verification reads
catalog grants through distinct API/worker logins, then policy/actor coverage and
recovery controls through a verified API login. It inspects directory metadata and the
supplied journal/control snapshot without starting any API, worker or service request.
No audit row is inserted and no file is created by inspection. CLI reports omit values,
credentials, organization IDs and absolute paths. Actual effective deployment settings
and protected operational handling remain the operator's responsibility.

## Audit admission and private export (September 15)

The [local recovery verifier](../design/phase-6-local-recovery.md) is another private
operator read. `verify-sources` requires offline acknowledgment and an exact
database name. It compares current controls/deletion history and reads registered
organization/document/key/hash/size metadata in one read-only Repeatable Read
snapshot, refusing RLS filtering. It opens canonical sources exclusively to verify
hashes and lengths and prints counts only. It does not repair files, import backups,
update database rows, insert Human audit events or bypass normal malware scanning.
Host/DBA restoration and any exact-byte repair require private operator evidence.

The [operational audit inventory](phase-6-audit-coverage.md) maps sensitive writes,
attempts, external file effects and private tools to their actual audit boundary.
Entra mutations/source downloads insert admission after current authorization and
budgets, before binding. New admissions identify one of 25 fixed server operations;
ordinary reads do not append events. Role denials and observed failures remain separate.

Private `EnterpriseAudit` export reads organization identity and scoped audit rows
under operator credentials in a read-only repeatable-read transaction. Its output
and independently retained receipt are private artifacts. It neither inserts a
Human event nor exports null-organization events. Offline archive verification
requires no database access. Source rows and accepted archive/receipt pairs retain
the documented indefinite policy; the [export contract](../design/phase-6-audit-export-retention.md)
defines bounds, late-commit limits and restore verification.

