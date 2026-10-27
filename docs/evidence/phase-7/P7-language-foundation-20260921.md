# Phase 7 language foundation — September 21, 2026

**Subsequent scope clarification:** Rand requires standard Western digits and
date/amount formats, not Arabic-Indic numeric extraction. This original failed
baseline is preserved as optional source-numeral coverage; it is not the revised
acceptance gate. The new [v0.2.0 text receipt](P7-standard-numerics-text-20260921.json)
passes 9/9 cases. Revised image OCR remains unmeasured. The historical results
below have not been relabeled as passing.

Phase 7 is **in progress**, not accepted. Rand authorized proceeding with Arabic
and mixed-language work. The first increment implements text parsing, experimental
Arabic OCR configuration, RTL text presentation and bounded Arabic reviewed-field
questions. Its most important finding is that the current Arabic raster OCR
accuracy is inadequate, including errors in dates and monetary digits.

## Implemented behavior

`arabic_text.py` builds an offset-mapped matching view for diacritics, tatweel,
alef variants and compatibility forms. It keeps raw strings intact and maps
Arabic/Eastern Arabic digits and numeric separators only in typed normalized
values. Leading-zero identifiers remain strings. Arabic field labels include
invoice number/date, due date, supplier TRN, currency, subtotal, VAT, total and
payment terms. Dates remain Gregorian ISO; ambiguous/conflicting due dates refuse.
This does not add general Arabic table or line-item extraction.

The local provider accepts `OCR_LANGUAGE=ara`, `ara+eng` and `eng+ara`, while
`eng` remains the default. Tesseract 5.3.0 and Debian's 4.1.0 Arabic model are
verified with the exact model hash in the receipt. Mixed mode checks individual
language availability. Arabic native PDF pages require rendered OCR; they are
not accepted through the English left-to-right glyph sorter. OCR keeps the
engine's Arabic token order. This routing needs native/multipage acceptance.

After the failed baseline, all OCR results containing Arabic letters receive
`arabic_ocr_requires_review`, a degraded warning, even with high engine confidence.
This is an experimental review aid. Source-byte correctness does not imply that
OCR recognized the correct amount. No model, symbol or digit is silently guessed
to make a sample pass. The production Dockerfile includes the pinned Arabic
package; the default demo remains English.

The review, correction, reason, reconciliation and question/citation surfaces
use automatic direction and bidi isolation while retaining source text. New
component tests verify exact Arabic correction submissions and mixed citation
text. These are DOM checks, not a completed visual browser demonstration.

Arabic questions such as `ما إجمالي الفاتورة؟` map through a whole-question
grammar to existing reviewed-field plans. Questions may target Arabic or English
facts; citations retain original Arabic numerals while confirmed normalized values
are used for typed amounts. Unknown additions, arbitrary arithmetic, payment
events and instruction text refuse. Question policy is
`bounded-ar-en-fact-intents-v1.3`; the existing answer engine and authorization
boundaries remain in place. General keyword/semantic retrieval is unchanged.

## Measured results

| Check | Result | Scope |
| --- | --- | --- |
| Canonical-text fixture evaluation | 9/9 cases: Arabic 5/5, mixed 3/3, English 1/1 | Exact expected normalized fields, required absences/warnings and raw UTF-8 evidence |
| Python extraction checks | 44 passed, 40 subtests; six gated tests skipped | Arabic normalization, model guards, reading-order mapping, due dates and existing local unit cases |
| Native-PDF check | 1 passed separately | Existing English due-date extraction and exact evidence |
| Reviewed-answer checks | 94 passed, then two additional source-numeral cases | Existing typed-fact cases plus Arabic/English questions, refusals and source/normalized-value separation; no database |
| Browser components | 63 passed serially | Full existing component set plus two Arabic/RTL cases; no live backend |
| TypeScript / ESLint | Passed | Browser static checks |
| Python basic lint | Passed | Isolated E4/E7/E9/F checks on changed parser/evaluation files |
| Documentation build | Passed | VitePress rendered all pages; existing chunk-size warning remains |

The six skips are the five separately gated historical OCR cases and the native
case run separately. They are not six missing tests or six fresh OCR passes.
The 40 unittest subtests are not added to the primary test count.

The [text receipt](P7-text-evaluation-20260921.json) and
[OCR comparison](P7-ocr-baseline-20260921.json) include the frozen dataset hash.
The image comparison exercised all nine fixtures in four modes (36 attempts):

| OCR mode | Arabic | Mixed | English |
| --- | --- | --- | --- |
| eng | 0/5 | 0/3 | 1/1 |
| ara | 0/5 | 0/3 | 0/1 |
| ara+eng | 0/5 | 0/3 | 1/1 |
| eng+ara | 0/5 | 0/3 | 1/1 |

These counts require every case check to pass; field-level results and recognized
text remain in the receipt. Inspection of the rendered `ar-basic.png` confirmed
shaped Arabic text and visible numeric source values. The engine reads many labels
but confuses numerals, separators and mixed text. Expected labels were not changed.
The two curated PNG examples and generation metadata are in
`samples/phase-7/language-v0.1.0/`; remaining generated images are preserved under
`outputs/phase-7/ocr-first/`. They are simple synthetic text layouts, not real invoices.

The comparison uses the local fast model family, not independent commercial OCR
providers. Tesseract documents the [Arabic language package](https://tesseract-ocr.github.io/tessdoc/Installation.html)
and the distinct [fast/best trained-data families](https://tesseract-ocr.github.io/tessdoc/Data-Files.html).
A future comparison must record its own model identity and results.

## Failed attempts and boundaries

The initial parallel browser attempt had two timeouts and a reconciliation
assertion that expected a reason to be one text node. The RTL markup isolates
that reason; the test now checks the containing paragraph's complete text. The
final serial run passes all 63 checks. No test timeout or product authorization
setting was relaxed. The failure output remains in the task tool history.

An initial Python check had 42 passing cases; the final warning/diacritic-date
checks raise this to 44. The global developer Ruff profile reported style findings,
including existing extractor code; only the stated isolated basic lint passed. No full
CI, package audit, deployment, retained-data migration or paid model call is implied.

The first documentation build could not read parent directories under the sandbox.
The approved rerun completed; its log is `outputs/phase-7/docs-build-approved.log`.

Docker was started for the isolated offline OCR container and stopped afterward.
The evaluation container used `--rm`, no network, a read-only source mount and a
new synthetic output directory. No database, service listener or retained upload
was used. No Git commit or push was performed for Phase 7.

## Remaining acceptance

Improve Arabic numeral OCR and evaluate alternative pinned model/provider choices;
add native/multipage, realistic-layout and line-item coverage; visually verify the
actual RTL review/citation workflow; exercise the persisted language journey; and
evaluate multilingual embeddings/cross-language semantic retrieval. Paid calls
need a separately bounded, authorized experiment. A legal synthetic dataset and
passing text parser do not close those gates.

Reproduction commands and scope are in the [quality plan](../../testing/phase-7-quality-plan.md).
