# Phase 2 task ledger

**Phase status:** In progress  
**Implementation status:** P2-T03 complete; P2-T04 pending and ready

P2-T01, P2-T02, and P2-T03 were completed on 2026-08-11. P2-T04 is unblocked and ready for an explicit start; it is not in progress.

| ID | Task | Status | Depends on | Planned evidence |
|---|---|---|---|---|
| P2-T01 | Contract, async architecture, P0 quality gates, and fixture-plan pre-review | Complete | Phase 1 complete | [Provider contract](../design/phase-2-extraction-provider-contract.md), [sample plan](../data/phase-2-sample-plan.md), [quality plan](../testing/phase-2-quality-plan.md), [approval record](../reviews/P2-T01-phase-2-implementation-start.md) |
| P2-T02 | Implement normalized pages/fields/evidence persistence and domain/state transitions, including `byte_start`/`byte_end` column semantics | Complete | P2-T01 complete | [P2-T02 contract and persistence evidence](../evidence/phase-2/P2-T02-contract-persistence-verification.md) |
| P2-T03 | Generate reproducible synthetic fixtures and versioned expected JSON labels | Complete | P2-T01 and P2-T02 complete | [P2-T03 verification](../evidence/phase-2/P2-T03-synthetic-fixture-verification.md) |
| P2-T04 | Implement local native-PDF and scanned-OCR providers | Pending (ready) | P2-T02 and P2-T03 complete | Native and scanned integration tests |
| P2-T05 | Define the optional Azure Document Intelligence adapter and credential-free contract tests | Pending (ready) | P2-T02 complete | Stubbed contract tests; no credentials required |
| P2-T06 | Implement the PostgreSQL durable queue and .NET worker | Pending (ready) | P2-T01 and P2-T02 complete | Transaction, claim, idempotency, retry, timeout, and recovery tests |
| P2-T07 | Add public API and Next.js processing-state behavior | Blocked | P2-T06 | API contract and rendered UI tests |
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
