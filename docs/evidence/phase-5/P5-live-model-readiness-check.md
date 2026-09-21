# Phase 5 — Live-model access readiness

**Checked:** 2026-09-09 11:55:20 UTC  
**Outcome:** Key authentication and selected-model metadata access verified;
real-model invoice answer evaluation **not run**.

Rand supplied the key locally and authorized the agreed low-cost synthetic pilot.
The key was loaded in process memory from the ignored root `.env`, never printed,
copied into evidence, passed on a command line or sent to another destination.
The file is also excluded from the root Docker build context.

## Checks performed

- One authenticated GET to OpenAI's `/v1/models/gpt-5-nano-2025-08-07` returned
  HTTP 200 with the exact requested model ID and `object=model`. Redirects were
  disabled, the request had a 20-second timeout and a bounded response buffer.
  No automatic retry or model fallback occurred.
- No generation, embedding, token-count, upload or other POST request was made.
  No question, invoice text, image or PDF was sent. There is no generation token
  usage or measured generation cost to report.
- 22 local tests passed via `test_cited_answers` and `test_cited_prompt`, including
  disabled generation and rejection of a synthetic counter for a live model.
  These are offline tests, not real-model quality or billing evidence.

The [model metadata endpoint](https://developers.openai.com/api/reference/resources/models/methods/retrieve)
does not establish funded credit, Responses write permission, available quota,
provider-side spending controls or a successful generation request.

The [official model page](https://developers.openai.com/api/docs/models/gpt-5-nano)
was rechecked: standard prices remain USD 0.05 input / 0.005 cached input / 0.40
output per million tokens. These are published rates, not a verified account bill.
The [deprecation schedule](https://developers.openai.com/api/docs/deprecations)
still lists December 11, 2026 for the selected snapshot's shutdown. No more
expensive replacement was selected.

## Why answer testing has not started

The application's coordinator explicitly rejects non-test providers. Python
still has a disabled answer adapter and only a synthetic byte counter. The
existing settlement path labels usage as test usage, not authoritative provider
accounting. A saved key does not complete these missing implementation steps.

Next implementation must bind the exact provider request and real token count
to the existing durable reservation, commit dispatch before the request, persist
provider identity/usage and unpublished results, and retain uncertain-cost holds
without automatic retry. Verify those paths before a small synthetic smoke test,
then run the frozen invoice-question/adversarial evaluation. Keep the shared
USD 8 admission ceiling and USD 2 reserve; no separate bypass script or ledger
reset should obtain a fresh allowance. Public generated answers remain disabled.

This check does not close the broader [roadmap gates](../../roadmap.md).
