# Audit operations

September 15, 2026. Admission records enterprise operation attempts before request-body
parsing. [Private export and archive verification](phase-6-audit-export-retention.md)
now implement bounded operator snapshots, offline verification and the local
indefinite-retention/restore runbook. The [operational coverage map](../security/phase-6-audit-coverage.md)
defines local acceptance and private operator boundaries.
See the [verification snapshot](../evidence/phase-6/P6-audit-admission-20260915.md)
for executed checks and a captured synthetic audit response.

## Admission and committed change are separate events

In Entra mode, a classified mutation or original-file download passes these stages:

1. Ingress limits and database-access verification.
2. Token validation, current membership resolution and role authorization.
3. Organization/actor request and concurrency limits.
4. An independent, durable `request.admitted` audit insert.
5. Body validation, endpoint execution and any business transaction.

The admission event has the resolved organization/actor, a server route category,
parsed target UUID when present and the bounded request ID. `Succeeded` describes
admission alone. New reason codes identify a fixed server operation, such as
`authorized_membership-grant` or `authorized_hold-release`; the coverage map lists
all 25 operations. Earlier method-only codes remain valid historical records.
Neither form means that the requested business operation succeeded.
The existing business events, such as `membership.granted`, remain the evidence
that a change committed. They are still written atomically with that change.

Admission uses the existing isolated audit sink. If its insert fails, the request
returns 503 before reading its body or executing the endpoint. A later parsing
failure, business rollback, client disconnect or process death does not erase an
already committed admission event. Repeated HTTP attempts each receive an admission
event, even when business idempotency correctly avoids another change event.

Membership writes distinguish genuine PostgreSQL uniqueness/concurrency conflicts
(409) from audit or storage failures (503). An audit-insert outage rolls back the
business transaction; it must not be mislabeled as a membership conflict.

Existing `request.failed` events still describe failed admitted mutation results
that reach the endpoint filter. An admission event without a terminal event has an
unknown outcome; it must not be interpreted as success or automatic proof of failure.
Request IDs can be supplied by clients and reused; they are correlation aids, not
unique attempt identifiers or identity proof. Each audit event has its own UUID.

## Coverage and boundaries

| Request | Evidence |
| --- | --- |
| Authorized classified mutation with malformed JSON or missing required body | Admission event survives binding rejection |
| Authorized source grant or original-file download | Admission plus existing source authorization events when reached |
| Successful business mutation | Admission plus atomic committed-change event(s) |
| Filter-observed failed mutation | Admission plus independent `request.failed` |
| Authentication/role denial, including an unclassified framework rejection route | Existing `access.denied`; no admission |
| Ingress or organization/actor budget rejection | Existing bounded rejection metrics; no admission/database write |
| Ordinary successful reads, health, Development profile | No new admission event |

Admission occurs before cited-request buffering even in a local Entra test profile
with request limits disabled. Development retains its existing response pipeline.
The policy does not turn the security table into an exhaustive HTTP access log:
pre-admission outages, middleware failures and process death before a successful
insert can leave no admission record. Rate-limited traffic is deliberately not
converted into unbounded database audit writes.

No raw URL/query, body, bearer token, source grant, document text or display name is
added to audit events. The existing organization audit viewer exposes admission
events through its normal scoped listing. No global audit HTTP route is introduced.

## Retention and acceptance

Application audit history currently remains append-only and retained indefinitely.
Document retention/deletion does not purge security events. This is the implemented
local behavior, not a compliance retention schedule. Export must not imply permission
to delete source events or weaken UPDATE/DELETE/TRUNCATE protections.

The private export contract defines consistent snapshots, safe output fields,
independent receipt verification and archive restoration without audit-table writes.
The operational coverage map accepts P6-T05 for the explicit local contract,
including the private operator activity boundary. Off-host immutable storage and
production retention automation are optional future deployment work. P6-T07 local
recovery and P6-T09 final security/secret review remain required.
