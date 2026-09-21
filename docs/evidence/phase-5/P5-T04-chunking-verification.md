# P5-T04 chunking and embedding-interface verification

**Date:** 2026-09-07
**Status:** Complete — provider-independent components verified

## Delivered

- Provider-free, byte-preserving Python page and reviewed-fact chunking.
- Versioned byte limits, deterministic IDs, line-boundary preference, bounded
  overlap, source-size limits, and fail-closed handling of oversized facts.
- ASP.NET snapshot export and authoritative reconstruction of proposed chunks.
- Stable IDs for new evidence spans, without rewriting historical citations.
- A disabled-by-default asynchronous embedding interface and whole-batch validator.
- A reproducible chunk-coverage audit on the existing synthetic Phase 5 pack.

The [wire/algorithm contract](../../design/phase-5-chunking-contract.md) specifies
ownership, limits, ID construction, error behavior, and remaining integration.

## Verification results

- 26 new .NET application cases passed, including the actual Python subprocess
  round trip, stable span replay, corrected/added/cleared facts, polygon-only
  references, and forged source/profile/chunk/evidence rejection.
- 24 focused Python tests passed: 10 chunking, 6 embedding-interface, and the
  existing 8 question-set tests. Chunking includes 100 seeded generated Unicode
  cases for exact offsets, coverage, and termination.
- Synthetic pack audit: 6 page chunks across 4 source identities (3 unique PDF
  files), all 46 labelled evidence quotes contained wholly in a chunk, no misses,
  exact source-byte coverage, deterministic replay, and disjoint index IDs.
- Report: `outputs/phase-5/chunking-v1/chunk-audit.json`.
- 2 audit CLI tests passed: report replay and a deliberately too-small profile
  that correctly reports evidence misses rather than lowering the gate.
- Full non-persistence .NET regression: 221 passed (109 domain, 51 application,
  36 infrastructure, 4 architecture, 21 API). This includes the 26 new cases.
- 2 targeted PostgreSQL round-trip regressions passed with newly deterministic
  span IDs: exact cited graphs and reviewed-fact/refusal history. The earlier
  full 37-test persistence run is recorded under P5-T03; it was not all rerun
  for this component-only change. The temporary native PostgreSQL server was
  stopped afterward; no application database was migrated.
- No pending EF model changes; no new database migration required.
- Documentation build and whitespace diff checks passed.

The focused Python total is 26 passing tests (24 component/question-set cases
and 2 audit CLI cases). The complete Python/web regression suites were not rerun
at this checkpoint; those remain part of the Phase 5 end-to-end acceptance gate.

The plain system Python lacks `pdfplumber`, so tests involving the existing PDF
audit were rerun successfully with the bundled Python runtime. The new chunking
and embedding-interface modules/tests also pass on Python 3.11 without additional
dependencies. Early .NET test-fixture failures were corrected to satisfy the
existing extraction page-count and per-field evidence requirements; those
production invariants were not relaxed.

## Reproduce

```powershell
$env:PYTHONPATH = 'apps/api'
python -m unittest discover -s apps/api/tests -p 'test_cited_*.py' -v
python -m unittest discover -s apps/api/tests -p 'test_phase5_chunk_audit_cli.py' -v
python tools/phase5_chunk_audit.py --output outputs/phase-5/chunking-v1/chunk-audit.json
dotnet test apps/backend/tests/UaeDocumentIntelligence.Application.Tests --no-restore --filter 'FullyQualifiedName~CitedIndexAssemblyTests'
```

Use a Python runtime containing the existing extraction/PDF dependencies for the
question-set and CLI audits. `PHASE5_PYTHON` optionally selects the Python
executable used by the .NET round-trip test; otherwise it uses `python` on PATH.

## What this does not prove

This is indexing-component verification, not retrieval recall or answer
correctness. The default profile preserved this small synthetic pack's evidence;
it has not been optimized against unseen real invoices or complex layouts.
Unicode-boundary tests do not establish Arabic OCR/language support.

No embedding provider or answer model was called, no source documents were sent
to an external service, and no vector column was added. Live indexing jobs,
HTTP transport, transactional idempotency, lexical/vector retrieval, API/UI,
semantic validation, and adversarial end-to-end acceptance remain outstanding.
Polygon-only evidence is preserved through its source ID, not converted to a
text span. Integration must retain that distinction when showing citations.

P5-T04 does not close the deferred embedding-storage portion of P5-T03. The next
provider-independent work is P5-T05 indexing integration and lexical retrieval.
