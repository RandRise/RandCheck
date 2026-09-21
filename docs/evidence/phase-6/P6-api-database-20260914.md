# Restricted API database and tenant mutation verification

September 14, 2026. The [API database contract](../../design/phase-6-api-database-access.md)
defines the allowlist, private provisioning, runtime activation and limits. The gate
defaults off locally. No deployment credentials, grants or schema were changed.

## Results

407 unique checks pass: 169 API, 189 Infrastructure and 49 PostgreSQL. The final
PostgreSQL run passed all 23 cases, superseding the earlier failures; the compatibility
run contributes 26 other passing cases. Documentation and private CLI builds pass.
Raw evidence is in `outputs/phase-6/api-database-20260914`. Test commands disabled
NuGet auditing for the offline environment; no fresh dependency audit is claimed.

| Evidence | Scope |
| --- | --- |
| `api.trx` | Full API authentication, permissions and workflow contracts |
| `infrastructure.trx` | Full infrastructure suite, including seven new connection/profile checks |
| `api-database-final.trx` | Restricted-login workflows, drift, private CLI, tenant substitutions, semantic modes and audit downgrade guard |
| `compatibility.trx` | Bootstrap, audit, upload/review, worker, deletion and recovery regressions; earlier failures remain in this file |
| `postgres-unique-tests.txt` | Final successful cases, deduplicated across runs |
| `retained-before.txt`, `retained-after.txt` | Matching read-only fingerprints for all 34 retained tables |
| `verification.json` | Final totals and build results |

PostgreSQL fixtures recreate only `document_intelligence_test` on loopback port
25434, with one runner at a time. Unique temporary roles are removed afterward.
No paid provider is invoked; semantic outcomes are disposable prepared fixtures.

## Verified behavior

- Private CLI grant configuration and its System audit event are transactional;
  audit failure restores the prior permissions. Read-only verification and exact
  database-name refusal are exercised through the executable.
- The API login permits successful membership changes, upload/cancel/reprocess,
  source grants/download, review, reconciliation, cited workflows and eligible deletion.
  Raw SQL attempts to remap identity, rewrite audit/history/accounting or obtain
  worker-only authority are denied.
- Excess/missing grants block the next API request with a generic 503. Cases cover
  role membership, identity updates, audit deletion, grant options, missing read/schema
  usage, private accounting writes, extra tables (including zero-column relations),
  unrelated schemas/sequences, CREATEDB and executable SECURITY DEFINER functions.
  Health remains a liveness response.
- Signed-token requests reject foreign document/job/review/field/case/run/finding
  substitutions. Successful owner operations accompany denials. Saved-question
  history is private to the owning actor, including against a same-organization peer.
- Vector, hybrid and nonredundant retrieval require prepared outcomes for the current
  organization and actor; owner replay succeeds without additional spending rows.
- Reconciliation link replacement records `reconciliation.document-unlinked` and
  commits the new link atomically. Forced audit failure returns the existing conflict
  response and preserves the old link/version; successful retry preserves previous
  evaluation snapshots.

## Corrections found during verification

The mutation test exposed a production audit-rule defect: removing a live case link
was treated as document deletion and returned 503. The guard now permits that specific
association removal with an explicit unlink event. Other audited deletion restrictions
remain in place.

Test corrections retain the actual contracts: a foreign review-field ID returns 404,
and a reconciliation persistence failure returns 409. Cited source preparation needs
the local private Python fixture. The older audit downgrade test now targets the
migration preceding membership/audit explicitly, instead of assuming it is the newest
migration. Initial failing runs are retained; repeated cases are counted only once
using their final successful result.

## Scope and next work

This is backend verification; no browser UI changed. The earlier deletion snapshots
remain the visual evidence for that increment. Enterprise deployment must provision
the login and explicitly enable the API gate on every instance. There is no database
RLS, and allowed rows remain accessible across organizations to a compromised service
credential. The contract records catalog-check cost and trusted-operator limits.

Request/resource limits are next. Live Entra/TLS/scanner/storage deployment, backup
expiry, durable journal operations and automatic audited restore repair remain open.
These results do not establish full Phase 6 acceptance.
