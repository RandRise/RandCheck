# P5-T03 persistence verification

**Date:** 2026-09-07
**Status:** Provider-independent foundation implemented and verified; embedding storage deferred

## Delivered

Nine tables persist versioned document indexes, page/reviewed-fact chunks,
canonical evidence spans, frozen retrieval sources, ranked hits, answers,
claims, and citations. The application has an organization-scoped repository;
there is no public question API or browser change at this checkpoint.

The migration is `20260907001143_AddCitedQuestionFoundation`. It preserves
the existing extraction, completed-review, and reconciliation identities.
Composite foreign keys prevent cross-organization and mismatched run/chunk/span
references. PostgreSQL validates exact quoted page bytes, finite ranking scores,
bounded ranks, outcome/reason shape, and cost metadata. Request keys are unique
per organization and actor. Index eligibility changes use optimistic concurrency.

Domain methods and EF tracked-write guards protect published evidence and
terminal answers. Retiring an index does not rewrite historical answers.
Reviewer-added facts retain review-action provenance without fabricated page
citations; corrected values retain their original machine evidence separately.

## Verification

- Domain: 109 passed, including 14 new cited-question cases.
- Application: 25 passed; infrastructure: 36 passed; architecture: 4 passed;
  API: 21 passed.
- PostgreSQL: 37 passed, including 7 new cited-question cases. Migration
  application, repeat application, rollback, and reapplication passed.
- `dotnet ef migrations has-pending-model-changes`: no pending model changes.
- VitePress documentation build passed. Node required an out-of-sandbox retry
  because the sandbox denied filesystem realpath access.

The .NET total is 232 passing tests. A redundant parallel build encountered a
Windows DLL lock while the persistence suite held its assembly; final regression
runs use `--no-build` to avoid that test-harness conflict. The final targeted
equal-length quote-tampering regression also passed (one rerun, not an additional
test in the 232 total).

Reproduction commands (set `TEST_POSTGRES_CONNECTION` to an isolated test server):

```powershell
dotnet test apps/backend/UaeDocumentIntelligence.sln --no-restore
dotnet ef migrations has-pending-model-changes --no-build --project apps/backend/src/UaeDocumentIntelligence.Infrastructure --startup-project apps/backend/src/UaeDocumentIntelligence.Infrastructure
```

Run `npm run build` from `docs` for documentation verification. The persistence
fixture drops only its exact `document_intelligence_test` database; never point
its connection at a server containing a database of that name that must be kept.

Database verification uses a disposable native PostgreSQL 16 cluster bound to
`127.0.0.1:55434`, separate from the application's database. Docker startup
stalled, so no result is claimed from Docker or pgvector. No development or
production database migration is applied by this verification. The temporary
native server was stopped after testing; its scratch data directory remains
under `tmp`. Docker did not respond to inspection or removal of the attempted
`uae-phase5-verification-20260907` container either, so that container's existence
and cleanup could not be confirmed. No global Docker restart was attempted.

## What this does not prove

These checks prove structural provenance, not semantic answer accuracy or UAE
regulatory compliance. No model was called and no invoice corpus was uploaded
to an external provider. The existing synthetic question set is still a
development fixture, not independent real-invoice validation.

Embedding persistence and vector shape remain deferred until the provider/model
decision. P5-T03 therefore remains partially complete overall. Deterministic
chunking and the replaceable adapter contract (P5-T04) are next; lexical/vector
retrieval, server-side answer validation, API, UI, adversarial end-to-end tests,
and Phase 5 acceptance remain outstanding. Polygon-only field evidence stays
in the existing source history and is not converted into invented text spans.

This checkpoint does not require a new approval gate to continue the already
authorized provider-independent work.
