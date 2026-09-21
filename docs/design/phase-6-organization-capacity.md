# Organization queue and source capacity

September 14, 2026. Organizations now have durable, privately provisioned capacity
policies. Every upload admission and reprocess admission checks the current policy
and PostgreSQL usage under the organization's row lock. API instances share the same
limits; restart does not reset them. This complements per-process request limits.

## What counts

| Resource | Charged usage | When capacity becomes available |
| --- | --- | --- |
| Active extraction jobs | Pending, Leased and RetryScheduled jobs in the organization | Succeeded, Failed or Cancelled state commits, or authorized deletion removes the job |
| Stored source bytes | Registered document `size_bytes` plus deletion records whose `completed_at_utc` is still null | Source removal and deletion completion commit |

Leasing, lease expiry and retry scheduling do not free a job slot. Terminal job
history is retained. Reprocessing uses one additional job slot and no additional
source bytes. It can proceed while an existing storage overage is being resolved.
Lowering a policy below current usage preserves data and blocks new admission for
the exhausted resource; it does not delete content or cancel jobs.

Storage accounting deliberately keeps pending file removal charged after the live
document is removed from the database. A missing completion event is conservative:
the bytes remain charged even if the file was removed before a completion failure.
Hold/dependency rules still govern which documents can be deleted. Capacity limits
never make a held or historical document eligible for deletion.

## Atomic admission and failure behavior

The API's upload preflight checks current capacity before source storage/scanning.
It is advisory and does not reserve space. After storage verifies actual bytes,
`UploadQueueStore` takes the organization lock again, reloads policy/usage, checks one
job and the actual source bytes, and commits document/run/job/audit together. A loser
in a concurrent upload race compensates its unadmitted source through existing storage
cleanup. Uploads and reprocessing use the same organization-first lock order.
Reprocessing checks foreign/missing/active document targets before reporting capacity.

Admission explicitly uses Read Committed transactions, including when the database
default is Repeatable Read, so usage reads after a waiting lock see committed admissions.
The organization lock is `FOR NO KEY UPDATE`: it serializes admission and policy/deletion
changes while allowing foreign-key checks for worker audit inserts. A stronger
`FOR UPDATE` lock could block those inserts and deadlock against a worker's document lock.

Worker terminal transitions only reduce usage. No mutable usage counter or additional
worker grant is needed. Policy changes and document deletion already use the same
organization lock. Decimal sums avoid overflow when accumulating bigint source sizes.
Usage queries run again after acquiring the lock; there is no cached per-process usage.

An exhausted resource returns a generic 409 problem with
`organization_queue_capacity_exceeded` or `organization_storage_capacity_exceeded`.
A required but missing policy returns 503 `organization_capacity_unconfigured`.
There is no time-based Retry-After promise: capacity depends on completed work, eligible
source deletion or an authorized policy change. Existing failed-operation audit records
the admitted failure; an unavailable audit sink still prevents the normal error response.

## Private policy administration

Migration `20260914125645_AddOrganizationCapacity` adds `organization_capacity_policies`.
There is one row per organization, positive limits, a version token and update time.
Limits support 1–100000 active jobs and 1–109951162777600 source bytes (100 TiB).
No quotas are silently seeded for existing organizations. The API role has SELECT only;
the worker has no policy-table grants. Reapply `EnterpriseAdmin api-role` after migration
to include the new read permission before enabling the restricted API database gate.

Private operators use:

```text
EnterpriseAdmin capacity-policy <private-capacity.json>
EnterpriseAdmin capacity-status <private-capacity.json>
```

`PHASE6_ADMIN_CONNECTION` provides the private operator connection. Both commands
require an exact `ExpectedDatabase`, a real `OrganizationId`, no verbose connection
diagnostics, and refuse retained `phase5_` targets. No migration or provider dispatch
is performed by either command. The policy command additionally requires
`MaxActiveJobs`, `MaxStoredBytes`, and `ExpectedVersion` when updating an existing row.
Omit ExpectedVersion only for initial creation. A stale version refuses the change.
Status returns limits/version, active jobs, live source bytes and pending-deletion bytes.

Example input for a newly provisioned fictional organization:

```json
{
  "ExpectedDatabase": "enterprise_database",
  "OrganizationId": "00000000-0000-4000-8000-000000000001",
  "MaxActiveJobs": 100,
  "MaxStoredBytes": 10737418240
}
```

Policy changes and a System `capacity.policy-changed` event commit together. The audit
contains only the organization target and before/after job and byte limits. Audit
failure rolls back the policy and version. Database triggers reject policy removal,
truncation and organization remapping; populated downgrade is refused. Quota changes
are not exposed to organization administrators through the public API.

`Capacity:RequirePolicy` is forced true outside Development/Testing. Local profiles
may omit policies unless they opt in. Once a policy exists, these admission paths
always enforce it, even when RequirePolicy is false. All API instances must run the
updated admission code and migrated schema.

## Scope and remaining operations

This is a quota on registered source bytes, not a hard filesystem-volume quota.
Unadmitted upload staging, quarantined files, crash or cleanup leftovers, extracted
text/evidence, embeddings, backups and journal files are not included in this byte
total. Concurrent uploads may temporarily stage more than remaining quota before
the final transaction rejects a loser. Request/body limits bound active upload traffic;
physical volume quotas and deployment storage monitoring remain. Private
[offline orphan recovery](phase-6-orphan-recovery.md) now covers reviewed, unregistered
promoted source files; staged files continue through the existing quarantine lifecycle.

The limits apply through these upload/reprocess paths. Trusted migration/operator
tools and a compromised service issuing arbitrary SQL are outside this admission
assurance; there is no general database insertion trigger or RLS policy. A restored
database restores its policy version. Schema 2 recovery exports now include capacity
policy presence, limits, versions and update times, and restore verification compares
them with the supplied independent snapshot. Older schema 1 exports are refused.
Operators must retain a current authoritative snapshot; the tool cannot establish
freshness if database, journal and snapshot are all rolled back together.
