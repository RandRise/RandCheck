# Nonredundant hybrid retrieval candidate v1

**Frozen before full-set execution, 2026-09-10.** The observed q024 miss motivates
this new candidate; it is development on a known evaluation set, not an unseen
holdout result. Earlier three-mode results and reference labels remain immutable.

Mode `hybrid-nonredundant`, version `pgvector-english-rrf60-c20-nonredundant-v1`,
uses the same two 20-candidate legs and RRF60 scores. It examines the top 20 fused
candidates in their existing order and selects at most five. A reviewed fact is
skipped only when an already-selected, higher-ranked PageText chunk fully contains
its single exact UTF-8 evidence span in the same organization/index/page, the
review action is Confirmed, and the effective displayed value exactly equals that
quote. All remaining candidates retain their original scores and order.

Corrections, additions, review-only evidence, different normalized values,
multi-span facts and cross-source text matches cannot be suppressed. A page that
has not been selected cannot suppress a fact. No new chunks are invented,
embeddings regenerated, source scope widened, or question-specific fields used.
The candidate is evaluated on all 30 frozen questions with the existing exact
quote metrics and saved query vectors. Full-set results, deltas and misses must
be reported before deciding whether to use it for answer evaluation.
