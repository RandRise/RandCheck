# Phase 5 cited-question contract

**Status:** Approved by Rand on 2026-09-01; implementation authorized
**Version:** 0.1
**Task:** P5-T01

This contract defines a narrow, evidence-first question workflow over reviewed
documents. It deliberately separates retrieval, answer generation, citations,
and refusal so each part can be tested independently. It does not turn document
text into trusted instructions, provide tax or legal advice, approve payments,
or make claims from model memory.

## Scope and source of truth

Version 0.1 answers questions only over a corpus explicitly selected by the
user: completed review revisions, or the completed revisions pinned by one
reconciliation case. A request cannot silently widen its corpus.

Each source is immutable and identifies:

- organization, document, extraction run, and completed review session;
- document role and filename;
- canonical page text and page number;
- UTF-8 byte offsets, quoted text, and normalized polygon where available; and
- the effective reviewed value and review-action provenance for reviewed facts.

The corpus is frozen when a question starts. A later extraction or review does
not change an earlier answer. Re-indexing creates a new versioned index record;
it does not overwrite an answer's source identities.

Two deterministic chunk types are allowed:

1. `PageText` chunks contain contiguous text from exactly one canonical page.
   A chunk cannot cross a page boundary in version 0.1.
2. `ReviewedFact` chunks represent effective reviewed fields. They retain the
   review session/action and their original page evidence. Reviewer-added facts
   without page evidence remain searchable but are visibly labelled as review
   provenance rather than quoted page text.

Chunk size, overlap, separators, table handling, and enrichment are parameters
of a versioned chunking profile. They will be selected by evaluation rather
than treated as universal constants.

## Ownership and trust boundaries

- ASP.NET owns the public question API, organization and corpus authorization,
  question/answer history, transactional persistence, and final validation of
  returned source identities.
- Python owns deterministic chunk construction, embedding adapters, retrieval
  orchestration, prompt assembly, and optional answer-model adapters.
- PostgreSQL is the canonical chunk, embedding, retrieval-run, answer, and
  citation store. EF Core migrations continue to own the physical schema.
- The browser renders answers and opens citations; it never calculates source
  authority or converts an unsupported result into an answer.
- The model receives only the bounded corpus selected for that request. It has
  no tools and cannot upload, edit, approve, pay, browse, or retrieve a wider
  corpus.

ASP.NET verifies that every requested review revision belongs to the execution
organization before calling Python. Retrieval applies the organization and
frozen-corpus predicates before lexical or vector ranking. ASP.NET then rejects
any returned hit whose organization or source IDs are outside that frozen set.
Phase 6 will replace the fixed development identity with real authentication
and authorization; Phase 5 must still prove the structural isolation now.

## Chunk and embedding records

Every chunk records at least:

- stable chunk ID and SHA-256 content hash;
- organization, document, extraction-run, and review-session IDs;
- chunk type, page number, chunk ordinal, canonical text, and optional heading;
- page-relative UTF-8 byte start/end when the chunk is a contiguous page span;
- cited evidence IDs and polygon when a smaller verified region exists;
- chunking-profile name/version and creation time; and
- active/superseded state without destructive replacement.

Every embedding records the chunk ID, provider alias, model name/version,
dimension, distance function, input hash, creation time, and nullable estimated
cost. The database vector shape and index are not fixed until the embedding
model is selected. A model change produces new embedding records and a new
retrieval-policy version.

Version 0.1 uses exact vector search for the small development corpus. pgvector
documents exact search as the default and notes that approximate indexes trade
recall for speed. HNSW or IVFFlat may be added only after a measured scale or
latency need and must be compared with exact search for recall.

## Retrieval policy 0.1

The baseline compares three modes on the same eligible chunks:

- lexical retrieval with PostgreSQL full-text search;
- vector retrieval with cosine similarity; and
- hybrid retrieval that fuses lexical and vector ranks with Reciprocal Rank
  Fusion (RRF).

Lexical queries use `websearch_to_tsquery` for user-entered syntax and
`ts_rank_cd` for ranking. Exact identifiers, invoice/PO numbers, TRNs, amounts,
and line codes must remain visible to lexical retrieval. Vector retrieval adds
semantic matching for paraphrased questions. RRF combines rank positions rather
than adding scores from incompatible scales. Mode, candidate limits, final
top-k, RRF parameters, embedding identity, and filters are stored on every
retrieval run.

The chosen production-like default is whichever mode passes the versioned
evaluation set, not automatically hybrid. Retrieval returns raw source text and
metadata; generated summaries are never substituted for citation evidence.

## Question and answer contract

A question request contains:

- the original question;
- one organization supplied by the execution context, never by trusted client
  input;
- either an explicit set of completed review-session IDs or one reconciliation
  case ID;
- the retrieval-policy version; and
- an idempotency/correlation ID.

Question length, corpus size, candidate count, final context size, and execution
time are bounded. The internal result is structured:

```text
status: Answered | PartiallyAnswered | InsufficientEvidence
answer: short user-facing text or null
claims: [{ text, citation_ids[] }]
citations: [{ id, document_id, filename, review_session_id,
              extraction_run_id, page_number, quote, byte_start,
              byte_end, polygon, provenance_type }]
reason_code: null | no_relevant_evidence | incomplete_evidence |
             conflicting_evidence | unsupported_scope | unsafe_source
```

Every factual claim must have at least one citation. Citations must reference a
retrieved chunk in the same run and an authorized immutable source. A validator
rejects unknown, out-of-corpus, malformed, or uncited claims before persistence.
The UI opens the document page and highlights the polygon or text span when it
exists; otherwise it opens the page and clearly labels review-only provenance.

## Refusal and conflict behavior

`InsufficientEvidence` is a successful, expected outcome—not a provider error.
The system refuses when:

- no eligible evidence clears the evaluated retrieval threshold;
- the available evidence does not support every material part of the question;
- sources conflict and the conflict cannot be stated faithfully;
- the question requests knowledge outside the frozen corpus; or
- retrieved content is quarantined as unsafe.

`PartiallyAnswered` is allowed only when supported and unsupported parts are
separated explicitly. The answer cannot fill gaps from general model knowledge.
Conflicting documents may be described as a conflict with citations to each
side, but the model cannot decide which business record is authoritative.

## Model-provider decision boundary

The retrieval, citation, refusal, persistence, and UI contracts do not depend on
one generation provider. Phase 5 can first return deterministic evidence hits
and cited reviewed facts. Enabling free-form answer generation requires Rand to
approve a provider/model after local, paid API, privacy, latency, and cost
trade-offs are compared. Provider credentials and model selection are
configuration, never stored in question text or source records.

## P5-T03 implementation checkpoint — 2026-09-07

The provider-independent storage foundation is implemented. A versioned
`CitedDocumentIndex` pins document identity, completed review, extraction run,
filename, hash, and chunking profile. `DocumentChunk` and `ChunkEvidenceSpan`
store bounded content, content hashes, exact page-relative UTF-8 spans, and
review-action provenance. Source metadata shared by chunks lives on their index.

`CitedRetrievalRun` freezes eligible source indexes and policy JSON before hits
are added. `CitedAnswer`, `AnswerClaim`, and `AnswerCitation` store outcomes and
claim-level evidence. User-facing factual answer text must be derived from the
validated claims; there is no second independently stored, uncited answer body.
Composite foreign keys bind citations to hits in the same run, chunks in the
frozen corpus, and spans in the same chunk. PostgreSQL also checks quote bytes
against canonical page text on span insertion/update.

Corrected facts retain the original machine evidence as original evidence, not
as a quotation of the corrected value. A citation with no page span is allowed
by the domain only for a reviewed fact, with the action retained for provenance.
Polygon-only machine evidence remains available through that original review
and field history; the new span table currently stores text-backed spans only.
P5-T04/P5-T07 must preserve that distinction when constructing chunks and
displaying navigation, and must not invent a page for review-only facts.

Domain methods and EF tracked-write guards protect immutable history; these
are not database-role authorization or blanket protection against privileged
SQL writes. The persistence checks validate source identity and quotation,
not whether a claim's meaning is actually supported. Semantic validation,
request authorization, orchestration, and UI remain later Phase 5 work.

Embedding records/vector shape remain unimplemented until the approved
provider/model decision boundary is resolved. No paid provider is selected and
no public question endpoint is enabled by this migration. See the
[verification report](../evidence/phase-5/P5-T03-persistence-verification.md).

## P5-T04 implementation checkpoint — 2026-09-07

Deterministic chunk construction, the disabled-by-default embedding protocol,
and ASP.NET source reconstruction are implemented and verified. The
[chunking contract](phase-5-chunking-contract.md) records the exact wire shape,
profile, ID scheme, bounds, and evidence handling. The
[verification report](../evidence/phase-5/P5-T04-chunking-verification.md) includes
an actual Python-to-ASP.NET round trip and synthetic-pack evidence coverage.
Live indexing transport/jobs and retrieval remain P5-T05 work; provider selection
and embedding storage remain deferred.

## P5-T05 lexical implementation checkpoint — 2026-09-07

On-demand indexing, authenticated private Python transport, parameterized
organization/corpus-scoped PostgreSQL keyword retrieval, and persisted evidence
replay are implemented. `Retrieved` is an intermediate evidence-ready state,
not a generated answer or refusal. The [lexical contract](phase-5-lexical-retrieval-contract.md)
and [verification report](../evidence/phase-5/P5-T05-lexical-verification.md)
record the measured synthetic baseline and the retained paraphrase miss.
Vector/hybrid comparison, model selection, answer validation, and the question
workspace remain outstanding; this checkpoint does not complete Phase 5.

## P5-T06 validation checkpoint — 2026-09-07

The 2026-09-07 P5-T06 checkpoint adds the
[answer validation/no-hit refusal contract](phase-5-answer-validation-contract.md)
and [verification report](../evidence/phase-5/P5-T06-validation-verification.md).
Structural citation validation does not establish semantic claim support.
The question entry point can persist a no-hit refusal but does not generate
answers from nonempty evidence. P5-T06 remains in progress.

## Research basis

The design follows primary guidance rather than treating RAG as one opaque
accuracy score:

- [PostgreSQL full-text search controls](https://www.postgresql.org/docs/current/textsearch-controls.html)
  define safe web-style query parsing and lexical ranking.
- [pgvector's official documentation](https://github.com/pgvector/pgvector/blob/master/README.md)
  describes exact versus approximate search and recommends PostgreSQL full-text
  search plus RRF or a cross-encoder for hybrid retrieval.
- [Microsoft's RAG design and evaluation guide](https://learn.microsoft.com/en-us/azure/architecture/ai-ml/guide/rag/rag-solution-design-and-evaluation-guide)
  recommends representative test documents/questions and evaluating chunking,
  retrieval, and generation as distinct stages.
- [Microsoft's chunking guidance](https://learn.microsoft.com/en-us/azure/architecture/ai-ml/guide/rag/rag-chunking-phase)
  treats invoices as structured or semi-structured documents and recommends
  choosing chunking from document structure and measured results.
- [OWASP's RAG Security Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/RAG_Security_Cheat_Sheet.html)
  treats retrieved documents as an injection and cross-tenant attack surface.
