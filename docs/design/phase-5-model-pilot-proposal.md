# Phase 5 model pilot proposal

**Current status, 2026-09-10:** Rand explicitly authorized paid embeddings using
the low-cost candidate. The development comparison profile is
`text-embedding-3-small`, 1536 dimensions, cosine; production selection still
depends on retrieval evaluation. Use the existing shared USD 8 admission ceiling
within the USD 10 pilot allowance, not another budget. Generation has already run:
11 calls, USD 0.001951 estimated settled usage and a failed 4/10 quality baseline.
See [continuation](../continuation.md) for the newest implementation and remaining
live-embedding prerequisites. The initial readiness text below is historical,
not the current claim that no paid calls have occurred.

**Initial status (historical):** Cost-first pilot direction accepted by Rand, 2026-09-08. GPT-5 nano
is selected for the temporary OpenAI answer pilot, subject to account access,
lifecycle and quality checks. On 2026-09-09 Rand supplied a local key and authorized
the synthetic pilot. A model-metadata GET verified selected-model access; no
generation request has run and funding/quota/Responses write access remain
unverified. See the [readiness check](../evidence/phase-5/P5-live-model-readiness-check.md).
The budget calculator, durable ledger, test-only private answer bridge and
versioned prompt preflight are implemented. The only counter uses synthetic
byte units, not GPT tokens. Paid dispatch is not connected, so end-to-end spending enforcement
is not yet established. Operational recovery and live-readiness gates remain
open; see the [preflight review](../evidence/phase-5/P5-T06-T08-prompt-preflight-verification.md).
The subsequent [journal/transport increment](../evidence/phase-5/P5-generation-journal-transport-verification.md)
adds saved preparation/outcome records, synthetic replay and a mock-tested HTTP
transport. It does not register a live adapter or prove end-to-end paid accounting.

## Recommendation

Use one low-cost answer model on the generated English UAE invoice pack:
GPT-5 nano, the cheapest compatible text candidate verified in this comparison.
Rand chose cheapest-first rather than a paid multi-model comparison. Luna/Terra
are therefore not included in the initial paid run. `text-embedding-3-small`
remains a separate proposed retrieval candidate; dimension/storage selection
is not implied by approval of the answer-model direction.

The listed nano snapshot `gpt-5-nano-2025-08-07` is scheduled to shut down on
December 11, 2026. Check availability and lifecycle again before live work;
block an unavailable/retired model rather than silently choosing a pricier one.
This is a temporary pilot choice, not a long-term production commitment.
[Official deprecation schedule](https://developers.openai.com/api/docs/deprecations).

Keep evidence-only behavior as the default. First build and test the
provider-independent budget and answer-adapter boundaries without credentials.
Paid execution comes later, after verified safeguards and live-test enablement.

## Options and current prices

Standard USD prices per million tokens, checked against official model pages
on 2026-09-08. Account access is unverified; recheck availability and rates
before implementing live dispatch or purchasing credit.

| Option | Role | Input / output price | Tradeoff |
| --- | --- | --- | --- |
| Evidence-only, current | Baseline and safe disabled mode | No model charge | Shows evidence; does not generate an answer |
| Local model, unselected | Alternative if cloud processing is unacceptable | No hosted token price assumed; hardware/runtime cost unknown | Requires separate hardware and quality evaluation |
| [GPT-5 nano](https://developers.openai.com/api/docs/models/gpt-5-nano) | Selected temporary answer pilot | $0.05 / $0.40 | Structured output supported; quality/access unverified and listed snapshot retiring |
| [GPT-5.6 Luna](https://developers.openai.com/api/docs/models/gpt-5.6-luna) | Deferred alternative | $0.20 / $1.20 | Not part of the initial paid run |
| [GPT-5.6 Terra](https://developers.openai.com/api/docs/models/gpt-5.6-terra) | Deferred comparator | $2.00 / $12.00 | No automatic upgrade or paid comparison |
| [text-embedding-3-small](https://developers.openai.com/api/docs/models/text-embedding-3-small) | Proposed retrieval candidate, not an answer generator | $0.02 input | Compare lexical, exact-vector and hybrid retrieval before selecting a policy |

The nano model page recommends Luna for most new cost-sensitive workloads;
our temporary selection instead follows Rand's lowest-price pilot preference.
Lower token prices do not prove lower cost per correct answer: reasoning,
latency, failures and retries must be measured. Record model and rate versions.
Luna/Terra cache-write pricing does not automatically apply to nano; verify
the exact selected endpoint's charges before dispatch.

## Optional organization-paid upgrades

Rand's product direction is a low-cost default with optional stronger models
paid for by the requesting organization. Billing, entitlements and model-setting
UI are future work, not implemented by this decision. A future organization
admin may choose only an approved/evaluated model, with an explicit price and
organization-specific allowance. The backend must validate that selection;
ordinary request input cannot bypass the allowance or enable a premium model.

Every tier keeps the same organization isolation, privacy controls, citation
requirements and minimum correctness gates. The basic tier must refuse unsafe
or unsupported answers, not sell correctness as a premium-only protection.
No automatic paid upgrade when the basic model fails. Upgrades must preserve
provenance and record the actual model/version/cost without changing access.

## Data boundary

API data is not used for training by default unless opted in. Abuse-monitoring
content may be retained up to 30 days, with documented exceptions. Responses
stores application state by default; propose `store=false`, but that does not
eliminate abuse-monitoring or prompt-cache retention. Cached tensors may remain
up to 24 hours. ZDR/MAM require approval. UAE residency requires additional
approval and endpoint/model-specific support; regional storage is not proof
of regional inference. Do not assume this API budget provides UAE residency or
zero retention. [Official data controls](https://developers.openai.com/api/docs/guides/your-data).

For this proposed pilot, send only synthetic questions and bounded text from
explicitly selected reviewed revisions. No original PDF/image upload, hosted
files/vector stores, external tools, browsing, background jobs, or real client
data. Keep prompts and raw outputs out of application logs. Evaluation outputs
belong in controlled artifacts. Real-data use requires a separate privacy,
residency and deployment review, including the
[server-log gate](../security/phase-5-server-log-policy.md).

## Proposed budget admission rules

The user's USD 10 test budget remains the outer ceiling. Rand has now authorized
the synthetic pilot, but live integration safeguards still need verification.
Application admission ceiling: USD 8, leaving
USD 2 unallocated. Do not automatically spend that reserve.

1. Default disabled. Require an approved model/endpoint/rate version, securely
   configured credentials, a funded API project and explicit live-test enablement.
2. Reserve the worst-case cost durably before dispatch, atomically across
   callers. Include all input, maximum billable output including reasoning,
   and applicable cache-write/surcharge costs. Unknown pricing or an unavailable
   ledger blocks the request.
3. Permit one in-flight paid request initially. Disable SDK/transport automatic
   retries and automatic model fallback. Each deliberate retry needs its own
   reservation; an ambiguous timeout keeps its reservation until reconciled.
4. Persist actual usage when known and reconcile it against the reservation.
   Replays must not dispatch twice. A process restart must not reset spending.
   If usage exceeds the reservation or billing cannot be reconciled, stop calls
   and investigate before continuing.
5. Count embeddings, indexing, questions and comparisons in the same pilot
   ledger. Record model/rate version, operation ID, token counts, status and
   cost, without copying prompt or invoice content into logs.
6. Block a call whose reservation would exceed the USD 8 admission ceiling.
   Also configure provider-side controls and inspect billing; the application
   ledger cannot cap unrelated calls made with the same account elsewhere.

### Illustrative bounded batch, not a bill estimate

Propose at most 8,000 total input tokens and 4,000 billable output tokens per
answer request. Input includes instructions, schema, question and evidence,
not just invoice text. Bound context explicitly; reject or record omitted
evidence rather than silently cutting citations or pretending all sources fit.

At nano's listed standard prices, assuming no cached-input savings:

| Planned batch | Calculation | Maximum under these assumptions |
| --- | --- | --- |
| 30 nano requests | 30 × (8,000 × $0.05 + 4,000 × $0.40) / 1,000,000 | $0.060 |
| Embedding allowance | 500,000 total input tokens × $0.02 / 1,000,000 | $0.010 |
| Total | One model/policy; no retries or additional rounds | $0.070 |

This is arithmetic for proposed limits, not measured usage or a guaranteed
provider bill. Regional/other surcharges, tax and currency conversion are not
included. The live dispatcher must validate the chosen endpoint's full pricing
and token accounting before this estimate can become an enforceable reservation.
Extra retrieval policies or prompt iterations consume additional reservations.

## Integration and evaluation sequence

The [approved contract](phase-5-cited-question-contract.md) stays in force:
ASP.NET owns public authorization/scope/history and final validation; Python
owns retrieval orchestration and the optional model adapter; PostgreSQL remains
canonical. No model may select new documents, execute tools or approve payments.

1. **Offline foundation:** fake-provider tests for default-off behavior, bounded
   prompts, budget admission, duplicate dispatch, concurrency, crash recovery,
   timeout accounting and invalid/truncated/refusal responses. No key needed.
2. **Retrieval comparison:** freeze the synthetic corpus and independent
   question labels; compare lexical, exact-vector/cosine and hybrid results.
   Record embedding model/version/dimension/input hash. Decide the vector
   dimension and storage migration explicitly after the comparison setup is
   approved; never mix embedding spaces.
3. **Answer evaluation:** evaluate nano against the evidence-only baseline on
   frozen retrieved evidence and a versioned prompt. No second paid model is
   required for the initial pilot. Structural citation validation is
   necessary but does not prove semantic support. Review every factual claim
   against its evidence; a model is not the sole judge of correctness.
4. **Adversarial and UI acceptance:** test injected instructions, cross-org
   attempts, insufficient evidence despite nonempty hits, source revocation,
   invalid citations, safe errors and generated-answer navigation. Show failures
   as explicit states, never as supported answers.
5. **Phase 5 acceptance:** publish separate retrieval and answer metrics,
   latency, token usage and cost; run the full regression and broader browser
   acceptance required by the [quality plan](../testing/phase-5-quality-plan.md).

Keep the existing gates: at least 30 questions, recall@5 of at least 95% for the
selected retrieval policy, valid provenance for every citation, zero cross-org
leaks, refusal on explicitly unanswerable cases, and no unsupported factual
answers. Do not lower thresholds after seeing failures. Synthetic results do
not establish real-world UAE invoice accuracy or regulatory compliance.

## Approval boundary and next step

Rand approved a cheapest-first answer pilot and future organization-paid
upgrades. This supersedes the initial Luna/Terra comparison proposal. The
selection does not approve real-data use, a production provider, server-log
deployment policy, billing implementation or Phase 5 completion. Funding and
credentials are now locally configured; detailed safeguards remain to be
implemented/tested and funding has not been verified.

The durable ledger, fake-provider integration and application-prompt preflight
are implemented. Next is actual process-failure/recovery testing and trusted
reconciliation, then exact provider-request counting/usage and embedding-specific
accounting. Test-only proposals remain unpublished.
Provider execution remains disabled until its separate checks pass.
