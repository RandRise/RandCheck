# Phase 2 quality plan

**Status:** Approved plan on 2026-08-11  
**Execution status:** P2-T02 scope verified; later Phase 2 gates pending

All P0 tests must run offline. Live Azure tests are optional and cannot gate local or CI acceptance. Phase 3 owns formal accuracy and Phase 7 owns Arabic.

## Layer-owned test matrix

| Priority | Owner/layer | Required tests |
|---|---|---|
| P0 | .NET Domain/Application | Complete state matrix, including `ProcessingCancelled`; Ready/Partial/Failed and typed-warning invariants; new-run versus same-job retry; cancellation; duplicate field identity; limits; nullable confidence |
| P0 | PostgreSQL persistence | Transactional document/run/job insert; unique organization/run; deterministic `SKIP LOCKED` claim; concurrent claim idempotency; DB-clock leases; heartbeat; expiry; max claims; token-fenced finalize/cancel; stale-worker no-write; crash recovery; prior reviewable-run preservation; page/evidence/warning round-trip |
| P0 | ASP.NET API/worker | Upload stores then returns `202` without provider call; compensation/orphan-cleanup contract; provider selection recorded; 120-second deadline; transient-only 5s/30s retry; safe errors; separate newest-job status; provider/model verification |
| P0 | Python contract/adapters | Schema 2.0 request/result/error; storage-root traversal rejection; SHA/size verification; exact alias dispatch; unknown major rejection; native PDF; 300-DPI scan OCR; canonical text; UTF-8 quote/offset; polygon/rotation; size/count limits; malformed response; fail-fast configuration |
| P0 | Compose end-to-end | Native two-page PDF and rasterized scan progress from `202` through durable job to reviewable persisted evidence; restart/crash recovery; no paid credentials |
| P1 | UI | Queued, processing, ready, partial, failed, cancelled/reprocess display; older reviewable output retained; no fabricated percentage |
| P1 | Optional Azure adapter | Sanitized/stub mapping, polling within one attempt, SDK retries disabled, actual model/version, credential fail-fast |
| P1 | Operations/telemetry | Queue delay/retries/total owned by .NET; provider duration/pages/model by Python; bytes by stored document; nullable cost and named/versioned price basis |

## Required P0 fixtures and failure doubles

- Reproducibly generated synthetic two-page native English invoice.
- The same pages rasterized at 300 DPI for scan/OCR behavior.
- Rotated/degraded input with reviewed partial or failure expectation.
- Corrupt and encrypted PDFs; supported synthetic PNG/JPEG; benign TXT mock/development input.
- Sanitized provider JSON plus malformed response.
- Deterministic network, timeout, 408, 429, 500, permanent 4xx, cancellation, lease expiry, worker crash, and stale-worker doubles.

## Evidence-specific assertions

- Page records remain contiguous and retain canonical text and rotation; provider-dependent physical dimensions are not persisted.
- NFC plus newline normalization is exact; other whitespace is unchanged.
- UTF-8 `byte_start`/`byte_end` offsets land on code-point boundaries and reproduce quotes; no misleading character-offset persistence names remain.
- Four-point clockwise/top-left-first upright-page polygons survive persistence and API serialization unchanged.
- Typed warnings enforce safe summaries, valid page/field scope, uniqueness, deterministic ordering, and `Ready`/`Partial` severity rules.
- Page/evidence foreign keys, field uniqueness, confidence nullability, and all input/result limits reject invalid data safely.

## Phase 2 release gate

- [ ] Every P0 test passes offline.
- [ ] PostgreSQL fencing, idempotency, cancellation, and crash tests pass under concurrent workers.
- [ ] Upload returns `202` before any provider call.
- [ ] Native and scanned Compose flows reach reviewable results without cloud credentials.
- [ ] Canonical pages, fields, and evidence round-trip through PostgreSQL and the public review API.
- [ ] Safe failure envelopes contain no fields, raw provider payloads, paths, document text, credentials, or stacks.
- [ ] Required builds, migrations, tests, exact totals, and local demonstrations are recorded in P2-T08 evidence.
- [ ] Integration Guardian reports no blocking cross-phase issue.

Fixture success proves deterministic behavior for the covered cases only. It must not be reported as extraction accuracy or production performance.

## P2-T02 completed checks

- [x] Domain/Application normalized-result, state, warning, evidence, limit, and nullable-confidence tests passed.
- [x] PostgreSQL page/evidence/warning constraints, hardening, legacy migration, and round-trip tests passed against the local database.
- [x] Infrastructure tests cover the bounded 5 MiB response client and contract mapping.
- [x] API/list-detail behavior avoids heavy extraction hydration in list reads and returns it for detail reads.
- [x] Guardian review passed with no blockers; exact suite totals and build results are in the [P2-T02 evidence record](../evidence/phase-2/P2-T02-contract-persistence-verification.md).

These checks close only P2-T02. The unchecked Phase 2 release gates above remain open.
