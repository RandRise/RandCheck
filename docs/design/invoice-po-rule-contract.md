# Invoice and purchase-order rule contract

October 6, 2026. This is the bounded S2 implementation contract for `invoice-po-1.0`. Independent source review and 168 focused checks passed: 100 domain, 22 application, 34 API and 12 real disposable PostgreSQL checks, within the [S2 receipt's boundaries](../testing/supplier-invoice-S2-20261006.md). Manual acceptance remains pending. It supplements the [input contract](supplier-invoice-input-contract.md). Application defaults still select legacy `1.0`. No retained database migration or extractor activation accompanies this batch.

## Selection and frozen sources

The existing evaluation endpoint accepts two additional optional fields:

```json
{"ruleSetVersion":"invoice-po-1.0","orderScope":"FullOrder"}
```

Use the existing strong case `If-Match` ETag and existing Reviewer/Operator permissions. Omitted rule version selects `1.0` and preserves its original engine and projection. Only the exact version names are accepted. Pair scope is `Unknown` (default), `FullOrder` or `Partial`; numeric enum values are invalid. Scope is the evaluator's declaration, frozen with actor and evaluation time. It does not prove delivery, supplier identity or that every invoice belongs to the complete order.

Pair evaluation requires completed reviewed invoice and PO sources. October 12 scope update: new pair reviews use invoice and PO only. No quotation is requested and there is no missing-quotation warning. Any linked quotation remains in historical source snapshots but is not compared by the pair engine. Role-specific invoice and PO identifiers are checked independently; they are not expected to equal each other. The invoice's explicit PO reference is compared with the actual PO number. The policy requires `supplier_trn`; a generic `trn` key alone leaves that check unavailable. Equality of checked TRN values does not verify supplier or buyer identity.

Every new run freezes its exact document, review and extraction IDs, file hash, extraction outcome, provider/adapter/model identity, structured warnings, original extracted row count, reviewed field values and explicit action kind. Corrected fields contain the entered stored correction plus the original source quote. Confirmed fields use original machine text, never legacy `normalized ?? raw`. Unchecked, cleared and manually added values remain identifiable and unavailable for evidence-backed arithmetic. Cleared machine values remain as historical context, so removing a code cannot hide a duplicate or cause a description fallback. Fields expose their frozen document snapshot ID and role; historical sources never come from current case pins.

Candidate row coverage requires provider `local`, adapter `local-1.6.0`, `Succeeded`, no warnings, non-empty rows and original description/quantity/price evidence. Projected row counts must still match. Legacy successful extraction and absence of warnings cannot establish row coverage. Each actual check separately requires checked and correctly typed evidence; candidate coverage does not confirm any field.

## Typed prerequisites

Every usable field must have an explicit latest `Confirmed` or `Corrected` action, machine source, page number and non-empty original quote. A completed review does not substitute for those conditions. `Added` fields currently lack page provenance and remain unavailable. Removing fields reduces coverage. Multiple aliases for one semantic value are ambiguous and are never resolved by choosing a first value.

| Check | Required evidence and type |
| --- | --- |
| Document numbers and invoice PO reference | `String` or `Identifier`; English numeric digits; separate role-correct keys |
| TRN values | `String` or `Identifier`; English numeric digits; equality only |
| Currency | `currency` typed `Currency`, recognized `AED`, `USD`, `EUR` or `GBP`, equal across pair |
| Line identity | `line_item_code` typed `String`/`Identifier`, otherwise `line_description` typed `String`; exact case/whitespace normalization, no translated or fuzzy equivalence |
| Quantity | `line_quantity` typed `Decimal`; matching explicit `line_unit` typed `String`, recognized `EA`/`EACH` or `HOUR`/`HOURS`; full-order scope and complete row coverage |
| Unit price | `line_unit_price` typed `Decimal`; currency, unit, `line_price_basis` typed `String` such as `1 EA`, and separate `line_price_amount_basis` typed `String` (`net` or `gross`), all compatible |
| Line total | `line_total` typed `Decimal`; currency and unit; explicit compatible `line_total_basis` typed `String` (`net` or `gross`); full-order scope and row coverage |
| Document total | `total` or `gross_payable` typed `Decimal`; currency and compatible `document_total_basis` typed `String`; full-order scope and row coverage |
| Tax | `vat`, `tax` or `vat_charged` and `line_tax` typed `Decimal`; compatible `tax_basis` typed `String`, explicit `tax_currency` typed `Currency` matching document currency; full-order scope and row coverage |

No defaults imply each/unit, per-pack, net/gross, zero tax, currency conversion or partial-invoice equivalence. Pricing denominator and tax amount basis are distinct. Tax currency is explicit because a generic VAT label may describe an amount in a different currency. Repeated line identities, including known untrusted identities, are permanently excluded; a third occurrence cannot make the identity unique again. Unusable identities or unverified row coverage withhold all line matching because missing identities could conceal duplicates. Header checks remain independent. Unmatched lines remain unavailable, not an overcharge conclusion.

The strict number parser accepts ASCII `0–9`, a required integer part, optional correctly grouped commas and one dot with one to six fractional digits. It accepts at most 18 integer digits after grouping removal. Malformed grouping, Arabic digits/separators, signs, exponents and attached currency/units are unavailable. It applies equally to original confirmed text and stored corrections. `Decimal` type alone does not validate syntax.

## Check results and precision

Checks are immutable records with `NoDifference`, `DifferenceFound` or `CouldNotCheck`, a reason, values, field IDs, unit/currency/basis, and finding ID when attention is needed. Arithmetic records signed **invoice minus PO** deltas; existing finding difference remains the absolute magnitude. Each check is independent. Never sum overlapping quantity, price, tax, line and document amounts or describe the sum as savings or exposure.

Run coverage is `Complete` only when recorded checks exist and none is unavailable; otherwise it is `Incomplete`. Zero differences with missing coverage means no difference among completed checks. Legacy runs return `NotRecorded`, not reconstructed coverage. Resolving, accepting or dismissing findings appends decisions and cannot change frozen checks or their coverage.

Signed and absolute differences use additive `numeric(24,6)` storage. Tolerances retain `numeric(18,6)` and must fit at most 12 integer and six fractional digits before evaluation. No rounding, truncation or clamping is accepted. Check deltas also enforce exact six-place storage bounds. Invariant formatting preserves English-digit decimal output independent of server culture.

## Integration boundaries

The frozen S1 `local-1.6.0` profile does not emit all explicit bases, including `line_total_basis`, price amount basis and tax currency. Its actual current sample extractions remain `Partial`; new comparisons must show unavailable checks. Fully covered rule fixtures can use synthetic reviewed, evidence-backed snapshots, but they do not establish extraction-to-review product completeness. Future basis extraction requires a truthful new adapter identity or a faithful manual evidence contract. This batch does not relabel or extend 1.6.

Activation of the new input profile and saved invoice workflow must explicitly select `invoice-po-1.0`. Preserved legacy `1.0` projection still maps invoice and PO numbers to the same `document_number` identity: an invoice containing both its own number and a PO reference retains that known legacy restriction. The new policy preserves role-specific keys and references separately. Keeping `1.0` as the default does not make it suitable for the new multi-number profile; routing and configuration activation remain later integration gates.

The additive migration creates immutable checks and nullable historical metadata, widens difference precision, and preserves old rows without backfilling invented coverage. API database grants add check read/insert only; worker permissions stay unchanged. SQL guards prevent later appends as well as updates/deletes. A server-assigned internal `creation_transaction_id` stores the full `pg_current_xact_id()` in `numeric(20,0)`; child evidence can be inserted only in that creation transaction. This supports an initial atomic save even when the parent is inserted already Completed. The marker is not exposed in the DTO and caller-supplied marker values are overwritten. Legacy rows keep null and legacy rules ignore it. PostgreSQL's full transaction ID includes the epoch and uses the top-level transaction through savepoints, avoiding bare 32-bit `xmin` comparisons. See [PostgreSQL 16 transaction identifiers](https://www.postgresql.org/docs/16/transaction-id.html) and [transaction information functions](https://www.postgresql.org/docs/16/functions-info.html).

Migration and updated role provisioning require separate deployment verification. No migration is applied to retained installations here. Downgrade refuses recorded pair history or values beyond legacy difference precision. UI pairing, required-field review controls, report/CSV and active extractor configuration remain later tasks.
