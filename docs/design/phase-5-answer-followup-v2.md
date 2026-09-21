# Thirty-case answer follow-up

Frozen before new generation on 2026-09-10. This development evaluation combines
prompt v3, the unchanged payment-event restriction and nonredundant hybrid v1.
It preserves the failed ten-case baseline and the original 30-question labels.

The manifest is `samples/phase-5/answer-followup-v0.2.0/plan.json`, SHA256
`f342359d97a8cb176013d4aed01a1833116b0a1fe510a294162586b494b8b752`.
Its 30 retrieval IDs bind exact questions, selected indexes and policy versions.
All have evidence. One attempt per retrieval, no automatic retry, at most 30
generation calls; each reserves USD 0.002, at most USD 0.06 additional under the
existing shared USD 8 ceiling, including the retained USD 0.000001 unknown hold.

`evidence-conditioned-refusals-v1` changes only the reference reasons: q025–q027
expect incomplete_evidence when nonempty selected evidence lacks the requested
fact; q030 expects unsupported_scope for a foreign-organization request.
q028–q029 retain unsupported_scope. No expected answer facts change. The foreign
mirror is absent, so q030 measures refusal behavior, not live tenant isolation.
Status, reason, factual completeness and citation support are scored separately.
Every factual claim must be supported by its actual cited spans; alternate valid
source passages may support an answer without matching the retrieval label quote.
Review each proposal manually against sources; structural validity alone is not
semantic correctness. Publish failures and raw outcomes without rewriting them.

The [official GPT-5 nano model page](https://developers.openai.com/api/docs/models/gpt-5-nano)
was checked on September 10: the selected snapshot remains
gpt-5-nano-2025-08-07, with standard input/cached-input/output rates of
USD 0.05/0.005/0.40 per million tokens. The new admission-rate version is valid
September 10–11 UTC; saved historical receipts retain their original versions.
The [Structured Outputs guide](https://developers.openai.com/api/docs/guides/structured-outputs)
supports nested anyOf with an object root, matching the pre-existing v3 envelope.

No model change, public generation enablement or automatic semantic acceptance
is implied. Public integration follows quality and security acceptance.
