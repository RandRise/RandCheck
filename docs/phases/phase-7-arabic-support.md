# Phase 7 — Arabic and mixed-language support

**Status:** Proposed  
**Outcome:** Arabic and mixed Arabic/English documents are processed and evaluated as a distinct capability.

## Why it is later

Arabic support adds OCR, normalization, layout, right-to-left presentation, and multilingual retrieval challenges. Adding it after the English workflow is measurable prevents unsupported bilingual claims.

## Work

- Build a labeled Arabic and mixed-language dataset.
- Evaluate Arabic-capable OCR providers.
- Normalize Arabic text without destroying source evidence.
- Support right-to-left values and page display.
- Extract Arabic field labels and values.
- Add multilingual embeddings and cross-language retrieval tests.
- Allow questions in Arabic or English.
- Report Arabic results separately from English results.

## Acceptance criteria

- [ ] Arabic samples are legally usable and versioned.
- [ ] RTL content is readable throughout the review flow.
- [ ] Arabic field extraction has a reproducible evaluation report.
- [ ] Mixed-language documents retain correct reading order and evidence.
- [ ] Cross-language questions are evaluated rather than assumed to work.

## Portfolio evidence

- Arabic OCR/provider comparison
- Language-specific evaluation report
- RTL review demonstration
- Documented normalization decisions
