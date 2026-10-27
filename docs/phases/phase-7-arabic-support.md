# Phase 7 — Arabic and mixed-language support

**Status:** In progress — authorized September 21, 2026

Latest: [actual upload/worker and reconciliation checks](../evidence/phase-7/P7-upload-workflow-20260922.md)
verify English/mixed invoice processing, saved answers with original citations,
and Arabic reconciliation display/persistence. A subsequent-upload refresh bug
is fixed. History file delivery remains unverified in the available browser;
representative coverage and multilingual semantic evaluation are still open.

Latest browser increment: [saved answers and line-price review](../evidence/phase-7/P7-browser-and-line-prices-20260922.md)
now work through the real frontend/API/database. Missing prices can be added as
numbers on separate lines, and answers preserve reviewer versus source provenance.
Ordinary table extraction still misses the six assessed line fields in both PDF
forms; it remains an explicit gap, not a passing OCR claim.

Latest: [two-page PDF and persisted workflow checks](../evidence/phase-7/P7-pdf-workflow-20260922.md)
cover native/scanned forms, reviewed corrections, saved Arabic/English answers
and original citations. Both database-backed tests pass. All eight scored
numeric/identifier fields are exact in both forms; a payment diacritic mismatch
remains. These fictional samples cannot estimate production accuracy. The agreed
priority is reliable review and evidence, without a zero-OCR-failure promise.

September 22: [targeted OCR refinement](../evidence/phase-7/P7-mixed-ocr-refinement-20260922.md)
passes all 9 original cases and 11/12 expanded synthetic cases. The remaining
failure is an Arabic payment label beside English wording. The model/profile is
opt-in; broader representative coverage and complete browser journeys remain open.

**Outcome:** Arabic and mixed Arabic/English documents are processed and evaluated as a distinct capability.

**Numeric scope clarified by Rand:** dates, totals, prices, VAT amounts and other
numeric fields use standard Western digits and formats. Arabic labels/text are
in scope; Arabic-Indic numeral extraction is optional. The new v0.2.0 text dataset
passes 9/9 cases; the offline `best`/`eng+ara` candidate passes 5/5 Arabic, 1/3 mixed
and 1/1 English synthetic image cases. See the [OCR/RTL report](../evidence/phase-7/P7-standard-numerics-and-rtl-20260921.md).
The earlier failed numeral
baseline remains historical evidence and is no longer a mandatory acceptance gate.

The [first implementation and evaluation](../evidence/phase-7/P7-language-foundation-20260921.md)
adds Arabic text parsing, explicit Arabic OCR configuration, RTL presentation and
bounded Arabic reviewed-field questions. All nine canonical-text cases pass;
The earlier Arabic-Indic-heavy raster OCR baseline failed. Phase 7 is
not complete. See the [task ledger](../tasks/phase-7.md),
[evidence contract](../design/phase-7-language-contract.md) and
[quality plan](../testing/phase-7-quality-plan.md).

## Why it is later

Arabic support adds OCR, normalization, layout, right-to-left presentation, and multilingual retrieval challenges. Adding it after the English workflow is measurable prevents unsupported bilingual claims.

## Work

- Build a labeled Arabic and mixed-language dataset.
- Evaluate Arabic-capable OCR providers.
- Normalize Arabic text without destroying source evidence.
- Support right-to-left values and page display.
- Extract Arabic field labels/text while keeping numeric/date values in standard formats.
- Add multilingual embeddings and cross-language retrieval tests.
- Allow questions in Arabic or English.
- Report Arabic results separately from English results.

## Acceptance criteria

- [x] Arabic samples are legally usable and versioned (original synthetic v0.1.0).
- [ ] RTL content is readable throughout the review flow.
- [x] Arabic field extraction has a reproducible evaluation report (text passes; OCR baseline fails).
- [ ] Mixed-language documents retain correct reading order and evidence.
- [ ] Cross-language questions are evaluated rather than assumed to work (typed questions tested; semantic retrieval remains open).

## Portfolio evidence

- Arabic OCR/provider comparison
- Language-specific evaluation report
- RTL review demonstration
- Documented normalization decisions
