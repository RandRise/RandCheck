# R4-T04 — Case references, categories and filters

September 30, 2026. Real UI, API, persistence migration and synthetic preview adapter.
Contract: [case references and categories](../design/case-reference-contract.md).

## Delivered behavior

- Cases receive a stable `REC-YYYY-000001` reference, scoped to organization and UTC
  creation year. UUID links remain stable; names remain free-form English/Arabic.
- Categories are Uncategorized, Office supplies, Services and Equipment. Metadata
  edits preserve pinned reviews, frozen comparisons, decisions and case references.
- Strong ETags prevent stale edits. The UI preserves unsaved metadata during stale
  recovery; a remotely closed case becomes read-only without discarding the draft.
- Search by reference/name and filters by category, status and inclusive UTC creation
  dates execute before server pagination. Applying/clearing filters resets paging.
- English/Arabic role guides explain references, categories, filtering and edit limits.

## Permissions and integration

| Role | List/filter/read | Create/edit mutable cases |
| --- | --- | --- |
| Operator | Yes | Yes |
| Reviewer | Yes | Yes |
| Viewer | Yes | No |
| None / administrator-only | No | No |

Frontend automated coverage includes each document role with and without the
independent administrator flag. Browser checks cover Operator, Reviewer, Viewer,
administrator-only, administrator+Operator, revoked and signed-out sessions.
Role switches clear private workspace state. Production Entra acceptance remains R5.

Metadata PUT uses the enterprise permission/audit boundary. Audit events retain
category/status and a title-changed boolean, without copying the title. The restricted
database account gains only name/category update permissions. Reference counters
remain private and assignment happens through the database insert trigger.

## Verification

- Full frontend suite: 217/217 across 18 files, including 17 independently authored
  metadata tests. Serial workers keep the resource footprint bounded.
- Request and synthetic fixture tests: 13/13. Includes stale/no-op writes, invalid
  metadata/filter rejection, role denial, unique references and title-free audit data.
- Focused ESLint and production Next build with TypeScript passed.
- Seven actual PostgreSQL checks passed: deterministic legacy backfill, counter
  continuation, concurrent allocation, organization/year isolation, seven-digit
  expansion, filter/date boundaries, restricted-account writes and denied counter/
  reference changes, preserved frozen history, migration rollback/reapply safeguards.
  Used only disposable native PostgreSQL on loopback port 25435; no Docker or retained
  application database. The populated metadata migration refuses destructive rollback.
- API/security regression: 134/134; mutation audit-operation coverage: 1/1;
  application reconciliation regressions: 8/8. No skipped backend checks.
- Independent agent authored frontend regressions and reviewed backend allocation,
  backfill, permissions and audit integration. Root reviewed database/schema contracts
  and preview parity. Review found a missing audit operation mapping; it was fixed.
  Final independent UI review passed without further actionable findings.

Commands: pinned `.node/node.exe` ran Vitest with `--root apps/web --maxWorkers 1
--no-file-parallelism --testTimeout 15000`, Node `--test` for fixture/request tests,
focused ESLint and `next build`. After loading `tools/use-project-runtimes.ps1`,
`dotnet test --no-restore` used these project/filter pairs:

| Project under apps/backend/tests | FullyQualifiedName filter |
| --- | --- |
| UaeDocumentIntelligence.Api.Tests | ReconciliationApiContractTests OR EnterpriseSecurityTests OR AuditAdmission |
| UaeDocumentIntelligence.Api.Tests (no-build) | AuditOperationCoverageTests |
| UaeDocumentIntelligence.Application.Tests | Reconciliation |
| UaeDocumentIntelligence.Persistence.Tests | Case_metadata OR Case_list_projects OR Reconciliation_graph_round OR Migration_apply_is_idempotent |

The persistence run used `TEST_POSTGRES_CONNECTION` for disposable loopback25435,
database `postgres`, user `recovery_case_test`; the fixture used and dropped only its
test database. PostgreSQL is stopped; lifecycle evidence is in
`outputs/recovery-case-metadata-postgres.log`. No TRX file was generated.

## Browser and preview

Created a bilingual case, edited its name as Operator, changed category as Reviewer
and administrator+Operator, and verified the same reference/UUID throughout. Arabic
search with category filtering returned the saved case. Viewer has filters and readable
details without mutation controls; administrator-only cannot open document workspaces.
Revoked and signed-out states display no case content.

Dark case creation/filter layout and light metadata editing checked at a 390px
viewport without horizontal overflow. Restored dark/default viewport and closed the
temporary verification tab; original user tabs were preserved.

Screenshot: `outputs/ui-preview/case-references-filters.png`.

The preview was restarted once for fixture changes, resetting temporary demo cases.
It remains running at http://127.0.0.1:3410. Fresh sample case
`2ee116e9-028b-4e5b-817c-0dd82de419ea` is titled
`September office supplies — لوازم المكتب`, reference `REC-2026-000001`, Office supplies.

## Rollout boundary

No retained database migration or deployment was performed. A real deployment needs
the reviewed migration and the normal private EnterpriseAdmin API-role configuration
step to update existing restricted account grants before application startup. Live
Entra walkthrough and the deferred manual history-download check remain separate.
