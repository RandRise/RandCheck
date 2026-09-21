# Phase 4 — Document reconciliation

**Status:** Complete; completion approved by Rand on 2026-09-07
**Outcome:** Related quotations, purchase orders, and invoices are compared using explainable rules.

The [Phase 4 task ledger](../tasks/phase-4.md),
[reconciliation contract](../design/phase-4-reconciliation-contract.md), and
[quality plan](../testing/phase-4-quality-plan.md) define the active work. The
first development boundary uses reviewed synthetic values and makes no real-
world discrepancy-detection claim.

## Work

- Add quotation and purchase-order schemas.
- Extract header fields and line items.
- Link related documents automatically and manually.
- Compare supplier, currency, quantities, unit prices, tax, and totals.
- Configure tolerances for rounding and permitted variance.
- Detect missing, duplicate, and inconsistent documents.
- Store every discrepancy as a structured finding.
- Link each side of a finding to document/page evidence.
- Allow a reviewer to resolve, accept, or dismiss a finding with a reason.

## Example finding

> Invoice total exceeds the purchase order by AED 1,500. Purchase order: AED 10,000. Invoice: AED 11,500.

## Acceptance criteria

- [x] A completed-review quotation, PO, and invoice can be pinned into one case.
- [x] Exact matches produce no false discrepancy.
- [x] Amount and quantity mismatches identify both values and their sources.
- [x] Tolerance rules are configurable and tested.
- [x] Every finding explains the rule that triggered it.
- [x] Reviewer resolution is recorded without deleting the original finding.
- [x] Re-evaluation preserves every earlier run and finding.
- [x] Synthetic rule results are excluded from client or production claims.

The backend acceptance criteria are verified in the
[P4-T01–P4-T05 evidence](../evidence/phase-4/P4-T01-T05-backend-foundation-verification.md).
The workspace, versioned synthetic procurement truth set, discrepancy report,
and live-stack scenario are verified in the
[P4-T06–P4-T08 evidence](../evidence/phase-4/P4-T06-T08-acceptance-verification.md).
Rand approved phase completion on 2026-09-07.

## Portfolio evidence

- Reconciliation domain model
- Rule-engine tests
- End-to-end mismatch scenario
- Precision/recall report for discrepancy rules
