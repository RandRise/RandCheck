# Phase 5 — First live synthetic generation

**Date:** 2026-09-09  
**Scope:** Private GPT-5 nano pilot using generated English UAE invoice evidence.
Public answer generation remains disabled. This is not Phase 5 acceptance.

**Outcome:** The v2 real-model answer passed structural citation validation and
the one-case source check. Its saved result replayed with the private service
offline. Two generation requests and two token-count requests occurred; the
canonical ledger settled **USD 0.000203** in total generation token-rate estimates.
No pending hold or reconciliation freeze remains. The original v1 failure is
preserved and still rejected.

## Integration now exercised

The standalone synthetic runner calls the existing ASP.NET coordinator and
canonical PostgreSQL ledger. Offline preparation freezes source, prompt, schema
and provider-wire hashes. Its `inputTokens=8000` is explicitly a reservation
ceiling (`openai-count-after-reservation-v1`), not a measured count. A USD 0.002
hold covers that input ceiling and 4,000 output tokens. Only after reservation
and one-winner dispatch commit does the private Python app count the exact
request once, then generate once if the count is valid and within the ceiling.

Provider identities, raw bounded count/response bytes, usage and the unpublished
proposal are saved through the immutable generation journal. Known usage settles
even when answer validation fails. Unknown usage retains its hold and freezes
admission. No redirects, automatic retries, provider fallback or public generation
route were added. The private service trusts the local worker boundary; this is
not production authentication or a defense against a compromised worker.

Only the explicit runner enables the live profile. Merely setting an API key does
not enable the default application. The launcher reads the ignored root `.env`
in memory, passes the key only to the ephemeral Python process, and omits child
diagnostics. The .NET process receives no API key. Child services are stopped on
exit. The model remains `gpt-5-nano-2025-08-07`, minimal reasoning, standard tier,
no tools, `store:false`, disabled truncation and bounded input/output/response sizes.
`store:false` is not a verified zero-retention or UAE-residency guarantee.

## Source preparation and first failure

An initial launch found that the older synthetic index was quarantined and
stopped with `evidence_unavailable` before preparation, reservation or provider
requests. The quarantine was not removed. A fresh copy of the existing synthetic
native PDF passed the real upload → local extraction → scripted review → indexing
→ scoped retrieval workflow: **20 API assertions passed**. Scripted confirmations
are fixture setup, not a human field-accuracy review.

Source report: `outputs/phase-5/live-pilot-20260909/source-preparation.json`.

- File: `fta-standard-services-native.pdf`, development pack 0.2.0.
- SHA-256: `6d629a9701b4b8cdd38d43c8449bb6fb9158b1e4f653ae8257d35715be742ca5`.
- Document: `107d0304-1c58-489f-9c80-31e1f62481d7`.
- Completed review: `84f201ea-6eac-471a-bee3-141d28fbe77f`.
- Ready index: `704af047-040c-4274-b1f2-052a97c61e7f`.
- First retrieval: `d299b50e-8ef7-423d-9942-e40de003ea5c`.
- Question: “What amount is due on the invoice?” Three lexical hits, topK 5.

The first actual model response was “Amount due (AED): 8,820.00”. The amount
matches the synthetic page. However, it paired page hit
`59b57b43-0ed6-451b-b9e3-f90a1ead3109` with invoice-number span
`e78f490b-5c15-0531-c657-ab62dfaca976`, which belongs to a different chunk. The
correct page span is `7ab44a21-7e0a-373d-c2ae-8ebe648d8ef0`.
The authoritative ASP.NET citation validator correctly rejected the proposal.
This is a real model failure, not a passing citation-quality test.

Reservation `57650416-091a-43a8-bc21-60997db45bb3` settled **USD 0.000094**:
901 measured/used input tokens, 120 output tokens, zero cached/reasoning tokens.
Exact token-rate arithmetic is USD 0.00009305, rounded up to microdollars.
Provider request: `req_14f3d303082c4d72afeddb37ebdecc63`.
Response: `resp_096dbfa12fc6f25d016aa1656b4c6487d2aa60f72827ede054`.
Response SHA-256: `a28152cb23e7babc8f57be2c74b652663bc93c4f16c69206f296ac550c8aebbe`.
The original outcome remains immutable. An offline saved-result replay after the
prompt update again rejected it, without contacting a private service or charging
again. No answer was published.

## Citation-pair correction

Prompt `cited-answer-prompt-v2` constrains citations to per-hit alternatives:
each permitted hit has only its own span IDs; null is permitted only when that
evidence item has no spans. It rejects excessive enum lists before dispatch,
without truncating evidence. The outer response contract is unchanged. The
existing ASP.NET provenance/citation validator was not weakened. Valid pair
selection still does not prove that the cited text supports a claim.

This uses nested `anyOf` and enums supported by the
[official Structured Outputs guide](https://developers.openai.com/api/docs/guides/structured-outputs).
OpenAI Docs guided the schema choice; no model upgrade was made. New preparations
require v2. Settled historical v1 outcomes remain replayable locally, subject to
current source authorization and the unchanged validator; they are not regenerated.

A separate versioned evaluation retrieval, `94b3d1a9-e377-41b2-83c5-6fc960e71563`,
uses the same Ready index, question, topK and three hits. Its stable request key
and source result are saved in
`outputs/phase-5/live-pilot-20260909/citation-v2-source.json`. This is an explicit
follow-up experiment, not a retry or replacement of the failed v1 operation.

## Successful v2 follow-up

The model returned “The amount due on the invoice is 8,820.00 AED.” It cited hit
`193cb1d2-4cb5-4fe4-b9ec-16b71257e9b1` and page span
`7ab44a21-7e0a-373d-c2ae-8ebe648d8ef0`, belonging to page chunk
`652030ca-5c13-2bd7-bffd-3fc38ec6664a`. The validated original page-1 quote
explicitly includes `Amount due (AED):8,820.00`, matching the expected source
assertion made before either model request. This one-case inspection is not an
automated semantic-support validator: `SemanticSupportEstablished=false` and
`PublicationAllowed=false` remain unchanged.

Reservation `fb0b0f4b-442a-4f6e-b5cd-91533d632b7e` settled **USD 0.000109** from
1,123 measured/used input and 130 output tokens, zero cached/reasoning tokens.
Exact token-rate arithmetic is USD 0.00010815. Generation request:
`req_f905f8e88daf4dd598a196d9711798c4`; count request:
`req_86c23163322e4dc8acb86f3f46dd391a`; response:
`resp_00440c66bdd89cfe016aa1694b01d487d2af17c85ad0ba1866`.
Response SHA-256: `5dd0e4f314890033b8e7d8ca8fbc63eaea1ec74cbf0b9e0da32b2a5455417394`.

With the private service stopped, a fresh .NET process returned the saved
validated proposal with `Replayed=true`, making no provider request. Report:
`outputs/phase-5/live-pilot-20260909/citation-v2-offline-replay.json`.
`ledger-after-replay.json` records both prompt versions, outcomes, usage,
identities and ledger balances after replay. Exactly two settled reservations,
four artifacts, USD 0.000203 settled and no freeze remained. This total sums
each individually rounded estimate; it is not an account invoice.

## Verification status

- 115 Python cited tests passed: 107 in `.venv`, eight PDF-audit tests in the
  existing native-PDF environment. The initial single-environment run failed
  three PDF-audit checks because `.venv` lacks pdfplumber; that environment gap
  remains open. Existing Starlette/httpx deprecation warning remains.
- 413 non-PostgreSQL backend tests passed: 111 Domain, 153 Application,
  67 Infrastructure, four Architecture and 78 API, zero failures/skips.
- Earlier focused live integration passed eight PostgreSQL cases using a real
  private HTTP hop and a mocked provider: valid/refused/incomplete/invalid answers,
  unknown usage, HTTP failure, overrun, explicit enablement and scope denial.
- Final affected PostgreSQL regression passed **55 tests**, zero failures/skips,
  including those eight live-profile mock cases, journal/coordinator/preflight
  and seven separate-process crash scenarios. Together with the non-PG suite,
  this is **468 unique backend tests**, not a full PostgreSQL regression. The
  earlier eight-case run is not counted again. TRX reports:
  `tmp/phase5-test-results/phase5-live-final-*.trx`.
- Solution and runner builds passed, zero warnings/errors. EF reports no pending
  model changes. No new migration is required. Documentation build and diff checks
  passed with existing bundle-size/line-ending notices. No browser, complete OCR,
  full Python-suite or full PostgreSQL rerun is claimed for this increment.

## Accounting and remaining gates

The retained database is `phase5_e2e_20260908` at loopback port 25434, migration 12.
The shared admission ceiling remains USD 8 within the USD 10 pilot. No database,
ledger, reservation or historical source was reset to perform these experiments.
The auto-resetting `document_intelligence_test` fixture is separate and never
stores paid pilot activity.

Final retained counts: four documents, seven retrieval runs, two indexes,
18 chunks, four generation artifacts and three pre-existing/no-hit answer rows.
Neither paid proposal was published into answer history. Temporary pilot/source
services were stopped; the isolated PostgreSQL test database was removed by its
fixture. The retained PostgreSQL service stays available.

Cost figures are provider-reported generation usage multiplied by the pinned
published token rates, not an independently reconciled account bill. No claim
is made that token-count requests are free, that remaining account credit was
verified, or that all provider retention/residency controls are satisfied. The
rate window expires 2026-09-10 00:00 UTC; reverify and version before later paid
admission. References: [nano model](https://developers.openai.com/api/docs/models/gpt-5-nano),
[Responses API](https://developers.openai.com/api/reference/typescript/resources/responses/methods/create),
[input-token count](https://developers.openai.com/api/reference/typescript/resources/responses/subresources/input_tokens/methods/count).

Next is a bounded generation-quality evaluation of answerable, unanswerable,
conflicting and injection-bearing evidence against independent expected labels.
Then finish the lexical/vector/hybrid comparison, semantic-support/public-answer
gates and broader Phase 5 acceptance. A single synthetic question does not prove
answer accuracy, real-invoice robustness, FTA compliance, production tenant roles,
privacy eligibility, operational recovery or billing reconciliation. Real customer
data remains out of scope until the Phase 6/8 controls are verified.
