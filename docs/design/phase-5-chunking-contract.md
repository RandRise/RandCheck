# Phase 5 deterministic chunking contract

**Task:** P5-T04
**Wire version:** 1.0
**Implementation:** Python chunker and embedding interface; ASP.NET snapshot/assembly boundary

## Source boundary

ASP.NET creates `ChunkingSnapshot` from a pinned document, successful extraction,
and completed review. It selects the latest action per field/occurrence. A
confirmed fact uses the normalized machine value when available; a correction
or addition uses the review value; a cleared field produces no reviewed-fact
chunk. Source page text remains unchanged even when a reviewer corrects a value.

The snapshot carries `schemaVersion`, `indexId`, `organizationId`, `documentId`,
`extractionRunId`, `reviewSessionId`, `reviewStatus`, `documentSha256`, `profile`,
`pages`, and `facts`. Each fact includes its action ID and original evidence IDs,
page numbers, and available exact quote offsets. Polygon-only evidence is passed
as an ID/page reference with null text offsets; its polygon remains in the
authoritative field-evidence record. No geometry or quotation is invented.

The caller must authorize the document and choose/reuse the index identity for
the pinned source/profile before dispatch. These library methods do not replace
execution-context authorization. New source revisions or profiles need new
index identities; the P5-T03 uniqueness constraints remain authoritative.

## Profile and algorithm

The initial development profile is `page-lines / 1.0.0-b2048-o256`: at most 2,048
UTF-8 bytes per chunk, with up to 256 bytes of overlap. These are a baseline,
not an evaluated production optimum. Profile identity includes the byte limits;
parameter changes cannot masquerade under the same version.

- Pages are processed in page-number order, never joined across boundaries.
- Text must already be NFC with LF newlines. Chunking does not normalize it again.
- A full byte-bounded window prefers the last newline in its latter half.
  Oversized lines fall back to UTF-8 code-point boundaries.
- Overlap starts at a valid code-point boundary, does not exceed the configured
  limit, and must make forward progress.
- Whitespace-only windows/pages are omitted. All other source bytes remain
  covered; strings are never trimmed, summarized, or enriched.
- Reviewed facts follow page chunks in ordinal field-key/occurrence order, in
  the form `field_key[occurrence]: effective_value`. They remain atomic. An
  oversized fact fails indexing instead of silently truncating the reviewed value.

The allowed profile maximum is 32–65,536 bytes; overlap is 0–one quarter of that
maximum. One request permits 2,000 pages, 1 MiB per page, 8 MiB total page text,
5,000 effective action records, 1,000 evidence references per fact, and 10,000
output chunks. No-evidence inputs fail rather than publishing an empty index.

## Output validation and repeatability

`ChunkingResult` echoes the source/profile identities and proposes chunks with
`id`, `ordinal`, `kind`, `content`, `contentSha256`, nullable page/span fields,
nullable field/action fields, and `evidenceIds`. Page chunks cannot claim review
provenance. Fact chunks cannot invent a page span.

ASP.NET reconstructs each proposal using `CitedDocumentIndex.AddPageChunk` or
`AddReviewedFact` against the original domain objects. It checks source/profile
closure, exact text/hash, IDs, ordinal progression, full non-whitespace page
coverage, bounded overlap, complete effective-fact coverage, and original
evidence references before publishing the in-memory index. Failed assembly does
not mutate the original extraction/review or persist a partial index.

Content hashes use SHA-256 of exact UTF-8 bytes. Deterministic IDs take the first
128 SHA-256 bits of these UTF-8 names, rendered as a GUID, not labelled UUIDv4:

```text
chunk:{indexId:N}:{ordinal}:{PageText|ReviewedFact}:{contentSha256}
span:{chunkId:N}:{pageNumber}:{byteStart}:{byteEnd}:{fieldEvidenceId:N|-}
```

`N` means lowercase GUID hex without hyphens; integers use invariant decimal.
Stable span IDs apply to newly constructed chunks. Existing stored citation IDs
are not rewritten. Idempotent job scheduling, concurrent index creation, and
transactional storage orchestration still need their service-level integration.

## Replaceable embedding interface

`EmbeddingAdapter.embed` is an asynchronous protocol. `DisabledEmbeddingAdapter`
is the provider-free default and raises an explicit error; it never returns fake
vectors. Test adapters exist only in tests.

`embed_checked` validates the selected provider/model/version/dimension and cosine
distance descriptor; 1–128 unique chunk inputs; exact input hashes; a 64 KiB
per-input and 1 MiB batch bound; output model identity; closed, unique chunk IDs;
matching input hashes; vector dimensions; finite numeric, nonzero vectors; and
nonnegative duration/nullable cost with currency. Valid provider responses are
returned in input order. A malformed batch fails as a whole, and cancellation
propagates without partial results.

No model, credentials, vector dimensions for persistence, provider timeouts/retry
policy, or paid integration is selected here. The adapter descriptor's runtime
dimension does not create a database vector column.

## Remaining integration

P5-T04 supplies callable Python/.NET components, not a new HTTP endpoint or a
background indexing job. P5-T05 must wire source authorization, private-service
transport, bounded execution, repository transactions/idempotency, and retrieval.
P5-T06/P5-T07 add answer validation and user-facing navigation. Structural
coverage and references do not prove semantic answer support, real-world
accuracy, or regulatory compliance. See [verification](../evidence/phase-5/P5-T04-chunking-verification.md).
