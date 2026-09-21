# Audit admission verification snapshot

September 15, 2026. Scope: [audit operations](../../design/phase-6-audit-operations.md).

## Result

Authorized, budget-admitted mutations and original-file downloads now persist
`request.admitted` before body parsing. A failed admission insert stops the request
with 503. Admission records an attempt; committed business events remain separate.
The local Entra profile also places cited-body buffering after admission, while
Development keeps its existing ordering.

Verification exposed a membership error-classification bug: a business audit outage
rolled back the change but returned 409. Only genuine membership uniqueness or
concurrency conflicts now map to 409; audit/storage failures reach the safe 503
boundary. Tests separately exercise admission outage, transaction audit outage and
a real unique-index violation.

## Checks and captured evidence

**277 distinct checks pass: 259 API and 18 PostgreSQL.**

Final totals are recorded in `outputs/phase-6/audit-admission-20260915/verification.json`.
The API suite covers malformed/missing bodies, framework route rejection, safe
identity/fields, refusal before body reads, source attempts and budget ordering.
Focused PostgreSQL checks cover restricted grants, tenant-scoped listing, committed
admission, business rollback/file compensation, append-only protections, membership
conflicts, source access and deletion replay without repeated work.

The HTTP audit-viewer response captured from the fictional two-organization fixture
is `outputs/phase-6/audit-admission-20260915/audit-viewer-snapshot.json`. This is a
response snapshot, not a browser screenshot. No browser component changed.

| Event | Count in the captured scenario |
| --- | ---: |
| `request.admitted` | 4 |
| `organization.created` | 1 |
| `membership.granted` | 2 |
| `membership.changed` | 1 |
| `access.denied` | 4 |
| `request.failed` | 2 |

The scenario verifies organization scoping and excludes display names, bearer
tokens and external subject IDs from the viewer response. Admission tests separately
check that private body/query/grant values are absent.

## Verification boundaries

Initial API diagnostics include sandbox certificate failures and two boundary-test
failures. The cited-body ordering was corrected; the unsupported-media test now
asserts the existing fail-closed framework-route denial. API verification was repeated
with Windows cryptography available. The focused database run exposed the 409/503
bug above; the successful confirmation supersedes that failing case.

A broad Phase 6 database run was deliberately stopped after 56 completed passes to
focus on the changed boundary. It was aborted, not a successful full-suite run, and
its cases are excluded from the final headline total. Selected PostgreSQL results
are deduplicated by test ID and name, taking the latest result for repeated cases.

No schema migration or deployment activation occurred. All 34 retained-table
fingerprints match. This increment does not complete Phase 6: private export,
archive/retention operations and final coverage acceptance remain. Audit history
currently remains append-only and retained indefinitely; an admission without a
terminal event has an unknown outcome.
