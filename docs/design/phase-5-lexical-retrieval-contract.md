# Phase 5 live indexing and lexical retrieval

**Checkpoint:** 2026-09-07 — provider-independent P5-T05 implementation.

This contract extends the [chunking contract](phase-5-chunking-contract.md).
It does not select an embedding or answer model. P5-T05 remains in progress
until lexical, exact-vector, and hybrid retrieval have been compared.

## Public development API

| Endpoint | Request | Result |
|---|---|---|
| `POST /api/cited-indexes` | `documentId`, `reviewSessionId` | Ready index metadata, chunk count, replay flag |
| `POST /api/cited-retrievals` | `question`, `requestKey`, `indexIds`, optional `topK` (default 5) | Persisted retrieval ID/status, policy, duration, ranked evidence |
| `GET /api/cited-retrievals/{id}` | ID | Organization-and-actor-scoped retrieval history |

The endpoints currently inherit the application's fixed development identity.
Organization predicates and hostile-scope tests are implemented; production
authentication, role authorization, and deployment exposure are not proved.
No public endpoint accepts an organization or actor override.

Evidence hits include document ID/filename, index/chunk IDs, extraction and
review IDs, rank/lexical score, content, optional review action, and exact
page/UTF-8 spans. Review-only facts can have no page span: callers must not
invent one. This is raw, untrusted evidence, not rendered HTML or an answer.

`Retrieved` means the evidence stage finished, including when there are zero
hits. It is distinct from an answered or refused question. Later validation
may complete or fail the run; current retrieval endpoints generate neither.

## Index construction and replay

ASP.NET loads the organization-scoped document, canonical extraction pages,
fields/evidence, and completed review history. Python constructs chunks from
the snapshot through `POST /internal/cited/chunks`; ASP.NET independently
reconstructs and verifies the proposal before saving the complete Ready graph.
The profile remains `page-lines/1.0.0-b2048-o256`.

There is one index per organization, review, and profile. Repeated or racing
requests resolve to the same stored index. Retired/quarantined indexes are
not silently republished. No partly built index is saved by this workflow.
Indexing is synchronous and on demand: there is no new background job,
automatic review-completion trigger, or durable retry queue.

## Versioned lexical policy

Python's `lexical-or-english-v1` extracts distinct lowercase literal tokens,
including identifier punctuation inside tokens, and joins quoted terms with
OR. It does not honor user-provided Boolean operators, add invoice synonyms,
call a model, or rewrite questions based on evaluation labels. PostgreSQL's
fixed English configuration performs stemming and stopword handling. A query
with no searchable terms returns no hits; it never falls back to all documents.

ASP.NET verifies the schema, policy, question hash, configuration, and top-K
binding. Its repository executes parameterized PostgreSQL SQL using
`websearch_to_tsquery`, `to_tsvector`, and `ts_rank_cd`. Organization and the
explicit pinned index set are predicates on candidates before ordering and
LIMIT. Ordering is score descending, then chunk ID ascending for stable ties
within a stored corpus. Scores are ranking signals, not confidence estimates.

Python owns the versioned lexical plan; ASP.NET owns authorization, persistence,
and database execution. This division does not implement the deferred
model-dependent vector/hybrid orchestration. The lexical primitives follow
[PostgreSQL 16 full-text search guidance](https://www.postgresql.org/docs/16/textsearch-controls.html).

## Transactions, state, and idempotency

A request key is unique within organization and actor. Identical trimmed
question, index set, top-K, and policy replay stored history without calling
Python. Reusing a key with different inputs returns 409. A Running request
also returns 409 on retry; it is never reported as successful.

New requests require every selected index to be Ready. The retrieval transaction
locks the scoped index rows with `FOR SHARE` and rechecks eligibility before
searching. Retirement waits through hit persistence, so a run cannot combine
pre-retirement ranking with post-retirement eligibility. Ranked hits and the
Retrieved transition commit atomically. Domain methods freeze the hit list;
tracked-write guards also prohibit restarting Retrieved runs or changing their
recorded retrieval duration.

The stored audit snapshot retains the original result after retirement.
As of P5-T08 (2026-09-08), public history and same-key replay check every frozen
source's current eligibility before returning content, including zero-hit runs.
A non-Ready or missing source produces 409 `evidence_unavailable`; the record
is not deleted or rewritten. New requests reject ineligible indexes. These
application/relationship controls are not blanket protection against a
privileged database writer or atomic erasure of already displayed browser data.

On an execution failure, bounded best-effort cleanup marks a still-Running
record Failed. A process crash or unavailable database can leave Running
history. There is no stale-run recovery worker: inspect the history and use a
new key to retry. Failure cleanup does not overwrite committed Retrieved runs.

## Bounds and safe failures

- Questions: nonblank, at most 4,000 characters; keys: nonblank, trimmed,
  at most 100 characters; corpus: 1–100 unique nonempty IDs; top-K: 1–20.
- Python lexical planning: at most 64 distinct tokens, each at most 100
  characters. Exceeding these bounds is rejected, not silently truncated.
- Both private routes authenticate the configured worker token before body
  parsing; absent or wrong tokens receive 404. No token means no dispatch.
- Private chunk body/response: 16 MiB; lexical request: 32 KiB. Streaming
  checks enforce actual bytes, not only Content-Length.
- Private HTTP deadline: 30 seconds; indexing/retrieval handler deadline:
  45 seconds; explicit SQL search/lock command timeouts: 15 seconds;
  best-effort failure cleanup: 3 seconds.
- Corpus validation loads metadata, not every selected index's chunk graph.
  Hit reconstruction loads only returned chunks/spans and their source metadata.
- Private failures expose safe error codes/messages, not source bodies.
  There are no external model calls or model charges in this workflow.

## Migration and remaining work

Migration `20260907013111_AddLexicalRetrieval` (ninth migration) adds the
Retrieved state checks and an English `to_tsvector(content)` expression GIN
index. The query uses that same fixed expression. The index is migration-owned
SQL, not an EF property; no vector extension or embedding shape is added.

Downgrade removes the GIN index and maps Retrieved rows to Failed with
`schema_downgrade`, preserving their hits and source history. Downgrade is an
explicit migration operation, not automatic recovery.

Remaining work includes vector/hybrid comparison, claim/citation validation,
answer/refusal orchestration, authorized evidence navigation, the question UI,
larger real-document evaluation, and full Phase 5 security/acceptance testing.
See the [verification report](../evidence/phase-5/P5-T05-lexical-verification.md).
