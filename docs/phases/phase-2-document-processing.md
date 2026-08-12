# Phase 2 — Real document processing

**Status:** Complete
**Outcome:** English native-text and scanned invoices can be processed asynchronously into one page-aware, provider-neutral result without requiring paid credentials locally.

The approved local Phase 2 outcome was accepted on 2026-08-12 through P2-T08. Optional P2-T05 Azure work remains pending; no Azure, extraction-accuracy, or production-readiness claim is made.

## Planning set

- [Task ledger](../tasks/phase-2.md)
- [Extraction provider contract](../design/phase-2-extraction-provider-contract.md)
- [Synthetic sample plan](../data/phase-2-sample-plan.md)
- [Quality plan](../testing/phase-2-quality-plan.md)
- [2026-08-11 implementation-start record](../reviews/P2-T01-phase-2-implementation-start.md)
- [P2-T02 contract and persistence evidence](../evidence/phase-2/P2-T02-contract-persistence-verification.md)
- [P2-T03 synthetic fixture evidence](../evidence/phase-2/P2-T03-synthetic-fixture-verification.md)
- [P2-T04 local-provider evidence](../evidence/phase-2/P2-T04-local-provider-verification.md)
- [P2-T06 queue/worker evidence](../evidence/phase-2/P2-T06-queue-worker-verification.md)
- [P2-T08 acceptance evidence](../evidence/phase-2/P2-T08-acceptance-verification.md)

## Problem and user outcome

Phase 1 proves upload, persistence, deterministic extraction, and human correction. It does not process real PDF text or scanned pages. Phase 2 proposes replacing that fixture-only path with durable processing that preserves the evidence needed for a reviewer to verify every extracted value.

## Approved scope

- English invoices only: native-text PDFs and image-only scanned PDFs.
- Python/FastAPI classification, text extraction, OCR, and provider adapters.
- ASP.NET as the public API, workflow, persistence, and job-state owner.
- A provider-neutral result containing document type and language; typed values and confidence; page, quote, UTF-8 byte offsets, polygon, and coordinate unit; provider, model, and schema versions; typed safe warnings and partial outcomes.
- Configuration-selected `mock`, `local`, and optional `azure_di` providers. The local path must work without paid credentials.
- A PostgreSQL-backed `extraction_jobs` queue processed by a .NET `BackgroundService`.
- Honest UI states: queued, processing, ready, partial, and failed.
- Reproducibly generated synthetic fixtures, a source/license register, and versioned schema 2.0 expected labels.
- Measurement hooks for later analysis, not accuracy claims.

## Approved processing flow

1. The upload API stores the file, then in one database transaction creates the document, extraction run, and durable extraction job. Database failure deletes the file; cleanup handles crash-gap orphans.
2. The API returns `202 Accepted` without calling an extraction provider.
3. A .NET worker atomically claims an available or expired job using `FOR UPDATE SKIP LOCKED`, a UUID lease token, and PostgreSQL's clock, then commits before calling Python.
4. The worker invokes the Python service, whose configured adapter uses mock, local, or Azure Document Intelligence processing.
5. ASP.NET validates and persists the normalized result and completes the job/run transactionally.
6. Lease-token fencing prevents stale writes; 30-second heartbeats, a 180-second lease, 120-second attempt deadline, and at most three claims make crashes and transient failures bounded.

This is a durable database queue, not an outbox. Phase 2 avoids database/broker dual writes. RabbitMQ may be evaluated later only if measured throughput or scale justifies it.

## Reliability and safety requirements

- Unique `(organization_id, extraction_run_id)` job identity and token-fenced concurrent claims.
- Retries only for network/timeout/408/429/5xx failures, after 5 then 30 seconds.
- Explicit provider and total timeouts, cancellation handling, and crash recovery.
- Safe public failures for corrupt, encrypted, unsupported, timeout, provider, malformed-result, and cancellation cases.
- No document text, credentials, provider payloads, or stack traces in public errors.
- Configuration fails fast for an explicitly selected provider whose required settings are absent.

## Acceptance gates

- [x] A reproducibly generated two-page native PDF completes through the local path.
- [x] A 300-DPI rasterized scan of the same invoice completes through OCR.
- [x] Normalized evidence uses page numbers, consistent quote/offsets, page rotation, and `[0,1]` polygons.
- [x] Canonical page text and pages persist through a new `extraction_pages` relation; UTF-8 offsets and evidence round-trip exactly.
- [x] The normalized contract is complete, versioned, and provider-neutral.
- [x] Local provider selection works without cloud credentials; invalid provider configuration fails fast.
- [x] Upload returns `202` without invoking a provider in the request lifecycle.
- [x] Concurrent workers cannot claim the same job; retry, timeout, cancellation, and crash-recovery behavior is verified.
- [x] Queue state transitions, lease expiry, heartbeat, stale-worker fencing, and the document/run/job state matrix pass PostgreSQL integration tests.
- [x] Partial and failed results are safe and do not duplicate documents, runs, jobs, or field values.
- [x] Provider/model/version, bytes/pages, queue delay, provider/total duration, retry count, outcome/failure, and nullable estimated-cost metadata are captured.
- [x] Native, scanned, low-quality, corrupt/encrypted, and simulated provider-failure fixtures are covered by tests.

## Measurement boundary

Phase 2 records telemetry hooks and fixture behavior. Phase 3 owns the versioned evaluation dataset, field-level accuracy metrics, provider comparisons, thresholds, and any defensible performance conclusions. Passing Phase 2 fixtures is not an accuracy claim.

## Out of scope

- Arabic OCR or bilingual evaluation; Arabic remains Phase 7.
- Formal extraction-accuracy evaluation; that remains Phase 3.
- Authentication, tenant enforcement, production deployment, RAG, and reconciliation.
- Broker adoption, automatic financial approval, or production-readiness claims.

## Approved implementation boundary

- [x] Normalized request/result/error contract and evidence coordinate rules.
- [x] Durable job states, lease/retry policy, and public-state mapping.
- [x] Fixture generation, licensing, and expected-label conventions.
- [x] Optional Azure adapter boundary and credential-free test strategy.

Implementation must remain within these approved contracts. Later tasks remain gated by the [Phase 2 task ledger](../tasks/phase-2.md).

## P2-T02 verified foundation

- [x] Schema 2.0 pages, UTF-8 byte-offset evidence, nullable confidence, typed warnings, provider/model/metrics, and `Partial`/`Cancelled` states are implemented.
- [x] EF Core migrations add the normalized model, harden evidence scope, and preserve the explicit legacy page-number policy.
- [x] The .NET client rejects responses beyond 5 MiB and list/detail queries keep heavy extraction hydration out of list reads.
- [x] The migrations are applied to the live local development database and EF reports no pending model changes.
- [x] Integration Guardian reported **APPROVED** with no blockers on 2026-08-11.

See the [P2-T02 evidence record](../evidence/phase-2/P2-T02-contract-persistence-verification.md) for exact verification totals and limitations.
