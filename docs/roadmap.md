# Roadmap

The roadmap is outcome-based. Dates are intentionally omitted until Phase 0 is approved and the available weekly effort is known.

## Status definitions

- **Proposed:** documented for review; scope can still change.
- **Approved:** acceptance criteria agreed; implementation may begin.
- **In progress:** implementation and verification are active.
- **Complete:** acceptance criteria and evidence are satisfied.

## Delivery roadmap

| Phase | Outcome | Status | Portfolio evidence |
| --- | --- | --- | --- |
| 0. Product definition | Agreed problem, persona, scope, naming direction, and success measures | In progress | Product brief and decision log |
| 1. Invoice foundation | One English invoice moves through upload, extraction, persistence, and review | Complete | [P1-T08 acceptance verification](./evidence/phase-1/P1-T08-acceptance-verification.md) |
| 2. Real document processing | Native PDFs and scans produce page-aware fields through replaceable providers | In progress | Approved contract and quality plan; implementation evidence pending |
| 3. Review and evaluation | Corrections and versioned evaluation create honest accuracy measurements | Proposed | Reproducible evaluation report |
| 4. Reconciliation | Quotations, POs, and invoices are linked and compared with explainable rules | Proposed | End-to-end discrepancy demonstration |
| 5. Cited questions | Users receive grounded answers with document/page evidence | Proposed | RAG and citation evaluation |
| 6. Enterprise security | Identity, tenant boundaries, roles, audit, and retention protect the workflow | Proposed | Threat model and security tests |
| 7. Arabic support | Arabic and mixed-language documents are processed and evaluated separately | Proposed | Arabic evaluation report |
| 8. Operations and deployment | CI/CD, observability, cost tracking, and cloud deployment support a public demo | Proposed | Live demo, dashboards, and runbooks |

## Milestone groups

### Milestone A — Reviewable invoice workflow

Phases 0–3. The result is a narrow but measurable document-extraction product.

### Milestone B — Business differentiator

Phases 4–5. The result is reconciliation and evidence-backed document questions.

### Milestone C — Enterprise proof

Phases 6–8. The result is security, bilingual expansion, observability, and deployment evidence.

## Roadmap rules

- Do not start a phase until its dependencies and acceptance criteria are reviewed.
- Do not claim performance from synthetic demonstrations.
- Keep machine output separate from reviewer corrections.
- Add Arabic only with a labeled Arabic evaluation set.
- Every AI answer or discrepancy must expose its evidence.
