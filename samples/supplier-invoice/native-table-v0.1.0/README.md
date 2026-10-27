# Native table geometry fixture v0.1.0

Project-authored fictional data, CC0-1.0. The PDF uses only core Helvetica, with
no embedded font program, personal data or real supplier details.

This separate one-page PDF preserves the English table positions, descriptions,
quantities and prices from the historical Phase 7 layout. It makes geometry
regressions reproducible without an ignored local output or Windows font.
Expected rows remain in the unchanged S1 `source-truth.json`; the manifest pins
this new PDF independently. It is not a replacement for the historical Arabic PDF,
its hashes, extraction receipts, evidence or OCR accuracy measurements.

Tests inspect actual pdfplumber character boxes, then exercise missing geometry,
wrapped descriptions, repeated headers, overlapping columns and uncertain cells.
The parser must refuse unsupported rows rather than guess. Regenerating with
`python samples/supplier-invoice/native-table-v0.1.0/generate.py` refuses to replace
existing files. Dependency changes require a new fixture version and review.
