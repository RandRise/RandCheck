# Phase 7 saved browser workflow and line-item prices — September 22, 2026

The real browser workflow now supports adding numeric prices for separate line
items, saving Arabic correction reasons, completing a review and reopening its
answers with the original evidence. This is a reviewer fallback for missing
table extraction, not an OCR accuracy improvement. Phase 7 remains in progress.

## Finding and application fix

The missing-field form previously hard-coded `String` and occurrence `0` for
every addition. It could not represent repeated numeric prices properly. The
form now lets the reviewer choose a type and position. Line-item positions map
to the extractor's existing one-based row numbers; ordinary singleton fields
still use occurrence zero. Repeated field headings display their line number.
The form resets to Text and position 1 after a successful save. Arabic/mixed
reasons use the existing bidi component so numeric and Latin runs stay together.

An initial browser attempt exposed a mistaken zero-based line convention in the
new control. It was corrected before the final run. The final fixture was added
as a separate document, preserving the first attempt until disposable cleanup.
The regression test now checks that adding line 2 preserves and displays line 1.

## Actual extraction coverage

The unchanged native/scanned PDFs both have two table rows, but their frozen
extraction receipts contain **zero of the six assessed structured line fields**:
two descriptions, two quantities and two unit prices. This is a table-parsing
gap. The local parser recognizes explicitly labeled lines such as
`Line 1 Unit Price: 400.00`; it does not currently reconstruct these ordinary
table rows. Native text also joins neighboring table cells. It is not evidence
that Arabic amount recognition failed.

The additional source expectations are `samples/phase-7/pdf-v0.1.0/line-items.json`.
[Final line coverage](./P7-line-coverage-final-20260922.json) uses row numbers 1 and 2;
the first zero-based assessment is retained separately. Both score 0/6, because
all these fields were absent. Header/totals results remain unchanged: 8/9 strict
fields per PDF, with only the known payment diacritic mismatch. Net row amounts
are not assumed to be gross line totals, and neither PDF is a production benchmark.

## Browser and persistence evidence

The actual Next frontend used the real .NET API, private Python chunking service
and an isolated PostgreSQL database. Extraction was imported from the hash-pinned
native receipt. Eight header confirmations and two reviewer-transcribed
descriptions were prepared by the fixture seeder. The browser then performed:

1. Correct `خلال 30 يوما` to `خلال 30 يوماً` with an Arabic reason.
2. Add `400.00` as Decimal unit prices for lines 1 and 2 with distinct reasons.
3. Complete the review; reload and verify both rows and the correction remain.
4. Pin the completed review and ask `What is the invoice training unit price?`.
5. Open the saved `400.00 AED` answer's price provenance: Added, with no invented
   page quotation. Currency and line-description decisions are also cited.
6. Ask `ما شروط دفع الفاتورة؟`; inspect the corrected answer and original page-2
   OCR quote. The source still lacks the diacritic, as it should.
7. Reload the page and use “Reopen a saved result” to restore both answers.

In-session screenshots verified the repeated row labels, Arabic/mixed reasons,
standard amount display, reviewer provenance and highlighted source text. These
were actual application responses, not the earlier synthetic UI adapter.
[The saved audit](./P7-browser-saved-workflow-20260922.json) verifies completed
history, Decimal types, row identities, saved answers, reviewer provenance and
exact UTF-8 source slices through the API. A database query found zero
`cited_spend_reservations`.

The history endpoint returned HTTP 200 with the correct stored actions/reasons.
The browser export button was exercised, but its download event was not captured
within 3 seconds on the initial attempt or 10 seconds on the final attempt.
No completed file-delivery claim is made. This needs a separate browser download
check; it does not invalidate the verified persisted history response.

## Reproduction and checks

`tools/phase7-browser-seed/` is fixed to the disposable `phase7_browser` database
on `127.0.0.1:55438`. It verifies source/extraction hashes, migrates the empty
fixture schema and refuses a populated database unless `--append-fixture` is
explicitly supplied. This only appends synthetic scenarios; it does not reset
or delete data. The test API uses port 5327, Python 8328 and Next 3328. Use an
isolated uploads directory, disable extraction/storage workers, and use the
Development profile only for this loopback test environment.

`tools/phase7-browser-audit.ps1` accepts the two browser-produced retrieval IDs
and verifies the saved workflow. `tools/phase7_line_coverage.py` scores the frozen
receipts without rerunning or modifying OCR. Both refuse to overwrite an audit.

Focused frontend verification: **9 tests pass** (review form and bidi component),
including numeric type, line numbering, preserving the first line and form reset.
The seeder builds and runs successfully. TypeScript and focused ESLint also pass.
Python syntax/basic lint and whitespace checks pass. Documentation builds
successfully with the existing bundle-size warning. The final port check found
no remaining test listeners on 3328, 5327, 8328 or 55438.

This does not cover browser upload/worker extraction, the reconciliation RTL
journey, general table extraction, arbitrary Arabic questions or multilingual
semantic retrieval. Those remain open. No paid call, retained database change,
commit or push was performed.
The temporary browser tab, frontend/API/Python listeners and disposable database
were closed/stopped, and Docker Desktop was stopped. Temporary Next type includes
were removed while preserving the pre-existing TypeScript configuration.
