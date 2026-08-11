# P2-T02 contract and persistence verification

**Date:** 2026-08-11  
**Task:** P2-T02 — normalized extraction contract, persistence, and domain transitions  
**Result:** Complete; Integration Guardian **APPROVED** with no blockers

## Delivered boundary

- Schema 2.0 normalized pages, UTF-8 byte-offset evidence, nullable confidence, typed warnings, provider/model/metrics metadata, and `Partial` and `Cancelled` state behavior.
- EF Core migrations:
  - `20260811094741_Phase2NormalizedExtractionContract`
  - `20260811100426_HardenExtractionEvidenceScope`
  - `20260811101909_PreserveLegacyPageNumbers`
- An explicit migration policy preserves legacy page numbers while normalized new results are validated at the application boundary.
- The Python extraction client stops response bodies beyond 5 MiB before deserialization.
- List reads avoid hydrating the heavy pages/fields/evidence/warnings graph; document detail reads hydrate the reviewable extraction graph.
- The migrations were applied to the live local PostgreSQL development database.

## Verification

| Check | Result |
|---|---|
| .NET tests | **137 passed**, 0 failed, 0 skipped |
| Domain | 72 passed |
| Application | 23 passed |
| Infrastructure | 6 passed |
| PostgreSQL persistence | 17 passed |
| Architecture | 4 passed |
| API | 15 passed |
| Python `unittest` | **3 passed** |
| Next.js tests | **5 passed** |
| Next.js lint | Passed |
| Next.js production build | Passed |
| .NET Release build | Passed with 0 warnings and 0 errors |
| EF model check | No pending model changes |
| Local database | Migrations applied and database up to date |
| Docker Compose configuration | Valid; sandbox emitted only warnings about inaccessible user configuration |
| Integration Guardian | **APPROVED**, no blockers |

## Honest scope

This evidence verifies P2-T02's normalized contract, domain, persistence, and supporting client/query boundaries. It does not claim real native-PDF extraction, scanned OCR quality, durable queue completion, cloud readiness, production performance, customer usage, or extraction accuracy. Those remain later Phase 2/3 tasks.
