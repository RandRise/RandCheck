# Supplier invoice S1 fixtures v0.1.0

Self-authored fictional development inputs released under CC0-1.0. No real parties, supplier data, bank details or personal information. These files measure bounded parser behavior, not accuracy on real invoices or customer demand.

`source-truth.json` was authored before extractor scoring. `manifest.json` freezes its SHA-256 and all six input hashes. Existing Phase 4/7 truth and results are preserved. The generator refuses to replace existing files.

- `english-invoice.pdf` and `english-po.pdf`: actual native A4 PDFs with a two-row aligned Description/Qty/Unit price/Net AED table. Invoice PO reference is body text, not a second document title. Expected total is 1260.00 AED, quantities 2 and 1, and unit prices 400.00 each. Net amounts 800.00 and 400.00 must not become gross line totals.
- Four `.txt` files: English pair and Arabic-label/English-number pair using explicit labelled rows. These are text-helper inputs; they do not establish Arabic PDF or OCR support.
- Numeric cases: valid ASCII amounts and explicit malformed/unsupported forms. Arabic digits and separators require review/correction; raw evidence must remain unchanged. Legacy Arabic conversion is a separate historical capability.

From the repository root, after the opt-in extractor profile is installed in the current source:

```powershell
.venv/Scripts/python.exe samples/supplier-invoice/v0.1.0/evaluate.py
```

Expected: invoice/PO roles, total 1260.00 AED, two rows with quantities 2/1 and unit prices 400.00/400.00, exact raw page quotes and native polygons. `Partial` is correct: units, price basis and explicit gross line total are incomplete. The command saves source values/evidence separately from normalized values in a fresh output folder and refuses to replace a supplied output directory.

Open the two PDFs and compare those printed values with the original row cells. No app provisioning, paid provider, OCR or UI preview is needed. This check does not exercise backend review corrections, source admission, a completed invoice/PO comparison, report export or live identity.

The geometry tests now use the separately pinned `../native-table-v0.1.0/` PDF,
which preserves the historical Phase 7 English table positions using only PDF
core fonts. A clean checkout no longer needs an ignored local PDF. The original
Phase 7 PDF, source truth and six historically missing line fields remain in their
original records. Neither this table fixture nor its adversarial geometry tests
claim public mixed-language OCR accuracy.
