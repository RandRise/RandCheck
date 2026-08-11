# Phase 4 — Document reconciliation

**Status:** Proposed  
**Outcome:** Related quotations, purchase orders, and invoices are compared using explainable rules.

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

- [ ] A quotation, PO, and invoice can be linked into one case.
- [ ] Exact matches produce no false discrepancy.
- [ ] Amount and quantity mismatches identify both values and their sources.
- [ ] Tolerance rules are configurable and tested.
- [ ] Every finding explains the rule that triggered it.
- [ ] Reviewer resolution is recorded without deleting the original finding.

## Portfolio evidence

- Reconciliation domain model
- Rule-engine tests
- End-to-end mismatch scenario
- Precision/recall report for discrepancy rules
