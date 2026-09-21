# Phase 5 payment restriction and response-contract fixes

**Date:** 2026-09-10. Private synthetic pilot only; public generation remains disabled.

## Outcome

Prompt `cited-answer-prompt-v3` adds status-specific response shapes, concise
single-fact/multiple-citation guidance and explicit payment-event refusal policy.
The server now applies `payment-event-restriction-v1` after citation validation,
both for new outcomes and for saved-result replay. Neither change establishes
general semantic support or completes Phase 5.

The frozen [ten-case v2 baseline](P5-live-quality-baseline-verification.md) remains
**4/10**, with its original outputs, labels, failures and costs intact. This is an
offline implementation checkpoint, not a new model-quality score.

## Response contract

The private provider schema uses an `answer` wrapper with three nested branches:

| Status | Claims | Reason | Unanswered parts |
| --- | --- | --- | --- |
| Answered | 1–20 | null | empty |
| PartiallyAnswered | 1–20 | allowed non-null reason | 1–20 |
| InsufficientEvidence | empty | allowed non-null reason | empty |

Every branch remains closed to extra properties. A shared claim definition
retains exact hit/span pairing, maximum eight citations per claim and bounded
text. Citation enum allowance is 980, leaving room for branch enums. The model
cannot select `no_relevant_evidence` with nonempty retrieved evidence; the
existing provider-free no-hit path still owns that reason.

The trusted private Python adapter unwraps `answer` into the unchanged ASP.NET
proposal contract. It rejects duplicate JSON keys, extra wrapper fields and flat
legacy provider output for new v3 requests. It does not delete duplicate claims,
change a status, repair malformed lists or rewrite factual text. Original raw
provider bytes and usage remain journaled. Known usage is settled before answer
rejection; malformed output must not be mistaken for unknown billing.

Duplicate claim text and duplicate citation pairs remain server-rejected. The
prompt asks the model to consolidate supporting citations, but whether it follows
that instruction needs a new model evaluation; JSON shape alone cannot enforce
unique factual meaning.

Official OpenAI documentation shaped this implementation: root unions and
`if/then/else` are unsupported, while nested `anyOf`, shared definitions and array
bounds support these status branches. See [Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs).
The existing [GPT-5 nano](https://developers.openai.com/api/docs/models/gpt-5-nano)
snapshot, reasoning, endpoints, tools prohibition and pricing window were not
changed. No newer model was substituted.

## Payment-event restriction

No authenticated, structured payment-event source is integrated. Consequently,
detected English payment-event questions must not receive a factual or partial
answer, and detected event assertions in other answers are rejected. This catches
the observed false claim, bare-date/yes answers to detected payment questions,
paid/unpaid/settled assertions and tested transfer/receipt formulations.

The check does **not** grant permission based on a date, amount due, payment
method, Net 30 terms, a PAID stamp, or instructions inside source text. Ordinary
due-date, invoice-number, payment-method and payment-term examples remain allowed
through this restriction, but still have no semantic acceptance guarantee.

This is deliberately conservative English pattern matching, not an entailment
model. It can over-block ambiguous wording, mixed questions and benign uses of
words such as “received,” and it can miss untested paraphrases or languages.
Compatibility normalization covers tested full-width and zero-width variants,
not all obfuscation. Passing it leaves `SemanticSupportEstablished=false` and
`PublicationAllowed=false`. Future payment-event support requires an explicitly
authorized source bound to organization, invoice and event; untrusted text alone
must never unlock it. Broader semantic and document-injection evaluation remain
release gates.

New preparation must use v3. Settled v1/v2 artifacts may still be read without
dispatch, but are rechecked under the current payment policy. Unsafe historical
answers are refused on replay; their original immutable records and settled
costs are not altered. Unsettled legacy preparation is not silently upgraded.

## Verification

- 436 non-PostgreSQL backend tests passed: 111 Domain, 176 Application,
  67 Infrastructure, four Architecture and 78 API. The Application set includes
  the real structural-validation-to-payment-policy regression and 22 policy cases.
- 126 Python tests passed: 112 cited modules in `.venv`, eight PDF-dependent
  evaluation tests in the existing native runtime, and six offline quality-report
  tests. The existing split-runtime dependency gap remains a release gate.
- Private runner builds with zero warnings/errors. Documentation builds; existing
  bundle-size warning remains. Initial sandbox Node path resolution required the
  usual scoped build escalation. No new dependencies were installed.
- Actual saved `p5-q-025` was denied by the updated coordinator with private worker
  port 28157 stopped. Saved `p5-q-009` still returned `Replayed=true`, two claims,
  policy version v1 and `PublicationAllowed=false`. Both reservations were verified
  Settled before invocation. No key was loaded and no provider was contacted.
- Read-only ledger check afterwards: 11 settled reservations, 22 artifacts,
  USD 0.001951 generation token-rate estimate, no pending holds or freeze. This
  matches the baseline; no accounting or source records were rewritten. Summary:
  `outputs/phase-5/quality-fixes-20260910/offline-replay-checks.json`.
- Frozen prepared manifest, question set and plan hashes match their previous
  pins. Original paid baseline results and costs were not regraded or overwritten.
- 50 focused PostgreSQL tests passed, including the real private .NET/Python
  bridge with mocked provider, valid refusal and denied payment claim, accounting
  before rejection, replay without redispatch, legacy v1/v2 settlement replay,
  old-preparation dispatch denial, prompt preflight and process-crash/restart
  checks. Final run: zero failures/skips, 7m56s. The fixture uses only
  `document_intelligence_test`; cleanup removed it. Retained paid DB was not reset.
- Total: **486 backend checks and 126 Python checks passed**, plus runner/docs
  builds and whitespace check. This is not a full PostgreSQL/OCR/web/browser
  regression or a new paid evaluation. TRX evidence is under
  `tmp/phase5-test-results/phase5-quality-fixes-final_net8.0_*.trx` and
  `phase5-quality-fixes-persistence-final.trx`. The zero-test TRX is the deliberately
  excluded PostgreSQL project in the non-PG run, not missing counted coverage.

The initial policy regex exceeded .NET's non-backtracking automaton limit; it
was split into small bounded-time expressions. A new test fixture also needed
valid field evidence offsets. Both were corrected before the passing unit run.
The initial database run used the earlier policy binary and exposed its rejection
failures; those results remain in the test-results directory, not counted as passes.

## What this does not prove / next work

- No paid call was needed for implementation. The pinned rate window ends at
  2026-09-10 00:00 UTC and has not been extended; reverify rates/access before
  later paid admission.
- No claim that v3 improves live model accuracy, reliably consolidates duplicate
  facts, or has already been accepted by the provider's schema parser. Mocked
  responses and contract tests are not a provider evaluation.
- No embeddings were generated or stored. Finish embedding integration and
  accounting, then compare lexical, vector and hybrid retrieval on the same
  organization-scoped corpus and independent question labels.
- Version the evidence-conditioned refusal reference policy and follow-up plan;
  do not rewrite v0.1.0 labels to hide failures. Keep the v2 baseline as comparator.
- Complete broader grounded-answer, calculation, refusal, embedded-injection,
  authenticated two-organization and full Phase 5 acceptance tests before release.
- Synthetic invoices remain development data, not proof of real-invoice accuracy,
  tax compliance, UAE residency or production privacy/security.
