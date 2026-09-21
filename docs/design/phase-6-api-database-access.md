# Restricted API database access

September 14, 2026. `ApiDatabasePolicy` defines one explicit allowlist used by both
private provisioning and runtime verification. It covers the public API's document,
processing, review, reconciliation, cited-question and administration workflows.
It does not provide PostgreSQL row-level tenant isolation: organization/actor checks
remain in the authenticated application and are tested through real signed-token HTTP.
See the [verification report](../evidence/phase-6/P6-api-database-20260914.md) for scope
and raw evidence.

## Allowed operations

| Data | API database permissions |
| --- | --- |
| Organizations and actors | Read; insert actors for membership provisioning; display-name UPDATE grants needed for row locks; no organization creation or actor identity/active-status rebinding |
| Membership/identity | Read and insert; only membership role/admin/active/version/time updates |
| Audit and history | Append/read audit, review actions, findings/resolutions, snapshots and answer evidence; no audit/history rewrite or deletion |
| Documents and jobs | Read/insert; bounded status, cancellation, lease-clear and timestamp/version updates; no organization/document/run identity, worker claim-attempt or extraction-cost changes |
| Extraction content | Read; owned children may cascade when an eligible run is deleted; API cannot insert worker pages/fields/evidence/warnings |
| Reconciliation | Read/insert case aggregates; status/version/time updates; live document-link replacement may delete the old association |
| Cited workflows | Read/insert indexes, chunks, retrievals and answers; index/retrieval lifecycle columns only are mutable |
| Prepared semantic outcomes | Read embeddings, reservations, generation artifacts and budget; no dispatch, settlement, artifact or embedding writes |
| Retention/deletion | Versioned policy/hold updates, insert immutable deletion records, first completion update; DELETE only on documents, extraction jobs/runs and live reconciliation links |

The full column list lives in `ApiDatabasePolicy.cs`. PostgreSQL requires UPDATE on
at least one column for `FOR SHARE`/`FOR UPDATE`; the policy preserves these locks.
Organization/actor display-name columns meet that requirement. Budget locking uses
UPDATE on `id` only; its existing `CHECK (id=1)` prevents changing the singleton identity.
The API cannot change budget amounts or reconciliation status. See
[PostgreSQL 16 privilege documentation](https://www.postgresql.org/docs/16/ddl-priv.html).

Private spending-event/recovery-audit tables and migration history receive no access.
Unknown public relations receive no permissions. Provisioning removes the target
role's direct table and column grants before applying the allowlist; grants inherited
through PUBLIC cannot be silently removed and cause verification failure.

## Provisioning and activation

Use a dedicated pre-created PostgreSQL login. The API role must not be an owner,
superuser, replication/bypass-RLS/createdb/createrole role, or member of another role.
It must not have database/schema creation, permissions on unrelated schemas/sequences,
or executable application SECURITY DEFINER functions. Table/column grant options,
TRUNCATE, REFERENCES and TRIGGER authority are rejected. Configure the worker with its
separate existing role and keep owner credentials in private deployment tooling.

The existing `EnterpriseAdmin` executable now accepts:

```text
EnterpriseAdmin api-role <private-provisioning.json>
EnterpriseAdmin verify-api-role <private-provisioning.json>
```

Input contains `DatabaseRole` and `ExpectedDatabase`. `PHASE6_ADMIN_CONNECTION` supplies
the private operator connection. Both commands require the exact database name and
refuse `phase5_` targets and verbose parameter/error logging. The commands do not create
a login, handle passwords, migrate, seed or dispatch providers. Configuration and a
System `api.database-role-configured` event commit in one transaction; audit failure
rolls back grants. Verification is read-only. The System event has no organization and
is available to private operators, not the organization-scoped audit viewer.

The old `tools/phase6_runtime_security_grants.sql` remains the earlier security-table
subset; it is insufficient to provision the complete workflow profile.
Use the new command for complete API provisioning and verification.

Activate with `ApiDatabase:Enabled=true`, `ApiDatabase:DatabaseRole=<role>`, and
`ConnectionStrings:Database` using that login. Non-loopback connections require
`SSL Mode=VerifyFull`; configured and actual database roles must agree. SQLite,
retained targets, verbose diagnostics, missing columns/tables, privilege drift and
invalid identities fail closed. No owner fallback or automatic grant repair occurs.

The middleware verifies before authentication's database membership lookup on every
`/api` request. Failure returns a generic 503 without credentials, role names or SQL
errors. `/health` remains a liveness endpoint, not a database-readiness assertion.
The gate defaults off for the existing local deployment; enabling and validating it
on every enterprise instance is a deployment prerequisite. This increment configures
only disposable test roles. No active deployment credentials or grants were changed.

## Tenant mutation checks and audit correction

Signed-token PostgreSQL tests now execute as a restricted login. Positive operations
cover membership changes, source grants/download, document upload/cancel/reprocess,
review actions/completion, case creation/link/replacement/evaluation/resolution,
cited-source preparation, reviewed answers, and deletion. Foreign document, job,
review, field and case targets are rejected without changing their data. Prepared
vector, hybrid and nonredundant queries require the owning actor and organization;
same-organization peers cannot reuse another actor's prepared outcome.

The checks exposed a live-link replacement blocked by the general audited-deletion
guard. A reconciliation link is now the sole additional association deletion allowed
by that guard and records `reconciliation.document-unlinked` with its prior role.
The replacement link and case change remain in the same transaction. An audit outage
rolls everything back. Previous evaluation snapshots keep their document/review/run
references and are not deleted by changing the current link.

## Limits and next work

The API service role can access allowed rows across organizations; a compromised
process/database credential is outside HTTP tenant-isolation assurance. Runtime
verification does not stop an owner changing grants during an in-flight request, prove
host/process isolation, or inspect every possible PostgreSQL extension/operator
capability. PostgreSQL defaults such as temporary-object privileges need deployment
review. Per-request catalog checks need workload measurement before a scaled rollout.

Rate/resource limits, deployed identity/TLS/scanner/storage checks, backup expiry and
audited restore repair remain open. No certification or full Phase 6 completion is
claimed by these local checks.
