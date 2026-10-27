# Supplier invoice and PO input contract

S1 contract, October 6, 2026. This defines the bounded input and evidence requirements for [S1](../tasks/supplier-invoice-product.md). The opt-in extraction implementation below passed independent source review and focused verification: 200 tests plus 43 subtests and a real native-PDF evaluator, within the [recorded boundaries](../testing/supplier-invoice-S1-20261006.md). The short manual checkpoint is ready. The [product plan](supplier-invoice-product-plan.md) remains the release scope; a passing extraction test cannot establish the complete product journey.

## Supported input and scope

The intended prototype accepts one supplier invoice and one purchase order for the complete order, in the same supported currency. Arabic and English text are in scope; supported numeric values use English digits (`0` through `9`). Start with legally usable synthetic English and Arabic/English PDFs whose layouts and expected fields are frozen before scoring. The current application accepts PDF, PNG, JPEG and text uploads, but acceptance of a file is not proof that its layout can be checked completely.

Each supported fixture must identify its language, representation (native PDF or scanned PDF), layout, exact file hash, page count, expected header fields and every expected line. Evidence, extraction uncertainty and comparison coverage are scored separately. A canonical-text parser test does not establish OCR or PDF reading-order accuracy. A synthetic PDF does not establish accuracy on supplier documents.

Document role must come from an explicit invoice, purchase-order or quotation title or other agreed role evidence. An invoice's reference to a PO must not make the invoice a purchase order. Conflicting or absent role evidence requires attention. Arabic purchase-order and quotation titles and number labels need their own fixtures. October 12 scope update: quotations are excluded from new pair reviews; recognition remains for existing documents and compatibility, and legacy quotation/PO/invoice results retain their old rules.

Partial invoices, credit notes, multiple invoices or orders in one file, translated-description matching, inferred missing rows, cumulative invoicing and delivery/payment assertions are outside this profile. An unknown or unsupported layout cannot produce a complete agreement result.

## Required values and coverage

Required means needed for a particular supported check, not that extraction can guarantee the value exists. Missing or invalid values limit coverage. Existing extraction `Ready` and completed review statuses do not establish this business coverage.

| Subject | Values needed | Coverage condition |
| --- | --- | --- |
| Document identity | Invoice number or PO number, explicit document role | Keep the identifier as text, including leading zeroes; resolve role conflicts before pairing. |
| Supplier | Supplier TRN/identifier on both documents | Compare explicit supported identifiers; a supplier name alone does not establish identity. Missing identifier makes that check unavailable. |
| Currency and document amount | Currency and document total on both documents | Use an explicit currency code or supported alias. No exchange conversion. Incompatible or unknown currencies make monetary subtraction unavailable. |
| Tax | Explicit comparable VAT/tax amounts and their currency/basis | Tax absent on both sides is not proof of zero tax. Different tax bases make the affected check unavailable. |
| Line coverage | Every expected invoice and PO row, with identity and source evidence | No extracted rows is missing coverage. A partial or unsupported table must remain visible. |
| Line identity | Item code, otherwise a description usable for exact normalized matching | Reordering may be handled by identity. Duplicates, ambiguous identities and untranslated descriptions are not silently matched. |
| Quantity | Valid quantity and compatible explicit unit | No inferred unit. Missing or incompatible units prevent a complete quantity check. |
| Unit price | Valid price, compatible currency, explicit unit and price basis | A price per item and a price per pack are not directly comparable. Missing basis limits coverage. |
| Line total | Valid amount with an explicit compatible net/gross basis | A net row amount is not a gross line total. Do not add tax or derive a replacement total silently. |

Existing field keys include `invoice_number`, `purchase_order_number`, `supplier_trn`, `currency`, `total`, `vat`, `line_item_code`, `line_description`, `line_quantity`, `line_unit_price`, `line_tax` and `line_total`. Unit, price-basis, total-basis and scope representation still need the bounded S2 contract; their absence must not be filled by a default. Supplier display name can be useful inbox information without becoming an identity check.

Review should prioritize uncertain fields, missing required values and comparison inputs. Existing completion still requires an explicit decision for every extracted machine field. Prioritization does not confirm other fields. A reviewer may remove an unusable field, but doing so makes the related check unavailable rather than passing it.

## Numeric policy

The opt-in extractor implements `supplier-invoice-number-v1`. Reviewed corrections and the comparison boundary still need the same acceptance conditions in S2; the existing permissive backend numeric paths are not sufficient.

Rand's October 6 feedback narrows this first-release policy to English digits. Arabic text support remains in scope. Existing historical Arabic-number behavior and tests remain intact; they do not establish support in this new profile. The profile is an opt-in parser change with no UI or backend change.

- Accept English ASCII digits (`0123456789`) only in supported numeric values. Arabic-Indic (`٠١٢٣٤٥٦٧٨٩`), Eastern Arabic-Indic (`۰۱۲۳۴۵۶۷۸۹`) and mixed digit forms are outside this profile. Keep their raw evidence and require explicit review or correction to English digits; do not silently convert them into checked values.
- Require an integer part. Accept at most 18 integer digits after removal of valid grouping and at most 6 fractional digits. Do not round or truncate to fit.
- Decimal separator is `.`, at most once, followed by one to six English digits.
- An optional thousands separator is `,`. The first group has one to three digits; every following group has exactly three. Arabic decimal/grouping separators are outside this first-release numeric syntax and require review.
- Leading/trailing whitespace may be excluded from the selected value span. Internal whitespace, signs, parentheses, exponent notation, currency symbols, attached units and spelled-out amounts are outside this profile.
- A comma is never inferred to be a decimal separator. Decimal commas, malformed grouping, multiple decimal separators and excessive precision require attention. An invalid value remains raw source text; it must not acquire a plausible normalized numeric amount.
- Identifiers are text and retain leading zeroes. Arabic letters in identifiers remain text; numeric portions in this profile use English digits, with no automatic digit conversion. Dates use English digits in valid Gregorian `YYYY-MM-DD`; other date formats require review. Neither identifier nor date parsing uses amount grouping rules.

| Source text | New-profile outcome |
| --- | --- |
| `1050.50`, `1,050.50` | Valid normalized `1050.50`, original quote retained |
| `0`, `0.000001` | Valid; numeric meaning retained without rounding |
| `١٬٠٥٠٫٥٠`, `۱٬۰۵۰٫۵۰`, `١050.50`, `1٬050٫50` | Outside the profile; original evidence retained, explicit review or correction needed |
| `1,23`, `1.234,56`, `1,234٬567`, `1 050.50` | Requires review; no numeric amount inferred |
| `1.2345678`, `-10.00`, `+10`, `1e3`, `AED 10`, `10 EA`, `.50` | Outside the profile; related check unavailable until valid supported correction |

Whether a source uses valid numeric syntax is distinct from whether OCR read the right number. Low-confidence valid numbers still need review. A correction retains its entered text, actor, reason and original evidence; valid normalization must not overwrite the source quote. The Python-only repair cannot close corrected-value parity in the backend.

## Evidence and manual values

The source file and canonical page text remain immutable. Every automatically extracted value must cite the exact original UTF-8 byte slice on its page: quoted text equals that slice and the raw field value. Normalized values are separate. Arabic matching may use a mapped derived view, but may not rewrite canonical text or evidence after offsets and geometry are constructed.

Preserve character-derived page geometry and confidence. The existing public schema 2.0 envelope requires a normalized polygon as well as the page and exact quote; a private parser result with only text spans does not establish a valid public extraction result. Do not invent a rectangle to satisfy that requirement. Missing geometry is outside the supported public input set. Row occurrences must be stable and unique within an extraction. Repeated headings, rows split across pages and duplicate field identities require explicit handling, not accidental overwrite or duplicate-contract failure.

Existing corrected machine fields keep their original page evidence. The UI/report must label a corrected value so the original quote is not presented as evidence that it contained the correction.

Existing reviewer-added missing fields have no saved page/quote provenance. They can be retained as reviewer-entered values, but cannot silently qualify for an evidence-backed comparison. A future manual fallback needs a saved source page and exact quote or validated region, a clear reviewer-entered label and history. No manual-provenance API or backend change is included in this draft.

## Layout boundaries and existing fixtures

| Existing material | Reusable truth | Current limitation |
| --- | --- | --- |
| `samples/phase-4/procurement-v0.1.0` | Fictional quotation, PO and invoice; existing scenario and reviewed comparison expectations | Prior Phase 4 success is not new ordinary-table extraction or new pair-policy proof. |
| `samples/phase-7/language-v0.1.0`, `language-v0.2.0`, `language-v0.3.0` | Frozen labels, text, normalized values and historical language outcomes | Preserve the original digit-scope decisions and failed results. New numeric safety expectations need separate fixtures. |
| `samples/phase-7/pdf-v0.1.0` | Fixed native/scanned mixed two-page invoice, header truth and original evidence | The historical six assessed description/quantity/price fields were missing. Net row amounts are not gross totals. Keep that receipt unchanged. |
| Existing `Line N Label: value` / `البند N ...` / `السطر N ...` tests | Labeled repeated fields, exact quotes, confidence and geometry | This is not ordinary aligned table support. It does not prove all rows or unit/basis coverage. |

An ordinary-table repair must first freeze the supported header names, column geometry/order, cell boundaries, page continuation and expected row values independently of the parser. Reject ambiguous column assignment or unsupported wrapped/multiline rows rather than guessing. Blank/malformed cells retain missing coverage. Do not generalize from a single parsed header/row shape.

Arabic presentation, header aliases and OCR configuration remain useful foundations. Arabic-only PO classification, representative Arabic tables, mixed reading order and units/bases remain unverified for this product profile. The default OCR configuration and paid-provider holds remain unchanged.

New supported fixtures use Arabic labels and characters with English numeric values. Keep separate unsupported Arabic-number cases to prove that the new profile requires attention rather than claiming complete coverage. Existing Phase 7 Arabic-number fixtures and receipts remain unchanged. Rand has not observed a character problem so far; this is user feedback, not a general Arabic accuracy result.

## Opt-in extraction implementation

Construct `LocalInvoiceExtractor(input_profile="supplier-invoice-v1")` or set `LOCAL_INPUT_PROFILE=supplier-invoice-v1` in the Python extraction process. The profile emits adapter `local-1.6.0`; the default remains `legacy` with its existing `local-1.3.0`, `local-1.4.0` or `local-1.5.0` identity. Existing startup/Docker defaults are unchanged. To use the new profile through the real backend/worker later, its configured expected adapter version must also be `local-1.6.0`. Starting only one side with the new identity fails version validation. This extraction-stage increment does not activate a whole-app workflow.

The new profile recognizes exact title lines (`Invoice`, `Tax invoice`, `Purchase order`, `Quotation`, supported Arabic equivalents) after mapped Arabic label normalization. A PO number/reference in an invoice body does not supply a PO title. Conflicting or absent titles produce `unknown` and `document_role_requires_review`; no role is silently assumed. Header fields require a label at the beginning of a line. Explicit numbered line fields and detected table-region lines are excluded from header matching.

Invalid Decimal, Date and identifier numeric portions retain the raw field and exact evidence, with `normalized_value=null` and `numeric_requires_review`. This prevents the Python profile from inventing a normalized amount. Existing backend projection can still fall back to raw text: S2 must reject unsupported raw numeric syntax before comparing; `null` alone does not close that integration requirement.

Table support is narrow: a header must explicitly contain Description, Quantity/Qty and Unit price (or supported Arabic aliases), with distinct reliable character geometry. Optional recognized columns include item code, unit, price basis, tax and an explicit gross total. `Net AED`, `Net amount` and ambiguous `Total` delimit cells but never become a gross `line_total`. Each non-whitespace header/row character must have geometry, and each row character must belong to exactly one cell. Missing geometry, overlapping/crossed cells, unsupported wrapping or blank cells cause refusal and a warning. Rows are numbered sequentially across supported pages with repeated headings; a heading is required on each page. No table continuation is inferred without it. A document mixing numbered line fields and ordinary table rows is refused for automatic row extraction, rather than merging complementary fields under an inferred shared occurrence.

After a refused table header or row, later lines on that page remain excluded from header extraction until a new supported heading. This may hide a legitimate later document total; the result requires review rather than borrowing an amount from an uncertain row. Recognized summary labels and the frozen sample's explicit notes terminate valid tables. Arbitrary notes and unsupported layouts remain conservative refusals. Native English PDF geometry proof and canonical Arabic parser proof are distinct from real Arabic/scanned OCR-table proof.

Coverage warnings include `supplier_required_fields_missing`, `line_coverage_missing` and `line_coverage_incomplete`. `line_table_requires_review` reports unsupported geometry/rows. Repeated explicit field identities are omitted with `field_identity_ambiguous`, rather than arbitrarily selecting one or failing the whole envelope. New-profile warnings are deduplicated and contract-ordered before envelope validation. These use the existing schema 2.0 warning contract; no new coverage/report schema or manual-provenance endpoint is introduced. Existing file, page, field and warning limits remain; very large or heavily degraded documents are outside the frozen supported set. OCR model metadata names only configured language/model settings and stays within the existing backend length bound.

## Meaningful batch checkpoint

Rand's latest instruction supersedes per-edit manual review: finish meaningful batches, including routine fixes and focused verification, then provide one useful manual checkpoint. Classification, English-digit numeric safety and bounded table/evidence behavior belonged to S1. Its local gates pass and its manual result remains pending. Rand's subsequent “Continue” authorizes the bounded S2 comparison batch; it does not imply a reported manual pass.

For each later gate, record the exact changed files and extractor version/profile, independent expected results, focused tests and a short setup/action/expected-result manual check. Preserve baseline copies, old fixture hashes, old adapter identities and historical receipts. A new behavior must not masquerade as an old adapter version.

S1's fixture-backed extraction and evidence results are recorded in [S1 verification](../testing/supplier-invoice-S1-20261006.md). S2 must enforce required coverage, numeric correction parity, currency/unit/basis compatibility and versioned comparison rules. Its new policy requires explicit pricing quantity/unit and net/gross unit-price amount basis separately, plus explicit aggregate and tax bases where applicable. Adapter `local-1.6.0` does not supply all these basis fields. Existing extracted pairs therefore retain unavailable checks; complete synthetic reviewed policy fixtures are not proof of a complete extraction-to-report workflow. A later extraction or manual-evidence increment must close that dependency with a new adapter identity or a faithful provenance contract before activation. Backend review presentation, manual provenance, saved pairing, report/export and integrated acceptance remain later tasks.
