# P5-T06/T08 — Versioned prompt preflight

**Date:** 2026-09-09  
**Scope:** Complete application-prompt assembly and a two-stage private preparation/
dispatch contract. Counting and usage are synthetic; no paid model, live token
counter, provider SDK, public answer publication or schema migration is enabled.

## Outcome

Python now assembles a versioned, immutable application prompt containing the
instructions, question, all selected evidence and citation IDs, response schema,
model identity and output limit. The backend obtains a bounded preparation
receipt before reserving spending. The receipt's input count replaces the fixed
8,000-input-token reservation used by the earlier test-only bridge.

The complete dispatch envelope, including original source bytes and preparation
receipt, is hashed into the durable reservation. A changed count, prompt, model
or counter version cannot reuse that operation. Python reconstructs and measures
the prompt at dispatch, compares the receipt, then passes that exact immutable
packet to the fake adapter without rebuilding it after measurement.

**Important limitation:** the only installed counter is in test code and counts
UTF-8 bytes as synthetic units. It is explicitly named `test-utf8-bytes-v1`.
These are not GPT tokens, not an estimate advertised as exact tokenization and
not real provider billing. Production counting and generation both default off;
non-test providers are rejected before preflight. This proves the reservation
plumbing and binding checks, not the amount a live provider will charge.

## Why the live counting gate remains open

OpenAI Docs notes that local plain-text tokenization does not fully capture
schemas and model-specific behavior. Its input-token endpoint accepts a model,
instructions and input for provider-side counting. This guided the separation
between a complete application packet and a future exact provider request.
No counting endpoint was called here. [Official token-counting guidance](https://developers.openai.com/api/docs/guides/token-counting).

The response schema closes each object and requires its fields, while the
existing ASP.NET validator still checks status semantics, citations and source
ownership. A structured response or correct citation ID is not semantic proof;
provider refusals remain distinct from a supported insufficient-evidence answer.
[Official structured-output guidance](https://developers.openai.com/api/docs/guides/structured-outputs).

Before live work, the chosen adapter must map the packet to the exact approved
provider request and count all instructions, schema and provider framing. It
must preserve those counted inputs through dispatch, bound billable output
including reasoning, and verify authoritative usage and applicable rates.
Provider counting access, privacy, any cost and repeated-preflight behavior must
also be verified before enabling external counting. No local heuristic silently
replaces an unavailable approved counter.

## Implemented boundaries

- `cited-answer-prompt-v1` separates fixed instructions from untrusted evidence
  and the question. It prohibits external tools/payment actions, invented facts
  and citation IDs, and requires explicit insufficiency for unsupported claims.
  These are prompt-design controls, not a passed model-injection evaluation.
- The prompt packet contains a closed response schema and explicit no-tools,
  no-storage and no-truncation intent. These are application settings in a
  provider-neutral packet, not verified provider endpoint options or a guarantee
  about external retention. Overlarge evidence/prompt packets are rejected;
  selected evidence is never silently shortened.
- An active actor check occurs before private preflight receives evidence.
  Current source eligibility is checked again after preflight, before admission,
  and after generation as in the earlier bridge. This is not atomic revocation
  through a network call or completed Phase 6 login/RBAC.
- `/internal/cited/answer-prepare` requires the worker secret and a configured
  test counter. Its source request is bounded to 65,536 actual bytes; the .NET
  client allows only a 4,096-byte receipt. Preparation has no generation call
  and makes no spending reservation.
- Receipt fields bind source hash, prompt hash/version, model, counter version,
  input count and output maximum. Unknown versions, bad hashes and counts outside
  1–8,000 fail before reservation. The backend accepts only the explicit synthetic
  counter version for this increment.
- `/internal/cited/answer-proposal` now requires base64 original request bytes
  plus the receipt, bounded to 98,304 actual transport bytes. Python compares a
  freshly prepared receipt, rejecting missing/tampered/stale receipts before
  the adapter. Duplicate properties and booleans masquerading as integer counts
  are rejected. Responses also bind the prepared prompt hash.
- The counter has a ten-second deadline; existing transport/coordinator deadlines
  remain. Counter failure, timeout or caller cancellation cannot silently trigger
  generation or a guessed reservation. Post-dispatch uncertainty still holds the
  allowance; actual input reported above its reservation is not settled as safe.
- Reservation persists the measured input maximum and the dispatch-envelope
  hash. Prompt/counter version and receipt content are cryptographically bound
  by that hash; they are not new separately queryable database columns. Durable
  readable generation metadata/result replay remains part of future answer
  persistence, not something this hash alone can reconstruct.

## Verification

- All **362 non-persistence .NET tests passed**: 111 Domain, 102 Application,
  67 Infrastructure, 4 Architecture and 78 API. Six new infrastructure cases
  cover the preparation endpoint, receipt parsing and transport limits.
- Initial focused PostgreSQL run: **28 passed**, covering 15 existing bridge
  cases and 13 new preflight cases. The final combined ledger/bridge/preflight
  run passed **40 cases, zero failures/skips**, including the 12 existing ledger
  cases. With the non-persistence suite, this is 402 unique .NET cases rerun.
- All **66 Python cited tests passed** across the two existing runtimes:
  20 route tests in `.venv`; 12 answer, 10 prompt, 10 chunking, 6 embedding and
  8 question-evaluation cases in the native-PDF runtime. Fifteen are new.
- Debug solution build passed with zero warnings/errors; EF reports no pending
  model changes. Documentation build and diff checks passed, with non-blocking
  bundle-size and line-ending notices respectively. No full PostgreSQL, OCR or frontend/browser regression is claimed
  for this increment; the prior 435-test full .NET run remains historical.

Tests check complete-packet coverage, immutable dispatch, instructions/schema
hash changes, Unicode bytes, no truncation, invalid count types and bounds,
counter timeout/cancellation, stale receipts, source quarantine during preflight,
changed-measurement replay conflicts and a real .NET/Python roundtrip. A separate
connection sees the committed measured reservation before the fake call.
The smaller reservation is also tested near the shared USD 8 ceiling.

An intermediate build ran before the new test callback was used and failed its
unused-field warning-as-error; completing the callback tests resolved it. No
warning policy or dependency version was relaxed. The existing Python HTTP-client
deprecation warning remains, and the one-runtime reproducibility gate is open.

Raw reports: `tmp/phase5-test-results/phase5-prompt-preflight-nonpg*.trx`,
`phase5-prompt-preflight-focused.trx`, `phase5-prompt-preflight-infrastructure.trx`
and `phase5-prompt-preflight-db-final.trx`. Count unique test cases rather than
adding repeated focused runs to the final result. The non-persistence command
emits an empty persistence report because that project is explicitly filtered out.

## Environment and reproduction

The existing loopback PostgreSQL cluster on :25434 is used only through the exact
disposable `document_intelligence_test` fixture. The retained synthetic
`phase5_e2e_20260908` database is not migrated/reset. Python test helpers are
temporary; no persistent application service or provider credential is needed.
Final SQL checks confirmed the retained acceptance database exists, the fixture
dropped only its disposable test database, and no other database clients remained.
The disposable database is recreated by the test fixture. PostgreSQL remains
running on loopback :25434; existing synthetic invoice evidence is preserved.

```powershell
dotnet test apps/backend/UaeDocumentIntelligence.sln --no-restore --filter 'FullyQualifiedName!~UaeDocumentIntelligence.Persistence.Tests' --logger 'trx;LogFilePrefix=phase5-prompt-preflight-nonpg' --results-directory tmp/phase5-test-results
$env:TEST_POSTGRES_CONNECTION='Host=127.0.0.1;Port=25434;Database=postgres;Username=document_intelligence;Password=local_development_only'
$env:PHASE5_PYTHON=(Resolve-Path .venv/Scripts/python.exe).Path
dotnet test apps/backend/tests/UaeDocumentIntelligence.Persistence.Tests --no-restore --filter 'FullyQualifiedName~Prompt_preflight|FullyQualifiedName~Generation_|FullyQualifiedName~Ledger_' --logger 'trx;LogFileName=phase5-prompt-preflight-db-final.trx' --results-directory tmp/phase5-test-results
```

For Python, set `PYTHONPATH` to `apps/api`, run `test_cited*routes.py` in `.venv`,
and run the five non-route component patterns listed above in the native-PDF
runtime. No dependency installation is necessary for this checkpoint.

## Next work and remaining gates

Follow-up: [application-process crash/restart verification](P5-T08-crash-recovery-verification.md)
records the next fault-injection increment and its narrower guarantees. A trusted
recovery workflow for stranded reservations still needs implementation and
verification. This remains possible without a paid API key.
Then complete provider-specific counting/usage integration before a live pilot.
Embedding accounting, result persistence/replay, semantic support, model-injection
evaluation and generated-answer/UI acceptance remain open. Raw-log privacy,
roles, external data controls and real-invoice accuracy also remain tracked in
the [roadmap](../../roadmap.md). No Phase 5 completion is implied.
