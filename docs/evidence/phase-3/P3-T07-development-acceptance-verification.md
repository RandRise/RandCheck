# P3-T07 development acceptance verification

**Date:** 2026-08-18  
**Scope:** D-015 synthetic development boundary  
**Result:** Phase 3 complete; development-only boundary approved by Rand

## Decision and claim boundary

Rand directed Phase 3 to use the previously generated English UAE invoices.
The frozen dataset is `phase3-uae-fta-field-complete-development` version
`0.2.0`: nine PDFs, ten pages, seven native documents, and two scanned
documents. Its manifest remains `claim_eligible: false` and every report
retains the synthetic exclusion.

This evidence supports local integration and regression decisions only. It is
not evidence of client, production, real-document, UAE-market, or legal/tax
compliance accuracy. The readiness audit intentionally exits `4` with ten
synthetic/non-claim blockers.

## Delivered implementation

- `apps/api/app/prediction_generation.py` maps live Phase 2 `ExtractionResult`
  values into the frozen Phase 3 prediction contract and retains expected
  provider failures as failed runs.
- `tools/phase3_predict.py` validates dataset identity, path containment, and
  source hashes before invoking the local provider.
- The local extractor recognizes common UAE summary labels, parenthesized
  currencies, adjacent PDF-column labels, and prioritizes AED `VAT charged`
  and `Gross payable` values without weakening the eight-field contract.
- The review workspace now emits explicit `MissingMachineValue`,
  `LowConfidence`, and `ConfidenceUnavailable` cues while continuing to show
  the original machine value, confidence, and evidence.
- `/api/documents/{documentId}/review-history` exports every session and action
  in reconstructable sequence, including actor, extraction run, machine-field
  identity, reason, and timestamp.
- `/api/metrics` now separates observed review-action counts and machine-change
  rate from the truth-derived evaluator correction rate.

## Frozen before/after result

The baseline and candidate use the same dataset fingerprint. The baseline is
retained at `outputs/phase-3/uae-development-v0.2.0/report.json`; the final
report and comparison are retained beside it as `final-report.json` and
`comparison.json`.

| Metric | Baseline | Final | Favorable change |
|---|---:|---:|---:|
| Recall | 0.194444 | 0.986111 | +0.791667 |
| Precision | 0.608696 | 1.000000 | +0.391304 |
| Field exact match | 0.608696 | 1.000000 | +0.391304 |
| Document exact match | 0.000000 | 0.888889 | +0.888889 |
| Failure rate | 1.000000 | 0.000000 | +1.000000 lower |
| Correction rate | 0.805556 | 0.013889 | +0.791667 lower |

All five frozen development rules passed:

- document exact match at least `0.75`;
- recall at least `0.75`;
- precision at least `0.90`;
- failure rate at most `0.25`;
- rotated/degraded scan recall at least `0.75`;
- zero permitted regression from the retained baseline for every rule.

These thresholds are synthetic development gates, not production acceptance
thresholds.

## Error analysis

The final provider emitted `71` of `72` expected fields and no unexpected
fields. All emitted values matched the strict normalized ground truth. Eight of
nine documents were exact matches. The only missing value was `invoice_date`
on `fta-standard-rotated-degraded-scan`; the OCR-degraded rendering did not
preserve a sufficiently recognizable date label/value pair. The document still
returned `Ready` because its required invoice-number and total fields were
present, and its degraded review cues remain visible.

No ground-truth label was changed after provider output was inspected. The
baseline, intermediate candidate, final prediction set, final report,
threshold set, comparison, and readiness audit are retained separately.

## Verification

| Gate | Result |
|---|---|
| Python unit/contract/CLI suite | `108` passed, `5` runtime fixtures skipped by their explicit host-runtime guard |
| Live UAE dataset in pinned Docker AI/OCR runtime | `9/9` runs recorded; `7` pdfplumber and `2` Tesseract |
| .NET solution including PostgreSQL persistence | `188` passed |
| Web review tests | `7` passed across Vitest and Node suites |
| Web lint and production build | Passed |
| VitePress documentation build | Passed |
| Development threshold comparison | `5/5` passed, exit `0` |
| Synthetic dataset readiness audit | Valid non-claim result, `10` blockers, expected exit `4` |

## Acceptance conclusion

P3-T01 through P3-T07 satisfy the D-015 development boundary. Rand approved
completion on 2026-08-18, confirming that the generated dataset may be replaced
later without blocking the current integration. A rights-cleared real UAE
dataset remains future validation and is not required or implied by this
development-only completion decision.
