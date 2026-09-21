# P5-T05 indexing and lexical retrieval review

**Date:** 2026-09-07
**Status:** Provider-independent indexing/keyword retrieval verified;
P5-T05 remains in progress (vector/hybrid comparison deferred).

## What is working

Completed reviews can now be indexed through ASP.NET and the authenticated
private Python service. ASP.NET verifies the returned chunks against the
original snapshot, then persists a Ready index. Repeated and concurrent index
requests reuse the stored index.

The new retrieval API searches only the selected organization-scoped indexes,
saves ranked hits and provenance, and reloads the same history on replay.
Each hit includes its document, extraction/review revision, chunk, and exact
page spans where available. A Retrieved response is evidence, not an answer.

The [implementation contract](../../design/phase-5-lexical-retrieval-contract.md)
records routes, ownership, limits, idempotency, state transitions, and failure
behavior. No paid model, embedding provider, or vector database shape was selected.

## Measured retrieval baseline

The evaluation uses the existing synthetic English UAE question set, with
4 source identities, 3 unique PDFs, 6 page chunks, 30 questions, 24 answerable
questions, and 46 labelled evidence items. Indexes for an identical invoice in
another organization are present during evaluation. No new labels or
question-specific synonyms were added.

| Measurement | Result |
|---|---:|
| Labelled evidence in first hit | 33/46 (71.74%) |
| Labelled evidence in top 5 | 45/46 (97.83%) |
| Answerable questions with **all** labelled evidence in top 5 | 23/24 (95.83%) |
| Mean reciprocal rank of first labelled evidence | 0.9375 |
| Observed cross-organization hits | 0 |
| Synthetic recall gate (at least 95% of answerable questions) | Met |

Across these 30 local runs, recorded retrieval duration was 55–192 ms;
nearest-rank median was 80 ms and p95 was 165 ms. This is a single tiny-corpus
run, not a load test. The timer covers eligibility/search and hit construction
before the final save/commit; it excludes Python planning, API transport,
response reconstruction, and any answer generation.

The query policy is `lexical-or-english-v1`, fixed English full-text search,
literal OR terms, top-K 5, and `page-lines/1.0.0-b2048-o256` page chunks.
The PostgreSQL evaluation constructs page-only fixture indexes from audited
canonical PDF text; it does not rerun extraction or the live HTTP chunker for
every evaluation question. The separate live test exercises that transport
with both a page chunk and a reviewed-fact chunk.

**Known miss:** `p5-q-022`, “What tax-registration identifier belongs to the
seller?”, returned no hits. Its expected evidence is invoice page 1,
`Supplier TRN: 000000000000701 | TIN: 0000000000`. The keyword policy does not
map “seller” to “supplier” or “tax-registration identifier” to “TRN”. The miss
is retained as evidence for later retrieval comparison, not patched with a
test-specific synonym.

Artifacts in the repository:

- Input generator: `tools/phase5_lexical_fixture.py`.
- Prepared input: `samples/phase-5/lexical-v1/evaluation-input.json`.
- Actual PostgreSQL results, per-question ranks and raw latency:
  `outputs/phase-5/lexical-v1/postgres-evaluation.json`.
- Canonical original question-set SHA-256:
  `c5d18418baf999c5fecbae217eaa6c8bde8124402a9864698fe961b92f4664aa`.
- Prepared input SHA-256:
  `0ca7b62b55c272753dcdbdd0aecc16d501d3009b984fcba682e876ed2cb74f5a`.

Both input and report are explicitly synthetic and `claimEligible: false`.
The test asserts fixture integrity and scope; it writes a separate quality
gate result rather than hiding a retrieval miss inside a passing test count.

## Verification

- Full .NET regression: 273 passed — 111 domain, 51 application,
  42 infrastructure, 4 architecture, 25 API, and 40 PostgreSQL persistence.
- New coverage includes the real private HTTP/Python-to-PostgreSQL round trip,
  racing index creation, request replay/conflict, exact provenance, no-hit
  behavior, foreign-scope rejection, safe private errors, request validation,
  parameterized hostile search text, and retrieval-state checks.
- All nine migrations applied, rolled back, and reapplied in a disposable
  database. Final targeted checks also cover populated lexical downgrade:
  Retrieved becomes Failed while hits survive, then reapplication succeeds.
- Final targeted checks cover added hit source metadata and tracked-write
  rejection of retrieval-duration changes/restarting Retrieved history.
- New private-route Python tests: 7 passed. Existing private worker extraction
  regression: 7 passed. The previous chunking/question-set verification remains
  recorded in [P5-T04](P5-T04-chunking-verification.md).
- .NET Debug build: zero warnings/errors; no pending EF model changes.
- Documentation build and whitespace checks passed.

The final two targeted PostgreSQL cases are reruns within the 40-test suite,
not additional tests added to the total. TRX evidence is retained under
`tmp/phase5-test-results/phase5-lexical-*.trx`.

The live HTTP checks use an isolated `.venv` (Python 3.11.9, FastAPI 0.141.1,
Uvicorn 0.52.4, HTTPX 0.28.1); these satisfy existing project ranges. The
bundled PDF runtime prepares the audited input. Global Python packages and
project dependency declarations were not changed. Starlette reports a
non-failing test-client HTTPX deprecation warning; it has not been suppressed.
The temporary native PostgreSQL 16 test server is isolated on localhost:55434.
Only its designated test database was reset; no application database was migrated.
The test server was stopped after verification.

## What this does not prove

- This is a tiny development corpus: top 5 can contain almost the entire
  eligible corpus. These metrics do not establish real-invoice retrieval
  accuracy, scale, production latency, or UAE tax compliance.
- Six unanswerable/adversarial questions were submitted to retrieval, but
  this layer emits no answers or refusals. Returning hits for those questions
  is not a refusal-policy pass. Grounded answers and reliable abstention remain
  P5-T06/P5-T08 work.
- Vector and hybrid retrieval, embedding quality, answer correctness,
  semantic claim support, citation navigation, and model cost are unmeasured.
- Organization/actor-scoped tests are not production authentication or a full
  security review. Document text remains untrusted. No model/tool execution
  occurs here, so this is not an end-to-end prompt-injection defense result.
- Indexing is on demand, not queued. A process crash can leave a Running
  retrieval record; there is no automatic stale-run recovery yet.
- The full Python/OCR and web/browser suites were not rerun at this checkpoint.
  They remain required before Phase 5 acceptance. No question workspace UI
  has been added or visually accepted.

## Reproduce

Use the existing project setup, plus an isolated disposable PostgreSQL test
database. `TEST_POSTGRES_CONNECTION` enables the existing destructive test
fixture: do not point it at an application database. `PHASE5_PYTHON` must name
a Python runtime with the private API dependencies installed.

```powershell
python tools/phase5_lexical_fixture.py
$env:PHASE5_PYTHON = (Resolve-Path .venv/Scripts/python.exe).Path
$env:PYTHONPATH = 'apps/api'
.venv/Scripts/python.exe -m unittest discover -s apps/api/tests -p 'test_cited_routes.py' -v
.venv/Scripts/python.exe -m unittest discover -s apps/api/tests -p 'test_worker_extraction.py' -v
dotnet build apps/backend/UaeDocumentIntelligence.sln --no-restore
dotnet test apps/backend/UaeDocumentIntelligence.sln --no-build --no-restore
dotnet ef migrations has-pending-model-changes --no-build --project apps/backend/src/UaeDocumentIntelligence.Infrastructure --startup-project apps/backend/src/UaeDocumentIntelligence.Api
```

The first command additionally needs the declared PDF dependencies. Full .NET
persistence execution requires the isolated test connection described above.

## Next work

Implement the provider-independent claim/citation validator and explicit
insufficient-evidence contract in P5-T06. Keep the answer-provider decision and
the vector/hybrid comparison open; do not label Phase 5 or P5-T05 complete.
