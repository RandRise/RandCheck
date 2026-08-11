# Phase 2 extraction and orchestration contract

**Status:** Approved for Phase 2 implementation on 2026-08-11  
**Implementation:** P2-T02 contract/persistence scope complete on 2026-08-11

This document defines the approved cross-service, queue, state, evidence, and failure contracts. The P2-T02 normalized contract and persistence scope is complete; provider fixtures, real PDF/OCR, queue orchestration, UI states, and Phase 2 acceptance remain later tasks.

## Ownership boundary

ASP.NET is the only public workflow owner. It validates uploads, chooses the configured provider alias, owns storage and document/run/job state, schedules retries, validates normalized responses, and persists results. Python/FastAPI owns classification, native text, OCR, and adapter dispatch. It invokes exactly the alias in the request and never owns orchestration retries. Provider-native payloads are not public contracts.

## Versioned worker request

```json
{
  "schema_version": "2.0",
  "job_id": "uuid",
  "document_id": "uuid",
  "extraction_run_id": "uuid",
  "storage_key": "organizations/uuid/documents/uuid/source.pdf",
  "sha256": "64-lowercase-hex-characters",
  "bytes": 12345,
  "declared_content_type": "application/pdf",
  "provider_alias": "local",
  "attempt": 1,
  "deadline_utc": "2026-08-11T12:00:00Z",
  "correlation_id": "bounded-safe-id"
}
```

No arbitrary filesystem path is accepted. Python resolves `storage_key` beneath a configured shared root, rejects absolute/traversal/symlink escapes, and verifies SHA-256 and byte length before processing. ASP.NET records the selected adapter alias/version at enqueue. Python returns the actual provider model/version, which ASP.NET verifies against the requested adapter.

Unknown major schema versions fail with `unsupported_schema_version`. A successful HTTP `200` contains only a validated `Ready` or `Partial` result.

## Normalized result

```json
{
  "schema_version": "2.0",
  "outcome": "Ready",
  "document_type": "invoice",
  "language": "en",
  "provider": { "alias": "local", "adapter_version": "pinned", "model": "tesseract-eng", "model_version": "pinned" },
  "metrics": { "provider_duration_ms": 0, "page_count": 1, "estimated_cost": null, "cost_currency": null, "price_basis": null },
  "pages": [
    { "page_number": 1, "canonical_text": "Invoice INV-001", "rotation_degrees": 0 }
  ],
  "fields": [
    {
      "key": "invoice_number", "occurrence": 0, "value_type": "string", "value": "INV-001", "normalized_value": "INV-001", "confidence": null,
      "evidence": [{ "page_number": 1, "quote": "INV-001", "start_utf8": 8, "end_utf8": 15, "polygon": [[0.1,0.2],[0.3,0.2],[0.3,0.25],[0.1,0.25]], "coordinate_unit": "normalized" }]
    }
  ],
  "warnings": [
    { "code": "page_ocr_degraded", "severity": "degraded", "safe_summary": "One page produced incomplete OCR text.", "page_number": 1, "field_key": null, "occurrence": null }
  ]
}
```

The zero values and names illustrate shape, not measured performance.

## Evidence and result invariants

- Canonical page text is Unicode NFC; CRLF and CR become LF; all other whitespace is preserved.
- Offsets are zero-based, end-exclusive UTF-8 byte offsets into that page's canonical text. Both land on code-point boundaries and reproduce `quote` exactly.
- Every polygon has exactly four clockwise points, starting top-left, with top-left origin, x right, y down, and each coordinate within `[0,1]`.
- Polygons reference the rotation-corrected upright page. Rotation is exactly `0`, `90`, `180`, or `270` degrees.
- Pages are unique and contiguous from 1 through N. A field's `(key, occurrence)` is unique.
- `Ready` and `Partial` require pages and at least one unique, validated field with evidence. `Ready` may contain only `info` warnings. `Partial` requires at least one `degraded` warning. Failed processing uses the error envelope, not a result body.
- Confidence is nullable and persisted nullable; when present it is within `[0,1]`.
- Limits: existing 10 MB upload cap, at most 50 pages, 1 MB UTF-8 canonical text per page, 5 MB normalized result, 500 fields, 10 evidence items per field, and 100 warnings.
- Duplicate field identities, missing required evidence, quote/offset mismatch, invalid polygons, excess limits, and other malformed results are permanent failures.

Page dimensions are deliberately absent. PDF, raster, Tesseract, and Azure dimension units are not portable, while normalized polygons already map to the rotation-corrected rendered page.

### Warning contract

Each provider-neutral warning contains a stable snake-case `code` of at most 100 characters, `severity` (`info` or `degraded`), a safe summary of at most 300 characters, and optional page or field scope. `field_key` and `occurrence` are supplied together; scoped pages and fields must exist in the same result. Warnings are unique by `(code, severity, page_number, field_key, occurrence)` and sorted deterministically by severity, page, field key, occurrence, then code.

Warnings contain no document text, extracted values, filenames, storage keys, tenant/user identifiers, provider payloads, credentials, or stack traces. They describe a bounded condition such as a degraded OCR page, not the sensitive content that caused it.

P2-T02 owns domain methods/transitions and the `extraction_pages` and `extraction_warnings` entity/table migrations, page/evidence/warning foreign keys and validation, structured polygon persistence, rotation, and nullable-confidence migration. Warning rows carry organization/run ownership plus optional page/field scope so `Ready` and `Partial` round-trip deterministically. P2-T02 must rename/replace any existing `character_start`/`character_end` persistence columns with unambiguous `byte_start`/`byte_end` columns; misleading character-offset names must not remain beside UTF-8 byte semantics.

## Versioned safe error envelope

```json
{
  "schema_version": "2.0",
  "error": {
    "code": "provider_timeout",
    "retryable": true,
    "summary": "The configured provider did not finish within this attempt.",
    "correlation_id": "bounded-safe-id"
  }
}
```

Errors contain no fields, provider payload, storage path, document text, credentials, or stack trace. Python classifies adapter/network failures. Only ASP.NET schedules retries. Retryable classes are network failures, timeout, HTTP 408/429, and provider 5xx. Azure polling remains inside one 120-second attempt and SDK retry amplification is disabled.

| Stable error code | Retryable | Classification |
| --- | --- | --- |
| `unsupported_schema_version` | No | Unknown request/result major version |
| `invalid_storage_key` | No | Absolute, traversal, symlink-escape, or missing storage key |
| `content_integrity_mismatch` | No | Stored bytes do not match expected size or SHA-256 |
| `input_too_large` / `page_limit_exceeded` | No | Contract limit exceeded |
| `unsupported_media_type` | No | Unsupported detected content |
| `corrupt_document` / `encrypted_document` | No | PDF cannot be safely processed |
| `ocr_unavailable` | No | Required pinned local OCR runtime is absent |
| `provider_configuration_error` | No | Selected adapter lacks valid configuration |
| `provider_authentication_failed` / `provider_rejected` | No | Provider permanent 4xx rejection |
| `malformed_provider_result` | No | Provider output violates the normalized contract |
| `provider_network_error` / `provider_timeout` | Yes | Network failure or bounded attempt timeout |
| `provider_throttled` / `provider_unavailable` | Yes | Provider 429, 408, or 5xx transient failure |
| `request_cancelled` | No | User/system cancellation; explicit reprocessing is required |
| `internal_extraction_error` | No | Unclassified safe terminal failure; never retried automatically |

## Durable queue and fencing contract

`extraction_jobs` states are `Pending`, `Leased`, `RetryScheduled`, `Succeeded`, `Failed`, and `Cancelled`, with unique `(organization_id, extraction_run_id)`. PostgreSQL time is authoritative.

An atomic claim selects a due `Pending`/`RetryScheduled` job or expired `Leased` job, ordered by `available_at`, `created_at`, then ID, using `FOR UPDATE SKIP LOCKED`. It changes the row to `Leased`, creates a new UUID lease token and owner, sets expiry to database `now + 180 seconds`, increments `attempt_count`, returns the row, and commits before any provider call.

The provider call is outside the database transaction. The worker heartbeats every 30 seconds and extends the lease only when state, owner, and token still match. Each attempt deadline is 120 seconds. At most three claims are allowed. Transient attempts use fixed delays of 5 seconds then 30 seconds; expired work at the limit becomes `Failed` with `worker_lease_expired`.

Finalization is one transaction conditioned on current `Leased` state and lease token. It persists pages, fields, evidence, terminal run/document transitions, and the terminal job state. A stale worker writes nothing and discards its response.

Cancellation of queued/retry work terminalizes immediately. Cancellation of leased work invalidates the lease token, marks job/run `Cancelled`, and best-effort cancels HTTP; a stale worker still cannot commit. Manual reprocessing creates a new run/job. Delivery retry reuses the same run/job.

This is a PostgreSQL durable queue behind an abstraction, not an outbox. Phase 2 avoids broker/database dual writes. RabbitMQ can be reconsidered only from measured scale.

## Storage transaction boundary

The file is stored before the database transaction. That transaction creates document, run, and job together. Database failure triggers file deletion; a bounded orphan-cleanup process handles the crash gap between storage and transaction/compensation.

## Document, run, job state matrix

| Event/result | Extraction run | Job | Document |
|---|---|---|---|
| Upload | `Queued` | `Pending` | `Processing`, unless an older reviewable run exists; then retain `ReadyForReview` |
| First claim | `Running` | `Leased` | unchanged |
| Transient retry | `Running` | `RetryScheduled` | unchanged |
| Ready | `Succeeded` | `Succeeded` | `ReadyForReview` |
| Partial with validated evidence and warning | `Partial` | `Succeeded` with result `Partial` | `ReadyForReview` |
| Permanent/exhausted failure | `Failed` | `Failed` | retain prior `ReadyForReview`; otherwise `ExtractionFailed` |
| Cancellation | `Cancelled` | `Cancelled` | retain prior `ReadyForReview`; otherwise new `ProcessingCancelled` state |

Review selection includes the latest `Succeeded` or `Partial` run. A separate status resource reports the newest job, allowing older reviewable output to remain usable while reprocessing runs.

P2-T02 owns the `ProcessingCancelled` document-state migration and mapping. A cancelled document with no reviewable output remains stored and may be explicitly reprocessed; it must not be reported as extraction failure or left indefinitely as processing.

## Providers and local runtime

- `mock`: deterministic development/contract adapter.
- `local`: pinned `pdfplumber`/`pdfminer` for native text, `pypdfium2` for rendering, and Tesseract through `pytesseract` with the English pack.
- `azure_di`: optional, explicitly configured, and verified offline with sanitized/stubbed responses.

P2-T03's canonical fixture generator is separately pinned to Python 3.11.15 on `linux/amd64`, Pillow 9.4.0, ReportLab 3.6.12, and hash-verified Bitstream Vera Sans 1.10. Its registered font notice and composite PDF licensing belong to fixture provenance, not the extraction runtime.

Before P2-T04 local-provider implementation or acceptance, P2-T04 must pin and register `pdfplumber`/`pdfminer`, `pypdfium2`, Tesseract/`pytesseract`, English `eng` data, the runtime container digest, and the exact OCR/model mapping. Runtime downloads are forbidden. Missing settings for an explicitly selected provider fail fast.

Schema 2.0 fixtures use the exact .NET field-value enum vocabulary and provider-metrics semantics. Cross-language contract tests enforce nullable/finite metrics, cost/currency/price-basis coupling, and rejection of unknown value types.

## Telemetry ownership

ASP.NET/PostgreSQL time owns queue delay, attempt/retry count, and total duration. Python owns provider duration, page count, and actual model/version. Stored document metadata owns bytes. Estimated cost stays null unless the provider supplies it or a named, versioned price basis calculates it. None of these hooks is an accuracy claim; formal accuracy remains Phase 3.

## P2-T01 approved decisions

- Approve the request/error/result, limits, canonical text, evidence, and persistence rules.
- Approve queue fencing, state matrix, retry/cancellation, storage compensation, and reprocessing semantics.
- Approve local runtime/license pins and optional Azure boundary.
- Accept the [Phase 2 quality plan](../testing/phase-2-quality-plan.md).

## P2-T02 implementation checks

- [x] Schema 2.0 pages, UTF-8 `byte_start`/`byte_end` evidence, nullable confidence, typed warnings, provider/model/metrics, and `Partial`/`Cancelled` transitions implemented.
- [x] `20260811094741_Phase2NormalizedExtractionContract`, `20260811100426_HardenExtractionEvidenceScope`, and `20260811101909_PreserveLegacyPageNumbers` applied to the local PostgreSQL database.
- [x] Legacy page numbers are retained by explicit policy while new normalized results enforce their contract limits before persistence.
- [x] Detail reads hydrate pages/fields/evidence/warnings; list reads avoid that heavy graph.
- [x] Python response bodies are bounded to 5 MiB before deserialization.
- [x] Integration Guardian reported **APPROVED** with no blockers. Exact verification is recorded in [P2-T02 evidence](../evidence/phase-2/P2-T02-contract-persistence-verification.md).
