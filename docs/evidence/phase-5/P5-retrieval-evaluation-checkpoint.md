# Phase 5 fixed-set retrieval evaluation checkpoint

**Date:** 2026-09-10. **Disposition:** lexical baseline complete; vector/hybrid
comparison blocked by an uncertain provider outcome. Phase 5 remains in progress.

## Frozen evaluation and result

The [evaluation contract](../../design/phase-5-retrieval-evaluation-v1.md) fixes
30 questions, 24 answerable cases and 46 deduplicated source/page/exact-quote
labels. All labels are present in the canonical corpus before ranking. The three
primary synthetic CC0 documents contain 60 chunks. No policy or question was
tuned after observing results.

Plan: `samples/phase-5/retrieval-evaluation-v0.1.0/plan.json`, SHA-256
`d4176993fac3bc00d0bdbaf8f1ee9e8bb4f7cda6c7b7ff7d50c3366e723e8406`.
The frozen plan contains 16 chunk batches followed by 30 query operations;
preflight measured 1,999 input tokens across the complete proposed run.

| Lexical metric | Result |
| --- | ---: |
| Macro labeled-evidence recall at 1 | 70.14% |
| Macro labeled-evidence recall at 5 | 95.83% |
| Questions with every label covered at 5 | 23/24 (95.83%) |
| Any labeled evidence at rank 1 | 21/24 (87.50%) |
| Mean reciprocal rank | 0.9167 |
| Recorded mean retrieval duration | 7.53 ms |

`p5-q-022` is the only answerable question missing evidence at 5. The lexical
baseline meets the frozen 95% labeled-recall threshold on this synthetic set;
this does not establish vector/hybrid improvement or answer quality. All six
unlabeled cases return hits; no refusal or answer generation was evaluated.
Durations describe one local run, not a latency benchmark. The retained database
has no foreign-organization invoice mirror: q30 checks selected-source scope
only and cannot substantiate live cross-tenant isolation.

## Paid run stopped and preserved

The user's earlier paid embedding authorization covered this frozen synthetic
run. Fifteen batches succeeded, storing 58 new vectors and reporting 1,684 input
tokens. Exact token-rate estimate: USD 0.00003368; conservative six-decimal ledger
settlement: USD 0.000039. The sixteenth batch, containing two chunks, returned
`transport_uncertain`: no HTTP status, request ID, response or final usage.

Operation `ccaecf4e-d611-5042-aa64-84d054d42346` remains `Uncertain` with its
USD 0.000001 reservation. Its preparation measured 16 tokens, which is not proof
of billable usage. The shared budget is frozen for reconciliation. No query
embedding from this evaluation was dispatched and no failed operation was retried.

Retained totals: 67 vectors, 31 paid reservations, 62 artifacts, USD 0.002001
settled, one unresolved hold, unchanged USD 8 ceiling. The original nine invoice
vectors remain intact. The lexical baseline independently added 30 runs, 49
sources and 127 hits, giving totals of 50 runs, 78 sources and 194 hits.

All earlier rows match their before-run fingerprints except the expected shared
budget update. Replaying all 30 lexical runs returned identical IDs, hits, scores
and durations with `replayed=true`; every table fingerprint stayed unchanged.
The frozen failed 4/10 answer baseline remains unchanged.

## Evidence and verification

Evidence directory: `outputs/phase-5/retrieval-evaluation-20260910/`.
It contains the original live output, sanitized stopped-operation record, before/
after ledger summaries, preservation/replay fingerprints, lexical results/replay
and `lexical-evaluation.json`. The report explicitly marks the comparison
incomplete with vector and hybrid pending. The full comparison reporter rejects
missing modes; partial scoring requires `--lexical-only` and all 30 questions.

The standalone runner builds with zero warnings/errors. Seven scoring tests and
165 cited Python regression tests passed. These include partial-label scoring,
missing/duplicate outputs, changed content/spans, invalid scores and out-of-scope
evidence rejection. No backend application behavior changed in this checkpoint;
the preceding 527-test backend result belongs to the earlier semantic checkpoint.

Pre-run backup: `tmp/phase5-e2e-before-retrieval-evaluation-20260910.dump`,
532,117 bytes, SHA-256
`4550BFF59B11F6565E710C0428F8B29D51300BA627F2955EA0BE5EA88F72E33E`.
Archive listing checked; not restore-tested. The live database is retained.

## Next step

Resolve the uncertain provider usage through verified billing evidence and a
live recovery path before any further paid dispatch. The current
[recovery contract](../../design/phase-5-recovery-contract.md) supports only
synthetic execution; real billing verification and account-wide freeze resolution
remain unimplemented. Missing response data cannot establish zero cost. Do not
reset the ledger, clear the freeze manually, reopen the failed operation or rerun
the paid launcher. The original plan and artifacts remain immutable.

After reconciliation, separately account for the two missing chunks and 30 query
embeddings, then run the frozen vector/hybrid policies against the same questions
and compare with this saved lexical baseline. Public question integration,
generated-answer quality and broader security acceptance remain open.
