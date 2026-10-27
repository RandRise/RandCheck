# Mixed-language PDF workflow fixtures v0.1.0

These are two representations of **one fictional invoice**, not two independent
accuracy observations. The source expectations were fixed before OCR. No source
expectation or extraction receipt was changed to turn a mismatch into a pass.

`manifest.json` pins both source PDF hashes and the actual extraction JSON hashes.
Generated PDFs are local outputs in `output/pdf/phase-7-language-v0.1.0/`;
the checked-in regression inputs are the two extraction receipts. The native
document has English native text on page one and Arabic text on page two, which
the current extractor sends through rendered OCR. The image-only PDF uses OCR
on both pages. Totals and payment terms are on page two.

Both forms extracted all eight required IDs/dates/currency/amount fields exactly.
Payment wording lost the final tanwin mark: `خلال 30 يوماً` became `خلال 30 يوما`.
The exact-match check remains false. Both outcomes remain `Partial` and retain
the `arabic_ocr_requires_review` warning. Table line-item extraction is not scored
in this pack; the presence of a rendered table is not evidence of line-item support.

A subsequent, separate assessment in `line-items.json` scores descriptions,
quantities and unit prices at the extractor's one-based row numbers. Both frozen
receipts have 0/6 of these structured fields. Run `tools/phase7_line_coverage.py`
with a new `--output` path. This does not change the original header expectations.

`tools/phase7_pdf_samples.py` builds the pack with ReportLab 4.4.9,
arabic-reshaper 3.0.0, python-bidi 0.6.7, Poppler, and Windows Arial. The font hash
used for this receipt is
`c9b76220a5be42ead4733611e417cd65c5fd8aeaa33eb56576ac378a37d130a1`.
Output is deterministic only with matching dependencies/fonts. New renderings
must receive new source hashes and extraction receipts.

`tools/phase7_pdf_evaluate.py --source <pack-directory> --output <new-directory>`
runs the pinned local extractor with `eng+ara`, `best`, `lines-v1`, and the default
30-second page budget. It checks every quoted UTF-8 source slice and records
field failures without hiding them. It refuses to overwrite an evaluation folder.

`ArabicPdfWorkflowPersistenceTests` imports these frozen real extraction receipts
into a disposable PostgreSQL database, retaining the Partial status and warnings.
It uses actual review/index/question/evidence handlers and a private local Python
chunking service. It confirms numeric fields, corrects the payment diacritic,
reopens saved review/history/answers, and checks original source citations,
stale review versions, cross-organization denial and unsupported-question refusal.
It does not exercise upload, worker ingestion, HTTP authorization middleware or
browser layout. There are no paid provider calls.
