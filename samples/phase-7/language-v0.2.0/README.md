# Language fixtures v0.2.0 — standard numeric fields

This is the required Phase 7 scope following Rand's clarification: dates, totals,
prices, VAT amounts, quantities and identifiers use Western digits (`0–9`) and
standard numeric/date formats. Arabic applies to labels and textual content.
The system does not translate numeric values into Arabic or spell amounts out.

The nine fixtures retain the v0.1.0 text/label scenarios with source digits and
numeric separators converted to the required formats. Expected raw string values
were adjusted at the same time, before evaluation. The old version and its failed
OCR results remain unchanged as optional Arabic-Indic numeral coverage. Comparing
the versions is a scope comparison, not proof of an OCR engine improvement.

These are original fictional project-authored samples, with the same rights and
provenance as v0.1.0. No customer or copied document data is included. Text parser
evaluation and image OCR evaluation remain separate. A text pass does not establish
OCR accuracy for the revised images.

Both Phase 7 evaluators default to this dataset. To reproduce the historical
baseline, explicitly pass `--dataset samples/phase-7/language-v0.1.0/cases.json`.
