# R4-T05a — Safe comparison retries and deliberate revisions

September 30, 2026. Scope: real comparison UI/API/persistence plus synthetic preview.
Rules and remaining document/processing work: [action repeat contract](../design/action-repeat-contract.md).

## Behavior and integration

- Case creation carries an actor/organization-scoped intent key. Retrying the same
  normalized original inputs recovers the same UUID/reference and current saved case;
  reusing a key for changed inputs conflicts. Atomic database uniqueness resolves
  simultaneous creates. Acknowledged success starts a fresh intent for the next case.
- The key is retained in memory and, when available, tab session storage. A late
  response clears only its own intent. Account/organization scopes remain separate.
  Legacy clients omitting the optional key still use legacy creation semantics.
- Re-pinning the same completed source is a no-op. Replace pinned review opens an
  explicit choice; cancellation writes nothing and replacement preserves old runs.
- Existing cases show New comparison before exposing Run comparison. Intentional
  repeated inputs remain valid, while a consumed ETag cannot create another run.
- Saved finding decisions show Revise decision. Exact action/reason repeats are
  disabled in the UI and no-ops on the server; meaningful revisions append history.
- Stale writes, transport failure, server failure and truncated successful responses
  require a read-only reload before another write. Reload preserves metadata, source,
  reason and tolerance drafts. Remote closure exposes retained inputs for copying.
- English/Arabic guides explain the shipped controls. Secondary replacement/cancel
  controls no longer inherit the primary button style.

No new permission or audit route is introduced. Create/replay still passes current
Review permission and enterprise audit admission. Viewer remains read-only;
administrator-only has no document access. A request attempt may be audited more than
once; duplicate business changes/events are prevented. Creation keys/hashes stay out
of views and human-readable audit payloads.

## Test/review gate

- Full frontend suite: 237/237 across 19 files. Twenty independently authored new
  action tests cover every document role with/without administration, pending guards,
  create retry/remount/late-response ownership, cancellation, no-ops, explicit reruns,
  stale and uncertain recovery, closed drafts and tolerance preservation.
- Request/preview fixtures: 15/15, including six concurrent same-key creations,
  actor separation, renamed-case replay, authorization denial, payload conflict,
  stable no-op versions, stale run/decision rejection and immutable earlier runs.
- API/security and audit-route checks: 141/141, including replay after role downgrade
  and revocation. Focused ESLint and production Next build/TypeScript passed.
- Application reconciliation: 12/12. Nine distinct actual PostgreSQL checks passed
  across focused runs. Initial 7/8 exposed the evaluation race; the corrected test
  and migration lifecycle rerun passed 2/2, forced six-insert creation contention
  passed 1/1, and concurrent pin/decision test passed 1/1. No unresolved failure.
- Independent review found and fixed late-create intent cleanup and body-decoding
  failure recovery. Metadata writes now share uncertain-write recovery. Final UI
  and backend static reviews found no remaining blocker.
- Actual PostgreSQL concurrency revealed a loser could hit unique run-number
  enforcement before the case ETag update, returning generic 409. The handler now
  checks the persisted token after rollback and returns stale 412 when appropriate.
  The duplicate run was already rolled back atomically; the fix makes recovery usable.
  Independent review identified the same ordering possibility for first-pin and
  decision-sequence uniqueness; the same recovery mapping is applied there too.
- Migration preserves the original full organization/creator index alongside the new
  partial unique request index. Downgrade refuses to discard durable request identity.

Commands used pinned Node Vitest with `--root apps/web --maxWorkers 1
--no-file-parallelism --testTimeout 15000`, Node `--test` for fixtures/request checks,
focused ESLint and `next build`. With `tools/use-project-runtimes.ps1` loaded, backend
checks used `dotnet test <project.csproj> --no-restore --verbosity minimal`:

| Project under apps/backend/tests | FullyQualifiedName filters |
| --- | --- |
| UaeDocumentIntelligence.Api.Tests | ReconciliationApiContractTests OR EnterpriseSecurityTests OR AuditOperationCoverageTests |
| UaeDocumentIntelligence.Application.Tests | Reconciliation |
| UaeDocumentIntelligence.Persistence.Tests | Case_repeat OR Case_metadata OR Migration_apply_is_idempotent |

Affected PostgreSQL reruns used Case_repeat_evaluation with Migration_apply_is_idempotent,
Case_repeat_creation_is_durable (`--no-build`), then Case_repeat_pin_and_decision.
The connection targeted disposable loopback25435 as `recovery_case_test`; fixture
database was dropped and server shutdown confirmed. Lifecycle evidence:
`outputs/recovery-case-repeat-postgres.log`. No retained application database was used.

## Browser checks

Operator created a case, pinned PO/invoice, ran a comparison, recorded an Arabic/English
decision, checked identical-decision prevention, canceled revision/rerun, and re-pinned
the same version without change. An intentional second run used a different tolerance.
Reviewer revised the first run; both decisions remained in its history while the second
run remained independent. Viewer could read the result/history with no new mutation
controls. Administrator-only was denied, combined administrator+Operator had permitted
controls, and revoked/signed-out sessions removed private case content.

Dark revision and light replacement layouts fit 390px without horizontal overflow.
Restored dark/default viewport and closed the temporary tab. Original user tabs remain.
Screenshot: `outputs/ui-preview/comparison-repeat-protection.png`.

One preview restart reset synthetic data. Preview3410 remains running without Docker.
Sample case `0d3b54da-99dd-4bd6-ba64-9cd97e84b0d9`,
`Repeat protection — مراجعة القرارات`, has PO/invoice pins and two comparison runs;
the first run has two decisions. No production deployment or retained database changes.

## Remaining boundaries

R4-T05b still covers document review/upload/processing and question repetition. This
task does not claim those newly audited gaps fixed. Real Entra acceptance remains R5,
and the manual history-download check remains pending. The durable creation guarantee
requires the new migration and a stable request key; separate intentional creations
and separate tabs with different keys are distinct cases.
