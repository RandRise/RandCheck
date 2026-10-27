# N09: results stage — October 23, 2026

## Approved scope and findings

Keep **Add invoice + PO → Check details → See results** on the existing page.
Improve outcome wording, counts, values/evidence, incomplete-check actions and
scenario reasons. Preserve all saved data, permissions, historical runs and retries.
Administrator design, new roles, exports and broader refactoring remain separate.

The real backend mapped coverage only for `invoice-po-1.0`, although the workflow
uses `invoice-po-2.0`. Consequently it returned zero/NotRecorded beside real checks.
The screen also duplicated checks and findings and used generic decision reasons.
The retained preview is a separate, older total-only simulation, not the real
comparison engine. Its stored results are not rewritten or reset.

## Implementation

- Coverage supports both pair policies. Passed, differing and unavailable counts
  are separate. `CompletedChecks` keeps its earlier meaning (passed + differed).
  Zero checks, legacy coverage and running comparisons cannot imply a match.
- Cards use actual saved checks, values and snapshot IDs. Linked findings supply
  decisions only; they are not counted again. Unmatched older findings remain.
- Missing checks explain the gap and offer one in-workflow review action when a
  saved source exists. Header prerequisites use header row 0; aggregate quantity
  gaps open broader details. The original backend explanation remains available.
- Reason selections depend on missing checks, quantities, identity or amounts.
  Decisions never modify frozen check outcomes. Existing stale-save, closed-case,
  permission and uncertain-response guards remain.
- The backend already compares supported document facts without asking whether
  this is the final invoice. No new required fields or scope question were added.

## Fresh verification and review

Evidence is in `outputs/results-stage-20261023/`, including a captured baseline of
1,248 files, before snapshots, scoped diffs, logs and test reports.

- Application: 11 focused tests passed, including both policy versions and exact
  saved source IDs.
- API contracts: 12 passed, including zero/legacy/running coverage and ETag guards.
- Real API with an isolated PostgreSQL database and restricted database login:
  3 scenarios passed (difference, incomplete, all passed). Each selects an older
  completed invoice review despite a newer completed review, pins the exact pair,
  evaluates v2, checks actual returned values, reopens results and rejects stale
  replay. Decisions leave checks, evidence and coverage unchanged. One evaluation
  audit record is asserted. Documents and identity tokens are synthetic fixtures.
- Initial API assertions failed on collection order, not changed records. Fixed
  by comparing every typed record by stable ID; all 3 reruns passed.
- Web: 166 focused tests passed (144 workflow/history/results tests plus 21
  decision/guide tests, plus 1 future-status regression). The latter proves a selected missing-check reason reaches
  the decision endpoint while the check stays incomplete and duplicate saves stay
  disabled. Initial failures used the former text input; updated interactions
  retain all draft/history/recovery assertions.
- TypeScript and scoped ESLint passed. Vite retains its existing non-failing
  configuration migration notice.
- Twelve read-only Edge views replay the captured real API fixtures for all three
  outcomes, dark/light and 1440/420px. Counts/cards agree, with no horizontal
  overflow, runtime exceptions or application writes. Two representative images
  were visually inspected. This replay is not live browser-to-backend integration.
  Python Playwright was unavailable; the existing temporary headless Edge approach
  was used without installing dependencies or restarting the retained preview.
- All 257 documentation targets resolve; the new SVG parses. All 1,248 baseline
  files remain. Scoped diff contains 11 modified and 11 new source/doc/test files.
  TypeScript also refreshed the existing generated `apps/web/tsconfig.tsbuildinfo`
  cache (12 changed existing files in total); its original bytes were not captured.
  No other baseline file changed. Final type checking disables incremental writes.
  VS Code opened captured before/after backend, workspace, card and guide diffs.
  The temporary PostgreSQL cluster was stopped after testing.

Independent reviewer found hidden unknown-reason explanations, misleading pair
wording for old quotation runs, and incorrect prerequisite row navigation. Fixes
and regression tests pass. Final independent recheck found no material issues.
A further unknown-status regression keeps future outcomes uncertain. Total fresh
verification: 166 web + 26 backend = 192 distinct parameterized checks; reruns are
not counted twice. Manual acceptance and the integration limits below stay open.

## Commands and receipts

Commands ran from the repository root unless noted. Backend runs used
`$env:DOTNET_ROOT=(Resolve-Path .dotnet).Path` and `--no-restore`.

- `.dotnet/dotnet.exe test apps/backend/tests/UaeDocumentIntelligence.Application.Tests --filter 'FullyQualifiedName~Results_coverage|FullyQualifiedName~Results_v2|FullyQualifiedName~Pair_projection|FullyQualifiedName~Completed_legacy_review'`
- `.dotnet/dotnet.exe test apps/backend/tests/UaeDocumentIntelligence.Api.Tests --filter 'FullyQualifiedName~Results_empty|FullyQualifiedName~Pair_policy'`
- `.dotnet/dotnet.exe test apps/backend/tests/UaeDocumentIntelligence.Persistence.Tests --filter 'FullyQualifiedName~Results_real_api'` with `TEST_POSTGRES_CONNECTION` pointed only at the temporary cluster.
- From `apps/web`: `../../.node/node.exe node_modules/vitest/vitest.mjs run app/reconciliation app/invoices/journey-results.test.tsx app/invoices/workflow.test.tsx app/invoices/journey.test.tsx --maxWorkers=2`; then the added decision test and `app/security/workspace-guide.test.tsx`. Final results-folder rerun: 15 passed, included in the total above.
- From `apps/web`: `../../.node/node.exe node_modules/typescript/bin/tsc --noEmit --incremental false`; scoped ESLint covered changed web files and results components/tests.
- `.node/node.exe outputs/results-stage-20261023/inspect-results.mjs` produced the read-only browser receipts. `final-summary.json` and `scope.json` record final counts and preservation.

## Limits and short manual check

Real API tests exercise the application and PostgreSQL with synthetic reviewed
documents and test identity. They do not prove live identity, extraction/OCR,
Next production wiring, reviewed export or deployment. Browser response replay
is presentation evidence only. No manual acceptance is claimed.

1. Finish the invoice and PO reviews, then choose **Show results**. Stay on step 3.
2. Check that Passed + Differed + Could not be checked equals Total checks.
3. Open a check: compare Invoice/PO values, supporting text and page numbers.
4. For a missing check, use its review action. Confirm the correct document opens.
5. Choose a reason and record a decision. Confirm counts/outcome do not change;
   reopen the comparison and read the decision history.
6. In the sample preview, confirm its simulation warning remains. An older result
   without full check records must not claim a complete match.
