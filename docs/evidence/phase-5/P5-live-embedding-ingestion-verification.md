# Phase 5 live embedding ingestion verification

**Date:** 2026-09-10. **Disposition:** private synthetic ingestion and offline
replay verified. Phase 5 remains in progress.

## Delivered behavior

The retained PostgreSQL database now stores nine real OpenAI invoice-chunk
embeddings, using `text-embedding-3-small`, 1,536 dimensions and cosine distance.
Three chunk operations and one query operation completed. The query searches the
selected invoice only; its vector remains in the immutable outcome journal and
does not enter the chunk-vector table.

`CitedEmbeddingSources` loads canonical, organization-scoped chunk text from
eligible reviewed indexes. `CitedEmbeddingCoordinator` validates the private
preparation, reserves the existing shared budget, commits dispatch before HTTP,
and saves/settles the outcome before invoking storage/search. Independent raw
usage checks preserve known charges even when vectors fail validation. Unknown
usage or lost transport retains its hold; there is no automatic paid retry.
Token mismatches preserve known cost and freeze admission for reconciliation.

A PostgreSQL advisory lease serializes embedding ingestion across processes,
including different operation IDs. Existing chunk/profile vectors are reused
before preparation or paid admission; partially stored batches are rejected for
explicit rebatching. Query admission requires complete selected-corpus coverage.
Caller/source checks run before preparation, after it and just before dispatch;
storage/search rechecks source eligibility afterward. Source row locks are not
held throughout the external request: retirement during that interval may still
incur a charge, but cannot publish or store an ineligible result.

The Python service is an explicit standalone factory on loopback, authenticated
with the worker token. The default API does not expose its routes. The .NET
client has bounded bodies/deadlines, no redirects/proxies and no HTTP logging.
The launcher passes the API key only to the private worker and stops both child
processes afterward. Public generation and semantic-acceptance flags remain false.

## Frozen live run

Plan: `samples/phase-5/embedding-pilot-v0.1.0/plan.json`; SHA-256
`ba9f727dccf93833ae08e630af5aa4f851e212928bb6f0b265a159a5aaa90099`.
It was verified before any dispatch and selects nine chunks, totaling 1,153 UTF-8
text bytes, from `fta-standard-services-native.pdf`. Source PDF SHA-256:
`6d629a9701b4b8cdd38d43c8449bb6fb9158b1e4f653ae8257d35715be742ca5`.
The source manifest declares every entity, identifier and transaction fictional,
CC0 licensed. No real client data or original PDF was sent.

| Operation | Purpose | Input tokens | Ledger USD | Result |
|---|---|---:|---:|---|
| 5980dbf0-d68b-56fd-ac23-aab3042da183 | Four chunks | 372 | 0.000008 | Four stored |
| 58b86a8c-9ac8-5e5b-b023-32cda155ed4f | Four chunks | 43 | 0.000001 | Four stored |
| 356dd9a6-fa90-574c-80e8-9f3dd106dcd7 | One chunk | 8 | 0.000001 | One stored |
| fb3a07bc-c6d8-5bb6-a26d-080225727600 | One query | 8 | 0.000001 | Five hits |

All 431 provider-reported input tokens matched the locally measured counts.
The [official model rate](https://developers.openai.com/api/docs/models/text-embedding-3-small),
checked 2026-09-10, is USD0.02/M standard input tokens: exact usage-rate estimate
USD0.00000862. The ledger conservatively rounds each operation upward to six
decimal places, totaling **USD0.000011**. This is application accounting from
provider usage, not a reconciled provider billing statement. New embedding
admission expires 2026-09-11 00:00 UTC; historical offline reuse is independent
of that rate window. The existing USD8 ceiling and generation rates are unchanged.

For "What amount is due on the invoice?", the first hit is source-page chunk
`652030ca-5c13-2bd7-bffd-3fc38ec6664a`, cosine similarity 0.447518738991582.
It explicitly contains "Amount due (AED):8,820.00". The remaining hits include
payment terms, invoice date, invoice number and subtotal. Similarities are not
confidence probabilities. One source-page hit does not establish general search
quality, factual answer quality or the planned 30-question comparison.

Automatic approval review first rejected execution because it lacked specific
external-payload authorization. No process or paid call ran on that attempt.
Read-only inspection of **Start phase 3**, turn
`01a08812-d801-7720-a596-d740832cb733`, verified Rand's instruction:
"its fine u can use paid embeddings it will not cost much". Source/hash inspection
also proved this was the fictional development fixture. The same launcher was
approved on re-review with this evidence; no workaround or new user approval was used.

## Replay and retained data

With the private worker stopped, `--replay-plan` injects an offline client that
throws if preparation or dispatch is attempted. All four operations replayed:
nine vectors reused, zero inserted, identical hit IDs/order/scores. Fingerprints
of every retained public table match before/after this replay. No new reservation,
artifact, spend event or cost was created.

Final retained database `phase5_e2e_20260908`, port25434:

- Migration14, pgvector0.8.1; seven documents, five indexes, 78 chunks, nine vectors.
- 11 Generation and four Embedding reservations, all settled; 30 artifacts.
- USD0.001962 total ledger settlement; zero pending holds; no reconciliation freeze.
- Pre-existing source tables, Generation reservations/artifacts and all33 original
  spend events match. The paid operations add12 expected spend events (45 total).
- The disposable `document_intelligence_test` database is removed; worker28158
  has no listener. The paid attempt report must not be deleted or overwritten.

Before-paid backup: `tmp/phase5-e2e-before-paid-embeddings-20260910.dump`, 326,610
bytes, SHA-256 `0F0D6BB49099390714D04DF263E779B671B89BCAFB0429B074527A72A0161203`.
Archive listing checked; a restore drill was not performed.

Evidence in `outputs/phase-5/embedding-pilot-20260910/` includes live/replay JSONL,
before/after state and content fingerprints, prior Generation event fingerprint,
resolved synthetic hit text, final retained state and test summary. Raw provider
vectors remain in the database/journal rather than these summaries.

## Verification and remaining work

**513 distinct backend tests passed:** 480 non-PG (111 domain, 213 application,
74 infrastructure,78 API,four architecture) and33 selected PG. This includes
nine ingestion cases for successful replay/deduplication, concurrent operation
IDs, source retirement, organization/default-disable boundaries, query coverage,
invalid vectors, token discrepancy, unknown usage and transport loss.

**165 cited Python tests passed**, including the private route's authenticated
preparation/proposal round trip with a mocked provider. The first broad run found
missing local PDF dependencies; repository-pinned pdfplumber/pdfminer/Pillow/
pypdfium2 were restored, then the suite passed. `pip check` passed. The existing
Starlette test-client deprecation warning remains.

Solution and standalone runner builds passed. The runner's first package audit
could not reach NuGet in the sandbox; the same build passed with approved network
access. Documentation build and whitespace checks passed. These results are not
a full PostgreSQL, OCR, web-browser or real-data acceptance run.

Next, persist versioned vector retrieval runs and connect hybrid retrieval. Use
a separately frozen plan to populate the remaining selected corpus, reusing
existing vectors, then compare lexical/vector/hybrid on the fixed question set.
Keep the failed4/10 answer baseline intact and public generation disabled until
the remaining quality/security gates pass.
