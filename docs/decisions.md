# Decision log

This page records product decisions separately from implementation details. A decision should include its reason and the consequence for the roadmap.

| ID | Date | Decision | Reason | Status |
| --- | --- | --- | --- | --- |
| D-001 | 2026-08-06 | Use Next.js for the product UI | Demonstrates React while providing a production application structure | Accepted |
| D-002 | 2026-08-06 | Use ASP.NET Core for the public enterprise API | Highlights backend, security, workflow, and persistence engineering | Accepted |
| D-003 | 2026-08-06 | Isolate AI processing in Python/FastAPI | Keeps OCR, extraction, RAG, and evaluation close to the Python AI ecosystem | Accepted |
| D-004 | 2026-08-06 | Do not use “Bayaan” as the product name | The name is crowded in regional AI and business markets | Accepted |
| D-005 | 2026-08-07 | Make the first MVP English-first | Reduces early OCR and evaluation complexity while preserving Arabic as a meaningful later phase | Proposed |
| D-006 | 2026-08-07 | Treat document reconciliation as the primary differentiator | Creates more business value and stronger portfolio evidence than generic PDF chat | Proposed |
| D-007 | 2026-08-07 | Use VitePress for planning documentation | Keeps the source in Markdown while providing fast local navigation and review | Accepted |
| D-008 | 2026-08-07 | Use PostgreSQL as the canonical persistent database with EF Core Code First migrations | PostgreSQL is the production target; SQLite behavior is not sufficient migration evidence | Accepted |
| D-009 | 2026-08-07 | Treat the current prototype database and synthetic data as disposable | The prototype has no migration history and contains no client or production data worth importing | Accepted |
| D-010 | 2026-08-07 | Seed one development organization and system actor for local workflows | Phase 1 needs structural ownership and attribution without pretending authentication already exists | Accepted |
| D-011 | 2026-08-07 | Require an actor for every review action and a reason for correction, addition, or clearing | Review history must remain attributable and explain material changes while confirmations may remain reason-free | Accepted |
| D-012 | 2026-08-07 | Separate document-processing and human-review states while preserving the existing API through DTO mapping | Database workflows need distinct lifecycles without coupling the browser contract to EF entities | Accepted |
| D-013 | 2026-08-07 | Use snake_case PostgreSQL table and column names from the initial migration | A consistent physical naming convention avoids a later destructive rename migration | Accepted |
| D-014 | 2026-08-07 | Structure the ASP.NET backend as a modular monolith using Clean Architecture boundaries, Vertical Slice organization, and DDD-lite | This preserves a practical single deployment while making domain, application, infrastructure, and feature responsibilities explicit | Accepted |
| D-015 | 2026-08-18 | Close the Phase 3 implementation gate with the generated UAE FTA field-coverage pack as a development-only benchmark | Rand chose the generated English UAE invoices for the remaining Phase 3 work; this enables reproducible integration and regression evidence now while explicitly deferring real-world accuracy claims | Accepted |
| D-016 | 2026-08-19 | Start Phase 4 with one reviewed quotation, purchase order, and invoice per case; immutable evaluation runs; exact identity rules; and configurable absolute numeric tolerances | This is the narrowest explainable reconciliation model that preserves Phase 3 provenance and can later accept richer or real datasets without rewriting the workflow | Accepted |
| D-017 | 2026-09-01 | Scope Phase 5 questions to explicitly selected completed review revisions, with versioned lexical/vector retrieval, claim-level citations, and deterministic insufficient-evidence outcomes | This reuses the Phase 2–4 evidence chain, keeps organization scope enforceable before ranking, and makes retrieval and generation independently measurable | Accepted |
| D-018 | 2026-09-08 | Treat raw server logs as a separately protected sensitive store, with allowlisted operational exports and explicit retention/access verification | Application log filtering does not sanitize PostgreSQL's own errors; see the [policy proposal](security/phase-5-server-log-policy.md) | Proposed, not implemented |
| D-019 | 2026-09-08 | Use the cheapest-first synthetic answer pilot: GPT-5 nano provisionally, subject to availability, retirement and quality gates; defer the Luna/Terra paid comparison | Rand chose cost-first; listed nano snapshot retires December 11, 2026. See the [pilot plan](design/phase-5-model-pilot-proposal.md) | Accepted pilot direction; live calls disabled; detailed safeguards proposed |
| D-020 | 2026-09-08 | Offer stronger evaluated answer models later as optional organization-paid upgrades, with the same privacy and minimum correctness gates | Rand wants organizations to pay for stronger models if needed; entitlements, pricing and billing implementation remain future work | Accepted product direction, not implemented |

## Decisions still needed

On 2026-09-10 Rand approved persistent invoice chunk embeddings in the existing
PostgreSQL/pgvector database. Vectors retain source revision and model metadata;
original text remains authoritative for citations. The small/1536/cosine profile
is the development comparison candidate, not production model acceptance. See the
[embedding storage contract](design/phase-5-embedding-storage-contract.md).

Phase 5 checkpoint, 2026-09-07: Rand accepted a USD 10 starting budget for API
development tests. Paid calls remain disabled until secure credentials, model
selection, and application-side spend enforcement are configured. This is a
test-spend boundary, not approval of a specific model or production provider.

On 2026-09-08 Rand accepted the cost-first pilot direction in D-019 and optional
organization-paid upgrades in D-020. D-018 remains proposed. The proposed USD 8
admission ceiling leaves USD 2 of the accepted future budget unallocated. No
paid call is enabled by these documents. As of 2026-09-09, the durable ledger
component and test-only private answer bridge are implemented and tested; full
token/usage accounting, live dispatch and operational recovery remain open. See
the [bridge review](evidence/phase-5/P5-T06-T08-answer-bridge-verification.md).

- Final product name
- Exact first user persona
- Expansion beyond the initial quotation, purchase-order, and invoice document types
- Production discrepancy thresholds after a rights-cleared real dataset exists
- Phase 5 live readiness: selected temporary OpenAI nano pilot access/lifecycle,
  quality and spending safeguards; production provider remains undecided
- Phase 5 embedding model, dimension, and provider after retrieval evaluation
