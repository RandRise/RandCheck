# P5-T06/T08 — Internal answer bridge

**Date:** 2026-09-09  
**Scope:** Ledger-to-private-service orchestration with synthetic fake providers.
No paid API, public generated-answer endpoint, billing feature or schema change.

**Follow-up:** [versioned prompt preflight](P5-T06-T08-prompt-preflight-verification.md)
adds a preparation receipt and changes the private dispatch envelope. The details
below record the earlier bridge-only checkpoint.

## Outcome

`CitedGenerationCoordinator` now loads a scoped frozen retrieval, reserves its
worst-case synthetic allowance, wins a durable dispatch transition, calls the
private answer service and validates the returned citations. The real
PostgreSQL/.NET/Python test uses an explicitly selected test-only Python entry
point. Production Python startup always selects the disabled adapter.

The returned type is `UnpublishedCitedProposal`: publication is always false,
as is semantic-support-established. No generated claim or answer is persisted,
and the existing public question endpoint remains evidence-only. The existing
invoice review and reconciliation workflows are unchanged.

## Safety and accounting behavior

- The coordinator's registered settings are disabled with no rate. There is no
  request-body or environment switch to enable it. Even explicit internal
  enablement rejects any provider alias except `test-only`. Removing this live
  gate is separate work, not a configuration task.
- Organization and actor come from the backend execution context. The caller
  supplies only an existing retrieval ID, not invoice text, model prices,
  arbitrary evidence IDs or a fresh spending operation ID. Retrieval must be
  nonempty and ready; its source indexes must remain eligible.
- The retrieval ID is the spending operation ID: one attempt per retrieval in
  this increment. The complete outgoing wire bytes are hashed; the ledger also
  binds rate/model versions and token caps. A repeated attempt cannot dispatch
  again or create another reservation. This is not an idempotent answer-result
  replay API: a finished attempt currently returns a conflict, not a saved answer.
- Only the caller that receives a successful committed dispatch transition may
  invoke the private client. No automatic retry or redirect is configured on
  the answer client. A dispatch write failure prevents the call.
- Timeout, cancellation, malformed/mismatched reply or unknown usage keeps the
  allowance held. Cleanup attempts an `Uncertain` transition with a separate
  three-second deadline. If cleanup also fails, the durable `Dispatched` hold
  remains. Nothing silently releases a possibly charged request.
- Valid synthetic usage settles before citation validation. Therefore an invalid
  citation result is not treated as a free request. Usage is fixed by the fake
  test adapters, not measured from a paid provider. The internal settlement
  reference explicitly starts with `test-usage:`.
- After settlement the coordinator checks current source eligibility again and
  runs the existing ASP.NET structural citation validator. Quarantine during a
  provider call prevents returning the proposal; known synthetic cost remains
  settled. This is a before/after eligibility check, not atomic revocation over
  the whole network call or completed Phase 6 authentication.

## Private transport

The new `/internal/cited/answer-proposal` route requires the existing worker
secret and defaults to 503/disabled for an authenticated caller. Wrong or absent
tokens return 404 before parsing content. The worker secret authenticates the
service boundary; it does not establish a user's organization membership.

Requests are limited to 65,536 actual UTF-8 bytes, with a 15-second Python body
read deadline. Unknown/duplicate JSON properties, invalid UTF-8, altered hashes
and foreign evidence are rejected without reaching the fake provider. The
existing Python answer-envelope component binds its own canonical request,
model and evidence. The bridge additionally returns the hash of the exact .NET
wire bytes, so different valid JSON escaping cannot break identity checks.

Replies carry the proposal as base64 inside JSON. The .NET client bounds the
whole response to 131,072 actual bytes, including streams without a trusted
Content-Length, then bounds the decoded proposal to 65,536 bytes. Required,
unknown and duplicate fields are checked. The coordinator rejects mismatched
model/hash, incomplete outcomes and out-of-bound usage. Private calls use a
30-second client deadline inside a 45-second coordinator deadline; failures
are not retried. No provider SDK or final model prompt is introduced.

The answer client removes default HTTP loggers and uses the existing fixed
operation/status/duration telemetry. Python errors are generic. This does not
close the raw database-log or arbitrary deployment logging gates.

## Verification

- Focused PostgreSQL run: 11 initial coordinator cases passed. The final suite
  also includes added timeout and model/usage/incomplete-response variants.
- Infrastructure run: 61 tests passed, including 9 new answer transport cases.
- Python cited subset: **51 tests passed** across the existing two runtimes:
  15 route tests in `.venv`, and 36 answer/chunk/embedding/question-evaluation
  tests in the native-PDF runtime. Eight route tests are new.
- Full .NET/database regression: **435 passed, zero failures/skips** — 111 Domain,
  102 Application, 61 Infrastructure, 4 Architecture, 78 API and 79 PostgreSQL.
  This adds 24 .NET cases: 15 coordinator cases and 9 transport cases.
- Debug build passed with zero warnings/errors. EF reports no pending model
  changes. Documentation production build passed with a non-blocking bundle-size
  warning; `git diff --check` passed with line-ending normalization notices.

The coordinator tests cover default/live disablement, foreign organization,
inactive actor, quarantined source, six concurrent callers sharing one dispatch,
the committed state visible from a separate connection before provider entry,
real .NET/Python roundtrip, fresh-service replay, safe provider failure,
cancellation, mismatched responses, invalid citations, mid-call quarantine,
dispatch rollback and settlement/cleanup failures. Every returned proposal
remains unpublished. The test-only fake does not evaluate invoice truth.

Raw reports are in `tmp/phase5-test-results/phase5-answer-bridge-*.trx`.
The first Python test invocation omitted `PYTHONPATH` and failed import before
tests ran; setting it to `apps/api` corrected the invocation. No package changes
were made. The existing `.venv` HTTP-client deprecation warning remains; a
single reproducible runtime is still tracked in the roadmap. Full OCR and
frontend/browser acceptance are not claimed as newly rerun.

## Environment and reproduction

Use the existing isolated PostgreSQL cluster on loopback `127.0.0.1:25434` and
the fixture's exact disposable database `document_intelligence_test`. Never
point reset tests at `phase5_e2e_20260908` or another retained database. No new
migration is needed for this increment. The existing synthetic acceptance
database is not changed by these tests.
Final SQL checks confirmed that `phase5_e2e_20260908` remains present,
`document_intelligence_test` was dropped by its fixture, and no other database
client connections remained. The disposable test database is recreated by the
fixture when needed. PostgreSQL is left running; private Python test helpers
were disposed. No persistent web/API/Python server was started for this increment.

```powershell
$env:TEST_POSTGRES_CONNECTION='Host=127.0.0.1;Port=25434;Database=postgres;Username=document_intelligence;Password=local_development_only'
$env:PHASE5_PYTHON=(Resolve-Path .venv/Scripts/python.exe).Path
dotnet test apps/backend/UaeDocumentIntelligence.sln --no-restore --logger 'trx;LogFilePrefix=phase5-answer-bridge-final' --results-directory tmp/phase5-test-results
$env:PYTHONPATH=(Resolve-Path apps/api).Path
& .venv/Scripts/python.exe -m unittest discover -s apps/api/tests -p 'test_cited*routes.py'
```

The native runtime tests use `tmp/phase5-native-runtime-20260908/Scripts/python.exe`
with the individual patterns `test_cited_answers.py`, `test_cited_chunking.py`,
`test_cited_embeddings.py` and `test_cited_question_evaluation.py`.

## Remaining gates / next work

1. Assemble a versioned full model prompt/schema and measure all billable input
   before reservation. This bridge reserves the existing 8,000/4,000 token caps
   for fake calls; a byte bound is not a token count. Bound live provider output,
   including reasoning, and verify authoritative usage/rates before replacing
   synthetic settlement. Add embeddings to the same shared pilot allowance.
2. Exercise actual process termination, lost dispatch/commit responses and
   trusted operational reconciliation of stranded holds. Current fault tests
   inject errors/cancellation and transaction failures; they do not prove OS
   crash recovery or resolve unknown real-provider charges.
3. Add result persistence/replay, semantic support, model injection defenses,
   generated-answer UI and evaluation before public answer publication. A
   correct citation ID does not prove the accompanying claim.
4. Keep credentials, funded access, model lifecycle, provider privacy, roles,
   raw server logs, real-invoice quality and broader acceptance gates open in
   the [roadmap](../../roadmap.md). Phase 5 is not complete.

We continued in the existing task using the repository checkpoint. No new task,
forced compaction or context-monitor automation was created.
