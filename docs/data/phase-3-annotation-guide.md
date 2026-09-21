# Phase 3 English invoice annotation guide

**Status:** Approved foundation; pilot calibration pending  
**Version:** 1.0  
**Field schema:** `invoice-fields-1.0`  
**Task:** P3-T03

## Annotation principle

Annotators label only what the invoice explicitly supports. They must not see
provider predictions, infer a value from arithmetic, repair a supplier's
document, or copy an earlier extraction result. Ambiguous source content is
escalated for adjudication rather than guessed.

The initial Phase 3 ontology covers eight header/summary fields. Line items,
addresses, customer identifiers, Arabic, and reconciliation values remain
outside this version.

## Field definitions

| Field | Type | Label when | Do not |
|---|---|---|---|
| `invoice_number` | String | Explicit invoice identifier issued by the supplier | Substitute PO, quotation, order, account, or filename values |
| `invoice_date` | Date | Explicit issue/invoice date | Use due, delivery, service, or print dates |
| `supplier_trn` | Identifier | Explicit supplier UAE tax registration number | Use customer TRN or infer/pad digits |
| `currency` | Currency | Explicit document/total currency | Assume AED from geography or a currency-less symbol |
| `subtotal` | Decimal | Explicit pre-tax subtotal for the invoice | Recalculate it from lines |
| `vat` | Decimal | Explicit VAT/tax amount for the invoice | Label the VAT percentage or derive the amount |
| `total` | Decimal | Explicit final invoice/grand total payable | Use subtotal, amount paid, balance from another document, or a derived sum |
| `payment_terms` | String | Explicit terms such as `Net 30` | Derive terms from invoice and due dates |

## Source and normalized values

`value` is the human-readable value as displayed, excluding its label and outer
layout whitespace. `normalized_value` follows these versioned rules:

- all text is Unicode NFC;
- outer whitespace is removed; meaningful internal spacing, punctuation, and
  case remain for string and identifier fields;
- unambiguous dates use ISO `YYYY-MM-DD`;
- currencies use the explicit uppercase ISO 4217 code;
- decimal amounts remove grouping separators and use `.` as the decimal mark;
- invoice monetary totals retain two decimal places when the source expresses
  ordinary currency precision;
- identifiers preserve leading zeroes.

If a date, currency, or decimal cannot be normalized without inference, it is
sent to adjudication. Normalization rules are not changed to match a provider.

## Presence, absence, and repetition

- The annotator checks all eight fields for every sample.
- A legitimately absent field is omitted from `ground_truth.fields`; this is an
  exhaustive absence decision, not unfinished annotation.
- Repeated instances use zero-based `occurrence` in reading order only when the
  ontology allows genuinely repeated values. Duplicate renderings of the same
  header value do not create another occurrence.
- Conflicting candidates, unreadable values, and unclear document roles require
  verification/adjudication before the label is frozen.

## Independent review workflow

1. Annotator A labels the source using this guide and records a canonical field
   hash and UTC completion time.
2. Reviewer B independently checks the source and labels without provider
   predictions. Reviewer B must be a different recorded reviewer.
3. If B accepts, the final label may remain revision 1.
4. If B requests changes, a third independent adjudicator resolves them after
   verification; the final label revision is at least 2.
5. Verification cannot predate annotation, and adjudication cannot predate
   verification.
6. The ledger's final canonical field hash must exactly match the dataset's
   frozen ground-truth fields.

Reviewer IDs may be pseudonymous workforce identifiers, but they must be stable
and traceable in the approved review system. Personal names are not required in
the repository.

## Pilot calibration gate

Before labeling the benchmark population, the reviewers jointly annotate a
rights-cleared pilot and record disagreements by field and cause. The guide is
revised before the validation/test split is frozen. Provider output is still
hidden during calibration. Any guide change after freezing creates a new guide
and dataset version.
