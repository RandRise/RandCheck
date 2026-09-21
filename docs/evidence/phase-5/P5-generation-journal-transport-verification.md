# Phase 5 — Saved generation results and bounded transport

**Date:** 2026-09-09  
**Outcome:** Durable preparation/outcome storage and synthetic result replay are
implemented. A separately tested OpenAI HTTP transport is present but unregistered.
No paid generation, counting or embedding request was made in this increment.

## What changed

Migration `20260909123847_AddCitedGenerationArtifacts` (migration 12) adds
`cited_generation_artifacts`. Each reservation can have one preparation and one
outcome. A composite foreign key binds organization, reservation and actor.
The records preserve exact bounded JSON bytes, hashes and command fingerprints.
EF and PostgreSQL row UPDATE/DELETE guards reject edits. These are not encryption,
authentication, privileged-owner tamper protection or a retention/deletion policy.

The internal `ICitedGenerationJournal` is implemented by the existing canonical
ledger, not a second budget store. Preparation and reservation commit together.
Known-cost outcome, spend event, reservation state and budget update commit
together. Unknown usage retains its hold; overruns and explicit reconciliation
flags freeze further admission. Identical outcome writes are idempotent; changed
payload/accounting commands are rejected. The active actor is held with a shared
row lock through the transaction, alongside the existing shared-budget lock.

The test-only coordinator now saves its dispatch envelope and unpublished result.
After restart it can return a settled saved result without measuring the prompt
or invoking the adapter again. It rechecks current actor ownership, source status,
evidence graph and request bytes. Quarantined evidence blocks replay. Reserved
work can resume from saved preparation; dispatched or uncertain work cannot be
redispatched. Legacy settlements without saved outcomes remain non-replayable.
No result is published and public answer behavior remains unchanged.

`cited_openai_transport.py` sends only frozen requests constructed by the existing
protocol builder. It uses a fixed HTTPS OpenAI destination, TLS verification,
bounded raw responses, total deadlines, no redirects and no automatic retries.
It does not read environment variables or key files. Errors omit credentials,
request content and provider error bodies. Cancellation and ambiguous failures
are not treated as proof of zero cost.

`httpx==0.28.1` is now a pinned runtime dependency in `pyproject.toml`; that version
already exists in `.venv`. No package was installed. The separate native-PDF
runtime lacks httpx and is not yet the unified runtime required for release.
The transport is not imported or registered by the default application routes.

## Verification

The initial focused PostgreSQL run passed **47 tests**, zero failures/skips:
journal, coordinator, prompt-preflight and process-crash scenarios. Coverage
includes atomic rollback on artifact insert failure, duplicate settlement,
unknown usage, overruns, scoped reads/writes, database immutability and saved
result replay. The final suite also checks artifact counts around every process
crash and direct cross-organization foreign-key rejection.

**102 Python cited tests passed**, zero failures/skips: 70 component, 20 private
route and 12 new transport cases across the two existing runtimes. All transport
requests use an in-memory HTTP mock and fake credentials, not OpenAI. Tests cover
fixed URLs and bytes, disabled defaults, mutation rejection, redirects, HTTP
errors, timeouts, cancellation, response bounds and sanitized diagnostics.

Full backend regression passed **541 tests**, zero failures/skips: 111 Domain,
143 Application, 67 Infrastructure, 4 Architecture, 78 API and 138 PostgreSQL.
This includes all 12 new journal cases and the updated process-crash assertions.
The earlier 47-case run and repeated Domain verification are not counted twice.
Final Debug build passed with zero warnings/errors; EF reports no pending model
changes. Documentation build and diff checks passed with the existing bundle-size
and line-ending notices.

Reports are in `tmp/phase5-test-results`: `phase5-generation-journal-full-*.trx`
retain Application, Infrastructure, Architecture and PostgreSQL results;
`phase5-generation-journal-full.trx` retains the final API result;
`phase5-generation-journal-domain-final.trx` retains the repeated Domain result.
The initial solution command used a shared report filename; per-assembly copies
were preserved as it ran. Use the unique prefix in the reproduction command below.

Reproduction:

```powershell
$env:TEST_POSTGRES_CONNECTION='Host=127.0.0.1;Port=25434;Database=postgres;Username=document_intelligence;Password=local_development_only'
$env:PHASE5_PYTHON=(Resolve-Path .venv/Scripts/python.exe).Path
dotnet test apps/backend/UaeDocumentIntelligence.sln --no-restore --logger 'trx;LogFilePrefix=phase5-generation-journal' --results-directory tmp/phase5-test-results
$env:PYTHONPATH=(Join-Path (Get-Location) 'apps/api')+';'+(Join-Path (Get-Location) 'apps/api/tests')
& tmp/phase5-native-runtime-20260908/Scripts/python.exe -m unittest test_cited_openai_protocol test_cited_answers test_cited_prompt test_cited_chunking test_cited_embeddings test_cited_question_evaluation
& .venv/Scripts/python.exe -m unittest discover -s apps/api/tests -p 'test_cited*routes.py'
& .venv/Scripts/python.exe -m unittest test_cited_openai_transport
```

Only disposable `document_intelligence_test` is reset/dropped by the tests.
A backup of retained `phase5_e2e_20260908` was created before the upgrade.
After the full regression passed, it was upgraded from migration 9 to 12 without
resetting it. Before/after counts
match: 3 documents, 4 retrieval runs (2 Retrieved and 2 Completed), 9 chunks and
1 organization. Its new canonical budget is USD 8 with USD 0 settled, no pending
reservations/artifacts and no reconciliation freeze. This is a local ledger
state, not evidence of OpenAI account credit or provider billing.

Backup: `tmp/phase5-e2e-before-journal-20260909.dump`, archive listing verified;
SHA-256 `4BA5F464B4915FEE8081B0F246BF81E39B78B80219216BCCF92559A53324CCD2`.
A complete restore drill is not claimed. Final read-only checks confirmed the
disposable fixture was removed, retained data remained, and no other database
sessions were active. PostgreSQL remains available on loopback port 25434.

No fresh OCR/frontend/browser acceptance or lint pass is claimed. The existing Starlette/httpx
deprecation notice remains; Ruff is absent in `.venv`.

## Exact remaining work before the paid smoke test

The coordinator still explicitly rejects live providers. The registered Python
counter and answer adapter remain disabled. A transport constructor flag is not
a ledger permit, and accepted parser data is not authoritative billing evidence.

Connect a private, explicitly enabled OpenAI workflow that:

1. Binds the actual count, frozen wire request and provider request identity to
   the saved canonical operation; does not repeat counting during generation.
2. Sends generation only after the canonical one-winner dispatch commit.
3. Records provider-reported usage and request/response identity independently
   of whether the answer is valid, refused or incomplete; preserves uncertain
   holds when accounting or commit results are unknown.
4. Reuses saved results and current access/source checks without another paid call.
5. Runs one authorized synthetic smoke case, then evaluates the generated English
   invoice pack under the shared USD 8 admission ceiling within the USD 10 budget.

Use the now-migrated retained database for paid work, never the auto-resetting test
fixture. Do not recreate the budget or use a second database to evade existing
spend or uncertain holds. Keep the next increment focused on this connection;
production roles, real-data rollout and embedding comparison remain separate work.

The initial rate window ends 2026-09-10 00:00 UTC; reverify/version it before later
quoting. Funded quota, actual Responses access, count-endpoint cost/privacy and
provider billing reconciliation remain unverified. The user has already supplied
the key and authorized this synthetic pilot; no additional product decision is
needed from them. Do not bypass the ledger, relabel paid calls as test-only, clear
holds, silently upgrade the model or enable public generated answers.

Documentation check: the official
[token-counting guide](https://developers.openai.com/api/docs/guides/token-counting)
describes full input counts, including structural formatting, and notes that
output usage includes non-visible tokens. Neither that guide nor the count
reference explicitly establishes a zero price or count-specific retention
guarantee in the material inspected. The
[data-controls page](https://developers.openai.com/api/docs/guides/your-data)
does not separately list the counting endpoint. Do not interpret that absence
as proof of free processing or zero retention; this remains synthetic-only.

See the [protocol review](P5-openai-protocol-verification.md),
[pilot proposal](../../design/phase-5-model-pilot-proposal.md) and
[remaining release gates](../../roadmap.md).
