# Phase 7 realistic PDF and persisted workflow check — September 22, 2026

The agreed direction is to make imperfect extraction reviewable and traceable,
without promising zero real-world OCR failures. This increment adds two-page
fictional invoices and database-backed language workflow checks. It does not
change OCR runtime behavior or the earlier failing fixture expectations.

## Actual PDF extraction

The pack contains native and image-only forms of the **same fictional invoice**.
Page one has English metadata and a two-row item table; page two has Arabic
labels/payment text with Western-digit totals. Both final PDF pages were rendered
and visually inspected. The extractor uses native text on the first native-PDF
page, rendered OCR on its Arabic page, and OCR on both scanned pages.

| Check | Native form | Scanned form |
| --- | --- | --- |
| Invoice ID, invoice date, due date, supplier TRN | 4/4 exact | 4/4 exact |
| Currency, subtotal, VAT, total | 4/4 exact | 4/4 exact |
| Payment terms, strict text match | Mismatch | Mismatch |
| Original UTF-8 evidence slices | All match | All match |
| Application extraction outcome | Partial; review required | Partial; review required |

The sole scored mismatch is `خلال 30 يوماً` becoming `خلال 30 يوما`: a missing
tanwin mark, with the 30-day period intact. Strict matching remains 8/9 fields
for each form; neither passes the all-fields-exact rule. No amount/date/ID error
was observed in this pack. These are correlated synthetic documents and provide
no estimate of a production failure rate. The item table is present for layout
realism; line-item extraction and full-page transcription are not scored here.

Frozen actual extraction, source hashes, expected values and failures are in
`samples/phase-7/pdf-v0.1.0/`. Source PDFs are local generated artifacts under
`output/pdf/phase-7-language-v0.1.0/`; PNG renders are in
`tmp/pdfs/phase-7-language-v0.1.0/`. The evaluator checks source hashes before
extraction and records exact extraction hashes for persistence regression tests.

Profile: `eng+ara` / `best` / `lines-v1`, 300 DPI, default 30-second page budget.
The pinned evaluation image is `uaedi-phase7-ocr-v1`, image SHA-256
`235ca09593f7e88a21362b966d881478415513d5df2d66d620d4908f9c3ba3b5`.
Arabic best model SHA-256:
`ab9d157d8e38ca00e7e39c7d5363a5239e053f5b0dbdb3167dde9d8124335896`.
The container had networking disabled. Raw OCR output is retained in
`outputs/phase-7/pdf-v0.1.0/`. Default application OCR settings remain unchanged.

## Saved workflow scope

`ArabicPdfWorkflowPersistenceTests` imports the frozen actual extraction into
real PostgreSQL, preserving its Partial status, warnings, canonical text, field
values and evidence byte offsets. It uses the real application review handlers,
index handler, private local Python chunking service, reviewed-question handler,
saved question reader and evidence handler, with fresh service providers across
save/reopen boundaries.

The checks cover numeric confirmations; correction of the payment diacritic
with an Arabic/mixed reason; completed review/history reopening; Arabic total,
VAT, due-date and payment questions plus an English total question; saved-answer
reopening and request replay; amount and currency citations; original page-one
and page-two quotes; stale review rejection; foreign-organization evidence denial;
unsupported Arabic question refusal; and zero paid-spend reservations.

Final persistence run: **2 passed, 0 failed, 0 skipped**, covering both PDF forms.
Receipt: `outputs/phase-7/pdf-workflow-run2/pdf-workflow.trx`. The initial backend
build passed with zero warnings/errors. The first run's two failures were test
assertions comparing .NET 100-nanosecond timestamp precision
with PostgreSQL microsecond precision, after reviews had saved successfully.
The assertion now checks stored precision and unchanged review fields. The test
also checks both citations that monetary answers require: amount and currency.
The initial failed receipt remains in `outputs/phase-7/pdf-workflow-run1/`.

The two new Python tools pass syntax compilation and focused Ruff checks.
Documentation builds successfully (with the existing bundle-size warning), and
the normal CRLF-aware whitespace check passes. An additional check with Git's
line-ending conversion disabled incorrectly treated existing CRLF lines as
trailing whitespace; no unrelated files were rewritten to satisfy that override.

The disposable pgvector/PostgreSQL container binds only `127.0.0.1:55437` and
uses temporary memory-backed data, separate from every retained application
database. The test fixture's database drop/recreate operations target only this
new container. It does not exercise HTTP upload/worker ingestion, authentication
middleware, browser layout, or multilingual semantic retrieval. No paid calls,
retained-data changes, commit or push are part of this increment.
The owned disposable database container was stopped/removed and Docker Desktop,
which was started for this validation, was stopped. Private Python test services
were disposed by the test harness.

## Remaining acceptance work

Phase 7 is still in progress. Next are browser-level saved history/reconciliation
RTL checks, broader representative invoice and line-item coverage, and separate
multilingual semantic retrieval evaluation. The prior mixed English payment
label failure remains documented. A representative labeled corpus is needed
before choosing quantitative production accuracy targets; this tiny pack cannot
set them. Continue with review and faithful evidence rather than tuning every
synthetic OCR mismatch to zero.
