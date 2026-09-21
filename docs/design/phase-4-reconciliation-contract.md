# Phase 4 reconciliation contract

**Status:** Approved foundation; implementation authorized by Rand on 2026-08-19
**Version:** 1.0
**Task:** P4-T01

This contract defines how completed review revisions become immutable inputs to
an explainable quotation, purchase-order, and invoice comparison. It does not
approve payments, modify extraction history, or treat synthetic results as
real-world performance evidence.

## Ownership and boundaries

- ASP.NET owns reconciliation cases, document links, frozen evaluation runs,
  findings, resolutions, and transactional consistency.
- Completed Phase 3 review revisions are the only reconciliation input. A case
  pins the review session and extraction run used for each document.
- Python may later extract richer line items, but the deterministic rule engine
  and its business decisions remain in the ASP.NET application/domain boundary.
- The browser displays values, rules, evidence, and reviewer resolutions; it
  does not calculate authoritative findings.
- The Phase 4 development scenario is synthetic and cannot support client,
  production, or UAE-market accuracy claims.

## Aggregate and immutability model

A `ReconciliationCase` belongs to one organization and initially permits one
active link for each role: `Quotation`, `PurchaseOrder`, and `Invoice`. A link
pins a completed review session; replacing a link is allowed only before a new
evaluation begins.

Each evaluation creates an append-only `ReconciliationRun` containing:

- a monotonically increasing run number and rule-set version;
- a complete tolerance-policy snapshot;
- the exact linked document, review-session, and extraction-run identities;
- effective reviewed fields copied as immutable snapshots with source IDs and
  page evidence where available; and
- zero or more immutable structured findings.

Re-evaluation never updates an older run or finding. Reviewer decisions are
append-only `FindingResolution` records. A latest decision may mark a finding
`Resolved`, `Accepted`, or `Dismissed`, but never deletes the original finding.

## Reviewed field and line-item contract

Header fields use occurrence `0`:

- `document_number`, `supplier_trn`, `currency`, `subtotal`, `vat`, and `total`.

Document-specific aliases are normalized at the snapshot boundary, including
`invoice_number`, `quotation_number`, and `purchase_order_number` to
`document_number`.

Line fields use the same positive occurrence for every value in one row:

- `line_item_code` or `line_description`;
- `line_quantity`;
- `line_unit_price`;
- `line_tax`;
- `line_total`.

`line_item_code` is the preferred matching key. A normalized description is a
development fallback and the finding must disclose when it was used. Ambiguous
or incomplete line identity produces a data-quality finding instead of a
guessed match.

Confirmed machine values retain their machine-field and evidence identities.
Corrected machine values retain the review action plus the original field/page
evidence. Reviewer-added values retain their review action; if no page evidence
exists, evidence-required rules emit a data-quality finding rather than
inventing a citation.

## Rule set 1.0

Rules execute deterministically in code order and findings use stable rule
codes and versions.

| Code | Comparison | Behavior |
|---|---|---|
| `DOC-001` | Required role | One finding for each missing quotation, purchase order, or invoice |
| `DOC-002` | Duplicate source | Same document or SHA-256 linked under more than one role |
| `SUP-001` | Supplier TRN | Exact normalized identifier across available roles |
| `CUR-001` | Currency | Exact ISO currency across available roles |
| `AMT-001` | Quotation vs PO total | Absolute numeric difference exceeds total tolerance |
| `AMT-002` | PO vs invoice total | Absolute numeric difference exceeds total tolerance |
| `TAX-001` | PO vs invoice VAT | Absolute numeric difference exceeds tax tolerance when both exist |
| `LINE-001` | Line presence | A matched source line is missing from the next document |
| `LINE-002` | Quantity | Absolute difference exceeds quantity tolerance |
| `LINE-003` | Unit price | Absolute difference exceeds unit-price tolerance |
| `LINE-004` | Line tax | Absolute difference exceeds tax tolerance |
| `LINE-005` | Line total | Absolute difference exceeds line-total tolerance |
| `DATA-001` | Required value/evidence | A rule cannot run honestly because a required reviewed value or citation is unavailable |

Every finding records the rule, severity, document roles, field/line identity,
both compared values, numeric difference when applicable, applied tolerance,
and a safe human-readable explanation. Each available side points to the frozen
document snapshot and its page evidence.

## Tolerance policy 1.0

The initial development defaults are absolute tolerances:

| Value | Default |
|---|---:|
| Quantity | `0` |
| Unit price | `0.01` |
| Tax/VAT | `0.01` |
| Line total | `0.01` |
| Document total | `0.01` |

All values must be non-negative decimals and are frozen into each run. A
difference equal to the tolerance passes; only a larger difference produces a
finding. Currency conversion, percentage tolerances, tax/legal opinions, and
automatic approval are outside rule set 1.0.

## Evaluation and claim boundary

Rule evaluation uses versioned truth cases containing expected finding
identities. Reports retain dataset/rule versions and publish finding precision,
recall, false positives, false negatives, and per-rule counts. The first
quotation/PO/invoice pack may be synthetic development evidence only. Real-
world performance requires a rights-cleared representative dataset and a
separate approved threshold decision.

## Error and concurrency behavior

- Cross-organization document, review, run, finding, or actor references are
  rejected as not found.
- Stale case or finding versions return a precondition failure without partial
  mutation.
- Invalid roles, values, tolerances, or resolution reasons return stable safe
  validation codes.
- Evaluation writes the frozen run and all findings atomically.
