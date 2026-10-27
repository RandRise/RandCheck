# Phase 7 language and evidence contract

Rand authorized Phase 7 on September 21, 2026. Work uses fictional local samples,
the existing review/security boundaries and provider-free evaluation. Paid model
experiments and retained database changes are outside this increment.

## Evidence

**Rand's numeric-scope clarification (September 21):** required date, amount,
price, VAT, quantity and identifier values use Western digits and standard
formats such as `2026-09-21`, `1050.00` and `AED`. Arabic support concerns labels
and textual content; values are not translated or spelled out in Arabic.
Recognition of Arabic-Indic source numerals is optional coverage, not a Phase 7
completion requirement. The required dataset is `language-v0.2.0`; v0.1.0 and its
failed results stay intact. Image OCR has separate versioned receipts.

Canonical page text is the extraction provider's reading-order text. A field's
raw value and quote must match its exact UTF-8 byte slice. Arabic matching may
remove vowel marks/tatweel and fold alef variants only in a derived string with
an explicit character-to-source map. Never normalize canonical text after spans
or character boxes have been constructed. Never reverse strings to display RTL.

Optional numeric normalization maps Arabic-Indic and Eastern Arabic-Indic digits to ASCII,
Arabic decimal/thousands separators to their numeric equivalents and explicitly
recognized dirham names to AED. Identifiers retain leading zeroes. Dates accept
only valid Gregorian ISO dates; ambiguous local dates require review. Supplier
names and free text retain their spelling. Arabic script detection is descriptive,
not a statistical language detector or OCR-quality score.

## Reading order and presentation

English native PDF behavior stays measurable against its existing fixtures.
Arabic native glyph order cannot be recovered by sorting x coordinates. Until a
native bidi adapter is evaluated, Arabic pages require rendered OCR with Arabic
model support. OCR token order must be preserved, including embedded Latin and
numeric tokens. A missing or changed model fails closed. Arabic support is opt-in;
the English OCR default remains unchanged.

The September 22 `lines-v1` candidate can re-read suspicious Arabic lines before
constructing canonical text: uncertain Latin tokens, separator-delimited rows,
or an incomplete trailing label/colon. It preserves each selected engine pass's
token order and maps crop coordinates back to the page. Overlapping line regions
and more than 80 lines retain the block output. Retries share a deadline and
cannot change observed numeric runs or protected Latin tokens. A higher-confidence
Arabic-only retry may replace an uncertain word, with the review warning retained.
The `best` model and `lines-v1` profile both require explicit configuration; their
identity is recorded in provider metadata. No post-extraction spelling repair is
used to match fixture labels.

Values, corrections, reasons and source quotations use automatic text direction
and isolated bidi paragraphs. The interface stays English. Physical source-page
coordinates remain unchanged; RTL styling must never mirror page evidence.

## Questions and evaluation

Arabic reviewed-field questions use a bounded, whole-question grammar mapping to
the existing typed fact plans. Unknown additions, payment-event claims, arbitrary
arithmetic and instruction text must refuse. An Arabic question may select a fact
from an English review and vice versa; existing source authorization, review
revisions and citations remain authoritative. Responses may retain English labels.

Keyword retrieval and multilingual embeddings are separate from typed questions.
Do not claim a grammar test or fabricated vector establishes semantic retrieval.
Record OCR, text parsing, reviewed answers and browser evidence separately, split
Arabic, mixed and English results. Synthetic samples establish bounded development
behavior only. Freeze expected labels before evaluating; preserve failed results.
