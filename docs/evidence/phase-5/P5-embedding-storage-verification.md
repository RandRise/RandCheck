# Phase 5 — persistent invoice embeddings

**Date:** 2026-09-10. PostgreSQL storage and internal exact search implemented and
verified. Live embedding ingestion/coordinator integration remains open.

## Result

Migration `20260910005624_AddCitedChunkEmbeddings` enables pgvector and adds
`cited_chunk_embeddings`. Canonical chunk text, evidence spans and revision
relationships remain unchanged. Vectors carry source-hash, model/profile and
settled-provider-outcome provenance. Identical replay reuses stored rows;
conflicting vectors cannot overwrite them. Batch failure rolls back all new rows.

`ICitedEmbeddingStore` takes only an operation ID. It revalidates the current
actor, selected source eligibility, immutable journal hashes, raw provider usage,
vector shape and recorded settlement before writing. Query embeddings never
enter the invoice vector table. Its internal exact cosine search filters source
and organization before ranking and rejects incomplete corpus coverage.

See the [storage contract](../../design/phase-5-embedding-storage-contract.md) for
the exact ownership, locks, model identity, SQL integrity checks and limitations.
The vector table is deliberately SQL-owned inside EF migrations; no new EF
vector entity/driver package is needed. Model-snapshot alignment alone does not
verify this raw schema, so PostgreSQL schema and migration tests cover it.

The provider's indexed vector list and input-usage fields follow the
[official embedding response contract](https://developers.openai.com/api/reference/python/resources/embeddings/methods/create).
The database representation and cosine operator follow
[pgvector](https://github.com/pgvector/pgvector).

## Verification

**504 distinct backend tests passed**, no failures or skips:

- 464 non-persistence tests: 111 domain, 204 application, 67 infrastructure,
  78 API and four architecture checks.
- 40 selected PostgreSQL tests: the new storage/search cases plus existing
  embedding ledger, journal, lexical retrieval, schema and migration checks.
- 36 new cases: 20 application and 16 PostgreSQL. They cover malformed/unbound
  artifacts, provider index order, usage mismatch, wrong dimensions, all-zero
  and underflow vectors, concurrent replay, source/actor/tenant restrictions,
  changed text, incomplete corpus, stable cosine ordering, unselected indexes,
  query-versus-chunk separation, conflicting batches, immutability and downgrade.
- An earlier focused PostgreSQL run passed 16 cases before two further storage
  tests and cancellation propagation were added. The final 40-case run covers
  the final code; earlier executions are not added to the distinct total.

`tools/phase5_embedding_storage_fixture.py` generated a deterministic fixture with
the actual Python preparation/outcome code, pinned local tokenizer and synthetic
vectors. The .NET verifier accepts it, including mixed Unicode text and its real
22-token count. No API key, HTTP call or provider embedding is involved. The
other C# fixtures use explicitly mocked receipts and unit vectors.

Solution and synthetic runner builds passed with zero warnings/errors. EF reports
no pending modeled changes. Documentation builds and whitespace checks passed;
the existing VitePress large-bundle warning remains. The initial sandboxed docs
build failed on parent-directory traversal; the same local build succeeded with
the approved execution escalation. No permission question remained blocked.

Final test evidence:

- `tmp/phase5-test-results/phase5-embedding-storage-final_net8.0_*.trx`
- `tmp/phase5-test-results/phase5-embedding-storage-final-pg.trx`

This is not a full PostgreSQL/OCR/Python/web-browser acceptance rerun. Existing
Python production modules were not changed; earlier 166-test Python results are
historical. No live-model accuracy or retrieval-recall improvement is claimed.

## Retained database

After the tests passed, migration 14 was applied to `phase5_e2e_20260908` on local
PostgreSQL port 25434. The new vector extension reports version 0.8.1. Every
pre-existing application table's row count/content fingerprint matches before
and after; these MD5 summaries detect accidental changes, not malicious tampering.

- Seven documents, five indexes and 78 chunks remain intact.
- 11 Generation operations, zero Embedding operations and 22 journal artifacts.
- USD 0.001951 settled, zero pending holds, no reconciliation freeze.
- **Zero retained vectors:** only synthetic vectors were written to the disposable
  test database. The live embedding pipeline has not yet populated this table.
- Disposable `document_intelligence_test` was removed by the test fixture.

Before-migration backup: `tmp/phase5-e2e-before-embedding-storage-20260910.dump`,
319,296 bytes. SHA-256:
`486C0EF504895A88ACED2AFCB476F5DEAEEB1C28D0A5DAD488F5114A311FFD00`.
The archive listing was checked; a full restore drill was not performed.
Previous backups were preserved. Before/after summaries and current state are in
`outputs/phase-5/embedding-storage-20260910/`.

## Next integration

Connect the canonical source loader, private embedding client/coordinator and
explicit pilot runner to the measured Python transport. The coordinator must
authorize before sending text, validate preparation, reserve and commit dispatch
through the existing USD 8 ledger, independently account for all outcomes, and
save/settle before invoking this storage port. Its accounting must retain known
charges even when vectors are invalid; the reuse verifier cannot replace that.

Use a frozen synthetic input plan and current rate/access checks for the already
approved paid pilot. Check for existing chunk/profile vectors before a new paid
operation to avoid redundant embedding charges. Then persist versioned vector
retrieval runs, add hybrid orchestration, and compare lexical/vector/hybrid on
the fixed question set. Public generation and semantic acceptance remain false;
the failed 4/10 live-answer baseline is preserved.
