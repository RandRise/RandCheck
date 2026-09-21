# P5-T02 cited-question set verification

**Date:** 2026-09-01
**Result:** Passed

## Scope

P5-T02 creates the development-only evaluation input for Phase 5 before any
retrieval or answer model is selected. The set is derived from the versioned
Phase 4 quotation, purchase-order, and invoice pack and remains explicitly
synthetic and ineligible for production or UAE-market performance claims.

## Artifacts

- `samples/phase-5/cited-questions-v0.1.0/question-set.json`
- `samples/phase-5/schemas/question-set-1.0.schema.json`
- `apps/api/app/cited_question_evaluation.py`
- `tools/phase5_questions.py`
- `tools/phase5_question_audit.py`
- `outputs/phase-5/cited-questions-v0.1.0/dataset-audit.json`

## Coverage result

| Measure | Result |
|---|---:|
| Source identities | 4 |
| Unique PDF files | 3 |
| Questions | 30 |
| Answered expectations | 24 |
| Insufficient-evidence expectations | 6 |
| Exact page-evidence labels | 46 |
| Single-document questions | 12 |
| Cross-document questions | 6 |
| Paraphrase questions | 6 |
| Unanswerable questions | 3 |
| Adversarial/isolation questions | 3 |

The fourth source identity deliberately registers the synthetic invoice under a
foreign organization. It exists only to prove that organization filters exclude
otherwise identical content.

## Evidence integrity

The audit verified:

- the Phase 4 truth-set hash and all three PDF hashes;
- all canonical page hashes using `local-procurement-v1/local-1.2.0` page logic;
- exact question-category closure against the quality-plan allocation;
- every answer claim has evidence inside its allowed corpus;
- every evidence quote occurs exactly once on its labelled canonical page;
- insufficient-evidence expectations contain no factual claims; and
- the isolation fixture cannot enter the primary organization's allowed corpus.

The source PDFs were also rendered to four page images and visually inspected.
All relevant headings, reconciliation fields, invoice details, and page
boundaries were legible and consistent with the labels.

## Automated verification

The focused `unittest` suite ran 8 tests successfully. It covers the committed
audit, schema/local references, category closure, claim/evidence requirements,
refusal shape, organization isolation, source/page hash tampering, and missing
or ambiguous evidence quotes.

The committed audit reports `passed: true` with canonical dataset SHA-256
`c5d18418baf999c5fecbae217eaa6c8bde8124402a9864698fe961b92f4664aa`.

## Claim boundary

This checkpoint proves dataset structure, reproducibility, provenance, and test
coverage only. It does not measure retrieval recall, answer correctness,
groundedness, latency, cost, UAE invoice compliance, or real-world performance.

