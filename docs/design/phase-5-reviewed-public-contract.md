# Reviewed-field answers in the application

September 11, 2026. The `/questions` workspace now offers **Answer from reviewed
fields** alongside the existing default evidence search. This is a bounded
English, backend-computed answer mode. Public here means an application API and
UI contract; it does not mean an Internet deployment or production identity.

## Request and source policy

`POST /api/cited-questions/reviewed` accepts `question`, `requestKey` and
`indexIds`. The limits are 4,000 question characters, a trimmed 100-character key,
and one to ten distinct indexes, with one completed revision per document.
Existing cited-request middleware enforces the 64 KiB body limit, read deadline,
safe error logging and `Cache-Control: no-store`. The handler has a 45-second
deadline. This route has no model, embedding, Python or spending dispatch.

An active actor and every selected Ready index must belong to the execution
organization. A PostgreSQL transaction locks actor/index eligibility through
commit. The existing reviewed-fact projection verifies completed decisions,
canonical chunks, source hashes and review/page provenance. Clear or missing
fields supply no answer operand. Selected documents must have unambiguous roles;
multiple documents with the same role refuse until the corpus is narrowed.

The new run policy is `reviewed-facts-corpus-v1`. It records the full selected
corpus, source digests, `reviewed-fields-from-selected-corpus-v1`, answer policy
`reviewed-fact-answer-v1.2`, English policy `bounded-english-fact-intents-v1.2`,
and missing parts. It considers all reviewed fields in that corpus. It is not
the old top-five lexical/hybrid ranking policy and its evaluation is separate.

## Answers and persistence

Supported questions select identifiers, dates, amounts, agreement, absolute
differences, maxima, and bounded training/onboarding line breakdowns. TRN and
TIN remain separate strings with leading zeroes. Amounts use decimal arithmetic
and reviewed currency. A date must be explicitly reviewed; payment terms cannot
manufacture one. Buyer TIN and compound supplier/buyer identifiers refuse.
Unsupported legal, payment-event and foreign-source questions refuse.

The backend builds claim text and citation references directly from verified
operands. Incomplete lookups can return `PartiallyAnswered`; incomplete
comparisons cannot produce a computed conclusion. Unsupported or wholly missing
facts return `InsufficientEvidence` without claims.

The store atomically writes a retrieval run, one hit per cited reviewed chunk,
answer, claims and citations in the existing schema. Provider is `reviewed-facts`
and model is `none`. Review citations use the saved hit; its original spans remain
available through the existing page-evidence endpoint. No migration is added.
An induced answer-insert failure rolls the entire request back.

Same actor/organization/key requests serialize through a transaction advisory
lock. Identical inputs replay the same saved result; changed question, corpus or
route policy returns 409. Completed history is read, never recomputed on GET.
Later policy changes do not rewrite an existing answer.

`GET /api/cited-questions/{id}` returns `stage: ReviewedAnswer`, a
`reviewedAnswer` with status, claims, citations and missing parts, and
`answerGenerationEnabled: false` (model generation remains disabled). The legacy
`answer` member remains null for this mode. Public history, alternate retrieval
GET and citation navigation recheck active actor, ownership and all source-index
eligibility. These read checks do not promise instantaneous revocation after a
response has already been delivered.

## Workspace and limits

Changing the question, sources or mode invalidates the previous result and
request identity. Saved answers reopen through GET. Claim text renders as inert
React text. Each source button opens review provenance; evidence cards also open
the exact original page quote, which can differ from a corrected reviewed value.
Answer, partial-answer and refusal states are distinct.

The private computation DTO still has `PublicationAllowed`, `ModelInvoked` and
`GeneralSemanticSupportEstablished` false. It is not exposed as a public response;
the new handler publishes its independently scoped, persisted answer contract.
This does not establish general natural-language understanding or enable the
older model-answer route. Existing paid evaluations remain unchanged.

See [integration verification](../evidence/phase-5/P5-reviewed-public-verification.md).
