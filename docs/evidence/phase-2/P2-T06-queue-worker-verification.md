# P2-T06 durable queue and worker verification

**Date:** 2026-08-11
**Status:** Complete
**Review:** Integration Guardian **APPROVED** with no blockers

## Delivered boundary

- Migration `20260811131151_AddDurableExtractionQueue` and PostgreSQL-backed `extraction_jobs` queue.
- Asynchronous upload returns `202 Accepted` after scoped storage and transactional document/run/job creation; database failure compensates storage and maintenance cleans bounded old orphans without following reparse points.
- Deterministic `FOR UPDATE SKIP LOCKED` claims using PostgreSQL time and monotonic aggregate-facing `BusinessNow`.
- 180-second leases, 30-second heartbeat, 120-second attempt deadline, maximum three claims, and fixed transient retry delays of 5 then 30 seconds.
- Lease-token fencing, stale-worker no-write behavior, pending/leased cancellation, manual reprocessing, delivery retry, prior-reviewable-output preservation, and latest-job primitives.
- Authenticated private Python `POST /internal/extractions` route; arbitrary-path legacy route disabled.
- Strict storage-key containment, symlink/traversal rejection, byte/SHA verification, schema/deadline/provider/error validation, bounded response, and normalized result validation.
- Verified local native/OCR provider wired through the worker; ActivitySource traces and Meter counters for claimed/completed/retried/failed/lease-lost outcomes.

The [approved extraction and orchestration contract](../../design/phase-2-extraction-provider-contract.md) remains the source of truth.

## Final verification

| Check | Result |
|---|---|
| Domain | 81 passed |
| Application | 18 passed |
| Infrastructure | 36 passed |
| PostgreSQL persistence | 29/29 passed; test command reported 1m16s and measured wall time was 89.4s |
| Architecture | 4 passed |
| API | 16 passed |
| .NET Release build | 0 warnings, 0 errors |
| Python host discovery | 71 total: 66 passed, 5 runtime-gated skipped |
| Existing pinned Docker runtime | The five host-skipped runtime tests passed 5/5 in 32.708s |
| Fixture verification | 22 artifacts, 999,112 bytes; passed |
| Next.js | 5 total: 2 Vitest component tests and 3 Node request/ETag tests |
| Next.js lint and production build | Passed |
| VitePress production build | Passed; final documentation rerun completed after this evidence update |
| EF model | No pending model changes |
| Local PostgreSQL | Migration already applied; database up to date |
| Docker Compose configuration | Passed |
| Integration Guardian | **APPROVED**, no blockers |

The final database suite includes concurrency/claim, fencing, pending and leased cancellation, reprocess/latest-job, future-clock `BusinessNow`, retry/exhaustion, and expiry terminalization coverage.

## Built images

| Image | Build result |
|---|---|
| AI | Built successfully from a 508.75 kB context; manifest list `sha256:e12aa436af1edf96cb40f7362bb9b4fd53370bf2e1789dd26359e9ac7f60c3dd` |
| Backend | Built successfully from a 930.76 kB context; manifest list `sha256:56a0c64c1c93729c656de35d65261ef7650a2610936053b02a448aa37f828cbb` |

## Real local Compose demonstrations

| Synthetic input | Observed workflow | Persisted result |
|---|---|---|
| Native English PDF | Upload returned `202`/`Processing`, then reached `Ready` | 8 fields; provider `pdfplumber`; recorded `processingMs` 1,140 |
| Scanned English PDF | Upload returned `202`/`Processing`, then reached `Ready` | 8 fields; provider `tesseract-eng-osd`; recorded `processingMs` 10,793 |

The timings are application-recorded observations from two local synthetic demonstrations. They are not benchmarks, service-level objectives, accuracy measurements, production-performance claims, or client outcomes.

## Honest scope

P2-T06 proves the approved local durable workflow for the covered synthetic native and scanned cases. It does not establish production reliability, throughput, extraction accuracy, Arabic support, Azure integration, tenant/user authentication, or client impact.
