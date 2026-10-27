# Arabic labels through the existing English row parser — September 22, 2026

The existing labeled-row extractor now accepts Arabic aliases for item code,
description, quantity, unit price, tax and total. This follows Rand's request
to keep the change simple and reuse the working English implementation.

For example, `Line 1 Unit Price: 400.00` and
`البند 1 سعر الوحدة: 400.00` both produce `line_unit_price`, occurrence `1`,
type `Decimal`, value `400.00`. `السطر` is also accepted as a row marker.
Values use the existing normalization and downstream field contract. No
translation, new OCR engine or general table reconstruction was introduced.

Arabic label matching uses the existing derived matching view. Value offsets
are mapped back to original source characters before extracting quotes,
confidence and geometry. Arabic descriptions retain their spelling, diacritics
and compatibility characters. Canonical page text remains unchanged.

## Verification

Run from the repository root:

```powershell
.venv/Scripts/python.exe -m pytest apps/api/tests/test_arabic_pipeline.py apps/api/tests/test_arabic_text.py apps/api/tests/test_arabic_ocr_layout.py apps/api/tests/test_local_extraction.py apps/api/tests/test_due_date_extraction.py -q
.venv/Scripts/python.exe -m ruff check apps/api/app/local_extraction.py apps/api/tests/test_arabic_pipeline.py --select E9,F63,F7,F82
git diff --check
```

Result: **65 tests and 43 subtests pass**, six opt-in runtime tests skipped.
Basic Ruff and whitespace checks pass. Documentation builds successfully with
the existing bundle-size warning. New cases cover English, mixed and
Arabic aliases, two pages/row positions, Western numbers, label diacritics and
tatweel, exact UTF-8 source quotes, value geometry and confidence. A public
extractor-envelope test checks standard dates/VAT/totals, both line prices,
unchanged canonical text and the retained Arabic OCR review warning. Its first
test-only attempt omitted required geometry; adding synthetic character boxes
fixed the fixture without weakening the production contract.

The existing English regression tests pass. OCR runtime and browser workflows
were not rerun. The frozen ordinary-table receipts still lack the six assessed
line fields; this alias extension does not resolve that distinct layout gap.
No production failure-rate or zero-failure claim follows from these tests.
Phase 7 remains in progress. No paid calls, retained-data changes, commit or push.
