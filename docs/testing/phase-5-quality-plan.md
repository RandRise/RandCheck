# Phase 5 quality plan

**Latest September 11 — Phase 5 development complete:** [Acceptance evidence](../evidence/phase-5/P5-development-acceptance-20260911.md) closes case selection, public prepared-query vector/hybrid integration, the declared source-security matrix and cross-phase regression. 860 unique backend tests, 329 Python tests and 27 web checks pass; the frozen 30-case HTTP set passes with 54 verified cited-fact references. Initial historical test-clock failures were repaired in test configuration and all affected cases rerun. Browser snapshots and 34 unchanged retained tables are recorded; zero new provider cost. Completion covers bounded English reviewed-field answers and evidence search. Arbitrary live query embeddings and general model answers are not enabled; the optional model route retains its failed 17/30 result and separate acceptance gate. Enterprise identity remains Phase 6. Older checkpoints below are historical.

**Previous September 11 — acceptance audit complete:** [Criterion-by-criterion audit](../evidence/phase-5/P5-acceptance-audit-20260911.md) verifies the bounded public workflow and records six additional adversarial database passes. Remaining work is the planned corpus/retrieval application integration, the complete declared RAG security matrix, and final regression. The approved contract makes model adapters optional: enabling OpenAI is not itself a closure requirement. Model answers remain disabled; existing paid results are unchanged.

**Previous application reviewed answers:** [Public integration verification](../evidence/phase-5/P5-reviewed-public-verification.md) records the new bounded reviewed-field answer mode, persisted citations/history and replay/access checks. The varied development fixture improved from 31/36 to 36/36; 591 backend, eight PostgreSQL and 24 web checks passed. Real services passed 20 legacy checks, 17 reviewed-answer scenarios and all nine reconciliation findings. Six browser snapshots, 34 unchanged retained tables and zero new cost are recorded. Phase 5 remains open for final criterion/adversarial acceptance and the unmet general model-answer gate; enterprise identity stays in Phase 6. Older checkpoints below are historical.

**Previous due-date repair:** [Extraction/review verification](../evidence/phase-5/P5-due-date-verification.md) adds explicit due-date extraction in local-1.3.0 and the missing review row. Fresh extraction returns 2026-08-31; confirmation, correction and clearing pass database/citation checks. 550 backend, nine unique PostgreSQL and 329 unique Python checks passed, including pinned OCR. All retained data and the historical 29/30 trial remain unchanged. Broader evaluation and public answer/history integration remain next; Phase 5 is open.

**Previous reviewed-fact trial:** [Private backend candidate](../evidence/phase-5/P5-reviewed-fact-verification.md) passes 29/30 unchanged development references, with 68 verified cited facts, 30 exact repeats, zero provider calls and all 34 retained tables unchanged. The remaining due-date answer lacks a reviewed field. 545 backend, six new PostgreSQL and seven evaluator tests passed. This bounded English route reads all reviewed fields in the selected corpus; it is not a general QA or like-for-like model improvement. Public integration, broader evaluation and security acceptance remain open. Phase 5 is still in progress.

**Historical paid source-v4 checkpoint:** [Recovered source-v4 evaluation](../evidence/phase-5/P5-source-recovered-evaluation.md) recorded all 30 cases: 17 end-to-end passes, nine rejected proposals, three transport failures and one accepted wrong-field answer. TLS recovery succeeded; known cost USD 0.012233 plus USD 0.006 unknown reservations remains below USD 0.06. Answer quality still blocks Phase 5 completion. Earlier checkpoints below are historical.

**Status:** Declared development scope accepted. Historical model-specific thresholds and failed model results remain separate from bounded reviewed-answer acceptance. Python runtime reproduction is documented in [the four-group runner](python-runtimes.md).

The [September 10 cross-phase regression](../evidence/phase-5/P5-full-regression-20260910.md)
records real-service and browser flows, the pinned OCR checks, recovery snapshots,
and earlier-phase regression coverage. It does not close the generated-answer gates.

**Latest 2026-09-10 checkpoint:** The [full retrieval comparison](../evidence/phase-5/P5-retrieval-comparison-verification.md)
is complete. [Nonredundant hybrid v1](../evidence/phase-5/P5-nonredundant-retrieval-verification.md)
closes q024, reaching 100% labeled recall@5 and full coverage for 24/24 answerable
development cases. The [answer comparisons](../evidence/phase-5/P5-low-reasoning-verification.md) completed at 19/30 minimal and 23/30 low. Source/citation and refusal failures prevent answer acceptance. The new scope restriction is checked on actual saved outcomes; public generation remains disabled.

**Earlier 2026-09-10 checkpoint:** [Private vector/hybrid runs](../evidence/phase-5/P5-semantic-retrieval-verification.md)
are persisted and replayed with the saved query embedding. Lexical, vector and
hybrid all find the expected page for one amount-due question. This does not
replace the fixed-set comparisons below; remaining corpus/query population and
retrieval evaluation are next. Public generation remains disabled.

**Earlier 2026-09-10 checkpoint:** [Private live embedding ingestion](../evidence/phase-5/P5-live-embedding-ingestion-verification.md)
stores nine real vectors for one synthetic invoice. One query found its expected
amount-due source page; offline replay required no new calls. This is a smoke
test only. Remaining corpus coverage, persisted vector retrieval runs, hybrid
integration and the comparisons below remain required; the failed 4/10 live
answer baseline is preserved.

## Evaluation set 0.1

The first evaluation set uses the versioned synthetic English UAE quotation,
purchase-order, and invoice pack already accepted as development-only Phase 4
integration data. It is replaceable and cannot support real-world accuracy or
compliance claims.

The set contains at least 30 versioned questions:

- 12 single-document questions covering identifiers, dates, parties, TRNs,
  amounts, VAT, and line items;
- 6 cross-document questions covering agreements, differences, and conflicts;
- 6 answerable paraphrases that do not repeat the exact source wording;
- 3 genuinely unanswerable or out-of-scope questions; and
- 3 adversarial/isolation questions, including prompt injection and a foreign
  organization corpus.

Each answerable question labels the relevant document, review revision, page,
chunk/fact span, expected claims, and acceptable citations. Unanswerable cases
label an expected reason code. Labels are authored independently from generated
answers and versioned with the source pack.

## Evaluation layers

| Layer | Measures | Required comparisons |
|---|---|---|
| Deterministic indexing | Chunk coverage, page/offset validity, stable hashes, reviewed-fact projection, idempotency | Re-run the same profile; change profile/source revision |
| Retrieval | Recall@1/5, MRR, relevant-hit rank, per-question/category failures, latency | Lexical, vector, and hybrid on the same eligible corpus |
| Citation | Claim coverage, citation precision, citation correctness, page/span validity, click-target authorization | Expected evidence labels and tampered citation cases |
| Generation | Answer correctness, groundedness, completeness, refusal precision/recall, stability, latency, nullable cost | Evidence-only baseline and each approved answer model |
| Security | Cross-tenant hits, injection-policy violations, stale/cache leakage, unsafe rendering | Threat-model red-team set |

Retrieval and generation results are always reported separately. A strong
generated answer cannot hide a retrieval miss, and a retrieved relevant chunk
does not count as a correct answer.

## Development release gates

These are narrow correctness gates for the synthetic pack, not production
service-level objectives:

- 100% of indexed chunks preserve valid organization, document, immutable
  source-revision, and page provenance.
- 0 cross-organization hits or citation targets across every retrieval mode.
- 100% of persisted factual claims have one or more valid retrieved citations.
- 100% of citation links resolve to the expected authorized page; exact spans
  reproduce their stored UTF-8 quote.
- Retrieval recall@5 is at least 95% over answerable questions for the selected
  default policy, with every miss listed.
- All explicitly unanswerable questions return `InsufficientEvidence`; no
  factual answer may be emitted for them.
- Adversarial document text cannot alter the source boundary, forge a citation,
  or invoke a tool.
- Repeated runs retain dataset, chunking, embedding, retrieval, prompt, and model
  versions plus latency and nullable estimated cost.
- Full .NET, Python, web, PostgreSQL migration, and documentation regressions
  pass before Phase 5 completion.

Generation-specific numeric thresholds beyond the zero-unsupported-answer gate
remain provisional until Rand approves a model/provider and baseline results
exist. Thresholds cannot be weakened after seeing test failures without a
documented decision and a new evaluation version.

## Required reports

The [v4 source-context verification](../evidence/phase-5/P5-source-context-verification.md)
records document metadata, literal citation-excerpt checks, compatibility and
integration tests, and the frozen 30-case evaluation. The explicitly approved
[paid run stopped after four cases](../evidence/phase-5/P5-source-context-partial-run.md)
with 26 unrun; no full v4 quality score exists. Excerpt
membership must not be counted as proof of entailment or comparison completeness.

The [embedding storage contract](../design/phase-5-embedding-storage-contract.md)
adds verified saved-outcome ingestion and database-backed exact cosine search.
Synthetic unit vectors and mocked usage prove storage/ranking mechanics only;
they do not satisfy retrieval-recall, live embedding, hybrid or answer-quality
acceptance. The full comparison still requires paid document/query embeddings,
versioned retrieval-run persistence and the frozen question set.

The [2026-09-10 offline fixes](../evidence/phase-5/P5-quality-fixes-verification.md)
introduce v3 status branches and a conservative payment-event restriction. They
do not replace the frozen baseline or satisfy the general semantic-support gate.
The next versioned evaluation must measure false refusals as well as unsupported
claims, duplicate facts, provider schema acceptance and retrieval improvements.

The [ten-case live baseline](../evidence/phase-5/P5-live-quality-baseline-verification.md)
completed with four end-to-end passes, a no-hit paraphrase miss, duplicate/malformed
refusal outputs and one unsupported paid-date claim that passed structural
validation. Preserve its frozen plan, references and outputs. No public generation
may be enabled based on its structural pass count. Payment-event support and
response-contract fixes precede a separately versioned follow-up; reference reason
semantics must distinguish no hits from nonempty but insufficient evidence.

The [first live pilot](../evidence/phase-5/P5-live-synthetic-generation-verification.md)
preserves a correct-amount/invalid-citation model failure and its actual usage.
The v2 citation-pair schema is a versioned follow-up, not a reclassification of
that failure. One synthetic amount question is a smoke test, not completion of
this evaluation set. Next evaluate independently labeled answerable, nonempty-hit
unanswerable, conflicting and injection-bearing cases through the same ledger.
Keep every failed and successful output, version and cost; do not cherry-pick
or automatically retry failures until they pass.

The [internal answer-bridge checkpoint](../evidence/phase-5/P5-T06-T08-answer-bridge-verification.md)
tests orchestration, accounting and citation structure with fake providers.
Those results do not count toward retrieval recall or real-model answer-quality
thresholds. Proposals remain unpublished until semantic-support and broader
acceptance gates pass.

The acceptance evidence publishes the source/query-set versions, runtime and
provider identities, all parameters, aggregate metrics, per-question outcomes,
failure examples, latency percentiles, nullable cost, and the exact limitations
of the synthetic dataset. Raw model outputs are retained as evaluation artifacts
but excluded from ordinary application logs.

The evaluation structure follows [Microsoft's RAG evaluation guidance](https://learn.microsoft.com/en-us/azure/architecture/ai-ml/guide/rag/rag-solution-design-and-evaluation-guide),
which recommends representative test questions and independent evaluation of
chunking, retrieval, and end-to-end model behavior.
