# P5-T06 citation validation and no-hit refusal review

**Date:** 2026-09-07
**Status:** Provider-independent components implemented and verified.
P5-T06 and Phase 5 remain incomplete.

## Delivered

- Strict, versioned validation of internal answer proposals: bounded JSON,
  required fields, duplicate/unknown-property rejection, claim-level citations,
  exact retrieved-source identity checks, and canonical citation reconstruction.
- Explicit separation between structural citation validity and semantic claim
  support. A structurally valid proposal is not persisted or published.
- A provider-free question entry point at `POST /api/cited-questions` and
  read-only history at `GET /api/cited-questions/{retrievalRunId}`.
- A deterministic, persisted `InsufficientEvidence` result when a successful
  retrieval returns zero hits, with reason `no_relevant_evidence`.
- Idempotent refusal replay, concurrent-writer handling, actor/organization
  scoping, and unchanged retrieval-duration history.
- Nonempty hits remain `EvidenceReady`, with `answer: null` and
  `answerGenerationEnabled: false`. There is no model answer endpoint.

See the [validation contract](../../design/phase-5-answer-validation-contract.md)
for exact wire fields, limits, status behavior, and trust boundaries.

## What the tests establish

37 new application tests cover accepted canonical UTF-8 evidence, malformed or
oversized proposals, duplicate JSON fields at multiple levels, uncited/duplicate
claims, fabricated source fields, unknown/swapped IDs, foreign and nonretrieved
chunks, explicit partial-answer gaps, no-hit reason consistency, and original
page evidence versus corrected review values.

One test deliberately attaches a real citation to a false factual claim.
Structural validation succeeds but returns `semanticSupportEstablished: false`;
no answer is saved. This is a tested boundary, not a claim that citation IDs
alone prevent hallucination.

Four new API tests verify input bounds on the question route and confirm that
there is no public endpoint accepting an answer proposal. Success-path workflow
and persistence behavior are exercised through real application services and
PostgreSQL, with an actual private Python HTTP planner in the fresh-request test.
This is not a browser acceptance test.

The new persistence cases cover:

- Fresh questions through the private planner and actual PostgreSQL retrieval:
  no hits become a refusal; positive hits remain evidence, not an answer.
- Racing completions of one empty Retrieved run: one saved answer ID, stable
  replay, unchanged retrieval duration, no claims, no provider call, and no
  cross-actor/organization history access. GET does not finalize a pending result.
- Structural citation validation against stored hit/span records, without
  completing the run or manufacturing an answer.

## Verification results

- Full non-persistence .NET regression: 274 passed — 111 domain,
  88 application, 42 infrastructure, 4 architecture, and 29 API.
- Full PostgreSQL regression: 43 passed, including all three new question/
  refusal/validation cases, existing live private HTTP retrieval, and all nine
  migrations with rollback/reapplication. Total .NET tests: **317 passed**.
- The initial 2 targeted PostgreSQL passes are reruns within the full suite,
  not extra tests added to the total.
- .NET Debug build: zero warnings/errors.
- No pending EF model changes; no schema migration was needed.
- Documentation build and scoped whitespace checks passed.
- No Python, OCR, or web source changes in this checkpoint. Their complete
  suites and browser/UI acceptance were not rerun and remain Phase 5 gates.

TRX results are retained in `tmp/phase5-test-results/phase5-validator*.trx`.
The existing isolated PostgreSQL 16 cluster on localhost:55434 is used only
with the designated disposable `document_intelligence_test` database. No
application database is migrated. The live test launches and cleans up its own
local private Python service using the existing `.venv` runtime.
The temporary database server was stopped after verification.

## Important remaining limitations

1. **A real citation is not proof of a supported answer.** Relevance, entailment,
   completeness, conflict handling, prompt-injection defenses, and reliable
   refusal with nonempty-but-insufficient hits still need implementation and
   evaluation. There is no free-form answer generation enabled to bypass this.
2. **A no-hit refusal is about this retrieval attempt.** It does not prove the
   requested fact is absent from the invoice. The existing “seller/TRN”
   paraphrase miss can still produce a refusal despite evidence existing.
3. **Partial-answer gaps are validated, not yet persisted.** The internal
   proposal preserves explicit unanswered parts, but publishing partial/model
   answers remains deferred until their full supported-output contract is ready.
4. **The API is a development evidence/refusal entry point.** It reuses pinned
   index IDs; completed-review/reconciliation-case request resolution, an
   interactive question workspace, and authorized citation navigation remain
   outstanding. Fixed development identity is not production authentication.
5. **Retrieval comparison remains open.** No embeddings, cosine-search backend,
   vector/hybrid comparison, or new real-world accuracy claim is added here.
6. **Crash recovery is limited.** A repeated question POST can complete a
   zero-hit Retrieved record. It cannot recover a crashed Running retrieval;
   durable retry/stale-run recovery remains separate work.

## API budget and what Rand needs to do

No paid API requests were made: this checkpoint spent **USD 0**. Rand's USD 10
starting test budget is recorded, but no API billing balance/key, model choice,
or spend cutoff has been configured or verified. Credentials must not be pasted
into the task or committed to the repository.

No dataset replacement or API setup was needed for this work. Before paid model
comparison, provide a concrete model/cost/privacy comparison, configure API
credentials securely and application-side budget enforcement, then run bounded
evaluations. Approval of the test budget does not complete those prerequisites.

## Reproduce

After existing project setup, select the isolated test database and a Python
runtime with the private API dependencies. Do not run the persistence fixture
against a server containing a valuable database named `document_intelligence_test`.

```powershell
$env:PHASE5_PYTHON = (Resolve-Path .venv/Scripts/python.exe).Path
dotnet build apps/backend/UaeDocumentIntelligence.sln --no-restore
dotnet test apps/backend/UaeDocumentIntelligence.sln --no-build --no-restore --filter 'FullyQualifiedName!~Persistence.Tests'
# Set TEST_POSTGRES_CONNECTION to the isolated local test server before this command.
dotnet test apps/backend/tests/UaeDocumentIntelligence.Persistence.Tests --no-build --no-restore
dotnet ef migrations has-pending-model-changes --no-build --project apps/backend/src/UaeDocumentIntelligence.Infrastructure --startup-project apps/backend/src/UaeDocumentIntelligence.Api
```
