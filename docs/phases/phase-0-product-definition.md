# Phase 0 — Product definition

**Status:** In progress  
**Outcome:** A narrow, agreed product that solves a specific review problem.

## Why this phase exists

Without an explicit user and workflow, the project could become a generic upload-and-chat demonstration. This phase establishes the business problem and prevents feature drift.

## Work

- Choose the primary user: accounts payable or procurement reviewer.
- Confirm the first workflow and documents.
- Define required fields and line items.
- Define UAE VAT and TRN validation behavior.
- Define the first discrepancy rules and allowed tolerances.
- Decide what requires human confirmation.
- Select a working product name.
- Confirm the evaluation dataset strategy.
- Agree on the MVP boundary and non-goals.

## Recommended decisions

- First workflow: English invoice extraction and review.
- Differentiating workflow: quotation ↔ purchase order ↔ invoice reconciliation.
- First data: fictional documents designed around realistic UAE procurement scenarios.
- Arabic: designed for, but implemented and evaluated in Phase 7.

## Exit criteria

- [ ] Primary persona approved
- [ ] First workflow written as a user journey
- [ ] MVP document types approved
- [ ] Required extraction fields listed
- [ ] Initial discrepancy rules listed
- [ ] Success measures agreed
- [ ] Product name selected or descriptive title explicitly retained
- [ ] Phase 1 scope approved

## Review questions

1. Do we begin with invoices only, or prepare quotation and PO schemas immediately?
2. Is the first user closer to finance/accounts payable or procurement?
3. Should the first demo emphasize extraction accuracy or mismatch detection?
