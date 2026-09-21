# Phase 5 completed retrieval comparison

**Date:** 2026-09-10. **Disposition:** all 90 fixed lexical/vector/hybrid retrieval
runs completed and replayed. This synthetic retrieval comparison is complete;
generated-answer and public integration acceptance remain open.

## Results

The unchanged [evaluation contract](../../design/phase-5-retrieval-evaluation-v1.md)
scores 24 answerable questions with 46 deduplicated source/page/exact-quote labels.
Six unlabeled adversarial/unanswerable questions are inspected separately.

| Metric | Lexical | Exact vector | Hybrid RRF60 |
| --- | ---: | ---: | ---: |
| Macro labeled-evidence recall at 1 | 70.14% | 17.36% | 52.08% |
| Macro labeled-evidence recall at 5 | 95.83% | 95.83% | 98.61% |
| Questions with all labels at 5 | 23/24 | 21/24 | 23/24 |
| Questions with any label at 5 | 23/24 | 24/24 | 24/24 |
| Mean reciprocal rank | 0.9167 | 0.5417 | 0.8194 |
| Recorded mean retrieval duration | 7.53 ms | 16.93 ms | 19.90 ms |

All three modes meet the frozen 95% macro labeled-recall threshold at 5. Hybrid
improves this metric by 2.78 percentage points over lexical, but lexical is
stronger at rank 1. Full-question label coverage is unchanged at 23/24 for hybrid
versus lexical. Do not call 98.61% an answer accuracy or complete-question success
rate. The public default has not been changed on the basis of these results.

Lexical misses q022 entirely. Vector misses some labels for q013, q017 and q024.
Hybrid's remaining gap is q024: invoice-primary page 2, exact reference
`Line 2 Unit Price: 1200.00`. The scoring requires the full labeled quote in a
canonical span, rather than accepting only the numeric value. No post-result
ranking tuning or reference-label changes were made.

All six unlabeled cases return hits in every mode. No new answer or refusal was
generated; therefore this does not evaluate refusal correctness. There were no
out-of-selected-scope hits, but the retained corpus still has no foreign-tenant
invoice mirror, so this run is not live cross-tenant acceptance. Durations are
single local development measurements, not a benchmark.

## Paid continuation and retained uncertainty

The user declined further usage investigation after the assistant proposed
keeping the tiny uncertain amount against the approved budget. The new
[pilot policy](../../design/phase-5-retained-unknown-pilot-policy.md) records
`HeldUnknown`, actual cost null, unchanged USD 0.000001 reserved, and an attributed
acceptance event. The original failed operation cannot be dispatched again.
This is explicit acceptance of small pilot uncertainty, not verified settlement.

Migration `20260910141409_RetainSmallUnknownEmbeddingHolds` adds the state and an
immutability trigger. Budget admission v2 includes retained unknown amounts.
The operator action rejects larger holds, other unresolved dispatches, known
overruns and flagged settled responses; no public route was added.

The separate frozen continuation has 31 operations: a new operation for the two
missing chunks plus 30 original not-yet-dispatched queries. All succeeded:
315 confirmed input tokens, USD 0.00000630 exact token-rate estimate, and
USD 0.000031 conservative six-decimal ledger settlement. There were no automatic
HTTP retries and the first failed attempt's artifacts remain intact.

Current database:

- 15 migrations; 69 chunk vectors (the original nine plus all 60 evaluation
  chunks). The 30 evaluation query vectors are retained in their saved outcome
  artifacts, separately from the chunk-vector table.
- 62 reservations: 61 settled and one HeldUnknown; 124 artifacts; 187 spend
  events. No active or unaccepted uncertain operation remains.
- USD 0.002032 settled + USD 0.000001 held unknown = USD 0.002033 counted against
  the unchanged USD 8 ceiling. Shared admission is no longer frozen.
- 110 retrieval runs, 176 source links, 494 hits across all historical work.
  This comparison reuses the 30 lexical runs and adds 60 semantic runs.

Every prior row is preserved except the explicitly audited held-state/completion
transition and expected budget update. Historical spend events and artifacts
are unchanged. All 31 continuation embedding operations and all 90 retrieval
runs replay without provider calls. Retrieval IDs, ranks, scores, spans and
durations match; every table fingerprint is unchanged across replay.

## Verification and evidence

- 220 application tests passed, including retained-amount budget boundaries and
  the HeldUnknown recovery assessment. 45 focused PostgreSQL tests passed across
  the hold/migration/embedding/semantic and ledger/journal suites. Tests exercise
  concurrent acceptance, foreign/inactive actors, overrun rejection, no revival,
  retained amount at the budget ceiling and SQL immutability.
- The standalone runner builds with zero errors/warnings. The complete scoring
  tool validated all 90 results against canonical source content and spans.
- Evidence: `outputs/phase-5/retrieval-evaluation-20260910/evaluation.json`,
  `retrieval-results.jsonl`, `retrieval-replay.jsonl`, `continued-embeddings.jsonl`,
  `continued-embeddings-replay.jsonl`, `continued-ledger.json`, hold transition/
  replay records and preservation/replay table fingerprints.
- Continuation SHA-256:
  `b6d217cf1b595764bb63415d0b29279b0a08ecb805841cded82df6d607a21184`.
  Original plan and the failed 4/10 answer baseline remain unchanged.
- Pre-change backup: `tmp/phase5-before-retained-hold-20260910.dump`, 1,734,036
  bytes; SHA-256
  `A30BC87DE55FEA300AF35861EAE79E6F18BDEB669BF0F806611644639615EE39`.
  Archive listing checked, not restore-tested.

Next work is the remaining q024 evidence gap and versioned cited-answer
integration/evaluation. Any ranking revision needs a new comparison run rather
than replacing these frozen results. Real billing correction, production
authorization and account-wide recovery remain separate future capabilities;
dashboard investigation is no longer a blocker for this accepted pilot hold.
