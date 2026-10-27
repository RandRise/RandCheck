# Reading-page layouts and button spacing

October 22, 2026 · D03. Rand authorized Document library, Earlier cases and
history, and Ask about documents improvements after accepting the shared themes.
The administrator dashboard follows as a separate stage. Visual/manual
acceptance of these layouts remains open. Only the existing Progress and Quality
Reviewer was used; no temporary specialists were created.

## Implemented scope

- **Library:** use the available width for a file list beside the reading area.
  Saved values open after an explicit file choice, in two-column cards that
  stack on a phone. Original downloads, evidence, review history, unchecked
  labels and historical quotations remain. Recovery buttons are separated from
  their explanations; selecting a file does not reprocess or confirm anything.
- **History:** a three-step reading guide, grouped filters and saved case cards.
  The existing named creation/recovery form moves below the list in a closed
  disclosure. Its permissions, request identities and retry payloads remain.
  Standalone styles do not change embedded invoice results.
- **Questions:** sources and question share the top row; answer and saved
  questions follow across the width. Phone layouts stack in that order.
  The numbered guide explains selection, asking and evidence. Successful
  explicit saved-answer loads focus the answer heading; stale work cannot
  move focus. Saved answers keep their original source versions.

`PageWorkflow` supplies the reusable reading guide. Focused CSS files own the
History and Questions layouts. These guides are not completion indicators.
The theme, backend rules, permissions, documents and audit history are unchanged.
Before/current diffs for library styling, History, Questions and the focus fix
were opened using `code --reuse-window --diff` in the existing VS Code window.

## Fresh verification

Evidence and captured before files: `outputs/workspace-layout-20261022`.
Commands below ran from `apps/web` with the repository Node runtime.

| Check | Command / evidence | Result |
| --- | --- | --- |
| Library, processing retries and review controls | `../../.node/node.exe node_modules/vitest/vitest.mjs run app/documents/library.test.tsx app/document-repeat.test.tsx app/review-experience.test.tsx app/document-simplification.test.tsx` | 60 passed |
| Case history, permissions, version selection and safe retries | Same runner with `run app/reconciliation` | 70 passed; final navigation rerun 9 passed |
| Questions, exact sources, saved results, failures and focus | Same runner with `run app/questions` | Final 52 passed, including 2 new stale-focus regressions |
| TypeScript | `../../.node/node.exe node_modules/typescript/bin/tsc --noEmit --incremental false` | Passed after focus fix |
| Scoped ESLint | Project Node + `node_modules/eslint/bin/eslint.js`, changed TS/TSX files | Passed; affected files rechecked after focus fix |
| Library rendering | `inspect-library.mjs` | 4 views: 1440/420px, Dark/Light; approximately 12px minimum Reprocess gap |
| History and Questions rendering | `inspect-history-questions.mjs` | 12 views: saved case list, opened case and question composer at both widths/themes |
| Saved-answer focus | `inspect-saved-answer-focus.mjs` | 2 views: browser-only intercepted GET fixtures at 1440/420px; answer heading focused and visible |
| Shared button spacing | `inspect-home-workflow-spacing.mjs` | 8 views: Home and an existing workflow at both widths/themes; no visible button/text intersections |

That is **182 distinct focused tests**; reruns are not added to the count.
All 26 browser views had no horizontal overflow, runtime exceptions or
application writes. The existing sample preview was not restarted or reset.
No retained saved questions were available, so the focus browser check used
explicitly synthetic, browser-only responses. It is not backend integration.
Existing tests cover Arabic text, source revision preservation, rejected/uncertain
saves and stale requests. No unrelated .NET/Python suite or production rebuild
was needed for this frontend batch.

## Findings and fixes

- Self-review moved saved questions below the answer to reduce unused space and
  keep reading order clear. It also corrected the empty-case message after the
  creation form moved below the list, and scoped the Questions border override
  against shared responsive styles.
- Independent review cleared the library and History layouts. It found that a
  saved answer could appear above the user's viewport after moving saved controls.
  The fix focuses the answer heading after rendering, using the existing request
  generation/mount guard both before scheduling and when focus executes. Tests
  cover successful focus, a late response and typing before the focus callback.
  The same reviewer rechecked the fix and final records; no material findings
  remain. The final eight Home/workflow spacing views were an additional
  implementation-agent check after that assessment.
- A newly added test incorrectly assumed Testing Library's role query excludes
  controls inside closed native `details`. It now verifies the closed parent and
  button containment; browser checks verify the disclosure's actual appearance.
  A subsequent navigation run timed out while static checks ran concurrently and
  a following test failed. An isolated rerun passed all 9 without changing timing
  limits or navigation assertions. The Vitest config warning and occasional React
  `act` warnings remain documented; they are not treated as product failures.

Independent baseline verification confirmed all 1,214 files remain, with exactly
16 expected existing-file changes and no unexpected changes. Nine additions are
the four UI files, three screenshots, one diagram and this report. All 244 local
documentation targets resolve; the new SVG parses as valid XML. Passing
these checks does not close manual acceptance or N04/G02/G03 real identity,
extraction/OCR, backend comparison, report/export and deployment gaps. Browser
coverage is representative, not every button/state or a full accessibility audit.

## Short manual check

1. **Library:** select a saved document. Read its values and open Processing
   details. Check the space around Reprocess document without activating it.
2. **History:** open a saved case and choose a saved comparison, if available.
   Confirm its evidence and earlier decisions are still reachable.
3. **Questions:** select sources and inspect the question/answer layout. If you
   have a saved question, reopen it; the page should bring its answer into view.
4. Repeat a quick look in Light mode and at a narrow window width. Report any
   clipped text, crowded buttons or space that still feels unhelpful.

The next design stage is the administrator dashboard after this visual review.
