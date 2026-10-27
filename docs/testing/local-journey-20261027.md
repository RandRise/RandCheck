# Fresh local invoice/PO journey — October 27, 2026

Status: fresh journey checks pass; independent final review cleared S6. This is agent-operated acceptance with fictional documents,
not user acceptance or proof of accuracy on arbitrary supplier layouts.

## Scope and isolation

S6 follows the [local completion ledger](../tasks/local-completion-20261027.md):
fresh invoice/PO uploads, actual worker extraction, explicit review against source
truth, exact saved versions, backend comparison, reasoned decisions, export,
failure/retry and reopening. Bounded Arabic/OCR and Admin integration are L1.

The baseline contains 1,376 files. Evidence lives under
`outputs/local-completion-20261027/journey/`. The isolated source copy is
`outputs/local-demo/e619b9164325`, using loopback ports 3308/8308 and its own Docker
database/upload volumes. The retained 3310/8310 workspace, previews, documents,
reviews and history remain untouched.

## Findings before verification

The demo configuration still selected the legacy local-1.3.0 adapter and its
guide described a quotation-led three-document journey. The current demo now
explicitly selects `supplier-invoice-v1` / `local-1.6.0`, English/Arabic OCR with
the packaged fast model and line layout. This configuration is not itself a
language-accuracy claim. Historical probes and fixtures remain available, clearly
separated from the current workflow.

The copied supplier sample pack is hash-checked before use. Its two PDFs have
1260.00 AED totals and two matching item descriptions, quantities and prices.
Visual inspection confirms that the printed row amounts are net; missing units,
price/amount bases and other gaps must not become confirmed facts. The expected
comparison outcome is incomplete, not a full match.

## Verification method

`tools/verification/invoice-journey.mjs` operates the actual browser and real API
only after verifying the disposable demo's ownership and loopback port. Stages
save receipts so completed work is not repeated. Scripted confirmation is limited
to this known fictional source truth, checked against rendered source pages and
actual extracted evidence. This is not product-side automatic confirmation.

## Fresh results

- The clean runtime source built and started successfully, migrations exited zero,
  and the database health/crash check passed. Three destructive-operation guard
  probes refused altered configuration, a foreign copy path and an unexpected
  project volume, preserving the empty demo.
- Actual browser uploads created one case only after both files and **Continue to
  review**. One upload alone could not continue. Case creation confirmed no values.
- The real worker used local-1.6.0, pdfplumber and schema 2.0 for both PDFs. Both
  returned Partial. Provider/version, processing and warning receipts are retained.
- The browser confirmed 15 invoice fields and 12 PO fields only after exact source
  truth, item/field label, value and displayed evidence checks. Completed actions
  retain the original machine field IDs and extraction IDs.
- invoice-po-2.0 compared the exact two saved reviews: **6 passed, 0 differed,
  5 could not be checked, 11 total**. Matching totals did not imply a match.
- Choosing **Requested the missing information** and **Acknowledge missing check**
  added one decision and changed the case version. Fields, checks, sources and
  coverage stayed byte-for-byte equivalent as JSON.
- Actual HTML/CSV downloads contain the same full snapshot ID, current case
  version, run/review IDs and decision. CSV reopened as 97 rows and 19 columns,
  including all 11 checks and the unchanged incomplete outcome.
- A fresh browser reopened the saved result and decision. A deliberate lost reply
  after the server accepted reprocessing was retried using the same operation key:
  two HTTP 202 replies referenced one new extraction, with the second marked replay.
  PostgreSQL confirms only invoice run 2 was added. Original review history and
  the saved comparison/decision remain unchanged.
- One deliberately truncated PDF failed with a safe explanation and a recovery
  button. A separate read-only check opened the same file in the review-capable
  legacy workspace, where review controls were also absent. The real review
  endpoint returned 409 `review_requires_successful_extraction`, and history
  contained no sessions. The saved pair was unaffected.
- Dark/light results were checked at 1440px and 390px: no page overflow, separated
  download controls and readable text. The layout batch issued no API writes.

These are 12 named journey assertions, including four theme/width layout variants,
plus the three reset guards. They are agent-operated browser/API checks, not unit
checks, customer accuracy evidence or user acceptance.

## Findings and limits

Independent early review identified missing web-container ownership verification,
weak visible-field binding and possible stale download acceptance in the harness.
All three are corrected: both owned services and the browser API destination are
checked; exact field/item/quote identities and saved actions are asserted; download
attempts have fresh directories and full content/version checks. Final recheck cleared S6. The final reviewer also distinguished the read-only library from the
review-capable route; the failed-document eligibility check above closes that
evidence gap without another upload or mutation.

Initial harness attempts used an incorrect Development-session assumption, a
wrong case-list envelope and an overly specific failure-panel selector. They were
corrected against actual contracts. Completed uploads/reviews were preserved and
resumed, not repeated. An initial guard probe preceded API readiness and stopped
before mutation; the ready-state rerun passed. One first-load library request
showed a transient fetch error; its cause was not captured. A fresh read returned
200 with correct CORS, no backend exception was logged, and the resumed browser
loaded the list successfully. New browser diagnostics record response/failure
status; the only cancellation in the successful rerun was the expected disposed
initial request. Do not describe the unexplained first request as a fixed defect.

No invoice/PO business rule or permission changed in S6. Missing item coverage
remains incomplete. Native English is verified here; bounded Arabic/scanned inputs
and real Admin browser/API integration remain L1. Restart/reset of this current
source and full declared CI remain L2. Entra and deployment remain optional external
tracks. Independent final review cleared S6.

## Short optional manual checks

1. Start with the two copied supplier PDFs. Confirm each value against its source,
   then show results. Expect the incomplete counts above.
2. Record the missing-information decision. Confirm that the check counts stay the
   same, then open both downloads and compare their snapshot suffixes.
3. Return to the saved check. Earlier results must stay unchanged after processing
   a document again; new extraction values still need a new review.

