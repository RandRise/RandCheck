# Phase 5 task ledger

**Latest September 11 — Phase 5 development complete:** [Acceptance evidence](../evidence/phase-5/P5-development-acceptance-20260911.md) closes case selection, public prepared-query vector/hybrid integration, the declared source-security matrix and cross-phase regression. 860 unique backend tests, 329 Python tests and 27 web checks pass; the frozen 30-case HTTP set passes with 54 verified cited-fact references. Initial historical test-clock failures were repaired in test configuration and all affected cases rerun. Browser snapshots and 34 unchanged retained tables are recorded; zero new provider cost. Completion covers bounded English reviewed-field answers and evidence search. Arbitrary live query embeddings and general model answers are not enabled; the optional model route retains its failed 17/30 result and separate acceptance gate. Enterprise identity remains Phase 6. Older checkpoints below are historical.

**Previous September 11 — acceptance audit complete:** [Criterion-by-criterion audit](../evidence/phase-5/P5-acceptance-audit-20260911.md) verifies the bounded public workflow and records six additional adversarial database passes. Remaining work is the planned corpus/retrieval application integration, the complete declared RAG security matrix, and final regression. The approved contract makes model adapters optional: enabling OpenAI is not itself a closure requirement. Model answers remain disabled; existing paid results are unchanged.

**Previous application reviewed answers:** [Public integration verification](../evidence/phase-5/P5-reviewed-public-verification.md) records the new bounded reviewed-field answer mode, persisted citations/history and replay/access checks. The varied development fixture improved from 31/36 to 36/36; 591 backend, eight PostgreSQL and 24 web checks passed. Real services passed 20 legacy checks, 17 reviewed-answer scenarios and all nine reconciliation findings. Six browser snapshots, 34 unchanged retained tables and zero new cost are recorded. Phase 5 remains open for final criterion/adversarial acceptance and the unmet general model-answer gate; enterprise identity stays in Phase 6. Older checkpoints below are historical.

**Previous due-date repair:** [Extraction/review verification](../evidence/phase-5/P5-due-date-verification.md) adds explicit due-date extraction in local-1.3.0 and the missing review row. Fresh extraction returns 2026-08-31; confirmation, correction and clearing pass database/citation checks. 550 backend, nine unique PostgreSQL and 329 unique Python checks passed, including pinned OCR. All retained data and the historical 29/30 trial remain unchanged. Broader evaluation and public answer/history integration remain next; Phase 5 is open.

**Previous reviewed-fact trial:** [Private backend candidate](../evidence/phase-5/P5-reviewed-fact-verification.md) passes 29/30 unchanged development references, with 68 verified cited facts, 30 exact repeats, zero provider calls and all 34 retained tables unchanged. The remaining due-date answer lacks a reviewed field. 545 backend, six new PostgreSQL and seven evaluator tests passed. This bounded English route reads all reviewed fields in the selected corpus; it is not a general QA or like-for-like model improvement. Public integration, broader evaluation and security acceptance remain open. Phase 5 is still in progress.

**Historical paid source-v4 checkpoint:** [Recovered source-v4 evaluation](../evidence/phase-5/P5-source-recovered-evaluation.md) recorded all 30 cases: 17 end-to-end passes, nine rejected proposals, three transport failures and one accepted wrong-field answer. TLS recovery succeeded; known cost USD 0.012233 plus USD 0.006 unknown reservations remains below USD 0.06. Answer quality still blocks Phase 5 completion. Earlier checkpoints below are historical.

**Phase status:** Development complete for the declared scope; foundation approved by Rand on 2026-09-01
**Implementation status:** P5-T01 through P5-T09 complete for bounded reviewed answers, keyword and prepared-query semantic evidence search. The optional unpublished model route needs separate quality/security acceptance before enablement.

| ID | Task | Status | Depends on | Planned evidence |
|---|---|---|---|---|
| P5-T01 | Define immutable corpus, chunk, retrieval, answer, citation, refusal, and ownership contracts | Complete | Phase 4 technical gate | [Cited-question contract](../design/phase-5-cited-question-contract.md) |
| P5-T02 | Create the versioned answerable, unanswerable, adversarial, and cross-tenant evaluation set | Complete | P5-T01 | [Question-set verification](../evidence/phase-5/P5-T02-question-set-verification.md) |
| P5-T03 | Implement chunk, embedding, retrieval-run, answer, claim, and citation persistence | Complete: canonical persistence, pgvector storage and semantic runs | P5-T01 | [Persistence verification](../evidence/phase-5/P5-T03-persistence-verification.md); [semantic runs](../evidence/phase-5/P5-semantic-retrieval-verification.md) ; [closure](../evidence/phase-5/P5-development-acceptance-20260911.md) |
| P5-T04 | Implement deterministic page/reviewed-fact chunking and a replaceable embedding adapter | Complete (provider-independent components) | P5-T02 and P5-T03 core | [Chunking verification](../evidence/phase-5/P5-T04-chunking-verification.md) |
| P5-T05 | Implement organization-filtered lexical, exact-vector, and hybrid retrieval with versioned policy | Complete: lexical/vector/hybrid policies, comparison and prepared-query public integration | P5-T03 and P5-T04 | [Lexical verification](../evidence/phase-5/P5-T05-lexical-verification.md); [semantic retrieval](../evidence/phase-5/P5-semantic-retrieval-verification.md); [completed comparison](../evidence/phase-5/P5-nonredundant-retrieval-verification.md) ; [closure](../evidence/phase-5/P5-development-acceptance-20260911.md) |
| P5-T06 | Implement the public question/refusal API and server-side claim/citation validator | Complete: bounded public reviewed answers, refusals and citation validation; model route disabled | P5-T05 compared retrieval policies | [Public reviewed-answer verification](../evidence/phase-5/P5-reviewed-public-verification.md); [validator](../evidence/phase-5/P5-T06-validation-verification.md) ; [closure](../evidence/phase-5/P5-development-acceptance-20260911.md) |
| P5-T07 | Build the cited-answer workspace with page/span navigation and visible refusal states | Complete: case selection, citations, partial/refusal and saved history | P5-T06 | [Public reviewed-answer verification](../evidence/phase-5/P5-reviewed-public-verification.md) ; [closure](../evidence/phase-5/P5-development-acceptance-20260911.md) |
| P5-T08 | Implement RAG threat controls and adversarial tests | Complete for declared development matrix; optional model red team remains an enablement gate | P5-T04 through P5-T07 | [Security checkpoint](../evidence/phase-5/P5-T08-security-verification.md), [request-boundary checkpoint](../evidence/phase-5/P5-T08-request-boundary-verification.md), and [threat model](../security/phase-5-rag-threat-model.md) ; [closure](../evidence/phase-5/P5-development-acceptance-20260911.md) |
| P5-T09 | Run end-to-end evaluation and publish Phase 5 acceptance evidence | Complete: development acceptance, cross-phase regression and snapshots | P5-T02 through P5-T08 | [Quality plan](../testing/phase-5-quality-plan.md) and acceptance report ; [closure](../evidence/phase-5/P5-development-acceptance-20260911.md) |

## Decisions required before provider-dependent implementation

September 11: [source-context v4 partial paid run](../evidence/phase-5/P5-source-context-partial-run.md)
was explicitly approved up to USD 0.06. Four cases were attempted: one accepted,
one validation rejection, two transport uncertainties. Twenty-six are unrun.
Known cost USD 0.000671 plus USD 0.004 unknown reservations is within the cap.
The network/count-stage failure and accounting guard block further dispatch.
Earlier v4 verification remains valid; no full v4 quality score exists.

Latest 2026-09-10 checkpoint: [nonredundant hybrid comparison](../evidence/phase-5/P5-nonredundant-retrieval-verification.md) closes q024, with full label coverage for 24/24 answerable development cases. Thirty new runs replay without charges or database changes. The [answer comparisons](../evidence/phase-5/P5-low-reasoning-verification.md) completed at 19/30 minimal and 23/30 low reasoning. Scope restrictions reject the observed legal/foreign-source failures on replay. Citation/source binding, public integration and broader acceptance remain open.

Earlier 2026-09-10 checkpoint: [persisted semantic retrieval](../evidence/phase-5/P5-semantic-retrieval-verification.md)
adds versioned vector/hybrid policies, atomic scoped run/hit persistence and
replay. Three real retained comparison runs reuse the previous query embedding;
zero new calls or charges. Next: remaining-corpus population under a frozen plan,
fixed-set lexical/vector/hybrid comparison, then public question integration.

Earlier 2026-09-10 checkpoint: [live embedding ingestion](../evidence/phase-5/P5-live-embedding-ingestion-verification.md)
connects canonical sources, private preparation/dispatch, independent accounting,
shared reservations and storage/search. Nine vectors retained from four settled
calls (431 input tokens, USD 0.000011 conservative ledger charge). Offline replay
reuses every vector/hit without a provider call or database change. 513 backend
and 165 Python checks passed. Next: vector retrieval runs, hybrid integration,
remaining-corpus population under a new frozen plan, and fixed-set comparison.

Earlier 2026-09-10 checkpoint: [persistent embeddings](../evidence/phase-5/P5-embedding-storage-verification.md)
adds canonical pgvector storage, independent saved-outcome verification and
internal exact search. 504 backend tests passed. Migration 14 is applied to the
retained database; all existing rows match before/after fingerprints. Zero
retained vectors or paid embedding calls. Next: canonical private coordinator
and ingestion, then versioned vector/hybrid retrieval and comparison. Paid
approval remains granted; the USD 8 shared ceiling is unchanged.

Earlier 2026-09-10 checkpoint: [measured embedding transport](../evidence/phase-5/P5-embedding-token-transport-verification.md)
adds pinned offline token counting, token-ID requests and a bounded private
preparation/outcome contract. 166 Python tests passed. Next: canonical ASP.NET
coordinator/usage validation and explicit private enablement, then vector storage
and retrieval comparison. No paid embedding calls yet; approval is already granted.

Earlier 2026-09-10 authorization/accounting: Rand explicitly approved paid embeddings.
[Input-only shared accounting](../evidence/phase-5/P5-embedding-ledger-verification.md)
now distinguishes embedding reservations without dummy output tokens or a second
budget. Retained DB is migration 13 with all earlier paid records preserved.
Next: verified token counting, bounded private dispatch and canonical vector
storage, then lexical/vector/hybrid evaluation. The existing USD 8 admission
ceiling is unchanged. No actual embedding calls yet.

Earlier 2026-09-10 embedding checkpoint: [wire/audit and ranking components](../evidence/phase-5/P5-embedding-ranking-primitives-verification.md)
implemented and offline-tested (144 Python checks). Text-embedding-3-small at 1536
dimensions is the explicit wire comparison candidate, not a production choice.
P5-T03 storage and P5-T05 database-backed vector/hybrid integration remain open.
Next implement input-only operations in the existing shared ledger, then private
transport/coordinator and canonical storage. No embeddings have been purchased,
persisted or used by the public question workflow.

2026-09-10 implementation: [quality fixes](../evidence/phase-5/P5-quality-fixes-verification.md)
add prompt v3 status-specific branches and a conservative server-side English
payment-event restriction on new and replayed proposals. This is not general
entailment validation; public generation and semantic acceptance remain disabled.
No paid follow-up or embeddings yet. Offline verification passed 486 backend
checks and 126 Python checks, plus builds; actual saved paid-date failure is now
denied on replay without redispatch or ledger changes. Next: embedding
storage/accounting, retrieval comparison and versioned follow-up evaluation.

Previous live quality checkpoint, 2026-09-09: ten-case baseline completed with nine paid
count/generation pairs and one no-hit refusal. Four cases passed end to end.
One due-date-as-paid-date hallucination passed structural citation validation;
four outputs failed the unchanged response contract. Public generation stays
disabled. New cost estimate USD 0.001748; shared total USD 0.001951, all settled,
no holds/freezes. Next: semantic payment-evidence support, versioned refusal/schema
and duplicate-claim fixes, paraphrase retrieval and evidence-conditioned labels
before another bounded run. See the
[quality baseline review](../evidence/phase-5/P5-live-quality-baseline-verification.md).

Previous implementation checkpoint, 2026-09-09: the private live pilot now performs
one count and generation after committed canonical-ledger dispatch. The first
answer had the correct AED 8,820.00 amount but invalid cross-chunk citation IDs;
it was rejected and USD 0.000094 settled. The failure is preserved, not relabeled
as a passing answer. Prompt v2 constrains allowed citation pairs, with a separate
recorded follow-up evaluation on the same synthetic source. The v2 answer passed
structural citation validation and the one-case source check, then replayed with
the private service offline. Total generation token-rate estimate: USD 0.000203,
two settled operations, no holds/freezes. 468 backend tests (55 focused PG) and
115 Python cited tests passed; not a full PG/browser regression. Public generation
remains disabled. Next is bounded independently labeled answer-quality evaluation.
Current results, regression evidence and remaining gates:
[live synthetic generation review](../evidence/phase-5/P5-live-synthetic-generation-verification.md).

Previous implementation checkpoint, 2026-09-09: durable preparation/outcome records
and synthetic saved-result replay now use the canonical ledger. Migration 12
adds scoped immutable artifacts. The bounded OpenAI transport is implemented
and mock-tested but remains unregistered; the coordinator still rejects live
providers. Verification: all 541 backend tests (including 138 PostgreSQL) and
102 Python tests passed, zero failures/skips. Build/model/docs checks passed.
The retained synthetic database was backed up and upgraded from migration 9
to 12 without a reset; its new shared ledger is USD 8, USD 0 recorded spend.
Next is the private live-provider
connection and actual usage settlement, then the already-authorized synthetic
smoke test. No paid request has run. See the
[journal/transport review](../evidence/phase-5/P5-generation-journal-transport-verification.md).

Previous implementation checkpoint, 2026-09-09: pure OpenAI Responses/count request
construction and usage/proposal parsing are implemented; 90 Python cited tests
passed, including 24 new protocol tests. No network/credential access or live
adapter registration was added. Transport, durable provider/result records and
ledger integration remain before the already-authorized synthetic pilot. See the
[protocol review](../evidence/phase-5/P5-openai-protocol-verification.md).

Previous implementation checkpoint, 2026-09-09: internal recovery execution and
audit are implemented with separate default-denied execution authority and a
test-provider-only guard. Reservation/budget/spend event/audit commit together;
request replay does not charge twice and evidence reuse is blocked. Migration
11 adds the audit; retained acceptance data is not migrated here. Full .NET
regression passed **529 tests**, including all **126 PostgreSQL** cases (18 new
execution and four new process-crash cases), with zero failures/skips. Build,
model/migration and docs/diff checks passed. See the
[execution review](../evidence/phase-5/P5-T08-recovery-execution-verification.md).
That next step is implemented for the synthetic coordinator in the latest checkpoint.
Real operator permissions, billing proof and global freeze resolution remain
open; no public recovery endpoint or paid call is enabled.

Previous implementation checkpoint: read-only recovery assessment is
implemented with default-denied authority and unavailable real evidence ports.
The scoped reader checks an active operator separately from the original
requester. Recommendations never execute, redispatch or clear a shared freeze.
All 403 non-persistence .NET cases and 52 combined PostgreSQL cases passed
(455 unique tests; zero failures/skips). Final build, model/migration alignment
and docs/diff checks passed. See the
[review](../evidence/phase-5/P5-T08-recovery-assessment-verification.md) and
[execution contract](../design/phase-5-recovery-contract.md). Internal execution
has since been added as documented above. Authenticated privileges and real
billing evidence remain open.

Previous test checkpoint: seven separate-process crash/restart cases
passed around reservation, dispatch, fake acceptance and settlement. Database
commits survive; uncommitted changes roll back; restart cannot dispatch twice.
No production code/schema or paid-call setting changed. Stranded dispatch holds
still require trusted reconciliation; host/PostgreSQL/backup recovery is not
proved. See the [crash review](../evidence/phase-5/P5-T08-crash-recovery-verification.md)
for the current verification scope and operational limitations.
Verification: 362 non-persistence .NET cases and 47 targeted PostgreSQL cases
passed (409 unique tests), followed by a final seven-case rerun with explicit
test/application schema isolation checks. Zero failures/skips in these final
runs; build, migration alignment and docs checks passed. No fresh full
database/Python-unit/OCR/frontend/browser acceptance is claimed.

Previous implementation checkpoint: versioned application-prompt
assembly and private preparation receipts now bind the measured input count,
prompt/model/counter versions and exact source bytes to the durable reservation.
Both preparation and generation remain disabled by default; the counter is
test-only UTF-8 byte units, not GPT tokenization or verified billing. See the
[preflight review](../evidence/phase-5/P5-T06-T08-prompt-preflight-verification.md).
Verification: 362 non-persistence .NET tests, 40 targeted PostgreSQL cases and
66 Python cited tests passed. Build, migration alignment and docs checks passed.
The earlier full database regression is historical, not newly rerun in full.

Previous checkpoint: the internal answer bridge
connects scoped retrieval, durable reservation/dispatch, private Python fake
generation, synthetic settlement and citation validation. It never publishes an
answer; registered generation settings and the Python adapter remain disabled.
See the [bridge review](../evidence/phase-5/P5-T06-T08-answer-bridge-verification.md).
Verification: 435 .NET tests passed, including 79 PostgreSQL cases, plus 51
Python cited tests across the two existing runtimes. No failures/skips in the
.NET run; no fresh frontend/browser or full OCR acceptance is claimed.

Previous checkpoint: the durable PostgreSQL spending
ledger is verified, with 12 new database cases and a full 411-test .NET pass
(347 non-persistence, 64 PostgreSQL; zero failures/skips). It adds one shared
USD 8 allowance, transactional reservations, duplicate-dispatch protection,
uncertain-cost holds and idempotent settlement. See the
[ledger review](../evidence/phase-5/P5-T08-spending-ledger-verification.md).
Public APIs remain evidence-only; no paid adapter is connected. The earlier
budget calculation and Python answer-envelope components remain documented in
the [component review](../evidence/phase-5/P5-T06-T08-provider-free-boundary-verification.md).
Application-process failure tests, read-only assessment and internal synthetic
recovery execution/audit are now implemented. Real authority/evidence adapters
remain, followed by provider-specific counting/usage
verification before paid calls. The internal idempotent settlement primitive
does not constitute an authenticated operator recovery workflow.
Fake-provider integration is implemented, but live models and semantic answer
acceptance are not. All privacy/live-readiness gates are tracked directly
in the [roadmap](../roadmap.md), including the Python runtime reproducibility gap.

Planning checkpoint, 2026-09-08: the [server-log policy proposal](../security/phase-5-server-log-policy.md)
and [model pilot proposal](../design/phase-5-model-pilot-proposal.md) are drafted.
The server-log policy remains proposed. Rand has since accepted cheapest-first
answer generation: temporary GPT-5 nano pilot, subject to lifecycle/access and
quality checks; no initial Luna/Terra paid comparison. Optional stronger models
are a future organization-paid feature, not weaker privacy for the basic tier.
The durable ledger component is implemented; integrated paid-call enforcement,
operational recovery and billing remain outstanding. No API funding
is needed for the remaining provider-free work; live calls require separate verified readiness.

Earlier P5-T08 increment: live PostgreSQL indexing/retrieval/history/evidence
logging and controlled failures were checked. Application EF exception/error
diagnostics now use content-free events; PostgreSQL server logs still contain
synthetic error values. Server-log access/retention/export policy remains an open
deployment gate; see the [database-log checkpoint](../evidence/phase-5/P5-T08-database-log-verification.md).

Rand accepted a USD 10 API-test budget. No API account/key, billing credit,
live model access or application-side spend cutoff has been verified/configured.
No paid requests have been made. This budget does not select a production model.

- Cheapest-first API pilot direction is selected; evidence-only remains the
  runtime default until live readiness passes. Production provider is undecided.
- The embedding model, dimension, and provider are selected from retrieval
  evaluation rather than hard-coded in the schema now.

The evidence, evaluation, persistence, lexical retrieval, citation, refusal,
and security layers can proceed without those decisions. Provider integration,
database vector shape, and free-form answer generation cannot.

Resume from the [continuation checkpoint](../continuation.md) for current
verification, environment cleanup status, and context/handoff preferences.
