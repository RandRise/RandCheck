# Phase 5 — Cited document questions

**Status:** Proposed  
**Outcome:** Users can ask questions across approved documents and verify answers against page evidence.

## Work

- Create page-aware and layout-aware chunks.
- Store embeddings in PostgreSQL/pgvector.
- Add keyword plus vector retrieval.
- Attach document, page, and region metadata to every result.
- Generate answers only from retrieved evidence.
- Refuse unsupported questions.
- Display citations that open the relevant page.
- Evaluate retrieval recall, citation correctness, groundedness, latency, and cost.

## Acceptance criteria

- [ ] Answers contain clickable document/page citations.
- [ ] The system distinguishes an answer from insufficient evidence.
- [ ] Users cannot retrieve documents outside their permitted organization.
- [ ] Evaluation contains answerable and unanswerable questions.
- [ ] Retrieval and generation metrics are reported separately.

## Portfolio evidence

- RAG architecture and threat analysis
- Retrieval evaluation report
- Demonstration of a correct refusal
- Cited-answer workflow
