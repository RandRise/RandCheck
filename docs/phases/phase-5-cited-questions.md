# Phase 5 — Cited document questions

**Status:** Development complete, September 11, 2026 (bounded English reviewed answers and prepared-query evidence search)
**Outcome:** Users can ask questions across approved documents and verify answers against page evidence.

**September 11 closure:** case selection, public prepared-query vector/hybrid
search, source screening and full cross-phase regression are verified. See the
[development acceptance report](../evidence/phase-5/P5-development-acceptance-20260911.md).
General model answers remain disabled and require separate acceptance.
Enterprise identity remains Phase 6; deployment and real-world accuracy are
not claimed by this synthetic development gate.

Rand authorized safe Phase 5 foundation work on 2026-08-25 and approved the
cited-question contract on 2026-09-01. Implementation may proceed through the
provider-independent evidence, evaluation, persistence, retrieval, refusal, and
citation layers. The authorized private nano pilot has now made actual model
calls through the canonical spending ledger; see the
[live pilot review](../evidence/phase-5/P5-live-synthetic-generation-verification.md).
Public model generation still waits for semantic-support and quality acceptance.
Embedding storage, private live ingestion and the lexical/vector/hybrid
development comparison are implemented with saved, settled outcome verification.
The new reviewed-field mode uses its own selected-corpus policy. See the
[storage contract](../design/phase-5-embedding-storage-contract.md).

## Work

- Create page-aware and layout-aware chunks.
- Store embeddings in PostgreSQL/pgvector.
- Add keyword plus vector retrieval.
- Attach document, page, and region metadata to every result.
- Generate answers only from retrieved evidence.
- Refuse unsupported questions.
- Display citations that open the relevant page.
- Evaluate retrieval recall, citation correctness, groundedness, latency, and cost.

## Approved planning boundary

- Questions are limited to an explicitly selected, organization-authorized set
  of completed review revisions or one reconciliation case.
- Canonical page text and reviewed-value provenance are reused from Phases 2–4;
  Phase 5 does not create a detached source of truth.
- Retrieval, generation, citation validation, refusal, and security are tested
  independently.
- Retrieved documents are untrusted data and cannot issue instructions or call
  tools.
- The model and embedding providers remain replaceable decisions.

See the [cited-question contract](../design/phase-5-cited-question-contract.md),
[task ledger](../tasks/phase-5.md), [quality plan](../testing/phase-5-quality-plan.md),
and [RAG threat model](../security/phase-5-rag-threat-model.md).

## Acceptance criteria

The [September 11 acceptance](../evidence/phase-5/P5-development-acceptance-20260911.md)
closes these criteria for the declared deterministic reviewed-answer route
and structural development identity. Prompt-injection coverage refers to
the recorded matrix and no-tool boundary, not a universal classifier.

- [x] Answers contain clickable document/page citations.
- [x] The system distinguishes an answer from insufficient evidence.
- [x] Users cannot retrieve documents outside their permitted organization.
- [x] Evaluation contains answerable and unanswerable questions.
- [x] Retrieval and generation metrics are reported separately.
- [x] Every persisted factual claim cites a retrieved, authorized, immutable source.
- [x] Retrieved prompt-injection text cannot change policy or invoke an action.

## Portfolio evidence

- RAG architecture and threat analysis
- Retrieval evaluation report
- Demonstration of a correct refusal
- Cited-answer workflow
