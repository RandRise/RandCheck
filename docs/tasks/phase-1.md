# Phase 1 task ledger

**Phase status:** Complete

All Phase 1 tasks and acceptance criteria are implemented, verified, documented, and approved for completion. Limitations remain explicit and are carried into later phases.

| ID | Task | Status | Depends on | Evidence |
| --- | --- | --- | --- | --- |
| P1-T01 | Database requirements and current-schema inventory | Verified | Phase 0 planning | [Review record](../reviews/P1-T02-pre-implementation.md) |
| P1-T02 | Phase 1 ERD and persistence contract | Verified | P1-T01 | [Database design](../data/database-design.md) |
| P1-T03 | EF Core mappings and versioned migrations | Complete | P1-T02 decisions | `apps/backend/src/UaeDocumentIntelligence.Infrastructure/Persistence/Migrations/20260807130238_InitialSchema.cs` |
| P1-T04 | PostgreSQL constraints, migrations, and persistence verification | Complete | P1-T03 implementation | [Verification evidence](../evidence/phase-1/P1-T04-database-verification.md) |
| P1-T05 | Human-review contract and pre-implementation review | Complete | P1-T04 | [Completed contract review](../reviews/P1-T05-human-review-pre-implementation.md) |
| P1-T06 | ASP.NET human-review workflow | Complete | P1-T05 contract | [Acceptance evidence](../evidence/phase-1/P1-T08-acceptance-verification.md) |
| P1-T07 | Next.js correction and confirmation UI | Complete | P1-T05 contract and P1-T06 API | [Acceptance evidence](../evidence/phase-1/P1-T08-acceptance-verification.md) |
| P1-T08 | Phase 1 acceptance verification | Complete | P1-T06 and P1-T07 | [Final verification](../evidence/phase-1/P1-T08-acceptance-verification.md) |

## P1-T01 verification

- The historical prototype entities, relationships, and `EnsureCreated` usage were inventoried before replacement.
- The former prototype files were replaced by the four-project .NET architecture and versioned migration.
- Tenant, versioning, evidence, and correction gaps recorded in the [review record](../reviews/P1-T02-pre-implementation.md).
- Rand classified the prototype database and synthetic data as disposable.

## P1-T02 review gate

- [x] Core schema separated from future conceptual entities.
- [x] [Integration Guardian pre-implementation review recorded](../reviews/P1-T02-pre-implementation.md).
- [x] [Quality Engineer PostgreSQL requirements recorded](../reviews/P1-T02-pre-implementation.md).
- [x] Integration Guardian findings on cross-run integrity, retry selection, delete rules, action invariants, and active reviews were incorporated.
- [x] Rand resolved and approved the database decisions listed in the ERD.
- [x] Integration Guardian completed the post-implementation review and confirmed the original blockers were resolved.
- [x] Rand approved the ERD decisions and Phase 1 database implementation boundary.

## P1-T03 implementation

- [x] Domain, Application, Infrastructure, and API projects are present.
- [x] EF Core mappings implement the approved PostgreSQL contract.
- [x] The Npgsql initial migration is present and applies to PostgreSQL.
- [x] Development seeding creates the approved organization and system actor.
- [x] Integration Guardian completed the post-implementation review.

## P1-T04 verification

- [x] 45 domain tests passed.
- [x] 7 application tests passed.
- [x] 2 infrastructure storage tests passed.
- [x] 4 architecture dependency tests passed.
- [x] 8 PostgreSQL persistence tests passed.
- [x] 5 API contract tests passed.
- [x] Migration application, repeat application, rollback, and reapplication passed.
- [x] A local API upload/read run returned a `Ready` invoice with 9 fields.
- [x] Python, Next.js, and documentation verification passed.
- [x] Integration Guardian confirmed the implemented integration and required regressions passed.

See the [P1-T04 verification evidence](../evidence/phase-1/P1-T04-database-verification.md) for commands, results, and limitations.

## P1-T05 review gate

- [x] Existing review entities, action invariants, DTO boundary, and Phase 1 acceptance criteria identified.
- [x] Request/response contracts and permitted state transitions agreed.
- [x] Effective reviewed-value and stale-edit behavior agreed.
- [x] Integration Guardian contract and final integration reviews completed.
- [x] Quality Engineer test plan and final verification completed.
- [x] No additional material product decision was required from Rand.
- [x] P1-T06 and P1-T07 implementation handoff and delivery completed.

See the [completed P1-T05 contract review](../reviews/P1-T05-human-review-pre-implementation.md).

## P1-T06 backend verification

- [x] ASP.NET commands/queries, explicit DTOs, and review endpoints implemented.
- [x] Review-session lifecycle and append-only review actions implemented.
- [x] Effective reviewed values project without mutating machine output.
- [x] Strong ETag concurrency, conflicts, validation, and safe failures implemented.
- [x] Migration `20260811073607_AddReviewActionValueTypeConstraint` applied.
- [x] Domain, application, persistence, architecture, API, and infrastructure tests passed.

## P1-T07 frontend verification

- [x] Machine, reviewer decision, and effective values are visually separated.
- [x] Confirm, correct, add, clear, complete, and reload interactions are implemented.
- [x] Required reason, stale-edit, pending-decision, and safe error states are represented.
- [x] Request helpers send `If-Match` and retain rotated ETags.
- [x] Five frontend tests, lint, and production build passed.

## P1-T08 acceptance verification

- [x] Complete .NET, Python, frontend, and documentation verification suites passed.
- [x] Upload-to-review-to-correction-to-completion-to-reload succeeded against PostgreSQL.
- [x] Invalid files, provider failure, stale edits, invalid transitions, and safe error contracts are covered.
- [x] Every Phase 1 acceptance criterion has linked evidence.
- [x] Integration Guardian verdict: **APPROVAL-READY; no blockers**.
- [x] Phase 1 completion recorded.

See the [P1-T08 Phase 1 acceptance evidence](../evidence/phase-1/P1-T08-acceptance-verification.md).
