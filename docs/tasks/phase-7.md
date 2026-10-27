# Phase 7 task ledger

Authorized September 21, 2026. Status: In progress.
See the [language contract](../design/phase-7-language-contract.md).

Latest: [actual uploads, saved answers and reconciliation RTL](../evidence/phase-7/P7-upload-workflow-20260922.md).
Real worker results are English 8/8 and mixed native/scanned 8/9 each, with only
the known payment diacritic mismatch among assessed fields. Fixed stale status
blocking subsequent uploads. Saved answers/citations and Arabic reconciliation
display/persistence pass. History has an explicit save-link fallback, but browser
delivery remains unverified. 34 focused UI tests pass. Phase 7 is not complete.

Prior: [reuse of the English labeled-row parser](../evidence/phase-7/P7-labeled-row-reuse-20260922.md).
Rand requested a simple extension of the working English path. Arabic aliases
now share the existing row fields, numeric formats and one-based positions;
65 focused tests and 43 subtests pass, with six runtime tests skipped. This
does not reconstruct ordinary tables or change measured OCR accuracy.

Prior: [real saved browser workflow and line prices](../evidence/phase-7/P7-browser-and-line-prices-20260922.md).
Numeric reviewer additions now support distinct line positions. Actual browser
correction/completion/reload, English price and Arabic payment answers, saved
reopening and original evidence checks pass. Both PDFs lack all six assessed
structured table fields. History API verification passes; browser download
delivery and reconciliation RTL remain open.

September 22 follow-up: [two-page PDF and persisted workflow checks](../evidence/phase-7/P7-pdf-workflow-20260922.md)
pass for native/scanned forms of one fictional invoice. Eight numeric/identifier
fields are exact in each form; a payment diacritic mismatch stays documented.
Both real PostgreSQL review/answer/citation tests pass. Rand agreed to prioritize
reviewable results and representative workflow evidence over chasing zero OCR
failures. This does not establish production accuracy or complete Phase 7.

September 22: [targeted line refinement](../evidence/phase-7/P7-mixed-ocr-refinement-20260922.md)
fixes both original mixed OCR failures: 9/9 original cases pass. The expanded set
is 11/12; English payment wording under an Arabic label remains a failure.
The candidate is opt-in; broader acceptance remains pending.

Rand clarified the numeric scope: Western digits and standard dates/amounts are
required; Arabic-Indic source numeral recognition is optional. Default evaluation
now uses `language-v0.2.0`, with 9/9 text cases passing. The offline candidate
`best` Arabic model with `eng+ara` passes 5/5 Arabic, 1/3 mixed and 1/1 English
synthetic image cases. See the [OCR and RTL report](../evidence/phase-7/P7-standard-numerics-and-rtl-20260921.md).

| ID | Task | Status |
| --- | --- | --- |
| P7-T01 | Versioned fictional Arabic/mixed/English dataset and evidence contract | Required v0.2.0 standard-numeric dataset added; prior v0.1.0 optional numeral fixtures preserved |
| P7-T02 | Arabic labels, normalization and exact evidence preservation | Arabic labeled rows reuse English fields with exact evidence; ordinary-table extraction remains 0/6 in both frozen PDF forms; typed reviewer additions support repeated rows |
| P7-T03 | Pinned Arabic OCR and provider comparison; mixed reading order | 11/12 expanded image cases pass; native/scanned two-page fixtures evaluated; remaining failures documented; broader representative coverage pending |
| P7-T04 | RTL review, correction, history and citation display | Real browser correction/reload, source viewing and reconciliation RTL pass for fixtures; history API verified; browser history file delivery unverified |
| P7-T05 | Arabic/English typed questions and cross-language evaluation | Actual upload/worker and saved English/Arabic answer journeys verified; semantic retrieval remains separate |
| P7-T06 | Multilingual embedding/retrieval evaluation | Pending; paid calls require separate authorization |
| P7-T07 | Language-specific results, focused regression and acceptance | Bounded workflow report and 34 focused UI tests pass; overall acceptance pending download delivery, representative coverage and semantic evaluation |

No phase is complete until its evidence exists. Do not repeat completed Phase 8
demo/reconciliation checks. Preserve prior samples, outputs and retained databases.

Evidence: [September 21 foundation](../evidence/phase-7/P7-language-foundation-20260921.md).
