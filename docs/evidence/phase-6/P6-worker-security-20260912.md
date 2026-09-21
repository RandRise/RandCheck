# Worker security verification — September 12, 2026

Rand authorized continuing Phase 6. This increment implements the
[worker database and audit contract](../../design/phase-6-worker-security.md).
It does not complete Phase 6 or establish production readiness.

## Result

| Check | Result |
| --- | --- |
| PostgreSQL security and affected queue regression | 35 passed, zero skips: 23 new worker cases and 12 existing queue/provisioning/grant cases |
| Infrastructure regression | 123 passed, zero skips; includes 12 new worker configuration/startup cases |
| API regression | 148 passed, zero skips; includes five new real-host worker-mode cases |
| Final changed-path check | The restricted-login and private provisioning CLI cases rerun against the final guard/provisioning code; counted once |
| Preserved data | All 34 retained table count/content fingerprints unchanged; original migration count remains 15 |
| Spending | 464 spend events and 153 reservations unchanged; no paid calls |
| Documentation / whitespace | VitePress production build and diff check pass; existing bundle-size warning |

The increment has 306 unique passing tests. Repeated runs are not added. Python,
web, domain/application and the full PostgreSQL suite were not rerun; their prior
evidence remains separate. No new schema migration, model enablement or retained
actor remapping occurred. Browser UI code and its prior snapshots are unchanged.

## What the checks prove

- A real, non-owner PostgreSQL login processes jobs for two fictional organizations
  under the exact restricted grants. Claims, normalized completion, retry and terminal
  failure commit with System actor audit events. Heartbeat behavior remains valid.
- Worker audit identity is provisioned separately from each document's creator and
  resolves to the job's organization. Missing, Human or inactive service identities
  roll back a claim without consuming an attempt. Deactivation blocks completion.
- Swapped organization, document, run, attempt, owner or lease token rejects both
  completion and failure. Provider identity is checked against the stored run.
- Final lease expiry is audited. Forced audit INSERT failures roll back claim,
  completion/output, retry, terminal failure and lease-expiry transitions.
- Restricted role SQL cannot read memberships, review actions or spending; create
  jobs; rebind document/job/provider identity; change actors; rewrite/truncate audit;
  or create tables. Excess column/table grants, CREATEDB, role membership, grant
  options and executable application SECURITY DEFINER functions fail verification.
- The private CLI configures/verifies grants and idempotently provisions a new
  organization System actor. Both new commands refuse retained database names
  before connecting. Runtime workers also refuse retained targets and never use
  the API database connection as an enterprise fallback.
- Startup refuses a missing/insecure private extraction credential before touching
  the database or claiming work. Remote database connections require certificate
  verification; connection error-detail and parameter logging options are rejected.
- Existing queue tests preserve database-clock fencing, concurrent claims, exact
  retry delays, reprocess behavior, cancellation, stale completion and final expiry.
  Existing worker tests preserve cancellation and provider-error classifications.

Audit fields are verified to exclude private provider summaries, owner names and
lease tokens. Application-generated System events use the existing append-only
table and protections. High-frequency heartbeats, rejected leases and operations
rolled back by an audit outage do not become durable state-transition events.

## Evidence and environment

Evidence is in `outputs/phase-6/worker-security-20260912`: main and final-delta
PostgreSQL TRX reports, Infrastructure/API TRX reports, retained fingerprints and
machine-readable verification. The initial Infrastructure fixture failure is
retained separately; adding explicit development session dependencies fixed the
fixture while keeping its assertions. No failing result is counted as passing.

Tests use PostgreSQL 16 on local port 25434, and recreate only
`document_intelligence_test`, with one runner at a time. Unique test roles are
removed after their cases; the disposable database is removed at suite completion.
LOGIN test roles rely on the existing loopback-only test authentication setup;
this is not a production password/TLS deployment exercise.

The existing workspace PostgreSQL server required a restart after an unclean
shutdown. Its recovery completed normally. Before any tests, the original database's
34 fingerprints matched the previous checkpoint. It was then queried read-only
for the final comparison. No original data or schema was modified.

Local .NET checks use `-p:NuGetAudit=false` to avoid offline audit metadata lookup;
this is not a dependency-vulnerability audit. The local runtime baseline and
deployed certificate verification remain separate open items.

## Remaining limits

The worker role is a trusted cross-organization service with direct access to
approved extraction columns. It is not a stored-procedure or row-level boundary
against a compromised worker. The API and worker still share a process and can
therefore share access to host configuration. Complete API grants, process
separation, durable operational-attempt logging/export, live Entra/TLS, protected
storage, malware quarantine, retention and rate limits remain Phase 6 work.
