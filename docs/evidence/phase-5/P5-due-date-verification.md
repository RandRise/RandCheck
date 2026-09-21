# Due-date extraction and review verification

September 11, 2026. This fixes the missing-field path discovered in the private
typed-fact trial. Phase 5 public answer integration remains open.

## Cause and change

The local extractor treated "Due date" as a neighboring-field boundary but never
emitted a due_date field. The review workspace also omitted due_date from the
missing-field rows. Consequently the saved completed review could not supply
q009, even though its page contained the date.

Local adapter **local-1.3.0** now emits an explicit **Date** field from "Due Date:"
or "Payment Due Date:" labels, with the exact page, UTF-8 byte span and available
location/confidence evidence. The supported automatic format is YYYY-MM-DD,
validated as a calendar date. Invoice dates, amount due, payment dates and Net 30
do not supply a due date. Other date formats require source review.

Every explicit due-date label is checked, including multiple labels on one line
and across pages. Matching dates retain source references (bounded to ten).
Different valid dates produce due_date_conflicting; empty, invalid or unsupported
dates produce due_date_requires_review. Both suppress the machine due_date and
emit a degraded field warning. The review workspace exposes a missing due-date
row that the reviewer can add after inspecting the source.

No public answer route, model request, ledger operation, database migration or
automatic update of old completed reviews is introduced.

## Fresh evidence

A fresh extraction of the existing synthetic PDF
`output/pdf/uae-invoice-development-v0.2.0/fta-standard-services-native.pdf`
returns invoice_date **2026-08-01**, due_date **2026-08-31**, and payment_terms
**Net 30** as distinct fields.

The versioned fixture at `samples/phase-5/due-date-v0.1.0/` freezes the real native
extraction, source PDF hash and adapter identity. Only nondeterministic extraction
duration is normalized to zero. The fixture is new; historical corpus, reviews,
answer outputs and reference labels remain untouched.

Database integration loads this extraction into a new disposable document,
completes scripted review scenarios, indexes reviewed facts, reloads them and
resolves the due-date citation. These confirmations/corrections are synthetic
test setup, not a claim of human review:

| Scenario | Answer | Original source evidence |
| --- | --- | --- |
| Confirm the extracted date | 2026-08-31 | 2026-08-31 |
| Correct it during review | 2026-09-15 | 2026-08-31, separately identified as original evidence |
| Clear it during review | InsufficientEvidence; no date claim | No fallback to page text or Net 30 |

Each scenario checks read-only repeat behavior and no spend reservation. The
application tests also cover reviewer addition, invalid/ambiguous reviewed
dates, and preserving original evidence when a reviewed date is corrected.

## Regression and retained results

Final results: **550** non-PostgreSQL backend checks, **nine** unique PostgreSQL
checks and **329** unique Python checks passed (316 application, seven fixture
generator, five pinned OCR/native and one new native due-date test). One initial
PostgreSQL case timed out creating its disposable database; all three due-date
scenarios passed on rerun. The latest per-case result is recorded in verification.json.
Native extraction repeated exactly, and all 34 retained table fingerprints match.

Evidence and final counts are recorded in
`outputs/phase-5/due-date-20260911/`. This includes full non-PostgreSQL backend
tests, the new due-date and existing typed-fact PostgreSQL checks, all Python
application tests, the dedicated fixture-generator environment and pinned
native/OCR regression cases. Initial compile/environment/timeout logs are retained
alongside the completed verification.

The prior adapter's five provider goldens remain unchanged. A local-1.3.0
subdirectory contains the same expected fields/pages with the new adapter identity;
the original Phase 2 fixtures contain no explicit due-date label.

The historical private trial remains **29/30**, with q009 missing a reviewed
date in its saved corpus. This increment proves the repaired path on a fresh
extraction and review; it does not relabel that trial as 30/30 or modify its data.
Existing documents require an explicit review addition/revision or a fresh
extraction/review. No inferred date is silently backfilled.

Next: broader document/wording evaluation and public answer/citation/history
integration with authorization, replay and security acceptance. See the
[reviewed-fact design](../../design/phase-5-reviewed-fact-answers.md).
