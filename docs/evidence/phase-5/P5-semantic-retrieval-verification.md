# Phase 5 persisted semantic retrieval verification

**Date:** 2026-09-10. **Disposition:** private vector/hybrid retrieval persistence
and saved-result replay verified; broader retrieval/answer acceptance remains open.

## Result

Three retrieval runs now compare the same saved question, "What amount is due on
the invoice?", against the nine embedded chunks of the synthetic invoice. Each
method ranks source-page chunk `652030ca-5c13-2bd7-bffd-3fc38ec6664a` first; it
contains the expected AED 8,820.00 amount due.

| Method | Saved run | Hits | First-hit score | Recorded retrieval duration |
|---|---|---:|---:|---:|
| Lexical | 33cdcd46-a697-4865-bdc6-4cc56452427a | 3 | 0.8000000119 | 101 ms |
| Exact vector | 89e9631b-6744-495c-94e7-d57072cf1c5c | 5 | 0.4475187390 | 131 ms |
| Hybrid | d8bc54f3-f5eb-4656-9b86-884eec38afeb | 5 | 0.0327868852 | 55 ms |

These are single-run development timings, not a latency benchmark. The scores
use different scales. All methods already succeed on this one question; this
does not demonstrate improved retrieval accuracy or complete the 30-question
comparison. No answer was generated.

## Implementation

The [semantic retrieval contract](../../design/phase-5-semantic-retrieval-contract.md)
freezes exact cosine and equal-weight RRF with constant60 and20 candidates per
leg. Query identity, sources, model and settled outcome are independently checked;
the persisted policy records query/reservation/artifact provenance. Scope and
source locks cover ranking through atomic run/hit persistence. Incomplete vector
coverage rejects the run. Same-key calls serialize and replay; failures roll back.

`CitedEmbeddingStore` shares its verified-outcome transaction and exact search
helper with the new private `ICitedSemanticRetrievalStore` implementation. The
existing schema's lexical/vector/fused score columns are populated appropriately.
Generic retrieval history now chooses fused, vector or lexical score in that
order. No migration, public mode selector, paid query creation or public
generation enablement was added.

The standalone runner adds `--retrieve-saved-plan`. It injects offline embedding
and frozen lexical clients; no worker, secret or provider access is used. The
original paid plan and operation IDs are unchanged. The new locally generated
`samples/phase-5/embedding-pilot-v0.1.0/lexical-plan.json` has SHA-256
`fbebc28ccc7f5fe1eca82c37f2e817381e7ede6a77ead7d0cec47b0aa6330f07`.

## Retained data and replay

The first run added exactly three retrieval graphs: three runs, three sources,
13 hits. Final totals:20 runs,29 retrieval sources,67 hits. Every pre-existing
row matches its before-run fingerprint, including old retrieval graphs, source
data, paid journals, reservations/events and vectors.

A second invocation replayed all three original IDs/hits/scores/durations.
Every public table's row count/content fingerprint matches before/after replay.
The paid ledger is unchanged across the entire increment: nine vectors,
11 Generation/four Embedding reservations,30 artifacts, USD0.001962 settled,
zero holds and no reconciliation freeze. **Zero new provider calls or charges.**

Backup before persistence: `tmp/phase5-e2e-before-semantic-retrieval-20260910.dump`,
530,788 bytes; SHA-256
`4098602FFA4F7DA3BB18ABAB1B6889351B506436704F0B0F352A1BD5038FE591`.
Archive listing checked (331 lines); not restore-tested. Retained DB remains
`phase5_e2e_20260908` on25434, migration14, vector0.8.1. The disposable fixture DB
is separate and removed after testing.

Evidence: `outputs/phase-5/semantic-retrieval-20260910/` contains first-run/replay
JSONL, exact saved policies/component scores, before/after fingerprints, filtered
historical retrieval fingerprints, unchanged ledger summaries and test results.

## Tests and limits

Five new application cases cover RRF agreement, empty legs, canonical ties,
component-score preservation, invalid values and output bounds. Seven new
PostgreSQL cases cover vector/hybrid persistence and history, concurrent replay,
changed-key inputs, wrong-question planning, incomplete coverage, foreign callers,
actor/source retirement and forced-write rollback.

The first PostgreSQL run found a real replay bug: raw policy-string comparison
did not account for jsonb normalization. Structural equality fixed it; all seven
focused cases then passed, including the race. Final regression: **527 backend
tests passed** (485 non-PG and42 selected PG), plus **165 cited Python tests**.
Counts and report paths are in `test-summary.json`. Solution/runner and documentation
builds passed, as did the whitespace check. The existing VitePress bundle-size
warning remains. No full PostgreSQL,
OCR or browser acceptance run is claimed.

Next: freeze the remaining corpus/query embedding population plan, reuse the
already stored vectors and evaluate lexical/vector/hybrid on the fixed question
set. Preserve the failed4/10 answer baseline. Public generation and semantic
acceptance remain disabled until their separate gates pass.
