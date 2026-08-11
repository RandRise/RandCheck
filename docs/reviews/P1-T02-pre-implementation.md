# P1-T02 review record

**Date:** 2026-08-07  
**Task:** Phase 1 ERD and persistence contract  
**Review status:** Complete; post-implementation blockers resolved and regressions passed

## Integration review

The Integration Guardian reviewed the current two-table prototype and proposed a tenant-ready, versioned core. Its blocking requirements were:

- keep extraction runs immutable;
- enforce organization, document, run, field, and review consistency with composite relationships;
- distinguish the latest attempt from the selected successful output;
- use static foreign-key delete behavior;
- define exact review-action invariants;
- prevent conflicting active review sessions;
- separate document aggregate status from extraction-run status; and
- replace `EnsureCreated` with controlled migrations only after policy approval.

The draft ERD was revised to include these requirements.

## Quality review

The Quality Engineer requires PostgreSQL verification for:

- migration from a blank database;
- idempotent migration application;
- constraints, indexes, and delete behavior;
- composite organization relationship integrity;
- immutable extraction retry history;
- append-only corrections;
- duplicate/hash scoping; and
- disposable rollback and reapplication.

SQLite-only tests will not count as PostgreSQL migration evidence.

## Post-implementation review outcome

The Integration Guardian found and re-reviewed cancellation handling, partial-file cleanup, request-ID bounds, extractor identity, aggregate collection encapsulation, and cross-layer persistence behavior. The production fixes and required regression tests are now present. The final full .NET suite passed 71 tests with no failures or skips; no further architecture review is required for P1-T03/P1-T04.

## Implementation verification — 2026-08-07

The approved model is now implemented through the four-project .NET solution and initial Npgsql migration. Automated database, domain, and architecture verification passed, and the local upload/read slice succeeded. See the [P1-T04 evidence record](../evidence/phase-1/P1-T04-database-verification.md).

The [P1-T04 evidence record](../evidence/phase-1/P1-T04-database-verification.md) contains the final totals and limitations.

## Rand decisions — 2026-08-07

- Use EF Core Code First with PostgreSQL as the canonical persistent database.
- Discard the prototype database and synthetic prototype data rather than import them.
- Seed a development organization and system actor.
- Require actor attribution for every review action and reasons for correction, addition, or clearing.
- Keep processing and review states separate while preserving API compatibility through DTOs.
- Use snake_case physical naming from the initial migration.
- Organize the ASP.NET backend as a modular monolith with Clean Architecture boundaries, Vertical Slice organization, and DDD-lite.
- Move the Phase 1 database foundation to **In progress**.
