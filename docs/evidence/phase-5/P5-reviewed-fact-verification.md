# Reviewed-fact answer trial

**Date:** September 11, 2026. **Status:** private offline candidate; Phase 5 open.

The final typed-facts v0.2.0 candidate passes **29/30** unchanged development
references, with **68 cited fact references** resolved through the scoped
workspace and checked against the frozen corpus. The remaining reference,
q009, asks for a due date that is absent from the completed reviewed fields.
The candidate returns `InsufficientEvidence/incomplete_evidence` with no claim.
We retain that as a failed reference answer; we did not change the expected
answer, add a synthetic review decision, or infer a date from Net 30.

## What this establishes

The covered questions now use explicit TRN/TIN field selection, decimal
arithmetic, complete selected-document operands and backend-generated citations.
q022 returns supplier TRN 000000000000701; q013 cites all three TRNs; q014 lists
all three totals; q016 computes an absolute AED 105 difference; q024 identifies
the PO's AED 2625 training line while citing all three documents. q019 separates
quantity 2 from unit price AED 1200, line tax AED 120 and gross AED 2520.
The six original unanswerable/out-of-scope cases refuse as labelled.

The retained corpus has no reviewed supplier TIN. Separate unit fixtures contain
both TRN and TIN with different values and leading zeroes, so their distinction
is tested without inventing a reviewed TIN in the retained documents. Corrected
values, cleared fields, ambiguous lines/roles, different line occurrences,
decimal edge cases, missing/mixed currencies and unsupported compound questions
are also covered with values and wording outside the 30 saved cases.

## Preserved development iterations

| Trial | Passes | Unmet reference cases |
| --- | --- | --- |
| typed-facts v0.1.0 | 25/30 | q003, q006, q009, q014, q019 |
| typed-facts v0.1.1 | 28/30 | q009, q019 |
| typed-facts v0.2.0 | 29/30 | q009 |

The first selector rejected ordinary reading verbs in three questions. v0.1.1
expanded those verbs and tightened compound-field handling. Review then found
currency attached to quantity in the breakdown; v0.2.0 fixes that and tests it.
Earlier outputs remain intact. Initial status-only counts were 26 and 29;
the fuller evaluator correctly counts the quantity/currency defect as a failure.

Manifests live in `samples/phase-5/typed-facts-v0.1.0/`, `typed-facts-v0.1.1/`
and `typed-facts-v0.2.0/`. Outputs respectively live in
`outputs/phase-5/typed-facts-20260911/`, `typed-facts-v011-20260911/` and
`typed-facts-v020-20260911/`. Each final output is hashed in `evaluation.json`.
`tools/phase5_typed_facts_report.py` independently checks the fixed expected
operands, currency, numeric types, complete computation attribution, fact text,
content hashes and original page spans. Selected claim text was also inspected.

## Verification

- All **545** non-PostgreSQL backend tests passed: domain 111, application 278,
  infrastructure 74, API 78 and architecture 4. This includes 35 new typed-fact
  cases. The full console log preserves per-assembly totals; the shared TRX name
  was overwritten by the runner and is not used as the full-suite inventory.
- All **six** new real-PostgreSQL cases passed: read-only answer/evidence access
  plus foreign organization, different actor, inactive actor, quarantine and
  superseded-index rejection. These use only disposable
  `document_intelligence_test`; the retained evidence database is not reset.
- All **seven** evaluator integrity tests passed using unittest: altered sources,
  fields, numeric types, currency, missing coverage, navigation and publication
  boundary checks. The local environment lacks pytest; the unittest execution
  is the completed test record.
- The pilot build passed. No public endpoint, browser, worker, extraction,
  reconciliation rule or database schema changed in this increment.
- [Earlier full regression](P5-full-regression-20260910.md) records 703 backend,
  304 unique Python and 20 frontend checks plus actual browser/source/review/
  reconciliation flows. Those are prior evidence, not tests rerun this turn.

All 30 final answers reproduced byte-for-byte in a second read-only run. All 34
retained public-table counts/content fingerprints match the pre-trial snapshot.
Final machine evidence, exact-repeat results, retained-table fingerprints and
readable snapshots are under `outputs/phase-5/typed-facts-v020-20260911/`.

## Cost, limitations and next gate

This trial makes **zero provider calls and incurs zero new model cost**.
No paid source-v4 output, reservation or saved answer is changed. The historical
source-v4 score remains **17/30**. The new route reads all reviewed fields in
the selected corpus, whereas the paid route used ranked evidence; 29/30 is not
a like-for-like model or RAG improvement. These are development cases used while
building the implementation, not an independent holdout.

The candidate is limited to supported English intents and reviewed fields.
There is no general semantic-support acceptance. Public generation remains
disabled, and typed answers are neither publicly exposed nor persisted.
The immediate next gate is deliberate handling of page-only/missing reviewed
facts such as the due date, followed by a fresh varied-corpus evaluation and
integration into the public answer/evidence/history flow with its authorization,
replay and security acceptance tests. The frozen due-date case remains unchanged.

See the [design](../../design/phase-5-reviewed-fact-answers.md).
