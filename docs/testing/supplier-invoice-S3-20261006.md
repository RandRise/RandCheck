# Supplier invoice S3 independent verification

October 6, 2026. Rand's Continue authorizes the saved invoice/PO workflow batch. Focused S3 source/automated gates pass: 324 distinct checks, with a separate coordinator-run short synthetic browser smoke. Rand's S1/S2/S3 manual feedback remains unreported. Historical truth and retained databases are preserved. No S4-S6 work, full demo, paid execution or broad build is included. The tester started only the owned disposable PostgreSQL cluster and ephemeral fixture-test servers; the coordinator owns the lightweight preview/browser checkpoint.

## Owned new verification files

- Domain.Tests: `SupplierInvoiceWorkflowDraftVerificationTests.cs`.
- Application.Tests: `SupplierInvoiceWorkflowHandlerVerificationTests.cs`.
- Api.Tests: `SupplierInvoiceWorkflowContractVerificationTests.cs`.
- Persistence.Tests: `SupplierInvoiceWorkflowPersistenceTests.cs`.
- Web: `apps/web/app/invoices/workflow.test.tsx`.

The coordinator owns `/invoices` production UI and preview parity. The tester owns these new tests, `tools/ui-preview/supplier-invoice-workflow.test.mjs`, and this receipt. Existing tests are reused; the coordinator explicitly authorized the schema inventory expectations below after saving their exact baseline. Project runtimes and installed packages are used; heavy compilers/tests run sequentially after the corresponding source freezes.

The frozen backend contract uses `workflowKind=InvoicePurchaseOrder`, nullable invoice/PO draft documents, and `PUT /api/reconciliation-cases/{caseId}/draft-documents/{Invoice|PurchaseOrder}` with `{documentId,requestKey}` and a quoted UUID `If-Match`. A retry fingerprints its original ETag and document selection, checks current authorization and role version, and never restores a superseded selection. The role version detects A→B→A. An unrelated role edit or a newer exact review pin does not invalidate a still-current selection receipt. Every new intent, including a no-op, participates in case optimistic concurrency; exact receipt replay is read-only.

## Independent acceptance matrix

| Scenario | Required behavior | Proof boundary |
| --- | --- | --- |
| Queued/unreviewed invoice and PO | Save draft references before completed reviews; no automatic comparison or review pin | Domain, application, real PG, UI mock |
| Draft reopen/refresh/stable URL | Saved case selects exact saved documents; no initial write | API, real PG, UI mock |
| Replace draft invoice or PO | Deliberate save updates draft selection; historical run sources remain untouched | Domain, real PG, UI mock |
| Same retry key and unchanged payload | One original operation/business audit; replay does not overwrite later draft state | API and real PG |
| Same key, changed document/scope/actor/org | Payload conflict within its scope; correct isolation across distinct actor/org scopes | Application, API, real PG |
| Stale ETag or uncertain save | Safe error/read recovery, unsaved selection retained, no automatic replay; synchronous duplicate submit blocked | API, real PG race, UI mock |
| Wrong organization or source/review ownership | No leaked document contents, invalid pin or cross-organization reference | Application, API, scoped PG references |
| Processing failure, wrong known document role, duplicate invoice/PO source | Honest next action or refusal, no unsupported pair claim | Application, UI mock |
| Completed review available | Explicit selection/pin confirmation captures exact document/review/extraction/hash; no adoption of a newer revision without intent | Application, API, real PG, UI mock |
| Deliberate comparison | Exactly `invoice-po-1.0`; require explicit supported order scope and completed pinned reviews; processing or incomplete review is not comparison readiness | API, UI mock |
| Review route and Back | Exact selected document plus validated `pairCase`/`pairRole` returns to saved pair; invalid/cross-case guidance cannot replace current selection | UI mock; bounded synthetic browser pass; Next/live identity pending |
| Viewer, Operator, Reviewer, separate admin-only, combined roles | Existing read/write boundaries; admin flag does not grant private document access | Existing and new API/UI role tests |
| Fresh EF scope with extraction warning | Exact original warnings survive review loading and freeze into pair source metadata; row coverage stays unverified and affected whole-order arithmetic is withheld | Actual PG, not in-memory projection |
| Migration/grants and old history | Additive schema applies in fresh disposable DB; least-privilege role supports intended writes/reads, immutable history survives draft/pin replacement; no pending-model drift | Actual PG |

The warning case addresses a newly found prerequisite: `DocumentRepository.ReviewQuery` did not include extraction warnings. A fresh EF scope could therefore project an empty warning list and wrongly promote row coverage. The implementation agent owns the bounded Include repair. The independent test must use a legally warning-bearing extraction that would otherwise qualify, a fresh evaluator scope, and saved warning/source/check assertions.

## Evidence and manual milestone

Initial focused runs: domain 11/11, application 22/22 and API 12/12 passed. Initial UI 23 cases had 19 passes and four failures from a test matcher that expected a filename inside `<bdi>` to be one text node with its parent label. The correction checks the exact parent text; unsaved selection, disabled state, request count and recovery assertions remain intact. The next UI run passed 28/28. These are intermediate counts; final affected runs and latest per-test results will be recorded below without summing reruns.

Final backend gates are now domain 25/25, application 59/59, API 66/66 and actual PG 20/20 distinct latest cases. No skipped/not-executed case is counted as a pass. The first attempt to compile the added API role tests failed CS0535 because the new repository fake omitted required `GetByCreationRequestAsync`; adding its unused null-return seam repaired test compilation without changing draft/authorization assertions. `api-final.txt` preserves this attempt; `api-complete-final.trx` is the final 66-case report.

Native initdb under the restricted Windows token failed with errors 87/3. The narrow authorized retry initialized a fresh PostgreSQL 16.4 cluster at `outputs/supplier-invoice-S3-20261006/pg-5a7dadb83c35/data`, loopback-only port 25441, max 20 connections/32 MB shared buffers. Actual server identity/loopback/port are in `pg-verified.txt`. The fixture only creates/drops `document_intelligence_test` there. No retained DB or prior data directory is used. This is a disposable database server; no app/preview/browser server has been started by the tester.

The first PG eight-case run was 7 passed/1 failed. The warning case had already proved exact fresh warning loading, frozen Arabic summary and Unverified coverage; its final assertion incorrectly searched check `Code=source_coverage` instead of `FieldKey=source_coverage` (`Code=PAIR-COVERAGE`). Only that property lookup was corrected. The first TRX/log remain preserved. Final affected reruns also add no-op concurrency and a queued draft source with no extraction children, proving deletion is blocked by the exact current-draft document FK until explicit clear.

The affected six-case run was 4 passed/2 failed. The warning fixture used identical synthetic source hashes for invoice and PO, so the engine correctly emitted `duplicate_source` before coverage checks. Role-specific synthetic hashes now establish distinct sources. The queued deletion fixture first encountered the existing required-deletion-record trigger (23503, no constraint name); a legal matching deletion record now satisfies that earlier guard so the exact new draft FK can be tested. Neither guard was disabled. Both failed receipts remain preserved; the expected warning, evidence, refusal, immutable history and exact FK assertions were retained. No production changes resulted from these test setup corrections.

Schema baseline: `schema-test-baseline.json` and `schema-test-before/apps/backend/tests/UaeDocumentIntelligence.Persistence.Tests/PostgresPersistenceTests.cs`, SHA-256 `24cabce33b10f9ba6ea4b3a06a8824a2ff08f22be73d9fadd3d7b7442a632464`. Authorized delta changes migration count 23→24, adds one table, ten constraint names and five index assertions; every prior assertion remains. Coordinator/reviewer source review passed this exact delta.

PG latest-name union is 20 passed: eight new cases and twelve existing regressions. `postgres-first.trx` is 7/8; `postgres-affected-final.trx` is 4/6; `postgres-recovery-regressions-final.trx` is 10/10. `postgres-distinct-final.json` names each final passing artifact and explicitly supersedes the original single concurrency fact with its changed-selection/no-op theory cases. This is a union of focused runs, not one all-green 20-case invocation. Existing coverage includes schema/migration rollback, immutable graph roundtrip, malformed raw values, case list/filters, repeat creation/evaluation/pin/decision races, restricted-role metadata and composite review ownership.

Cleanup completed: `fixture-database-absent.txt` is 0, `pg-stop.txt` confirms stopped, and `cleanup.json` confirms port 25441 has no listener. The exact owned directory is preserved as evidence. No retained DB or historical cluster was changed; no owned DB/test process remains active.

Final affected UI: 137/137 in ten files, including 29 new invoice workflow cases and 108 existing Home/Compare/navigation cases. Node fixtures: 17/17, including three new supplier workflow cases and fourteen existing regressions. TypeScript `--noEmit --incremental false` and affected five-file ESLint `--max-warnings 0` both exit 0. The complete automated total is 324 distinct checks (170 backend +137 UI +17 Node); intermediate runs and S1/S2 historical totals are not added.

A final coordinator copy-only correction removed “Quotations are optional in existing comparisons” from the invoice page header, since legacy comparisons still expect quotation. No logic or styles changed. Hash verification caught this delta before writing the final gate manifest. The earlier web freeze is preserved as `web-production-before-copy-fix.json`; the repeated affected UI selection passed 137/137 (`ui-copy-final.json`) and page-only lint exit 0 (`eslint-copy-status.json`) against the updated web freeze. Backend/PG, prior TypeScript and Node results remain applicable to their unchanged source.

`production-final-verified.json` confirms all 29 frozen backend/web production paths. `verification-final-source-hashes.json` records these plus seven owned verification paths (36 total). `final-status.json` records exact final reports, counts, static statuses and cleanup. All source/test changes during failed attempts are the documented independent fixture/interface corrections; the sole later product delta is the coordinator's copy-only correction above.

### Exact focused commands and reports

Each command uses `. ./tools/use-project-runtimes.ps1` first. All .NET runs use the existing restored packages, one project at a time. Final project builds are Debug through focused tests; no broad solution/production build was run.

```powershell
dotnet test apps/backend/tests/UaeDocumentIntelligence.Domain.Tests --no-restore --filter 'FullyQualifiedName~SupplierInvoiceWorkflowDraftVerificationTests|FullyQualifiedName~ReconciliationDomainTests|FullyQualifiedName~ReconciliationRuleEngineTests' --logger 'trx;LogFileName=domain-final.trx' --results-directory outputs/supplier-invoice-S3-20261006
dotnet test apps/backend/tests/UaeDocumentIntelligence.Application.Tests --no-restore --filter 'FullyQualifiedName~ReconciliationHandlerTests|FullyQualifiedName~ReconciliationListTests|FullyQualifiedName~ReviewHandlerTests' --logger 'trx;LogFileName=application-final.trx' --results-directory outputs/supplier-invoice-S3-20261006
dotnet test apps/backend/tests/UaeDocumentIntelligence.Api.Tests --no-restore --filter 'FullyQualifiedName~ReconciliationApiContractTests|FullyQualifiedName~Workflow_api|FullyQualifiedName~Saved_case_creation_key|FullyQualifiedName~ReviewApiContractTests' --logger 'trx;LogFileName=api-complete-final.trx' --results-directory outputs/supplier-invoice-S3-20261006
```

PG initial filter: `FullyQualifiedName~Workflow_pg|FullyQualifiedName~Expected_schema_constraints_and_indexes_exist|FullyQualifiedName~Migration_apply_is_idempotent_and_rollback_reapply_succeeds` (`--no-build --no-restore`). Affected filter: `FullyQualifiedName~Workflow_pg_fresh|FullyQualifiedName~Workflow_pg_concurrent_new|FullyQualifiedName~Workflow_pg_queued|FullyQualifiedName~Reconciliation_graph_round_trips|FullyQualifiedName~Reconciliation_metadata|FullyQualifiedName~Reconciliation_case|FullyQualifiedName~Supplier_pair_confirmed_malformed` (`--no-restore`). Final recovery/regression filter: `FullyQualifiedName~Workflow_pg_fresh|FullyQualifiedName~Workflow_pg_queued|FullyQualifiedName~Case_repeat_|FullyQualifiedName~Case_list_projects|FullyQualifiedName~Case_metadata_filters|FullyQualifiedName~Case_metadata_restricted|FullyQualifiedName~Review_action_composite_foreign` (`--no-restore`). All run against the owned `TEST_POSTGRES_CONNECTION` on port25441 and write the three named TRX reports above. The two unmatched `Reconciliation_metadata`/`Reconciliation_case` terms in the affected filter do not claim extra coverage; exact actual test names/counts are in the final union receipt.

```powershell
node apps/web/node_modules/vitest/vitest.mjs run app/invoices/workflow.test.tsx app/reconciliation/page.test.tsx app/reconciliation/action-repeat.test.tsx app/reconciliation/workflow-context.test.tsx app/reconciliation/case-navigation.test.tsx app/reconciliation/case-metadata.test.tsx app/reconciliation/comparison-settings.test.tsx app/page.test.tsx app/document-simplification.test.tsx app/security/workspace-navigation.test.tsx --root apps/web --maxWorkers=1 --reporter=json --outputFile=../../outputs/supplier-invoice-S3-20261006/ui-copy-final.json
node --test tools/ui-preview/supplier-invoice-workflow.test.mjs tools/ui-preview/fixtures.test.mjs
```

From `apps/web`: `node node_modules/typescript/bin/tsc --noEmit --incremental false`; `node node_modules/eslint/bin/eslint.js app/invoices/page.tsx app/invoices/review-context.tsx app/invoices/workflow.test.tsx app/page.tsx app/reconciliation/page.tsx --max-warnings 0`. Static command/exit/timing JSON and exact text logs are saved under the S3 outputs. Vitest logs retain the existing Vite native-config-loader advisory; it is not a failed check.

### Short manual checkpoint

On the coordinator’s **synthetic local preview**, open [the saved sample check](http://127.0.0.1:3410/invoices?case=246b5e58-f804-4c73-98b8-ba7396387c9d), refresh, then use **Open invoice review → Return to invoice check**. Expected: **Completed invoice.txt** and **Completed purchase order.txt** remain saved and the return link opens that same check; no comparison starts automatically. The coordinator records browser smoke separately. This exercise does not prove real extraction, database persistence or live identity; the actual isolated PG evidence above covers persistence.

### Separate coordinator browser smoke

Coordinator browser smoke passed on the actual components in the **synthetic preview**, Operator / Sample documents: created check `246b5e58-f804-4c73-98b8-ba7396387c9d`, saved Completed invoice.txt and Completed purchase order.txt, refreshed and retained both, observed no automatic pins and disabled comparison, opened the exact invoice review and followed its validated return link back to that same saved check. Console errors were empty. Artifacts: `outputs/supplier-invoice-S3-20261006/browser-smoke.json` and `browser-saved-pair.jpg`.

This bounded browser smoke is separate from the automated 324 and from actual PostgreSQL evidence. No comparison, upload, full demo or real identity flow was exercised. Rand's S1/S2/S3 manual feedback remains unreported; live identity R5/R6, OCR, full extractor-to-backend parity and customer-demand gaps are unchanged.

Prepare one short user action sequence on the new invoice workflow: save a known invoice/PO pair, reopen its stable URL, inspect the next review action, deliberately confirm exact completed revisions and compare with a declared scope. If shown through preview, label the scenario and state as **synthetic**. Preview/jsdom cannot establish actual draft persistence, original-file hashing, live identity, native browser behavior, extraction coverage or customer usefulness. Actual PostgreSQL evidence is recorded separately. S1/S2 manual acceptance and R5/R6 gaps remain open.
