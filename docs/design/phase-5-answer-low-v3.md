# Controlled low-reasoning comparison

Frozen September 10 before dispatch. The preceding prompt-v3/minimal-reasoning
evaluation passed 19/30 end to end, despite 30/30 structural acceptance. Preserve
all outputs, including missing comparison citations, omitted documents, the wrong
tax qualifier, TRN/TIN mismatch and refusal-status/reason errors.

This version changes only reasoning effort from minimal to low on the same
gpt-5-nano-2025-08-07 snapshot. Prompt v3, response schema, selected source content,
retrieval ranks/scores, 30 questions, expected claims, refusal references, token
limits and shared USD 8 ceiling remain unchanged. All 30 reranked evidence lists
were compared field-for-field, excluding new hit IDs, and match the prior lists.
New request keys/run IDs distinguish this deliberate versioned attempt; earlier
operations cannot be reopened or overwritten. This is not a blind automatic retry.

Manifest: `samples/phase-5/answer-low-v0.3.0/plan.json`, SHA256
`ffca5a39729d0f5a57f72212afa54618824322e0e000bbed9183ff5d41ab7321`.
One call per case, at most 30 calls and USD 0.06 reserved. The private
`nano-low-v1` worker profile binds low effort into both count/generation hashes
and its explicit receipt. ASP.NET requires that receipt only for the matching
internal settings profile and checks the returned provider effort. Public API
requests cannot select a profile or price. No new public enablement.

The [official reasoning guide](https://developers.openai.com/api/docs/guides/reasoning)
describes the quality/latency tradeoff; model-specific settings are retained for
the approved GPT-5 nano pilot. No quality improvement is assumed before scoring.
Use the same digest-bound manual assessment rubric and report failures. All
earlier cited limitations, including development-only data and the absent foreign
mirror, remain in effect.
