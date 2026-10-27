# N13: sidebar icons, phase guidance and final-test plan

October 26, 2026. Scope: sidebar icons and visible current/saved/upcoming invoice
steps. Explain current administrator access and JSON downloads. Prepare the final
fresh-document acceptance checklist. Keep admin dashboard redesign, report/CSV
implementation and dependency remediation separate.

## Findings and changes

- The current local identity is Staff. Administration links already exist for
  Admin only: Members & access (`/organization`), Audit history and Retention &
  holds. There is no redesigned dashboard yet. Permissions, roles, accounts and
  identity configuration were not changed to expose those links to Staff.
- Sidebar links now have labelled outline icons from `lucide-react` 1.61.0.
  Static named imports avoid an entire icon catalog at runtime. Icons are
  decorative, hidden from assistive technology and not independently focusable.
  Existing text labels, destinations and active-page semantics remain.
- Invoice phases have numbered badges: purple/current with text, green/saved
  with a check and text, grey/upcoming. `aria-current="step"` identifies only
  the current phase. Reopening a saved phase shows it as current.
- Green **Files saved** requires the persisted pair with no known type issue,
  pending selection, uncertain save or document-loading error. **Reviews saved**
  additionally requires the saved review pins for that pair and no unsaved review
  edits. These describe saved setup, not a match. Results remain the current
  reading/decision step; old results are labelled available, never automatically
  completed just because someone opened them. No new persistence or API writes.
- The existing JSON action downloads review history, with a
  `review-history-{documentId}.json` filename. It is not a readable comparison
  report. No export format or history content changed. S5 still needs readable
  reports/CSV and useful business filenames.
- [Final user acceptance](final-user-acceptance.md) lists fresh fixture pairs,
  scenario IDs and expected behavior. Export, actual identity and full processing
  prerequisites are explicit. No manual pass is inferred from creating the plan.

## Verification

Evidence folder: `outputs/navigation-progress-20261026/`.

- Focused Vitest run: 72 passed, 1 new test failed because it expected raw server
  wording instead of the existing conflict/reload message. Corrected that expected
  message; all 23 journey tests passed on rerun. Total distinct passing tests: 73
  across navigation permissions, journey, results, start-check, phase rendering
  and appearance. No existing assertion was weakened.
- TypeScript (`tsc --noEmit --incremental false`) and scoped ESLint passed.
- Five live browser views passed against the existing isolated Next/API app:
  start page, saved workflow in both themes, Light at 420px and active review in
  Dark at 420px. No horizontal overflow or captured runtime exceptions. Staff
  was denied direct access to `/organization`. No API writes were made; the
  saved case response was identical before/after. These use synthetic retained
  records and Development identity, not real-account acceptance.
- The first browser script stopped during navigation because it read a body
  before the new document existed. An optional-body guard fixed the test harness;
  the complete rerun passed.
- Independent review found a checklist ambiguity: missing/unchecked/unsupported
  AED currency blocks evaluation, unlike an evaluable AED pair with missing
  price/quantity facts. Scenario C14 now isolates that gate and requires unchanged
  previous results. C05 now explicitly limits quantity conclusions to this
  invoice and PO, with no full/partial question or remaining-to-bill claim.
  The reviewer rechecked these corrections and found no remaining material issues.
- All 1,286 baseline files remain, with 12 scoped modifications and 5 additions.
  Before/after diffs and the acceptance checklist were opened in the existing
  VS Code window. All 277 checked documentation links resolve; the SVG is valid
  XML and the checklist contains 52 unique scenario IDs. These scenarios remain
  planned, not passed. No unrelated application build was run.

Commands (from `apps/web`, using the installed Node runtime):

```powershell
../../.node/node.exe node_modules/vitest/vitest.mjs run app/security/workspace-navigation.test.tsx app/invoices/journey.test.tsx app/invoices/journey-results.test.tsx app/invoices/start-check.test.tsx app/invoices/components/workflow-phase.test.tsx app/appearance.test.tsx
../../.node/node.exe node_modules/typescript/bin/tsc --noEmit --incremental false
```

## Dependency finding

Installing Lucide with scripts disabled added one package. Existing dependency
versions did not change; npm reclassified two React typing entries from dev-only
to optional dev because of the peer relationship. `npm audit` reports 8 issues in
existing packages: 2 moderate, 5 high and 1 critical (Next.js). Lucide is not named
in that audit. The detailed JSON and lockfile comparison are retained. Treat this
as an open dependency-remediation task before deployment. No `audit fix --force`
or unrelated framework update was performed.

## Manual check

1. Refresh Home. Sidebar icons should sit comfortably beside their labels in both
   themes. Staff should not see administration links.
2. Start a check: step 1 is purple, steps 2/3 grey. Save both files and continue:
   step 1 becomes green, step 2 purple. Finish and save both reviews: step 2
   becomes green when step 3 opens. Result outcomes still state differences or
   missing checks separately.
3. Return to a saved step. That step becomes current without modifying the saved
   document values or results. Try a narrow window and keyboard navigation.

Manual acceptance remains open. This visual batch does not verify live upload
workers, Arabic OCR, real identity, reviewed report/CSV or deployment.
