# Phase 5 answer validation and no-hit refusal

**Checkpoint:** 2026-09-07 — provider-independent P5-T06 components.

This extends the [cited-question contract](phase-5-cited-question-contract.md)
and [lexical API](phase-5-lexical-retrieval-contract.md). It adds strict
proposal validation and a narrow, deterministic no-hit refusal. It does not
enable model-written answers or establish semantic support for a claim.

## Private pilot follow-up — 2026-09-10

The [quality fixes](../evidence/phase-5/P5-quality-fixes-verification.md) retain
this canonical proposal contract and structural validator. Prompt v3 adds an
OpenAI-only wire wrapper, `{"answer": <proposal>}`, whose nested schema constrains
the status/list/reason combinations below. The private adapter unwraps it without
repair and preserves the original response bytes. This does not change a public
API, enable publication or retroactively rewrite historical v1/v2 outcomes.

After structural validation, the coordinator applies the conservative English
`payment-event-restriction-v1` check to new results and settled replay. Payment
events have no verified source in the current pilot; detected event questions or
assertions are not accepted as factual answers. An invoice's dates, terms,
method, amount due or untrusted PAID stamp cannot grant verified-event status.
This restricted policy is not a general semantic validator and can both over-block
wording and miss untested paraphrases. Semantic/publication flags remain false.

## Development question API

`POST /api/cited-questions` accepts the same bounded `question`, `requestKey`,
`indexIds`, and optional `topK` as the existing evidence endpoint.
`GET /api/cited-questions/{retrievalRunId}` reloads the result without writing.
Both use the existing execution identity and organization/actor-scoped history.
Production authentication remains Phase 6 work.

| Stage | Retrieval | Answer |
|---|---|---|
| `EvidenceReady` | Retrieved, evidence available or not yet finalized | null; no generated answer |
| `InsufficientEvidence` | Completed, zero hits | Saved no-hit refusal with stable ID and reason |
| `Running` / `Failed` | Original retrieval state | null; never disguised as refusal success |

Every response explicitly sets `answerGenerationEnabled: false`. The original
`/api/cited-retrievals` endpoints remain evidence-only and do not finalize
answers. Request-key scope is shared: organization, actor, and identical
question/corpus/top-K/policy. A question POST can finalize an existing zero-hit
Retrieved record. A GET cannot. Replaying an unfinished Running request still
returns 409, as documented in P5-T05.

New POST requests have a 45-second linked deadline including retrieval and
refusal persistence; question GET has a 15-second deadline. The existing
retrieval/private transport bounds still apply.

## No-hit refusal policy

`no-hits-refusal-v1` applies only after successful retrieval with zero hits.
The server, not provider text or a client flag, selects this outcome:

- Answer status: `InsufficientEvidence`.
- Reason: `no_relevant_evidence`.
- Claims/citations: empty.
- Provider/model: `evidence-only` / `none`.
- Generation duration: zero; model cost: null, because no model ran.
- Retrieval duration: the existing frozen value, unchanged.

The response message says that the selected retrieval policy found no matching
evidence. It does **not** claim the fact is absent from the original documents:
the known lexical paraphrase miss remains a counterexample.

The answer and Completed transition save atomically. Concurrency conflict
reloads the winning refusal; repeated requests retain the same answer ID.
No-hit completion does not reread document contents or invoke a model. If a
POST is interrupted after retrieval commits, a later identical POST can finish
the Retrieved record. This does not solve crashed Running retrieval recovery.

Nonempty hits never automatically become Answered, PartiallyAnswered, or a
semantic refusal. Other refusal reasons and supported answers require later
evidence-sufficiency/support checks. This endpoint rejects previously completed
answer modes it cannot yet represent rather than silently dropping their claims.

## Untrusted answer proposal

The internal `CitedAnswerValidator.Validate` component accepts UTF-8 JSON and
the server-loaded Retrieved run and exact retrieved chunk set. No public route
accepts model proposals or user-supplied answer completion.

```json
{
  "schemaVersion": "1.0",
  "status": "Answered",
  "reasonCode": null,
  "claims": [
    {
      "text": "The invoice states a total of 100.00.",
      "citations": [{ "hitId": "opaque-hit-guid", "spanId": "opaque-span-guid" }]
    }
  ],
  "unansweredParts": []
}
```

The GUID placeholders illustrate references, not executable example IDs.
The proposal has no separate answer body, filename, source ID, quote, page,
polygon, URL, organization, provider, model, or cost fields. Those cannot be
overridden by adding properties: unknown properties are rejected at every level.

Validation policy `citation-structure-v1` enforces:

- At most 65,536 actual UTF-8 bytes and JSON depth 12; valid UTF-8; no trailing
  commas, comments, duplicate properties, unknown fields, or missing required
  fields. Field names/statuses are case-sensitive; numeric statuses are rejected.
- At most 20 unique nonblank trimmed claims, each at most 4,000 characters;
  1–8 unique citations per claim; no unsupported control characters in text.
- Answered requires claims and no reason/unanswered parts. Refusal requires
  an allowed reason and no claims/unanswered parts. Partial proposals require
  claims, a reason, and 1–20 explicit unique unanswered parts, each at most
  1,000 characters. Partial proposals are structurally checked only: their
  unanswered parts are not yet persisted or published by this checkpoint.
- `no_relevant_evidence` cannot be asserted with nonzero hits in this baseline.
- Every loaded chunk must match exactly one hit in the run; no omitted,
  duplicate, extra, nonretrieved, or foreign-organization chunk substitutions.
  Frozen source membership and content hashes are checked.
- Every cited hit belongs to the same run; every cited span belongs to that
  hit's chunk, organization, and extraction. Page numbers and UTF-8 quote
  lengths must be valid. PageText quotes must equal their source chunk text.
- A missing span is allowed only for a reviewed fact retaining a review action.
  Corrected facts keep review provenance separate from original page evidence;
  an old machine quote is not relabelled as proof of a corrected value.

Canonical-page byte equality remains enforced by the original index assembly
and PostgreSQL span checks. This validator does not reload all original pages,
and it is not protection against a privileged writer rewriting trusted history.

## Structural validity is not semantic validity

The result includes resolved server evidence and explicitly reports
`semanticSupportEstablished: false`. Validation has no persistence side effects
and does not call `Complete`. A false claim with a real citation can pass
structural checks: a test deliberately demonstrates this boundary.

An eventual answer pipeline must additionally establish relevance, claim
support, question coverage, conflicting-source handling, refusal correctness,
and prompt-injection controls before publishing factual text. Citation
existence, an exact quote, or a high retrieval score alone cannot prove these.
No model adapter consumes the validation result at this checkpoint.

## Provider/budget boundary

Rand accepted a USD 10 starting API-test budget in this task. No paid calls,
credentials, API billing setup, embedding dimension, or answer model are enabled
by this change. Before paid tests: configure credentials securely, select the
models, measure token usage, and implement application-side budget enforcement.
Accepting the budget is not evidence that any of those prerequisites exist.
