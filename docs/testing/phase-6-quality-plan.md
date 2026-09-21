# Phase 6 quality plan

## Final local acceptance — September 16

Use the [claim-to-evidence matrix](../security/phase-6-local-acceptance.md) and its
method-name PostgreSQL filter. Require a nonzero selected test count, no failures
or skips, positive owner paths and denied peer/foreign paths. Keep fresh API,
infrastructure, browser, Python service and scanner results distinct from earlier
worker/storage/restore/runtime evidence; do not sum historical reruns into current
totals. The initial filename-based PostgreSQL filter selected no tests and is not
verification; the final method filter supersedes it.

Build a separate production browser output with synthetic server-only markers.
Review current Git candidates, reachable historical text blobs, explicit application
and database log roots, static JavaScript/maps and prerendered HTML/RSC payloads.
`tools/phase6_secret_review.py` emits paths/rules/line numbers and file hashes, never
matched values. Every accepted finding must match an individually reviewed
path/hash/rule/line decision. Missing builds, unreadable text and oversize inputs
fail the review. Test detection across all scopes, redaction, missing-build refusal
and invalidation of decisions when content changes. Raw database logs remain a
separate sensitive boundary; credential scanning does not establish content privacy.

Do not delete raw logs, private configuration or working databases to obtain a clean
scan. Exclude generated/private working directories from Git and scan selected logs
explicitly. Preserve tracked example configuration. Publication file inventory and
curated screenshots are Phase 8 work, not implied by passing a credential review.

## Local recovery and backups — September 15

Verify the [local contract](../design/phase-6-local-recovery.md) with a read-only
registered-source check and an actual PostgreSQL custom dump/restore against only
`document_intelligence_test`. Match complete table fingerprints and source bytes
after restoring a same-capture backup. Keep the authoritative journal/controls
outside the restored set. Test missing, corrupt, locked and misdirected sources,
bounded coverage refusal, cancellation, offline acknowledgment, exact database
selection, stale controls and RLS refusal under a non-owner read-only login.
Existing deletion, hold, control-snapshot and append-only regressions must pass.

The drill requires `pg_dump`, `pg_restore` and `psql` on PATH or `TEST_POSTGRES_BIN`,
plus the normal disposable PostgreSQL fixture connection. Run only one database
runner at a time. Counts and synthetic fingerprints are evidence; real backup dumps,
private documents, credentials and source copies stay out of the repository. No
production restore, retention SLA, automatic backup expiry or stale-history repair
is claimed. Preserve normal malware checks after integrity verification.

## Operational audit coverage — September 15

The [coverage map](../security/phase-6-audit-coverage.md) is P6-T05's local acceptance
contract. Check every classified operation against the permission inventory for a
distinct server-owned admission reason. Keep raw paths, query/grant values and
non-UUID route values out of events; ordinary reads must remain unaudited. Existing
method-only events remain readable. Verify the new code through malformed-body HTTP
and restricted PostgreSQL membership requests, alongside durable commit, denial,
worker, cleanup, append-only and private export regressions. Semantic retrieval's
isolated context must retain the bound identity: force `question.saved` failure in
vector, hybrid and nonredundant modes, verify no run/hits survive, then retry and
replay without duplicate business audit. Preserve unbound Development behavior.
Historical evidence
for unchanged paths remains separately linked, never added to current test totals.

Private database reads and offline verification use operator receipts, not an
application Human identity. Global events, incomplete attempts, external side
effects and operational logging limits must stay explicit. This closes audit
coverage only; local recovery and the final security/secret review remain required.

## Private audit export and archive retention — September 15

Verify exact organization/time bounds under a read-only database login; no document
table reads or audit-table writes are needed. Test same-time UUID ordering, late
transaction commits, row-limit refusal, missing permissions and silent RLS filtering.
Run the real CLI for export and offline verification, checking fixed failure output
and retained-database refusal. Corrupt data/manifests, scope, order, IDs, required
fields, line boundaries and file sets; verify checksums against an independent
receipt. Interrupt writing and test restored copies without a database connection.
Source audit UPDATE/DELETE/TRUNCATE and destructive downgrade protections must remain.
The [runbook](../design/phase-6-audit-export-retention.md) distinguishes local
indefinite retention from future production expiry automation.

## Audit admission — September 15

Verify malformed/missing JSON body admission, framework route rejection, current
server-derived identity, safe fields, source-download attempts and audit-outage
refusal before body reads. Run the complete API suite to cover Development behavior
and request-limit ordering. Under real PostgreSQL restricted API grants, verify
durable admission and organization-scoped listing, followed by a successful mutation.
Force admission insert failure separately from business audit insert failure: the
latter must still exercise transaction rollback and upload compensation. Replayed
deletion records another admission without repeating deletion work or its events.
Verify that a business audit outage returns 503 while a real unique-index violation
still returns a membership conflict, with neither failure leaving partial writes.
Export and archive/retention acceptance remain separate outstanding work.

## Runtime and dependency baseline — September 15

Capture fresh direct/transitive NuGet, complete npm and Python environment advisory
reports before and after updates. Verify actual SDK/runtime versions and both API
framework minimums; prove the old .NET host refuses the rebuilt private tool. Run
the full non-PostgreSQL backend suite, representative PostgreSQL migration/audit/
tenant/quota/source checks, browser tests/lint/production build and Python runtime
groups. Record native image identity separately from package audit status. The
[runtime contract](../design/phase-6-runtime-baseline.md) defines the selected versions
and [report](../evidence/phase-6/P6-runtime-20260915.md) distinguishes executed checks
from outstanding deployment verification.

## Protected source keys — September 15

The [key contract](../design/phase-6-source-key-protection.md) requires encrypted XML,
restart persistence, shared-instance grants, application isolation and wrapping
certificate rotation. Unsafe paths, plaintext/malformed/oversized XML, DTDs, expired
or missing certificates and lost key directories must refuse access safely. Production
HTTP tests retain current membership enforcement and durable source audit. Direct
and CLI resource probes must create no key. Full API and source/readiness PostgreSQL
regressions accompany the change; deployed identities and shared mounts remain unverified.

## Deployment preflight increment — September 14

The [preflight contract](../design/phase-6-deployment-readiness.md) distinguishes
configuration validity, observed resources and live deployment acceptance. Tests require
fixed diagnostics without credentials/paths, strict bounded CLI input, aligned Entra
browser/API settings, canonical CORS origins, secure database/service profiles and
enterprise resource controls. Actual restricted-login probes compare full-row database
fingerprints and source bytes before/after direct and CLI inspection. Missing capacity
or worker actors, stale controls and privilege drift block readiness. Complete API
regression accompanies these checks; the tool never reports a fully verified deployment.

## Offline recovery increment — September 14

The [recovery contract](../design/phase-6-orphan-recovery.md) requires private CLI
inventory before execution, exact database/root/control binding, bounded discovery,
fresh plans and offline acknowledgment. PostgreSQL/filesystem checks preserve registered,
young, busy, unknown and held sources; recheck new registration and changed file hashes;
reject unresolved deletion journals; and distinguish authorization/completion audit
outages with conservative replay. Capacity snapshots must detect absent/changed policies
and reject legacy schema 1. Existing deletion/recovery, quarantine, capacity, API and
Infrastructure regressions accompany the new cases. No live storage cleanup is run.

## Durable capacity increment — September 14

The [capacity contract](../design/phase-6-organization-capacity.md) requires competing
uploads across two restricted API hosts to admit only one and clean up the loser.
Checks cover a Repeatable Read database default, worker completion/audit while an
admission lock is held, independent organizations, reprocess history, retry occupancy,
terminal release and pending-deletion bytes across a source-removal outage. Private
CLI checks cover exact database/version binding, policy audit rollback, SQL grants,
immutable organization binding and populated downgrade refusal. API profile checks
require policies outside local profiles. Full API/Application/Infrastructure suites
and related PostgreSQL permission, tenant, deletion, recovery and queue regressions
accompany these checks. See the [evidence](../evidence/phase-6/P6-capacity-20260914.md).

## Request-limit increment — September 14

The [request-limit contract](../design/phase-6-request-limits.md) is verified with
monotonic-clock budget tests, parallel admission, bounded-partition saturation and
recovery, signed-token HTTP admission, spoofed headers, cancellation/failure release,
and real Kestrel chunked-body boundaries. Actual PostgreSQL memberships prove shared
organization capacity, separate actor/organization partitions and fresh revocation.
Restricted-login upload/source tests check throttling before file, queue, scanner or
workflow audit side effects, then successful retry after window expiry. Body deadlines
must distinguish stalled input, client cancellation and downstream workflow deadlines.
Whole API and related restricted-login tenant regressions accompany the new checks.

## Restricted API database increment — September 14

The [API database contract](../design/phase-6-api-database-access.md) adds actual
restricted-login HTTP mutation checks, raw SQL denial checks, next-request privilege
drift refusal, private CLI provisioning/verification and transactional grant-audit
rollback. Positive owner workflows accompany foreign document/job/review/field/case/
run/finding substitutions. Prepared semantic variants and saved-question history
are checked for both actor and organization binding. Reconciliation link replacement
must audit the unlink, preserve old evaluation snapshots and roll back on audit
failure. API and Infrastructure regressions plus related PostgreSQL bootstrap,
audit, worker, upload/review, deletion and recovery checks accompany this increment.
Only disposable databases and local provider fixtures are used. Deployment activation,
request/resource limits and resistance to compromised service credentials are separate.

## Worker authority increment — September 12

[Worker verification](../evidence/phase-6/P6-worker-security-20260912.md) records
35 PostgreSQL security/queue checks, 123 Infrastructure and 148 API checks. The
restricted login is exercised against actual PostgreSQL, including excess grants
and audit-outage rollback. System actor provisioning, deactivation, lease tuple
tampering, persisted provider identity and safe startup configuration are covered.
The original retained database is read-only; test roles and disposable data are
removed. This does not establish resistance to a compromised worker using arbitrary
SQL within its allowed extraction columns, or verify live certificate deployment.

## Private-service increment — September 11

[Verification](../evidence/phase-6/P6-private-service-access-20260911.md) records
330 Python application, 111 Infrastructure and 143 API checks, all passing with
no skips. Admission is tested before body reads and provider dependencies;
service-key scope, expiry, rotation/revocation, malformed configuration, safe
telemetry, HTTPS-only enterprise dispatch and the production mode gate are covered.
All private HTTP handler chains disable redirects, proxies, cookies and framework
HTTP logging. Phase 5 local embedding restrictions remain enforced. Retained
table fingerprints are compared read-only. Deployed TLS, durable operational
audit, worker DB authority and whole-phase security acceptance remain pending.

Use disposable synthetic identities and data. Never reset retained Phase 5 data
or spend records. Preserve failed attempts and record latest passing outcomes
without counting reruns as additional tests.

The API and membership/audit increments verify:

- Every current business route rejects anonymous/query-token/header-identity
  access before body binding; health remains public and API responses are no-store.
- Offline RSA-signed tokens traverse the actual JWT bearer validator: signature,
  audience, issuer, expiration, future validity and unsigned-token refusal.
- Tenant/object identity, delegated scope, authorized client and version;
  malformed/duplicate identity and app-only token refusal. Database permissions
  remain authoritative with missing, forged or elevated token role claims.
- Role matrix across all current route templates, including mutation denial
  before deserialization and default denial for new routes.
- Server-resolved organization context and membership recheck; relational actor
  lookup rejects missing, inactive, revoked, System, ambiguous and legacy-only bindings.
- Locked production defaults, local-only development identity and no execution
  context fallback before authorization.
- Existing backend regressions preserve Phase 5 behavior under the deliberate
  development test profile; read-only retained-table fingerprints remain exact.

The membership/audit PostgreSQL scenarios exercise real repositories and signed
tokens for two fictional organizations: documents, review/history, processing,
aggregate metrics, cases, sources, saved questions and evidence navigation; membership
and audit scoping; data/admin permission separation; token-role escalation denial;
version conflicts, revocation and organization transfer; concurrent final-admin
protection; private bootstrap executable and retained-database refusal; immutable
audit through EF/SQL/downgrade; runtime grants with forbidden DML; upload, review,
index, question and reconciliation events; and forced audit-insert failures with
transaction rollback and file compensation. Test fixtures use only local providers.
The runtime-grant test creates and removes one uniquely named NOLOGIN test role;
it grants access only inside the disposable database.

Next acceptance layers add real Entra/browser login/logout, the remaining HTTP
mutation/semantic isolation matrix, worker/storage identity, operational auditing, malware
quarantine, expiring-link replay, retention/legal holds/restore and rate-limit
behavior. Each affected workflow must rerun its earlier-phase regression.
Offline token validation alone is not end-to-end OIDC sign-in acceptance.

## Browser increment

The [browser report](../evidence/phase-6/P6-browser-access-20260911.md) records
44 web checks, 139 API checks, the new PostgreSQL session test, lint/build,
production dependency audit and eight browser snapshots. The fictional UI fixture
and signed-token database tests are separate from live Entra acceptance.
