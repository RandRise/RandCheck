# Reviewed-answer integration verification

September 11, 2026. The bounded reviewed-field mode now works through the real
application API and browser, including explicit due dates, identifiers, decimal
comparisons, citations, partial answers, refusals and saved history.
See the [contract](../../design/phase-5-reviewed-public-contract.md).

## Evidence

| Check | Result |
| --- | --- |
| Varied synthetic facts and wording | Baseline 31/36; revised policy 36/36 |
| All typed-fact checks | 75 passed, included in the backend total |
| Non-PostgreSQL backend | 591 passed across five assemblies |
| Public PostgreSQL integration | Eight passed: concurrent replay, persistence, refusal, access rechecks and rollback |
| Frontend | 24 unique checks passed; lint and production build passed |
| Earlier public evidence flow | 20 real HTTP checks passed |
| New reviewed-answer HTTP flow | 16 scenarios plus one partial-answer supplement passed; 41 unique cited-fact references checked across cases |
| Reconciliation regression | Three real extracted/reviewed documents; all nine expected findings and one saved resolution passed |
| Browser | Due date, exact page quote, saved history, three-document maximum, unsupported refusal and partial answer verified |
| Provider use | Zero model/embedding calls or new reservations; USD 0 new model cost |
| Retained database | All 34 table fingerprints unchanged |

The new varied fixture changes values, dates, leading-zero identifiers and line
occurrences. Initial failures exposed buyer TIN being mapped to supplier TIN,
compound buyer/supplier TRN selection, missing general monetary maxima, and
plural item wording. These are fixed in version 1.2 and regression-tested.
The fixture was used to develop the fixes: **36/36 is not an independent holdout
score** or a like-for-like improvement over the older ranked/model pipeline.

The HTTP cases cover due date, TRN, missing TIN, unsupported buyer TIN, agreement,
total/VAT differences, maxima, line breakdown, foreign-source/legal/payment-event
refusals, ambiguous roles and compound questions. Every original case checks
same-key exact replay, saved answer equality, changed-input 409, no-store, selected
source binding, review action and exact original page quotes. The partial-answer
supplement checks the supported invoice date while the quotation date is missing.

All real-service data is synthetic in new isolated database
`phase5_reviewed_public_20260911`, using API 8088, private extraction 8009 and
web 3006. Scripted review confirmations are fixture setup, not human accuracy
judgments. PostgreSQL tests use only disposable `document_intelligence_test`.
The retained `phase5_e2e_20260908` database was only fingerprinted.

Real startup also exposed stale backend extraction defaults. The Python adapter
and API configuration now default to `local-1.3.0`, matching the already-tested
due-date extractor. After that change, all 74 infrastructure and 83 API checks
passed again; they are repeat verification, not additional unique tests.
Python production code was unchanged in this increment; the preceding due-date
checkpoint's 329 checks, including all five pinned OCR cases, were not rerun.

Evidence is under `outputs/phase-5/reviewed-public-20260911`: `verification.json`,
`http-acceptance.json`, `partial-http-verification.json`, `full-stack-baseline.json`,
`phase4-cross-phase.json`, TRX/log files and six images in `SNAPSHOTS.md`.
Initial compilation/environment attempts remain preserved alongside successful
final logs. No provider usage-page access was required.

## Phase 5 boundary and next step

The earlier 29/30 private typed trial and 17/30 paid source-v4 results remain
frozen. Old completed reviews still require a deliberate new revision to add a
missing date; there is no silent backfill. Model-generated public answers remain
disabled. The new route is a bounded reviewed-field answer mode.

Phase 5 remains open for its final acceptance audit: map the implemented routes
to each existing criterion, finish the outstanding RAG/adversarial checks and
record which general semantic/model-answer requirements remain unmet. Keep
enterprise identity work in Phase 6 rather than inventing a new Phase 5 gate.
No new paid run is implied by this checkpoint.
