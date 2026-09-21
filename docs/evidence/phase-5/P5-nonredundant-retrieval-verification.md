# Nonredundant hybrid retrieval verification

On September 10, the new `pgvector-english-rrf60-c20-nonredundant-v1` policy
closed the remaining q024 gap. The top five now include the invoice's page 2
unit price alongside the quotation and purchase order. Unchanged confirmed facts
are suppressed only when an already selected page covers their exact source bytes.
Corrections, review-only facts and evidence from other documents remain distinct.
See the [versioned rule](../../design/phase-5-nonredundant-retrieval-v1.md).

| Policy | Macro labeled recall@5 | Full coverage | Recall@1 | Mean retrieval ms |
|---|---:|---:|---:|---:|
| Original lexical | 95.83% | 23/24 | 70.14% | 7.53 |
| Original vector | 95.83% | 21/24 | 17.36% | 16.93 |
| Original hybrid | 98.61% | 23/24 | 52.08% | 19.90 |
| Nonredundant hybrid v1 | 100% | 24/24 | 52.08% | 19.40 |

This is the same development set used to diagnose the gap, not an independent
holdout. The 46 deduplicated labels, all questions, source texts, top-K and
original scores/outputs remain unchanged. Six unlabeled refusal/adversarial cases
still have hits; retrieval success does not establish correct answers or refusals.
The absent foreign mirror prevents a live tenant-isolation claim.

Thirty new runs were persisted without provider calls, new embeddings or charges.
All 30 replayed with identical IDs, hits, scores and durations; every database
table fingerprint matched across replay. Retained state before answer generation:
140 retrieval runs, 225 sources, 602 hits, 69 vectors, 62 spend reservations,
124 artifacts, 15 migrations. USD 0.002032 settled plus USD 0.000001 retained
unknown continues to count against the unchanged USD 8 ceiling.

Verification: 222 application tests, eight selected PostgreSQL semantic retrieval
tests, eight report tests and runner builds passed. The first PostgreSQL command
used a class-name filter that matched no tests; the corrected method-name filter
ran all eight reported tests. UTF-8 containment, confirmed/changed review values,
scope, page/index differences, ranking order and policy replay are covered.

Artifacts: `outputs/phase-5/nonredundant-retrieval-20260910/` contains immutable
results, replay, the 120-row comparison report and before/after fingerprints.
The next [answer evaluation](../../design/phase-5-answer-followup-v2.md) binds
these exact new runs; public answer integration remains subject to its results.
