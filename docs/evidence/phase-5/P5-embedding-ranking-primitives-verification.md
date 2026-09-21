# Phase 5 embedding wire and ranking components

**Date:** 2026-09-10. Offline implementation, not live embedding integration or a
retrieval-accuracy benchmark. No paid calls, keys, database migrations or routes.

## Implemented

`apps/api/app/cited_embedding_protocol.py` prepares deterministic embedding wire
bytes for the proposed `text-embedding-3-small` comparison candidate, explicitly
requesting 1536 dimensions and float output. It keeps organization, actor,
operation, purpose, selected indexes, item IDs and exact UTF-8 input hashes in a
local binding; provider wire data contains only model, input texts, dimensions
and encoding format. Query and chunk purposes cannot reuse the same binding.
The provider model ID is not represented as an immutable dated snapshot.

The implementation follows the [official embedding API](https://developers.openai.com/api/reference/python/resources/embeddings/methods/create)
and [embedding guide](https://developers.openai.com/api/docs/guides/embeddings).
The guide identifies 1536 as the model's default dimension; it is an explicit
comparison candidate here, not a selected production retrieval policy or a new
database column. No model fallback or dimension truncation exists.

Local request bounds: 1–32 unique items, one item for query embedding, 1–20
sorted unique source-index IDs, at most 8000 UTF-8 bytes per item and 65,536 wire
bytes total. These byte bounds are **not measured token counts**. Before live
dispatch, implement verified model-specific token counting and both per-input
and batch token admission; do not treat these bytes as billable tokens.

Response inspection checks raw-byte size, strict JSON/duplicate keys, request
binding, provider request ID, exact model identity, known usage fields, vector
count, unique response indexes, numeric finite/nonzero vectors, component range
and exact dimensions. Out-of-order responses map back to original input IDs and
hashes. Invalid vectors invalidate the entire vector batch while retaining known
usage, raw bytes and response hash. Missing/ambiguous accounting returns unknown
cost, never a free success. Raw text/vectors are excluded from record repr/errors.

The [model pricing page](https://developers.openai.com/api/docs/models/text-embedding-3-small)
lists standard input pricing of USD 0.02 per million tokens. The module records
a reviewed rate reference and computes a microdollar-rounded usage estimate;
this is not an authenticated bill or authorization to dispatch. Unknown extra
billing fields fail closed. Usage larger than an anticipated reservation is not
clamped; a future shared-ledger coordinator must preserve it and freeze on overrun.

`apps/api/app/cited_vector_ranking.py` adds exact cosine ranking and reciprocal
rank fusion (RRF), with deterministic chunk-ID tie breaking. Cosine scales vectors
before normalization to handle very small/large finite components. RRF adds
`1/(60 + rank)` by default, not incompatible lexical/cosine raw scores. This follows
the [RRF algorithm description](https://learn.microsoft.com/en-us/azure/search/hybrid-search-ranking),
without introducing Azure or a remote vector store.

Ranking rejects duplicate/foreign-scope IDs, mismatched model descriptors and
invalid vectors before top-k truncation. Bounds are 2000 candidates and 3,072,000
total vector components; RRF validates all IDs against the supplied eligible set.
Original lexical/vector ranks are retained in fused results.

**These are caller-supplied scope checks, not authentication or SQL filtering.**
ASP.NET must fetch only authorized, Ready, frozen-source records before ranking,
verify embedding input hashes against canonical chunk content, require complete
embedding coverage for the comparison and recheck eligibility before saving hits.
The pure ranking function cannot verify any of those database conditions.

## Verification and unchanged state

- 18 new offline tests cover wire determinism, scope/purpose/hash binding,
  response ordering, malformed vector/accounting separation, duplicate keys,
  numeric and resource bounds, exact geometry, ties, RRF and a mocked
  response-to-ranking path. Synthetic test vectors do not measure semantic recall.
- 144 Python checks passed: 130 cited tests in `.venv`, eight PDF-dependent
  evaluation tests in the existing native runtime, six offline report tests.
  No packages installed. Existing httpx deprecation warning and split-runtime
  dependency gap remain.
- No backend code changed; previous backend test counts are historical and not
  counted as new verification. No retained database write, paid dispatch, key
  read, source mutation or baseline relabeling occurred.
- `DisabledEmbeddingAdapter` remains the default. The new modules contain no HTTP
  transport, route, provider invocation or publication path. Generation's public
  enablement remains false; the v3 payment/format fixes remain in place.

## Next integration work

1. Extend the existing shared budget/journal for explicitly input-only embedding
   operations. Current generation admission requires positive output tokens/rates;
   do not invent output tokens, use a separate allowance, or reset the retained
   ledger to work around it. Preserve known-cost settlement, uncertain holds,
   overrun freezes and one-attempt/replay semantics for both workload types.
2. Add the private no-retry transport and audited coordinator, real token counting,
   bounded deadlines and immutable storage/replay. Set explicit rate-validity and
   model-access admission before any paid embedding run; this arithmetic reference
   does not extend the previous generation rate window.
3. Add canonical, organization/source/model/dimension-bound embedding persistence
   and migration tests, including source retirement and cross-organization cases.
   Pick the small-corpus exact-search storage shape; no approximate index needed yet.
4. Connect lexical/vector/hybrid modes to the same scoped corpus and versioned
   retrieval records. Compare on the independent 30-question set, with per-query
   and indexing costs in the same allowance. Then run a separately planned answer
   follow-up, preserving the original 4/10 baseline and v3 quality limitations.

This checkpoint does not prove live embeddings, improved paraphrase retrieval,
end-to-end privacy, real-invoice accuracy, payment verification or Phase 5 completion.
