# Language fixtures v0.1.0

Nine original fictional fixtures: five Arabic, three mixed Arabic/English and
one English control. The text and expected field values in `cases.json` were
written before running the parser and OCR evaluation. No real invoice, customer,
personal data or copied third-party document text is included. These project-authored
synthetic fixtures may be used, modified and redistributed with the project.
This is not a decision about the repository's software license.

`cases.json` SHA-256:
`6a9835bc799f0f967ca01644c18690b479dcc60e18a48b85656abe20d30903eb`.
Do not update this version to make a failing OCR result pass. Changes to labels,
coverage, font or layout require separately recorded versions/receipts.

The set covers Arabic-Indic and Eastern Arabic-Indic digits, leading-zero tax
identifiers, Arabic decimal/thousands separators, diacritics, tatweel, nearby
mixed labels, Gregorian ISO due dates, ambiguous dates and conflicting dates.
Names/free text retain their spelling. Document numbers are strings, not numbers.
Arabic text is in logical Unicode order. This is a development set, not a held-out
accuracy benchmark or a comprehensive Arabic annotation review.

`tools/phase7_evaluate.py` scores canonical text without OCR. The separate
`tools/phase7_ocr_evaluate.py` shapes the same source strings into PNGs with RAQM
and DejaVu Sans and compares English, Arabic and both combined Tesseract orders.
It records font/image hashes and uses a new output directory for every run.
Rendered images contain the fixture text only; they are not realistic invoices.

See the [language contract](../../../docs/design/phase-7-language-contract.md) and
[quality plan](../../../docs/testing/phase-7-quality-plan.md).
