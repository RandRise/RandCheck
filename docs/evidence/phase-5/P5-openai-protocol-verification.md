# Phase 5 — OpenAI request and accounting protocol

**Date:** 2026-09-09  
**Outcome:** Provider-specific request construction and reply/accounting parsing
implemented and verified offline. Live transport and ledger integration remain
unfinished; no paid generation, counting or embedding call occurred in this increment.

## Implemented

`apps/api/app/cited_openai_protocol.py` is a pure module with no network, credential,
environment or database access. No existing route, adapter registration, runtime
setting, dependency or migration changed. The production coordinator still
rejects non-test providers and the private Python routes remain disabled by default.

The request builder pins `gpt-5-nano-2025-08-07` and revalidates the complete
versioned source packet. It serializes a bounded, immutable Responses request
with strict JSON schema, minimal reasoning, bounded output, standard service tier,
no tools, no streaming/background work, `store=false` and disabled truncation.
It does not send internal organization/actor/retrieval IDs as provider metadata.

The counting payload is derived from the same request and includes the model,
instructions, input, reasoning, output schema and tool/truncation controls
accepted by the [official counting endpoint](https://developers.openai.com/api/reference/typescript/resources/responses/subresources/input_tokens/methods/count).
Generation-only options are not sent to that endpoint. Separate wire hashes and
the original context-bound source hash prevent a receipt being reused after a
prompt/limit change or across an identical prompt in another organization.
This constructs the requests; it has **not obtained a real provider token count**.

Reply parsing separates accounting from answer acceptance. Terminal incomplete,
failed, cancelled, refused and malformed-answer responses can retain valid usage.
Missing or contradictory usage, unexpected model/tier/tools/modalities, unknown
metering fields or nonzero unpriced cache-write usage fail closed instead of
becoming zero cost. Reported overruns are retained and detectable, not clamped.
The [Responses reference](https://developers.openai.com/api/reference/typescript/resources/responses/methods/create)
defines the provider usage and terminal response fields used here.

Rate arithmetic uses decimal values and distinguishes cached input from ordinary
input. Output already includes reasoning tokens, so they are not charged twice.
Exact token-rate cost is retained separately from the ceiling-rounded microdollar
amount required by the existing ledger. See the
[reasoning accounting guide](https://developers.openai.com/api/docs/guides/reasoning).

The [published nano rates](https://developers.openai.com/api/docs/models/gpt-5-nano)
checked today are USD 0.05 input, 0.005 cached input and 0.40 output per million
tokens. The initial rate version is valid only from 2026-09-09 00:00 UTC until
2026-09-10 00:00 UTC; outside that window quoting fails closed. Recheck rates and
lifecycle before deliberately versioning a later rate window. This is not a
provider invoice, tax calculation or account-level billing reconciliation.

## Verification

**90 Python cited tests passed**, zero failures/skips, across the existing two
runtimes: 70 component tests and 20 route tests. The component run includes
**24 new protocol cases**; the initial focused 46-test run is not counted twice.

Coverage includes immutable requests and schema, forged source/wire/receipt
rejection, same-text foreign-context binding, Unicode/injected evidence, bounded
and duplicate-key JSON, invalid counts, cached/reasoning usage, rounding, stale
rates, refusals, incomplete replies, invalid outputs, unpriced fields, overruns,
sanitized exceptions, and no socket/environment access. Existing disabled-route
tests still pass.

```powershell
$env:PYTHONPATH=(Join-Path (Get-Location) 'apps/api')+';'+(Join-Path (Get-Location) 'apps/api/tests')
& tmp/phase5-native-runtime-20260908/Scripts/python.exe -m unittest test_cited_openai_protocol test_cited_answers test_cited_prompt test_cited_chunking test_cited_embeddings test_cited_question_evaluation
& .venv/Scripts/python.exe -m unittest discover -s apps/api/tests -p 'test_cited*routes.py'
```

Ruff is unavailable in `.venv`; no lint pass is claimed and no package was installed.
The existing Starlette/httpx deprecation notice remains. Full .NET, PostgreSQL,
OCR and browser acceptance were not rerun: their source/schema is unchanged in
this increment. One reproducible complete Python runtime remains a roadmap gate.

## Remaining integration, not another user decision

These parsed objects and hashes are **not authority or proof**. A caller can
construct them; the future trusted transport must bind the actual provider
request/response and account/project to the correct canonical ledger operation.
Never settle money merely because this parser accepted a caller-supplied object.

Next, persist readable preparation, request identity, usage and unpublished
result records; add the bounded no-retry/no-redirect transport; and make the
coordinator commit dispatch before sending the frozen request. Preserve known
usage even if proposal validation fails. Preserve uncertain holds if transport,
parsing, pricing or commit outcomes are unknown. Replays must return stored
results without a new request, after current scope/source checks.

The user already authorized the low-cost synthetic pilot and the previous
[readiness check](P5-live-model-readiness-check.md) verified model-metadata access.
The first generation smoke test still requires the above integration and a
current verified rate window. Count-endpoint privacy/cost behavior, funded quota,
provider controls and complete billing reconciliation still need verification.
Do not bypass the ledger or release an uncertain hold to get a test through.
Semantic support, citation acceptance, real invoice accuracy, embeddings and
public generated answers remain separate [roadmap gates](../../roadmap.md).
