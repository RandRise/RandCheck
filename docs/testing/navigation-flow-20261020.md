# Numbered invoice workflow verification

October 20, 2026. Approved scope: simple Home and one saved invoice/PO workspace
with Add documents, Check details and See results; earlier approved G02/G03
backend checks and exact inspected-review binding. Visual redesign, Owner,
reviewed export and real-account migration are separate.

## What changed

Home offers Check an invoice and Continue saved work. New checks no longer need
a routine name/category form, manual revision selection or scope question.
Uploads and saved choices, both document reviews, results, evidence and decisions
remain in the numbered workspace. Earlier document links and quotation cases
remain accessible. The generic initial name is distinguished by its saved reference.

New `invoice-po-2.0` rules report supported quantity and unit-price differences.
Aggregate amounts require matching, fully checked quantities and trusted complete
row coverage, plus compatible currency and bases. They never infer final invoice,
remaining balance or payment approval. Version 1 and its saved evidence remain.
The new migration extends existing immutable-evidence protections to version 2;
rollback refuses to remove a policy that has stored version 2 runs.

## Findings and fixes

The separate Progress and Quality Reviewer inspected source against the captured
pre-batch working tree. Root implemented fixes and regression checks:

- Cancelled browser Back previously allowed another listener to clear drafts.
  Capture-phase cancellation now prevents the other handlers from running.
- Inline decision saves needed a separate busy guard and a confirmed token passed
  to the parent. Navigation blocks during saving; the next pin uses the new token.
- A later concurrency review found that accepting a child's new save token could
  conceal another user's changed invoice pin. The parent now accepts it only when
  the child's pre-write token matches its own. Otherwise writes stop for recovery.
  Show results also requires the exact invoice review inspected locally; recovery
  clears that acknowledgement. A concurrent-pin integration regression passes.
- Clicking the current step/document must not clear dirty state. It is now a no-op.
- Closed checks now pass a read-only boundary into document review and processing.
- Evidence navigation now uses the invoice's own frozen row, document and extraction
  identity. Replaced documents or changed extractions do not select an unrelated row.
- Exit wording now says Back to Home, without implying unsaved edits were saved.
- Encoding damage introduced during editing was repaired; new Arabic guidance is
  stored as UTF-8 with English digits.

Initial web failures included clicks before asynchronous controls became enabled,
Home's initialization race and old help-route expectations. Home now blocks
creation until initialization finishes. Tests await actual readiness and retain
legacy protocol assertions. The initial domain test incorrectly expected an
unrelated unique row to be blocked by another duplicated row; its assertion now
targets the ambiguous row and withheld aggregate. No production rule was weakened.

The initial persistence run verified current grants after intentionally downgrading
the schema. It now verifies runtime grants before that destructive test-only
downgrade. Downgrade refusal/evidence assertions remain. The disposable schema
also passes full migration rollback/reapply.

## Fresh verification

Receipts and captured before files are under `outputs/navigation-flow-20261020/`.
The original dirty tree contained 298 status entries and was not reset.

| Check | Result | Evidence |
| --- | --- | --- |
| Domain pair-policy tests | 69 passed | `domain-final.trx` |
| Application reconciliation handlers | 67 passed | `application-final.trx` |
| API reconciliation/security contracts | 51 passed | `api-final.trx` |
| PostgreSQL pair evidence and migration lifecycle | 7 passed, no skips | `persistence-final.trx` |
| Focused web workflow/review/navigation | 242 passed | `web-final.json` |
| Root routing, extracted document page and access regression | 27 passed | `web-extra.json` |
| Review-request retry contracts | 3 passed | `review-request.log` |
| TypeScript / affected ESLint | Exit 0; zero lint warnings | `typescript.log`, `lint.log` |
| VitePress guide build | Passed; chunk-size advisory | `docs-build-final.log` |

Commands used local runtimes. Backend tests use `dotnet test <test-project>
--no-restore --disable-build-servers --filter <focused-filter>`. Filters are
`SupplierInvoicePairPolicyVerificationTests`, `ReconciliationHandlerTests`,
`ReconciliationApiContractTests|ReconciliationRepeatSecurityTests`, and
`Supplier_pair|Migration_apply_is_idempotent`.

Web checks use Vitest for `app/invoices`, `app/reconciliation`, `app/review`,
review experience/selection, document navigation/repeat/simplification and
workspace navigation/guidance. These are simulated HTTP/component tests, not
live extraction integration. `journey-results.test.tsx` uses the real results
component, workflow hook and services, with synthetic HTTP and document-review UI.

NuGet vulnerability-feed access failed initially. Targeted cached restores used
command-only `-p:NuGetAudit=false`; no package policy was changed. This is not a
successful vulnerability scan. EF tooling reported version 8.0.10 versus runtime
8.0.31; Vite reports a future config-loader advisory.

Source/link checks cover 7 documentation files and 202 local links with no missing targets (`docs-links.json`).

Final independent recheck found no remaining material source finding. Root's
self-review and static checks also passed after fixes. This assessment does not
replace the manual and integration checks below. The first documentation build
was blocked by sandbox path access; the authorized local retry built successfully.

## Preservation and limits

The database checks used a new disposable PostgreSQL cluster on port 55440,
never retained application data. That temporary server was stopped after testing. No real accounts or retained schema were migrated.
Port 3410's in-memory preview was not restarted or reset. Its older simulated
backend cannot verify version 2, even though the UI source hot reloads.

The browser connector was unavailable. An isolated Edge headless attempt failed
with a GPU process error and produced no screenshot. No visual/browser acceptance
is claimed. The new workflow SVG is an illustration, not test evidence.

Manual acceptance, real identity/account cutover, full extraction-to-database
integration, Arabic/OCR quality, reviewed report/export and deployment remain open.
The earlier quantity-times-price impact example is not implemented in this policy.
G02/G03 are implemented source slices; the overall milestones remain open.

## Short manual check

1. From Home, start or continue a check. Expect the 3 numbered steps and invoice/PO
   choices only. The saved reference should remain the same as you move.
2. In Check details, type a correction and try Back. Cancel should keep it. Save
   deliberately, complete both reviews and proceed without a revision picker.
3. Against the updated backend, read results and evidence in step 3. Missing
   facts must stay Could not check. Record a decision, return to details, then
   reopen an older result: its original values must remain.
4. Open an earlier quotation and saved case through Documents/history. Confirm
   their review, evidence and decisions remain accessible.

These checks are instructions, not recorded user acceptance.
