# P1-T04 database and foundation verification

**Date:** 2026-08-07  
**Verification status:** Passed  
**Phase status:** Complete as of P1-T08; this page preserves the P1-T04 checkpoint

This record covers the implemented Phase 1 database foundation and the existing upload/read vertical slice. It does not claim production readiness, extraction accuracy, client use, or business impact.

## Implementation inspected

- Four production .NET 8 projects: Domain, Application, Infrastructure, and API.
- Dependency rules enforce the intended Clean Architecture boundaries.
- EF Core Code First uses Npgsql for the canonical PostgreSQL schema.
- Initial migration: `apps/backend/src/UaeDocumentIntelligence.Infrastructure/Persistence/Migrations/20260807130238_InitialSchema.cs`.
- Docker Compose exposes PostgreSQL on host port `55432`.
- Development-only seeding creates a fixed local organization and system actor when `DevelopmentData:Seed` is enabled.
- The private Python service uses the deterministic mock extractor for the synthetic sample.

## Reproduced verification

| Check | Command or method | Result |
| --- | --- | --- |
| .NET solution | `dotnet test apps/backend/UaeDocumentIntelligence.sln --no-restore --verbosity minimal` | **71 passed, 0 failed, 0 skipped:** 45 domain, 7 application, 2 infrastructure, 4 architecture, 8 PostgreSQL persistence, 5 API contract |
| .NET production build | `dotnet build apps/backend/UaeDocumentIntelligence.sln -c Release --no-restore` | Passed with 0 warnings and 0 errors |
| Python extractor | `python -m unittest discover -s apps/api/tests -v` | **1 passed** |
| PostgreSQL migration | `dotnet ef database update` against `localhost:55432` | Database already at the latest migration |
| EF model alignment | `dotnet ef migrations has-pending-model-changes` | No pending model changes |
| Next.js production build | `npm run build` in `apps/web` | Passed; `/` and `/_not-found` generated |
| VitePress production build | `npm run build` in `docs` | Passed after this documentation update |
| Compose definition | `docker compose config --quiet` | Valid |

The PostgreSQL persistence tests recreate an isolated `document_intelligence_test` database and verify migration from zero, repeat application, rollback and reapplication, required schema objects, check constraints, unique indexes, cross-organization relationship rejection, retry history, review-action foreign keys, and delete behavior.

## Post-review safeguards

The final regression suite also verifies:

- the document and running extraction attempt are persisted atomically in the first `SaveChanges`;
- request cancellation terminalizes the run as `Cancelled` and saves compensation with an independent bounded token;
- provider timeout produces a terminal cancelled run and an externally visible failed document result;
- generated partial files are removed after storage cancellation or stream failure without touching pre-existing files;
- external request IDs are trimmed or replaced and cannot exceed the persistence limit;
- provider-returned extractor identity is persisted and projected;
- aggregate navigation collections expose non-castable read-only wrappers; and
- API health, JSON/problem contracts, request IDs, upload `415`/`413`, and unknown-route `404` behavior pass contract tests.

The Integration Guardian confirmed its original blockers were resolved. After the read-only wrapper and storage-cleanup regression tests passed in the full suite, no additional architecture review was required.

## Local upload/read result

A fresh local run started the private Python service and ASP.NET API, uploaded `samples/uae-invoice.txt`, then read the created document through `GET /api/documents/{id}`.

```text
Status: Ready
Document type: invoice
Language: ar-en
Extracted fields: 9
Invoice number: INV-AE-2026-001
Extractor: mock-invoice-v1
Oversized request ID: 101 supplied characters normalized to a generated 32-character ID
```

This is a deterministic synthetic-fixture result using the mock provider. It demonstrates service integration, persistence, DTO projection, and retrieval only; it is not OCR or model-accuracy evidence.

## Completion boundary

P1-T03 and P1-T04 were completed at this checkpoint. Phase 1 was later completed through [P1-T08 acceptance verification](./P1-T08-acceptance-verification.md).

The offline restore used cached packages with NuGet audit disabled because the audit feed was unavailable. The final test command used `--no-restore`; this environmental limitation does not represent a dependency-vulnerability assessment.
