# Supplier invoice S1 baseline and fixture preparation

October 6, 2026. S1 is active; this receipt covers the unchanged baseline and new independent fixture preparation. It does not close S1, manual acceptance, live identity, R5/R6, Arabic accuracy or customer demand.

## Current checkpoint

Rand subsequently requested continuing to meaningful manual milestones rather than stopping after every simple edit. The coordinator authorized the bounded opt-in S1 profile, independent fixture preparation and actual native PDF verification. Classification, numeric safety and narrow table support form this extractor milestone; S2 and later product increments remain pending.

New `samples/supplier-invoice/v0.1.0/source-truth.json` freezes four English/Arabic-label text helpers, two English native PDFs, 28 numeric acceptance/refusal cases and source-authored table cell positions independently of extractor output. Supported numeric syntax is ASCII digits, dot decimal and correctly grouped comma only. `manifest.json` freezes truth and input hashes. Both English PDFs were rendered with existing Poppler at 110 dpi and visually inspected: clear titles/values, distinct aligned columns, no clipping or overlap. Poppler printed a Symbol-font warning while producing both pages; neither fixture uses Symbol and visual inspection found no missing characters. No dependencies were installed.

The final frozen focused gate passes **200 unique tests plus 43 unittest subtests**, with zero failures/skips, in **6.07 seconds**, exit 0. The independent test file contributes 54 tests; the implementation file contributes 79. The remaining 67 tests cover affected unchanged behavior, including two existing actual native checks. Do not sum overlapping baseline/intermediate runs. Formal independent source review passed. Rand's meaningful manual checkpoint remains pending.

The compact user-run evaluator also passes, exit 0. It checks four text helpers and 28 numeric vectors, then runs both real English native PDFs through public `LocalInvoiceExtractor.extract` using `supplier-invoice-v1`/`local-1.6.0`. Roles, headers, exact two rows, six line fields per PDF, UTF-8 source quotes and native polygons match independently authored truth. Both results are correctly `Partial` for missing unit/price-basis/explicit gross-total coverage; footer text creates no extra row or false table warning.

The independent Phase 7 bridge now finds all six expected description/quantity/unit-price fields from the original real native PDF geometry. It uses actual pdfplumber character reading and `_native_page`, followed by the new parser. This is not public mixed-language PDF extraction or OCR acceptance; the old native/scanned 0/6 receipts remain unchanged. Arabic-label/English-number helpers, mock public-envelope serialization and synthetic geometry refusal tests have their own labelled boundaries.

Production changes are confined to `apps/api/app/local_extraction.py` and `apps/api/app/arabic_text.py`. The new profile is opt-in through `LocalInvoiceExtractor(input_profile="supplier-invoice-v1")` or `LOCAL_INPUT_PROFILE`; startup/default legacy identities remain unchanged. The standalone evaluator uses the opt-in profile directly. It does not activate the backend/worker or UI; their adapter version configuration must agree before a later real-app integration.

## Short manual milestone

**Setup:** From the repository root use the existing `.venv`. **Action:** Run the command below, open both English PDFs in `samples/supplier-invoice/v0.1.0`, and compare their row cells with the printed results. **Expected:** invoice/PO roles, total 1260.00 AED, two quantities 2/1, two prices 400.00/400.00 and exact evidence PASS; `Partial` honestly identifies the remaining unit/basis gap. Give feedback before S2 or another dependent product increment.

```powershell
.venv/Scripts/python.exe samples/supplier-invoice/v0.1.0/evaluate.py
```

The command saves raw values, normalized values, page quotes and polygons into a fresh `outputs/supplier-invoice-S1-20261006/manual-*` folder. It verifies frozen truth/input hashes before reading. This is actual native Python extraction with representative sample data, not a synthetic browser preview or a completed invoice/PO product journey.

## Exact final command and receipts

```powershell
$env:PYTHONPATH = 'apps/api'
$env:RUN_DUE_DATE_NATIVE = '1'
$env:RUN_LOCAL_PROVIDER_INTEGRATION = '1'
.venv/Scripts/python.exe -m pytest apps/api/tests/test_supplier_invoice_extraction.py apps/api/tests/test_supplier_invoice_fixture_integration.py apps/api/tests/test_arabic_text.py apps/api/tests/test_arabic_pipeline.py apps/api/tests/test_arabic_ocr_layout.py apps/api/tests/test_due_date_extraction.py apps/api/tests/test_local_extraction.py::LocalProviderSelectionTests apps/api/tests/test_local_extraction.py::LocalProviderFailureTests apps/api/tests/test_local_extraction.py::LocalProviderApiTests apps/api/tests/test_local_extraction.py::LocalProviderRuntimeFixtureTests::test_native_pdf_matches_exact_expectation -q --junitxml=outputs/supplier-invoice-S1-20261006/final-focused-corrected.xml
```

Only the native golden method is selected from the runtime fixture class; no scanned/image/OCR runtime case was executed. `RUN_DUE_DATE_NATIVE` selects its existing native PDF proof. Arabic OCR-layout tests use fake OCR data/transports. The local Python 3.11.9 development environment has the required native package pins; this is not a new enterprise Python 3.11.16 or OCR-image acceptance run.

Saved evidence under `outputs/supplier-invoice-S1-20261006`:

- `final-focused-corrected.xml`, `.txt` and `final-focused-corrected-status.json`: 200 passing testcase elements, 43 separately reported subtests, exit 0. The JUnit aggregate includes subtests; unique test count comes from testcase elements.
- `final-corrected-source.json` and `final-source-verification.json`: exact before/after hashes for both production files, both new test files, frozen truth and evaluator; all unchanged throughout the corrected gate and evaluator.
- `final-evaluation.txt`, `final-evaluation-status.json`, and `final-evaluation/receipt.json` plus two `*-extraction.json` files: compact public native results and full raw/evidence output, exit 0.
- `english-invoice-qa.png` and `english-po-qa.png`: inspected native fixture page renders. Skill operation marker succeeded once immediately before creating the two PDFs.

For an explicit saved evaluator output directory, the successful verification command was:

```powershell
.venv/Scripts/python.exe samples/supplier-invoice/v0.1.0/evaluate.py --output outputs/supplier-invoice-S1-20261006/final-evaluation
```

That directory now exists; use the short command with its fresh default folder for a new manual run.

## Initial failures and corrections

The intermediate independent selection passed 48/48 in 2.68 seconds before later coverage additions; `independent-intermediate.xml/.txt` and `intermediate-evaluation*` preserve that checkpoint. Its helper receipts used confidence 95 and are superseded by the corrected final helper receipts; actual native PDF results used native confidence separately. Neither intermediate pass is added to the final distinct count.

The first combined 200-test run failed **36**, passed **164**, with **43 subtests**, in **63.25 seconds**, exit 1. Preserve `final-focused.xml/.txt`, `final-focused-status.json` and `final-source.json`. Thirty-four public mocked tests had geometry-free synthetic fields, which violate the existing public schema's normalized polygon requirement; two independent mocked-envelope tests had synthetic confidence 95 outside the required range 0–1. Actual public native PDF and existing legacy checks passed. These were invalid test fixtures, not evidence that the actual native reader failed.

Test-only repairs supplied explicitly synthetic valid geometry to the public mock helper and changed helper confidence to 0.95. Intentional no-geometry/refusal cases, independent expected values, field/quote/role assertions, validator and timeouts were preserved. `failure-production-hash-check.json` proves both production source files unchanged across the failure and correction. Frozen truth, manifest, text and PDF bytes were not rewritten. The complete corrected selector then passed 200/200; no initial failure artifact was overwritten.

## Remaining acceptance boundaries

No OCR, new Arabic native/scanned public extraction, backend source admission or completed-review persistence was performed. Existing backend projection can fall back to raw text when normalization is null; S2 still needs supported numeric validation and corrected-value parity before comparisons. No full row unit/basis coverage, pair policy, manual provenance API, report/export, live identity/R5/R6 or customer-demand proof is claimed. S1's broader evidence-through-review requirement remains pending; this is the completed extraction-stage checkpoint awaiting Rand's feedback.

## Earlier contract review checkpoint

The only product-plan change at this checkpoint is the new [input-contract draft](../design/supplier-invoice-input-contract.md). The implementation agent confirmed no extractor or existing test changes; the coordinator preserved eight original files and their SHA-256 hashes in `outputs/supplier-invoice-S1-20261006/source-baseline.json`. The testing agent created only baseline output and this receipt.

**Setup:** Open the input-contract draft. **Action:** Review Supported input, Proposed numeric policy and Layout boundaries. **Expected:** One complete invoice and PO in the same currency; English-digit `1050.50` is supported, Arabic-number forms need explicit review or correction, and `1,23` needs attention; net row amounts are not gross totals; missing or unsupported rows cannot imply complete agreement. Rand's October 6 numeric-scope feedback is incorporated; this is still a document review rather than a new parser result.

At this earlier checkpoint only the document had changed; no extractor, real API or UI-preview behavior had changed. Rand's later cadence instruction supersedes the edit-by-edit pause; the completed extractor milestone still needs a short manual check and actual feedback before dependent implementation.

## Unchanged parser baseline

Run from the repository root with the existing local `.venv` Python 3.11.9 runtime; no installation, OCR execution, full build, demo or paid call:

```powershell
$env:PYTHONPATH = 'apps/api'
.venv/Scripts/python.exe -m pytest apps/api/tests/test_arabic_text.py apps/api/tests/test_arabic_pipeline.py apps/api/tests/test_local_extraction.py::LocalProviderSelectionTests::test_classifies_procurement_document_titles apps/api/tests/test_local_extraction.py::LocalProviderSelectionTests::test_extracts_repeated_reconciliation_line_fields_with_page_evidence -q --junitxml=outputs/supplier-invoice-S1-20261006/baseline-existing.xml
```

Result: exit 0; **19 tests passed, with 15 additional unittest subtests**, in 17.64 seconds. Subtests are not added to the unique test count. Saved outputs are `outputs/supplier-invoice-S1-20261006/baseline-existing.xml` and `baseline-existing.txt`. These passing tests establish the existing parser behavior, including explicitly labelled rows; they do not establish the proposed stricter numeric profile or ordinary-table support.

## Independent fixture plan

New fixture truth belongs under `samples/supplier-invoice/v0.1.0`. Keep every Phase 4/7 file, expected value, manifest and historical result unchanged. Use only self-authored fictional parties and values, with an explicit CC0 release and no real bank, identity or customer data. Author expected values before scoring extractor output. Each actual PDF needs its exact hash, page count, representation, source-authored cell boundaries and expected fields. Text/helper fixtures must identify themselves as parser-level tests.

| Fixture family | Independent expected result |
| --- | --- |
| English invoice and PO | Explicit respective titles; distinct text identifiers, same fictional supplier TRN and AED; two complete source-authored rows. Invoice body PO reference must not override invoice title. |
| Arabic/English invoice and Arabic-only PO title | Explicit roles and Arabic PO number label, with English-digit numeric values and leading-zero identifiers retained. Arabic source quotes remain unchanged. |
| Numeric acceptance | English ASCII digits only; dot decimal; comma grouping in exact three-digit groups. At most 18 integer and 6 fractional digits; normalized value separate from the quote. |
| Numeric refusal/uncertainty | Arabic-Indic/Eastern or mixed digits, Arabic separators, `1,23`, `1.234,56`, malformed grouping, excessive precision, signs, exponent, currency/unit suffixes and missing integer part require attention, with no silently accepted conversion. |
| Header and line separation | A missing document total stays missing when a row amount exists. Explicit net row amounts must not be promoted to gross line totals. |
| Ordinary aligned columns | Freeze supported headers and per-character/cell geometry. Two expected descriptions, quantities and unit prices; keep item identity, unit and amount basis explicit. Delimiter rows are a separate text shape. |
| Unsupported/ambiguous rows | Wrapped description, repeated/restarted indices across pages, blank or malformed cells and ambiguous column assignment require explicit missing coverage rather than inferred rows. |
| Provenance | Exact original UTF-8 page slice equals raw value/quote; source geometry and page are retained separately from normalized values. Synthetic helper polygons do not prove actual PDF/OCR geometry or backend review persistence. |

Start small: four canonical invoice/PO documents (English pair and Arabic/English pair), plus narrowly targeted variants for each separate modification. Do not create a general layout benchmark or imply accuracy on real supplier documents.

## Frozen Phase 7 ordinary-table evidence

Existing `samples/phase-7/pdf-v0.1.0` truth covers a two-page mixed invoice. Its native PDF hash is `e6dc0255c374659712e66b04da33bc6333712148fab1aef1ec63e62bee60a990`; scanned PDF hash is `029c75f02956915f40f0c573e1d4d1a9e7f06fa4badd96acc8153a5a6e705082`. The frozen canonical native text concatenates cells:

```text
DescriptionQtyUnit priceNet AED
Document review training2400.00800.00
Workflow configuration session1400.00400.00
```

The source generator `tools/phase7_pdf_samples.py` puts description at x42, quantity at x325, unit price at x390 and net amount at x486, with rows at y435 and y395. These are generator-space positions, not invented normalized extraction polygons. Source truth is descriptions as shown, quantities 2 and 1, unit prices 400.00 each, and **net** amounts 800.00 and 400.00. Plain concatenated text cannot unambiguously establish those cell boundaries; use actual character geometry for any claimed support.

Historical native/scanned extraction assessed headers at 8/9 exact and the six description/quantity/unit-price fields at 0/6. Preserve those historical receipts. At the earlier baseline checkpoint no new PDF reading, OCR, source admission, completed-review or comparison acceptance had been performed. The current native proof is described separately above. Reviewed manual additions still lack saved page/quote provenance; this fixture work does not repair that gap.
