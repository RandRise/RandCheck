# P2-T08 Phase 2 acceptance verification

**Date:** 2026-08-12  
**Status:** Complete  
**Scope:** Local English native-text and scanned-invoice processing; no accuracy or production claim

## Acceptance result

Phase 2 is accepted for its approved local boundary. Upload is asynchronous, durable job state is public through a safe API, the UI renders queued, processing, ready, partial, failed, and cancelled outcomes, and operators can cancel or manually reprocess. Existing reviewable output remains available while a replacement run is active. Optional Azure work (P2-T05) remains pending and is not required for this outcome.

## P2-T07 verification rerun

| Check | Result |
|---|---|
| API contract tests | 17/17 passed, including `202` upload and public processing-state shape |
| Application / domain / infrastructure / architecture | 18/18, 81/81, 36/36, and 4/4 passed |
| Web rendered/request tests | 6/6 passed: 3 Vitest and 3 Node tests |
| Web lint and Next.js production build | Passed; TypeScript and static generation completed |
| .NET Release build | Passed with 0 warnings and 0 errors |
| Docker Compose configuration | Parsed successfully |

The host Python environment did not contain `pytest`, PostgreSQL was not listening on port 55432, and Docker Desktop was not running on 2026-08-12. Those runtime suites are therefore not reported as fresh reruns.

## Runtime and persistence evidence used by the gate

The unchanged `dda6ff7` processing foundation completed the pinned verification recorded in [P2-T06](./P2-T06-queue-worker-verification.md): PostgreSQL 29/29; Python 71 discovered with 66 host passes and the five runtime-gated tests passing 5/5 in the pinned container; a native two-page PDF and 300-DPI scanned two-page PDF each went from `202`/Processing to Ready with 8 fields; and all 22 registered fixtures (999,112 bytes) verified.

P2-T07 changes only public endpoint/UI behavior and tests. Current .NET and web regression/build results cover that changed boundary; P2-T06 covers the unchanged runtime boundary.

## Acceptance mapping

| Gate | Evidence |
|---|---|
| Native and scanned local completion | P2-T04 goldens and P2-T06 Compose demonstrations |
| Page-aware normalized evidence and exact UTF-8 round trip | P2-T02 persistence and P2-T04 provider evidence |
| Provider-neutral contract and configuration safety | P2-T02 and P2-T04 evidence |
| `202` upload outside provider lifecycle | API contract test and P2-T06 workflow |
| Claims, retries, cancellation, recovery, heartbeats, expiry, and fencing | P2-T06 PostgreSQL and worker suites |
| Safe partial/failure behavior, non-duplication, and telemetry | P2-T02/P2-T06 suites |
| Required fixture classes | P2-T03 register and P2-T04/P2-T06 verification |
| Honest processing UI and recovery actions | P2-T07 rendered tests and production build |

## Limitations

This acceptance proves deterministic behavior for covered synthetic fixtures and the local workflow. It is not extraction-accuracy evidence, a benchmark, a production-readiness assessment, a customer outcome, or proof of Azure, Arabic, authentication, tenant enforcement, or deployment capability. Phase 3 owns formal evaluation and accuracy claims.
