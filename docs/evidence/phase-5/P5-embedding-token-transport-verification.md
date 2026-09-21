# Phase 5 — measured embedding requests and private transport

**Date:** 2026-09-10 (UAE). Components verified; canonical coordinator and live
embedding dispatch are not connected yet.

## What changed

The embedding path now has a pinned, local token counter, an explicit HTTP
transport and a private preparation/outcome contract. Default/public applications
do not import or register a live embedding adapter or route.

- `cited_embedding_tokens.py`: `tiktoken==0.12.0`, `cl100k_base`, pinned vocabulary
  SHA-256 `223921b76ee99bde995b7ff738513eef100fb51d18c93597a113bcffe865b2a7`.
  Reads and hashes the local vocabulary once, then constructs an ordinary-text
  encoding from those exact bytes and the pinned library's pattern. It does not
  use an ambient cache loader that could download data during measurement.
  Missing/corrupt vocabulary or wrong package version fails closed.
- New wire version `openai-embeddings-token-ids-v1` sends integer token arrays
  for `text-embedding-3-small`, 1536 dimensions, float output. Token IDs round-trip
  to the exact source UTF-8. Special-token-looking document strings remain literal
  text. The earlier pure text-wire protocol remains unchanged; the new measured
  binding additionally includes the counter, vocabulary, token wire and counts.
- A pilot request has at most four inputs and 8,000 total tokens, matching the
  shared budget ceiling. It is not silently truncated or split into paid calls.
  The private source/preparation still binds org, actor, operation, purpose,
  indexes, input identities and exact text hashes. These are local bindings,
  not a substitute for ASP.NET source authorization.
- `cited_embedding_transport.py`: explicit key and enablement only, fixed HTTPS
  `/v1/embeddings`, exact measured bytes, certificate verification, no retries,
  redirects, environment proxies or model fallback. Thirty-second total timeout,
  five-second connect timeout; cancellation propagates. Errors retain only fixed
  codes/status and sanitized request IDs, not provider error bodies or secrets.
- `cited_embedding_workflow.py`: unregistered private schema `embedding-pilot-v1`.
  Preparation counts locally and verifies the complete saved dispatch size.
  Proposal rebuilds the receipt, sends once per invocation and preserves raw
  response/usage without publishing. The caller must still enforce durable
  at-most-once dispatch; invoking this function twice is not protected by a ledger.
- Known usage survives unusable vectors. A provider/local token discrepancy keeps
  reported usage (never clamps it), rejects vectors and requires reconciliation.
  Unknown usage remains null, not a zero-cost result. The canonical coordinator
  must independently verify the raw usage and settle/freeze accordingly.

The official guide specifies `cl100k_base` for third-generation embeddings;
the API accepts token arrays and reports input usage as `prompt_tokens` and
`total_tokens`. These details drove the counter, wire format and audit contract.
[Embedding guide](https://developers.openai.com/api/docs/guides/embeddings),
[create-embeddings reference](https://developers.openai.com/api/reference/python/resources/embeddings/methods/create).

## Payload sizes

The private source is capped at 49,152 bytes; its exact base64-source-plus-receipt
dispatch must fit the existing 98,304-byte preparation journal limit. The HTTP
response is capped at 256 KiB, below the older pure parser's 4 MiB allowance.
The outcome saves raw response bytes once as base64, without duplicate vector
arrays; metadata is capped at 8 KiB and the complete envelope at 512 KiB. The
maximum-sized malformed-response test confirms that unknown outcomes still fit
the journal. An overlarge/truncated provider response remains uncertain, not free.

## Verification

- **166 Python checks passed:** 152 cited tests, eight PDF-evaluation tests in the
  existing native-PDF runtime, and six offline quality-report tests.
- 22 new tests: eight tokenization, eight HTTP transport and six private workflow.
  They include official-constructor token parity, fixed known token IDs, exact
  8,000-token boundary, malformed receipts, literal Unicode/special markers,
  offline loading, corrupted assets, size caps, missing/changed usage, no retries,
  cancellation and sanitized errors. Provider responses were mocked, not live.
- The independent frozen 30-question set was counted locally: **299 total input
  tokens**, six to sixteen per question. This is not an invoice-chunk token count,
  an embedding output or retrieval-quality measurement. Dataset SHA-256 remains
  `bcec1450d0c3d458417d274de57e0c5a4b3ce0824b3d756eb0d73f41374655be`.
- Public vocabulary download and offline readiness check succeeded; `pip check`
  reports no broken installed requirements. The dependency is an explicit
  `embedding-pilot` optional extra, not a default app requirement.
- No backend changes or rerun in this increment. The preceding 503 distinct
  backend checks remain prior evidence, not newly executed tests.
- Documentation build and whitespace check passed; the existing large-bundle
  warning remains. Frozen paid preparation/evaluation hashes are unchanged. The Python
  run emitted a Starlette/httpx deprecation warning; no dependency migration was
  attempted as part of embedding work.

## Local setup and continuation

The public vocabulary lives under the ignored `.venv/share/tiktoken/` directory.
It contains no customer data or API key. A clean environment needs explicit
provisioning before running the new embedding tests:

```powershell
& .venv/Scripts/python.exe -m pip install --only-binary=:all: tiktoken==0.12.0
& .venv/Scripts/python.exe tools/phase5_prepare_tokenizer.py --download
& .venv/Scripts/python.exe tools/phase5_prepare_tokenizer.py
```

The downloader contacts only the pinned public vocabulary URL, verifies SHA-256
before an exclusive write, and refuses to overwrite a corrupt existing file.
Normal tokenizer construction never downloads or rewrites the asset.

Next implement the ASP.NET embedding client/coordinator and its independent
outcome verification. It must load current canonical eligible sources, bind the
private contract, store the preparation and reserve `Workload=Embedding` in the
same existing ledger, commit dispatch before HTTP, and settle/save outcomes before
vector acceptance. Recheck source eligibility on results and replay. Add private
service/runner enablement only with a frozen synthetic plan and current rates.
Then implement canonical organization/source/model/dimension-bound vector storage
and the lexical/vector/hybrid comparison against the independent question set.

**No paid embedding calls, API key reads, retained-DB writes, migrations, rate
extensions or public enablement occurred.** The retained DB remains migration 13,
with the prior 11 settled generation calls / 22 artifacts / USD 0.001951. This
increment does not prove better retrieval or answer accuracy and does not complete
Phase 5. Preserve the failed 4/10 live answer baseline and existing paid budget.
