# Phase 5 embedding storage

**2026-09-10 integration update:** The private source loader, accounting/coordinator,
loopback client and standalone pilot now implement ingestion through this storage
port. Nine real chunk vectors and one saved query outcome passed live settlement
and offline replay. See the [live verification](../evidence/phase-5/P5-live-embedding-ingestion-verification.md).
Public dispatch remains disabled. Subsequent [semantic retrieval integration](phase-5-semantic-retrieval-contract.md)
now persists versioned vector/hybrid runs; fixed-set evaluation remains open.
Historical no-live-ingestion statements below describe
the earlier storage-only increment.

**Scope:** internal PostgreSQL storage and exact search of saved embedding outcomes.
Rand approved storing invoice chunk embeddings in PostgreSQL/pgvector on 2026-09-10.
This is a development comparison profile; production model selection remains open.

## Ownership and identity

ASP.NET owns canonical persistence. Python computes embeddings but cannot insert
rows or authorize documents. `ICitedEmbeddingStore` takes a saved operation ID,
with organization and actor supplied by the server execution context. It exposes
no public route and cannot dispatch a provider request or change spending.

`cited_chunk_embeddings` stores one immutable vector per organization, chunk,
provider, model, model version, dimension and distance. Each row also pins the
index, input hash, settled reservation, outcome artifact, raw-response hash and
creation time. The index already identifies the document, completed review,
extraction revision and chunking profile. Original chunk text and evidence spans
remain the citation source; a vector does not replace them.

The current profile is `openai / text-embedding-3-small / text-embedding-3-small /
1536 / cosine`. A changed model version uses a separate identity. A changed source
or chunking profile uses a new immutable index/chunk. Changing dimensions requires
a deliberate schema/profile migration; vectors of different spaces cannot mix.

## Admission to storage

Before insertion or saved-query search, the store verifies:

1. The current actor is active in the server-selected organization.
2. The operation belongs to that actor/organization and is a settled Embedding
   reservation, using the matching model and zero output tokens.
3. Both immutable journal artifacts exist and match their stored hashes; the
   preparation matches the reservation's request hash.
4. The saved Python source, preparation, outcome and raw response agree on the
   source, scope, model, counter, token totals and vector indices. Duplicate JSON
   keys, uncertain usage, usage disagreement, wrong dimensions, nonfinite or zero
   vectors and unexpected response fields are rejected. Values are float32 in
   storage; underflow to an all-zero vector is rejected as well.
5. Settled cost matches reported input usage and the saved input-only rate.
6. Every selected index remains Ready, its document hash matches and its pinned
   review remains Completed. Source rows are locked through the transaction.
7. For chunk writes, the input ID, exact text and hash match canonical chunks in
   that selected corpus. Query inputs can never populate the chunk table.

The verifier checks **reusability**, not all billing outcomes. It must not be used
to discard known usage from invalid vector results. The pending coordinator must
settle known charges and preserve uncertain holds before attempting storage.
The storage verifier compares saved token receipts; it does not independently
reimplement the Python tokenizer or prove a live HTTP call occurred.

## Persistence and replay

The whole batch commits or rolls back. Concurrent identical replays reuse the
existing rows. Different vectors for the same chunk/profile fail with a conflict;
the existing vector is never silently replaced. Storage itself makes no paid
call and does not change the ledger, including when storage fails after settlement.

Composite foreign keys bind vectors to organization/index/chunk/content hash and
organization/reservation. A database trigger requires the associated settled
embedding outcome and eligible index, and rejects row updates/deletes. It is an
additional integrity guard; application verification still owns the full raw
response check. Privileged database administrators remain outside this boundary.

Migration 14 enables the existing pgvector extension and creates the vector table.
This table is explicitly **SQL-owned within EF migrations**, not an EF entity.
The Npgsql adapter sends real[] parameters with explicit casts to vector and reads
scalar IDs/scores. No vector-driver type or dependency crosses into Domain.
Schema tests cover the raw table because EF model-difference checks cannot detect
changes to migration-owned SQL. Downgrade refuses a nonempty vector table; an
empty-table rollback leaves the potentially shared extension installed.

## Exact retrieval boundary

`SearchQueryOutcomeAsync` verifies a settled saved query embedding and its exact
selected corpus. It requires complete vector coverage for every canonical chunk
in that corpus and profile. Missing vectors fail explicitly rather than silently
improving or worsening an evaluation by changing its denominator.

SQL filters organization, selected indexes, source hash and model profile before
cosine ranking. A materialized eligible set uses exact search, with chunk UUIDs
as stable tie-breakers and a maximum of 20 hits. Results contain chunk/index IDs
and similarity; existing chunk text and spans can supply citations later.

This internal query does not yet create a `CitedRetrievalRun`, implement hybrid
fusion, select a public retrieval policy, or demonstrate recall/answer accuracy.
Live ingestion/coordinator wiring and the fixed lexical/vector/hybrid comparison
are the next integration work. No change to the public answer flags is implied.

The representation and explicit casts follow [pgvector's storage and query
support](https://github.com/pgvector/pgvector). Raw response validation follows the
[OpenAI embedding response contract](https://developers.openai.com/api/reference/python/resources/embeddings/methods/create).
