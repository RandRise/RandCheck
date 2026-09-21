# P4-T06–P4-T08 reconciliation acceptance verification

**Date:** 2026-08-19  
**Result:** Technical Phase 4 gate passed; completion approved by Rand on 2026-09-07

## Delivered boundary

- A dedicated `/reconciliation` Next.js workspace creates cases, pins completed
  quotation/PO/invoice review revisions, configures absolute tolerances, runs
  comparisons, and displays rule, severity, both values, difference, tolerance,
  and source page for each finding.
- Strong ETags protect case links, evaluations, and append-only finding
  decisions. Every resolution requires a reason.
- Local provider `local-1.2.0` honestly classifies English quotation, purchase
  order, and invoice PDFs and extracts repeated line identities, quantities,
  unit prices, tax, totals, and page citations.
- The versioned `uae-procurement-reconciliation-synthetic` v0.1.0 pack contains
  one fictional quotation, one fictional PO, and the previously generated Phase
  3 UAE FTA field-coverage invoice with a reconciliation-data supplement.
- The source, generator, truth labels, hashes, and report are reproducible under
  `tools/phase4_*`, `output/pdf/uae-procurement-reconciliation-v0.1.0`, and
  `outputs/phase-4/reconciliation-v0.1.0`.

The invoice remains a visibly watermarked synthetic tax-invoice fixture with
the UAE guidance fields used in Phase 3. The PDF pack is not an issued invoice,
legal advice, or a structured UAE MoF eInvoice.

## Live-stack acceptance

`tools/phase4_acceptance.ps1` executed this flow against local PostgreSQL,
Python/FastAPI, and ASP.NET:

1. Upload all three PDFs and wait for durable extraction.
2. Require document types `quotation`, `purchase_order`, and `invoice`.
3. Start a review for each document, confirm every machine field, and complete
   all three revisions.
4. Create a case, pin the exact completed revisions, and evaluate rule set 1.0
   with default tolerances.
5. Compare actual finding identities with the versioned truth set.
6. Accept one finding with a reason and verify that its resolution remains in
   the returned immutable run history.

Result: **3 reviewed documents, 9 exact expected findings, 0 false positives,
0 false negatives, and 1 retained reasoned resolution.** Every finding had both
compared field-snapshot IDs. The invoice line evidence points to page 2; the
original FTA field-coverage invoice is preserved on page 1.

The synthetic precision and recall are both `1.0` for this single controlled
case only. They do not estimate real-world UAE document performance.

## Verification matrix

| Check | Result |
|---|---:|
| .NET domain | 95 passed |
| .NET application | 25 passed |
| .NET PostgreSQL persistence | 30 passed |
| .NET infrastructure | 36 passed |
| .NET API | 21 passed |
| .NET architecture | 4 passed |
| Full .NET total | 211 passed |
| Python discovery across declared pinned runtimes | 110 passed |
| Local-provider integration module, including native/OCR goldens | 36 passed |
| Next.js component/request tests | 10 passed |
| Next.js lint and production build | Passed; `/` and `/reconciliation` prerendered |
| VitePress production build | Passed |
| Docker AI and ASP.NET Release image builds | Passed |
| Compose validation | Passed |
| Pending EF model changes | None |
| Migration applied to live acceptance database | `20260819094812_AddReconciliationFoundation` |
| PDF visual QA | 4 pages rendered and inspected; no clipping or overlap |

The Phase 2 deterministic fixture generator deliberately uses Pillow 9.4 while
the production local provider uses Pillow 12.3. Its seven tests passed in the
separate pinned fixture runtime; the remaining Python suite and all five local
provider runtime fixtures passed in the production adapter runtime.

## Claim boundary

This evidence proves software integration, deterministic rule behavior,
evidence preservation, and audit history on an original fictional case. It
does not prove legal compliance, fraud detection, production readiness,
real-document accuracy, or UAE-market discrepancy performance.
