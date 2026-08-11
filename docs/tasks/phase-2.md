# Phase 2 task ledger

**Phase status:** In progress  
**Implementation status:** P2-T06 complete; P2-T07 pending and ready

P2-T01 through P2-T04 and P2-T06 were completed on 2026-08-11. P2-T07 is the next ready task but has not started. P2-T05 remains ready but optional and has not started.

| ID | Task | Status | Depends on | Planned evidence |
|---|---|---|---|---|
| P2-T01 | Contract, async architecture, P0 quality gates, and fixture-plan pre-review | Complete | Phase 1 complete | [Provider contract](../design/phase-2-extraction-provider-contract.md), [sample plan](../data/phase-2-sample-plan.md), [quality plan](../testing/phase-2-quality-plan.md), [approval record](../reviews/P2-T01-phase-2-implementation-start.md) |
| P2-T02 | Implement normalized pages/fields/evidence persistence and domain/state transitions, including `byte_start`/`byte_end` column semantics | Complete | P2-T01 complete | [P2-T02 contract and persistence evidence](../evidence/phase-2/P2-T02-contract-persistence-verification.md) |
| P2-T03 | Generate reproducible synthetic fixtures and versioned expected JSON labels | Complete | P2-T01 and P2-T02 complete | [P2-T03 verification](../evidence/phase-2/P2-T03-synthetic-fixture-verification.md) |
| P2-T04 | Implement local native-PDF and scanned-OCR providers | Complete | P2-T02 and P2-T03 complete | [P2-T04 local-provider verification](../evidence/phase-2/P2-T04-local-provider-verification.md) |
| P2-T05 | Define the optional Azure Document Intelligence adapter and credential-free contract tests | Pending (ready) | P2-T02 complete | Stubbed contract tests; no credentials required |
| P2-T06 | Implement the PostgreSQL durable queue and .NET worker | Complete | P2-T01 and P2-T02 complete | [P2-T06 verification](../evidence/phase-2/P2-T06-queue-worker-verification.md) |
| P2-T07 | Add public API and Next.js processing-state behavior | Pending (ready) | P2-T06 complete | API contract and rendered UI tests |
| P2-T08 | Run Phase 2 acceptance verification and publish evidence | Blocked | P2-T03–P2-T07 | Test totals, build results, and honest local demonstrations |

## P2-T02 completion gate

- [x] Schema 2.0 normalized contract and domain transitions implemented.
- [x] Pages, UTF-8 evidence, nullable confidence, typed warnings, provider/model/metrics, and `Partial`/`Cancelled` states persist and hydrate.
- [x] Initial, hardening, and legacy-page-policy migrations applied locally; EF reports no pending model changes.
- [x] Bounded 5 MiB client and list/detail hydration split verified.
- [x] Automated suites and builds passed with the exact totals in the [evidence record](../evidence/phase-2/P2-T02-contract-persistence-verification.md).
- [x] Integration Guardian reported **APPROVED** with no blockers on 2026-08-11.

## P2-T03 implementation boundary

- [x] Generate fixtures reproducibly from synthetic source data only.
- [x] Record each generated artifact, generator input/version, font/tool source, applicable license, size, and SHA-256 in a source/license register.
- [x] Store versioned expected labels and validate every label against extraction schema 2.0 before it can be used by tests.
- [x] Keep labels focused on deterministic contract behavior and reviewed outcomes; do not derive or report accuracy from this fixture set.
- [x] Preserve Phase 3 as the owner of evaluation-dataset design, accuracy metrics, thresholds, and provider comparisons.
- [x] Link implementation, automated verification, and Guardian review before marking P2-T03 complete.

Final implementation and verification are recorded in the [P2-T03 evidence](../evidence/phase-2/P2-T03-synthetic-fixture-verification.md). Integration Guardian reported **APPROVED** with no blockers on 2026-08-11.

## P2-T04 implementation boundary

- [x] Implement the internal/testable `local` provider while preserving the deterministic `mock` provider.
- [x] Extract native English PDF text with pinned `pdfplumber`/`pdfminer` versions.
- [x] Render scanned PDFs with pinned `pypdfium2` and OCR scanned pages plus PNG/JPEG inputs with pinned Tesseract/`pytesseract` and English `eng` data.
- [x] Record exact library versions, runtime/container digest and image ID, OCR/model identity, language-data versions/hashes, sources, and licenses.
- [x] Verify native and scanned fixture paths against the provider-neutral schema without converting fixture behavior into an accuracy claim.
- [x] Enforce page/pixel/decompression limits, OCR model hashes, normalized-result validation, truthful mixed-provider identity, and a path-independent disabled legacy local route.
- [x] Keep Azure adapters, durable queue/worker orchestration, UI processing states, Arabic, and formal evaluation outside P2-T04.
- [x] Link automated host, container, fixture, golden, lint, and Compose verification.
- [x] Record Integration Guardian **APPROVED** verdict with no blockers on 2026-08-11.

The verified local extractor is wired through P2-T06's authenticated storage-key worker route. The arbitrary-path legacy HTTP route remains disabled; Compose's `EXTRACTION_PROVIDER=mock` setting applies to that disabled compatibility route, while worker requests explicitly select the verified `local` alias. See [P2-T04](../evidence/phase-2/P2-T04-local-provider-verification.md) and [P2-T06](../evidence/phase-2/P2-T06-queue-worker-verification.md) evidence.

## P2-T06 implementation boundary

The [approved extraction and orchestration contract](../design/phase-2-extraction-provider-contract.md) remains the source of truth. P2-T06 must not redefine queue states, transaction boundaries, leases, retry classification, cancellation, storage-key trust, result validation, or state mapping without a reviewed contract change.

- [x] Persist document, extraction run, and `extraction_jobs` row transactionally after storage succeeds, with compensation/orphan cleanup for failure gaps.
- [x] Implement deterministic PostgreSQL `FOR UPDATE SKIP LOCKED` claiming, database-clock leases, UUID fencing tokens, heartbeat, expiry recovery, and at-most-three claims.
- [x] Invoke Python outside the claim transaction through the authenticated versioned storage-key request; reject traversal and verify size/SHA before provider work.
- [x] Keep retries in .NET and only for classified transient network/timeout/408/429/5xx outcomes, using the approved 5-second then 30-second schedule.
- [x] Finalize results and run/document/job states transactionally only when the current lease token matches; stale workers write nothing.
- [x] Implement queued/leased cancellation, manual reprocessing versus delivery retry, prior reviewable-output preservation, and newest-job status primitives.
- [x] Wire the verified `local` provider through the private worker boundary while keeping the arbitrary-path legacy route disabled.
- [x] Add Activity/Meter instrumentation and unit/API/Python regression coverage.
- [x] Complete the final PostgreSQL rerun including the newest reprocess and skew/terminalization tests.
- [x] Complete the pinned container build and Compose native-PDF plus scanned-PDF workflow demonstrations.
- [x] Record Integration Guardian **APPROVED** with no blockers on 2026-08-11.

P2-T06 is intentionally sequenced before optional P2-T05 Azure work because it unlocks the existing local provider for the real workflow. This prioritization does not approve or implement Azure.

## P2-T01 review gate

- [x] Phase 1 boundaries and documented limitations identified.
- [x] ASP.NET/Python ownership and normalized contract approved.
- [x] Evidence polygon, rotation, page, quote, and offset rules approved.
- [x] Queue/state matrix, storage compensation, transactional enqueue, lease fencing, retry, timeout, cancellation, and recovery semantics approved.
- [x] Synthetic/licensed fixture plan approved.
- [x] Integration Guardian reported approval-ready with no blocking issues.
- [x] [Quality test plan](../testing/phase-2-quality-plan.md) accepted with all P0 gates assigned.
- [x] Rand approved implementation to begin on 2026-08-11.

## Planned quality gates

P2-T02 through P2-T08 must cover both native and scanned paths, the versioned request/result/error contract, provider configuration, upload/provider separation, PostgreSQL lease fencing and idempotency, bounded transient-only retry, cancellation/crash recovery, state preservation during reprocessing, safe errors, and evidence persistence/round-trip integrity.

The telemetry contract must capture provider/model/version, input bytes/pages, queue delay, provider and total duration, retries, outcome/failure, and nullable estimated-cost metadata. Phase 3—not Phase 2—will turn a labeled evaluation set into formal accuracy measurements.

## Status rules

- **Proposed:** phase scope exists but is not approved for implementation.
- **In review:** a planning artifact is under review; no implementation is implied.
- **Blocked:** work must not begin until its named gate is satisfied.
- **Pending:** sequenced future work with no completion claim.
- **Complete:** requires linked implementation, review, automated verification, and evidence.
