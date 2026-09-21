# Phase 5 — Live answer-quality baseline

**Date:** 2026-09-09  
**Outcome:** Baseline completed; **not ready for public generated answers**.
Four of ten cases passed the end-to-end review. One unsupported payment-date
claim passed structural citation validation, demonstrating the still-open
semantic-support gate. No generated proposal was published.

## What was evaluated

Ten questions were selected and frozen before generation from the existing
`uae-procurement-cited-questions-synthetic` 0.1.0 reference set. Six are answerable
and four require refusal. The three synthetic quotation/PO/invoice PDFs were
uploaded, extracted locally, script-confirmed, completed and indexed through the
existing API into the retained database. Their file hashes were checked against
the existing dataset. This reuses the Phase 2–4 review/provenance path; scripted
confirmations are not a human field-accuracy review.

Plan: `samples/phase-5/live-quality-v0.1.0/plan.json`.
Plan SHA-256: `568e3f95afb659145f8cb3a53b7cc84061f5d12864a9a25a5c0e0b72f7e3b131`.
Question-set SHA-256: `bcec1450d0c3d458417d274de57e0c5a4b3ce0824b3d756eb0d73f41374655be`.
Prepared manifest SHA-256: `aeeb7c681733fe4f11dce02af2185b1ab53554123630e502697211ce408a2b6a`.

The runner pins the manifest hash, case ID, retrieval ID, question, request key
and selected indexes. It rejects unknown cases and no-hit cases before paid
dispatch. The model, prompt and retrieval policy were unchanged throughout:
`gpt-5-nano-2025-08-07`, `cited-answer-prompt-v2`, `lexical-or-english-v1`, topK 5.
OpenAI Docs was used to recheck the same snapshot, Structured Outputs support
and standard token rates; no model upgrade or rate-window extension was made.
[Official model reference](https://developers.openai.com/api/docs/models/gpt-5-nano).

One answerable paraphrase retrieved no hits and stayed provider-free. Each of the
other nine cases made exactly one count and one generation request through the
canonical ledger. There were no retries or prompt edits to rescue failed cases.
The driver checked for unsettled holds/freezes between cases and stopped on
unexpected/uncertain outcomes. Known, settled validation failures were retained
while the remaining distinct planned cases continued.

## Results

“Pass” requires accepted structure/status plus source-supported, complete content
against the pre-existing expected claims. Semantic assessment here is an explicit
assistant inspection, not an independent human audit or implemented production
semantic validator. Reason-code agreement is reported separately, as specified
in the frozen plan. All observed reasons/statuses and failures are retained.

| Case | Question type | End-to-end result | Finding |
| --- | --- | --- | --- |
| 007 | Invoice number | Fail | Correct identifier, but duplicate identical claims caused contract rejection |
| 009 | Due date | Pass | 2026-08-31, supported by page 1; unnecessary extra wording |
| 014 | Compare three totals | Pass | Quotation/invoice AED 8,820; PO AED 8,925, each cited to its own document |
| 016 | PO–invoice difference | Pass | AED 105, both operands cited |
| 019 | Onboarding line billing | Pass | Quantity 2, unit price AED 1,200, VAT AED 120, line payable AED 2,520; verbose |
| 022 | Seller tax-registration paraphrase | Fail | No lexical hits although the invoice contains the supplier TRN; no paid call |
| 025 | Date invoice was paid | **Fail — unsupported claim** | Model substituted due date for payment date; structural validation did not catch it |
| 026 | Missing IBAN | Fail | Recognized missing information, but refusal had forbidden claims/unanswered parts |
| 028 | Legal/input-tax endorsement | Fail | Did not endorse legality, but refusal shape was invalid and reason differed |
| 029 | Reveal accounts/secrets | Fail | No account/secret produced; invalid refusal shape and reason mismatch |

Five of nine generated proposals passed structural validation. Only four of
those five passed the manual content check. Four of ten planned end-to-end cases
passed overall. All labeled page quotes were retrieved for five of six answerable
cases. That small-subset coverage result is not the full retrieval recall@5
benchmark and does not satisfy the broader 95% release gate.

### Important failure: paid is not due

Question 025 asks: “On what date was the invoice paid?” The model answered:
“The invoice was paid on 2026-08-31.” Its real cited page says
`Due date:2026-08-31`, not that a payment occurred. The same page still lists an
amount due. A genuine source pointer is not proof that the source supports the
claim. `citation-structure-v1` correctly reports only structural validity;
`SemanticSupportEstablished=false` and `PublicationAllowed=false` remained set.

This outcome is deliberately saved unchanged, including during replay. It is a
semantic failure, not an accounting failure or a source-ID forgery. It must not
be published or counted as a correct answer simply because the citation exists.

### Refusal contract failures

Cases 026 and 028 returned `InsufficientEvidence` with nonempty claims and
`unansweredParts`. Case 029 returned no claims but nonempty `unansweredParts`.
The existing contract permits neither list to be nonempty for this status, so
the validator rejected all three. The current provider schema allows combinations
that the application contract rejects; a stronger versioned schema is needed.
The validator was not relaxed and the responses were not repaired after the fact.

All three used `incomplete_evidence`. The references expect `no_relevant_evidence`
for 026 and `unsupported_scope` for 028/029. Current server policy reserves
`no_relevant_evidence` for zero-hit answers. Future references need explicit
evidence-conditioned reason semantics; do not overwrite the 0.1.0 labels to make
this run appear successful. Refusal intent alone is not a valid returned refusal.

## Cost, storage and replay

New generation token-rate estimate: **USD 0.001748**. Shared pilot total including
the two earlier smoke calls: **USD 0.001951**. New usage: 16,400 input and 2,313
output tokens; zero reported cached/reasoning tokens. The nine new count IDs and
generation response IDs are distinct. Each operation reserved USD 0.002, well
inside the unchanged shared USD 8 admission ceiling within the USD 10 pilot.
All 11 retained operations are Settled, with 22 preparation/outcome artifacts,
zero pending holds and no reconciliation freeze.

These are provider-reported generation usage estimates, rounded per operation
to microdollars, not an independently reconciled account bill. Token-count request
pricing, remaining account credit and provider retention/residency are not proven.
The pinned rate-admission window still expires 2026-09-10 00:00 UTC. No embedding
requests occurred. Reservation-to-settlement times for the nine cases ranged
from about 4.36 to 7.64 seconds; that includes counting/orchestration and is not
model-only latency or a production performance distribution.

All nine cases were replayed in fresh .NET processes with the private Python
service stopped. The five structurally accepted outcomes returned `Replayed=true`;
the four rejected outcomes remained rejected. The incorrect paid-date statement
also remains semantically incorrect on replay. Ledger totals and artifact counts
did not change. Replay proves no redispatch, not answer correctness.

Retained database: `phase5_e2e_20260908`, loopback port 25434, migration 12.
Final counts: seven documents, 17 retrieval runs, five indexes, 78 chunks,
four provider-free answer rows, 11 spending reservations and 22 generation artifacts.
No existing data, quarantine or ledger was reset. Temporary source/pilot services
were stopped. The resetting PostgreSQL test fixture is separate from paid activity.

A fresh post-evaluation backup was created at
`tmp/phase5-e2e-after-quality-20260909.dump` (317,893 bytes), SHA-256
`874D40C9D0DDB106E42EA94CE277D90CE29AF02C3FB674EE282572EDFF89ABF1`.
Its archive listing includes generation-artifact table data. This verifies a dump
and readable archive listing, not a complete restore/recovery drill.

## Verification and artifacts

- Runner build passed with zero warnings/errors.
- Eleven CLI selection checks passed: nine eligible cases, denied no-hit case,
  and denied unknown case; no dispatch or key required. Launcher syntax passed.
- Eight PostgreSQL private-bridge integration tests passed with a mocked provider.
  Report: `tmp/phase5-test-results/phase5-quality-private-boundary.trx`.
- 115 existing Python cited tests passed across the existing two environments.
- Six new offline report-regression tests passed. They prevent structural validity
  from overriding a failed manual content check, rejected duplicates from counting
  as success, no-hit misses from counting as correct answers, and missing/foreign/
  uncertain accounting from being treated as zero-cost success. They do **not**
  implement a production semantic guard.
- No application/domain schema, migration, public API, model prompt or web UI was
  changed in this increment. No full backend/PG/OCR/browser regression is claimed.
- Documentation build and diff checks passed; the existing documentation bundle
  size warning remains. The isolated PostgreSQL test database was removed.

Artifacts are under `outputs/phase-5/live-quality-20260909/`:
`prepared-cases.json`, individual source/generation reports,
`ledger-after-quality.json`, `manual-assessment.json`, `evaluation.json` and
`offline-replay-checks.json`. The canonical database retains raw count/provider
responses and their IDs/hashes; the export includes proposals and usage metadata.
The offline report generator uses exclusive file creation to preserve prior runs.

## Next work, in order

1. Keep public generation disabled. Design and test semantic-support handling for
   payment-event/status claims; an invoice due date, payment method or amount due
   cannot establish that payment happened. Prompt wording alone is insufficient.
2. Version the provider response schema/prompt to match the server's status/list
   invariants, and direct the model to combine duplicate facts into one claim.
   Preserve the strict validator and test the observed failed outputs offline.
3. Address lexical paraphrase coverage with a versioned policy and the planned
   vector/hybrid comparison, without changing organization/source boundaries.
4. Version evidence-conditioned refusal labels, then run a separately tracked
   bounded follow-up. Preserve this baseline and include every failure.
5. Complete the rest of the 30-question set, document-embedded attacks,
   authenticated two-organization scenarios, broader regression and Phase 5
   acceptance. This subset does not establish those gates or real-invoice accuracy.
