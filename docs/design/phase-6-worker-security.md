# Worker database authority and durable audit

Implemented September 12, 2026. This increment gives the extraction worker a
dedicated database connection, restricted grants and a provisioned System actor
for each organization. It also closes a lease-binding gap. Phase 5 stored results,
review behavior, queue retry rules and spending records remain preserved.

The subsequent [storage increment](phase-6-storage-safety.md) requires enforced
workers to configure `Storage:SecurityMode=Enforced` and `MalwareScan:Mode=ClamAv`
at startup. Source integrity verification and scanning precede extractor dispatch.

## Authority at the queue boundary

A completion or failure must match the stored job ID, organization ID, document
ID, extraction-run ID, attempt, lease owner and lease token, with a live lease.
Previously, document/run/attempt were not included in the lease lock predicate.
Provider identity is now validated against the persisted extraction run, rather
than copied claim metadata. Swapped identities cannot redirect a valid lease.
The existing database clock, fencing, cancellation, retry delays, maximum attempts
and idempotent completion remain in force.

Workers remain trusted cross-organization services. They obtain organizations
from stored jobs, not browser membership or caller-selected human claims. Worker
sessions reject human cancellation and reprocess methods; those remain public
handler operations under the user's existing permissions and audit context.

## Dedicated connection and database policy

The API host resolves `ExtractionWorker:SecurityMode` to Development only in
Development/Testing when unset. Other environments default to Locked. Explicit
Development outside local/test environments and unknown modes become Locked.
Enterprise deployment requires explicit `Enforced` mode. Standalone Phase 5
local tools keep their existing development configuration.

Enforced workers require `ConnectionStrings:WorkerDatabase` and
`ExtractionWorker:DatabaseRole`; they never fall back to the API connection.
The worker connection must name the configured role and the same database, host
and port as the API connection. Retained `phase5_*` databases are refused. Remote
database connections require `SSL Mode=VerifyFull`; plaintext is allowed only for
loopback development/test connections. Error-detail and parameter logging options
are rejected. Deployed certificates and proxy/pooler arrangements remain unverified.

Startup also validates the extraction client's ServiceKeys credential and HTTPS
configuration before claiming work. Missing local configuration therefore cannot
consume job attempts. Receiver-side key expiry and actual service availability
still depend on the private-service deployment and rotation procedures.

Each worker session verifies its authenticated database role and effective grants.
The role must own no database, schema or relation, belong to no other role, have
no elevated role attributes or schema/database CREATE rights, and have no
executable application SECURITY DEFINER functions. Unexpected grants, including
column grants, PUBLIC/inherited rights and grant options, fail verification.

One code allowlist in `WorkerDatabasePolicy` drives provisioning and verification:

| Data | Worker access |
| --- | --- |
| Existing documents, jobs, runs and extraction outputs | SELECT |
| Document processing state | UPDATE processing status, update time and concurrency token |
| Job lease and result state | UPDATE lease, attempt, retry/error/result and completion fields |
| Extraction run result | UPDATE output/metrics/status fields; no organization/document/provider rebinding |
| Pages, machine fields, evidence and extraction warnings | INSERT; no UPDATE/DELETE |
| Actors | SELECT only IDs, organization, kind, external subject and active flag for service lookup |
| Security audit | INSERT; SELECT generated recording timestamp for EF's INSERT result |
| Memberships, human reviews, reconciliation, cited answers, embeddings and cited spending | No table or column access |

No job/run/document creation, DELETE, TRUNCATE, REFERENCES, TRIGGER or table/schema
ownership is granted. The policy denies excess public-schema table/view/column
access and requires its expected tables. It does not modify the retained schema.

This is a restricted trusted-service role, not row-level isolation or a database
stored-procedure boundary. A compromised worker with arbitrary SQL could still
misuse its permitted extraction columns or omit application audit writes. The
worker currently shares a process with the API, whose configuration contains both
connections. Process separation, complete API runtime grants and stronger database
enforcement remain additional hardening work; these limits are not hidden by the
separate-connection design.

## Provisioning

Use a reviewed deployment database already at migration 16 or later. There is no
new migration in this increment. The private EnterpriseAdmin tool refuses retained
`phase5_*` databases before connecting. It neither creates logins nor handles role
passwords, migrates data, starts workers or dispatches providers.

1. A database administrator creates the dedicated login with no elevated
   attributes, role memberships or object ownership. Supply its credentials through
   the deployment secret store. Role names use lowercase ASCII letters, digits and
   underscores, start with a letter and are at most 63 characters.
2. Create a secret-free input file containing `{"DatabaseRole":"enterprise_extraction"}`.
   Set `PHASE6_ADMIN_CONNECTION` privately and run
   `EnterpriseAdmin worker-role <input.json>`. This transaction replaces the role's
   public-table/column grants with the extraction allowlist, verifies them, and
   records a global `worker.database-role-configured` event with the safe role name
   and policy version. It rejects authority it cannot safely remove, rather than
   editing PUBLIC rights or another role's grants. It is repeatable.
3. For each organization, use an input file with `OrganizationId` and `DatabaseRole`
   and run `EnterpriseAdmin worker-actor <input.json>`. This creates a new active
   System actor with subject `service:extraction-worker:<role>` and an atomic
   `worker.actor-provisioned` event. Repeating the command returns the existing
   active System actor. It cannot remap a Human actor or reactivate a disabled one.
4. Configure the worker connection, role, `SecurityMode=Enforced` and the existing
   private-service ServiceKeys credentials. Enable the worker only after provisioning
   and deployment checks. Provision the service actor before queuing an organization's
   first job; a missing/disabled actor fails the claim without consuming an attempt
   and can block progress behind that job until an operator resolves it.

Database administrators still control emergency service-actor deactivation. It is
not a human membership operation. Revocation applies to subsequent state transitions;
it does not cancel a provider operation already in flight. Heartbeats retain their
lease checks, and a result cannot commit after its System actor is deactivated.

## Durable events

| Action | Committed transition |
| --- | --- |
| `worker.claimed` | Pending/retry/expired lease to a new lease |
| `worker.completed` | Leased to Succeeded, with normalized output |
| `worker.retry-scheduled` | Leased to RetryScheduled |
| `worker.failed` | Leased to terminal Failed |
| `worker.lease-expired` | Final expired lease to Failed |

Each event uses the organization's active System actor, stored job ID, generated
request ID, enumerated before/after status and attempt number. It omits lease
tokens, owner/machine names, source paths, content, provider summaries and secrets.
The event and job/document/run/output updates commit in the same transaction.
Audit insert failure rolls back the whole transition. Audit outcome Succeeded
means that the transition committed; the after-state records a failed job when
applicable. These events appear in the existing organization-scoped audit viewer.

The existing append-only EF rules and PostgreSQL UPDATE/DELETE/TRUNCATE trigger
protect the events. Runtime grants deny audit rewrite. Heartbeats, stale/fenced
attempts and rolled-back operations do not create these state-transition events;
safe operational diagnostics remain separate. Raw worker exception logging has
been removed. Durable operational-attempt capture, external export, log retention
and owner/superuser tamper controls remain open.

See the [verification report](../evidence/phase-6/P6-worker-security-20260912.md).
