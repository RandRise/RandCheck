# N12: case questions, bounded review search and spacing

October 26, 2026. Approved from three user screenshots: replace loose action
links, avoid browsing thousands of completed reviews, and separate touching
headings/descriptions. Preserve the existing theme and three-stage invoice flow.

## Findings and implementation

- History and Questions navigation actions now look like buttons. They remain
  native links for keyboard navigation and opening another tab; no duplicate
  destinations or new pages were added.
- The older inline `.section-heading` style still applied inside invoice results.
  Shared scoped grid styling now separates titles/descriptions in embedded results
  and History. Action rows wrap with gaps; Questions navigation and search controls
  receive consistent spacing in both themes.
- The previous review list fetched 50 entries and appended every further page.
  It could grow into thousands of options. The real backend now searches filenames
  across accessible completed reviews and returns 10 per page. Previous/Next
  replace the displayed page. Searching does not remove already selected reviews.
- Opening Questions from a case shows **Use this case's documents**. The general
  review catalog is not fetched on arrival. Clicking explicitly prepares all of
  the case's currently pinned completed reviews together. For the test invoice
  case this means its invoice and PO. Reading the page never prepares indexes or
  confirms values automatically. Older quotation cases retain their sources.
- **Choose other documents** opens the separate filename search. A single-document
  question can use one review; a comparison can use both. The selected saved
  versions are kept. Case sources refer to the current case links, which can
  differ from the sources of an older saved comparison; this is stated on screen.

The new review browser hook, request service, types and constants are separate
modules. Backend query handling stays in Application/Infrastructure with the
existing store interface. Search treats `%`, `_` and `~` literally, preserves
organization/completed-review filters, and retains stable ordering and no-store
responses. Search is filename-only here, unlike the broader Document library
search. The existing offset ceiling of 10,000 remains; a search starts at offset 0.

## Findings fixed during verification

1. Initial web tests caught a paging click racing an answer submit. The synchronous
   operation guard now also protects search/page actions; retained assertions pass.
2. Independent review found that adopting saved-question sources could leave the
   case-only panel visible. Adopting those sources now opens general browsing;
   regression coverage includes clearing them afterward.
3. ESLint flagged loading-state writes inside an effect. Loading/error status now
   derives from the query whose asynchronous response settled. Late responses are
   ignored after a new search, Clear or closing the browser panel.
4. The new database fixture initially used a helper argument as a filename when
   it actually supplied a storage key. Fixture filenames were corrected; the
   strict search assertions were retained. The initial failed receipt remains.
5. Browser startup failures traced to relative temporary profile paths. The test
   helper resolves them to absolute paths. Headless Edge then started successfully.
6. Live case preparation first returned 503 because Python was not running.
   Starting the existing local service enabled the same real browser/API action.
   No error was hidden or replaced with a simulated success.
7. Final review caught an unescaped apostrophe in the button label. Escaping it
   preserves the displayed text; the focused ESLint rerun passed (`eslint-closure.log`).

## Verification evidence

Evidence is under `outputs/questions-polish-20261026/`:

- 101 focused web tests passed in the final shipping receipt (`web-tests-shipping.json`). They cover exact case pairs, permissions,
  history, safe retries, saved-source adoption, page replacement and late responses.
- 2 real PostgreSQL tests passed: search/paging/literal Arabic filenames/tenant
  exclusions/incomplete reviews, plus existing exact-source/evidence behavior.
  The fixture creates and drops only `document_intelligence_test`; retained
  `uaedi_validation_20261023` was not reset.
- 14 cited API boundary/logging tests passed. Scoped ESLint and TypeScript passed;
  the updated API built with 0 warnings and 0 errors. No full web build ran.
- Live Next.js → .NET → Python → PostgreSQL source preparation passed with the
  synthetic reviewed pair. Exactly one explicit case-source POST was made; no
  comparison or decision write occurred. The complete saved case response was
  equal before/after, including the user's already-created comparison 2.
- Eight screenshots cover History/results, case Questions in Dark/Light at
  1440/420px, the selected pair and filename search. No horizontal overflow was
  measured in those views. The results heading has a measured 10px gap above its
  description. Captured browser runtime exceptions: none.

Initial receipts are retained. The first isolated .NET restore could not retrieve
NuGet vulnerability data in the sandbox; the normal online check succeeded after
running with network access. No audit check was disabled. Vitest still reports
the existing future Vite configuration warning and some React `act` warnings in
older browser-history tests; these are not claimed as a clean test console.

Key commands from the repository root (web commands run in `apps/web`):

```powershell
.dotnet/dotnet.exe test apps/backend/tests/UaeDocumentIntelligence.Persistence.Tests --artifacts-path outputs/questions-polish-20261026/dotnet --filter 'FullyQualifiedName~Reviewed_source_search|FullyQualifiedName~Workspace_lists_only_completed'
.dotnet/dotnet.exe test apps/backend/tests/UaeDocumentIntelligence.Api.Tests --artifacts-path outputs/questions-polish-20261026/dotnet --filter 'FullyQualifiedName~CitedWorkflowLoggingTests'
# In apps/web:
../../.node/node.exe node_modules/vitest/vitest.mjs run app/questions app/reconciliation/workflow-context.test.tsx app/reconciliation/results app/appearance.test.tsx
../../.node/node.exe node_modules/typescript/bin/tsc --noEmit --incremental false
../../.node/node.exe node_modules/eslint/bin/eslint.js app/questions app/reconciliation/reconciliation-workspace.tsx
```

The PostgreSQL run used `TEST_POSTGRES_CONNECTION` pointing to the owned local
test cluster on port 55623, not a production database. Browser script and JSON
receipts document the synthetic test case and requests. The test profile is
temporary; the user's browser/theme and retained preview were not changed.

## Review, preservation and limits

Before/after baseline: all 1,276 existing files remain, with 18 scoped modifications
and 10 additions. Scoped diffs were opened in the existing VS Code window.
The browser helper's before-copy was initially missing. Its prior bytes were
reconstructed by removing the two added lines and verified against the original
baseline SHA256; the recovered copy now supports an exact diff. The recovery
receipt is `browser-helper-baseline-recovery.json`. The Progress and Quality
Reviewer rechecked the fixes and evidence and found no remaining material issues
in N12. Manual acceptance remains open. Live full answer generation was not tested.
The Impeccable launcher lacked its engine/cache permissions; existing project
styles, screenshots and skill refinement guidance were used directly. Nothing
was installed to bypass that limitation.

The owned test app (3310), API (8310), Python service (8010) and test PostgreSQL
cluster remain running for manual checks. Process ownership is recorded in the
existing validation receipt. Python is needed for preparing document sources;
upload workers and paid generation remain disabled. The generic invoice picker
and saved-case picker still have their earlier scale limitations. Real sign-in,
full upload processing, Arabic/scanned OCR, original downloads, exports and
deployment are not newly verified by this batch. No manual acceptance is inferred.

## Three short manual checks

1. Refresh Earlier cases and history. Open the saved case: **Ask about this case's
   documents** and **Return to invoice check** should look like distinct buttons.
2. Open Questions from that case and choose **Use this case's documents**. Expect
   the invoice and PO together. Choose **Choose other documents**, search `09999`,
   and confirm one result. Clear the search and check Previous/Next; the list
   should stay at 10 reviews per page and your selected pair should remain.
3. Return to the invoice results. The description should sit below **Saved
   comparison 2**, with clear spacing. Check the same pages in Light mode and at
   a narrow width. Results and decisions should remain unchanged.
