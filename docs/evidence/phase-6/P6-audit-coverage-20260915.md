# Audit coverage verification snapshot

September 15, 2026. [Coverage and exclusions](../../security/phase-6-audit-coverage.md).

## Result

The review found and fixed two gaps. Admission events now distinguish 25 sensitive
operations with fixed server-owned names. Semantic retrieval now carries the bound
audit identity into its isolated database context, so the saved run and hits commit
with `question.saved`. No schema, browser or package version changed.

Historical method-only admission codes remain valid. Missing historical semantic
events are not reconstructed. The audit contract is accepted for the independent
local project; Phase 6 still requires the local recovery contract and final
security/secret review. Real Entra, TLS and host operations remain future deployment
checks, not claimed test results.

## Executed checks

**497 distinct checks pass: 260 API, 219 Infrastructure and 18 PostgreSQL, with
zero failures or skips.** The backend solution builds with zero warnings/errors.
The final API run follows the semantic fix and supersedes the initial API run;
it is counted once. PostgreSQL contributes 11 cross-workflow checks and seven
semantic/deletion checks. Documentation and touched-file whitespace checks pass.

| Check | Evidence |
| --- | --- |
| Every authorized operation has a unique safe admission code; ordinary reads add no admission; request bodies remain unread at admission | `AuditOperationCoverageTests` plus real-host malformed body, source, denial, budget and outage checks |
| Admission reason persists with the authenticated organization under a restricted PostgreSQL login | `Phase6_admitted_binding_failure_persists_under_restricted_login_and_is_tenant_scoped` |
| Semantic vector, hybrid and nonredundant runs carry the Human actor and request ID; audit outage rolls back run/hits; retry succeeds; replay adds no business event | Expanded `Phase6_api_tenant_semantic_prepared_outcomes_are_actor_and_organization_bound` theory; prepared synthetic outcomes only |
| Existing unbound Development semantic scores, policy, evidence and replay behavior | `Semantic_retrieval_persists_scores_policy_evidence_and_replays_without_spend` theory |
| Membership, append-only guards, review, worker, capacity, quarantine, deletion and orphan removal | Focused PostgreSQL regressions; broader historical evidence linked individually in the coverage map |
| Private export and offline verification with a read-only login | Actual CLI fixture and captured archive/receipt |

The current run does not repeat every historical Phase 6 test. Earlier worker
outage, source-access, retention and archive adversarial checks remain separately
linked and are not counted again. Windows certificate tests use the project runtime
with certificate access. PostgreSQL runners execute serially and recreate only the
disposable `document_intelligence_test` database.

## Captured snapshots

Artifacts are under `outputs/phase-6/audit-coverage-20260915`.

- `audit-viewer-snapshot.json`: actual HTTP response from the fictional membership
  scenario. Its 14 rows include four admissions: one `authorized_membership-grant`
  and three `authorized_membership-change`, alongside four denials, two observed
  failures, two granted memberships, one changed membership and organization creation.
- `synthetic-export/receipt.json` and its `audit-<export-id>` directory: a verified
  private CLI archive containing two synthetic events. The test confirms no audit
  write or document-read privileges are needed and the database event count is unchanged.
- `verification.json` and TRX files: current test counts and outcomes. Synthetic
  HTTP/CLI snapshots demonstrate backend behavior; they are not browser screenshots.

## Accepted boundaries

Admission success is not business success. Missing completion leaves an unknown
outcome, especially around external file effects. Download authorization does not
prove receipt of bytes. Ordinary reads, throttling, worker heartbeats and private
read-only operator tools do not create business events. Null-organization events
remain outside organization viewers/exports. Private operations require separately
protected operator evidence; no Human identity is inferred from database credentials.

Audit rows and accepted archive/receipt pairs remain indefinitely retained under
the local runbook. Append-only controls do not defeat a malicious database owner;
checksums rely on an independently trusted receipt. No migration, provider call,
deployment, commit or push was performed.
