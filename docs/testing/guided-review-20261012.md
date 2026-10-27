# G01 guided value review

October 12, 2026. Rand approved the simpler review direction. This is its first
bounded implementation batch, not completion of the combined invoice/PO flow.
Manual acceptance of this batch and the earlier quotation change is pending.

## Change and limits

Saved pair links open active reviews in a one-value view. Other document reviews
offer the same view as a choice. It puts warning values first, shows the returned
source quote, and requires explicit Keep this value, correction, or exclusion.
Reasons appear only for changes; short reason buttons reduce typing. I can't tell
does not save anything until Leave this value out is chosen. Original values and
history remain, and missing/excluded values are explained as possible check gaps.

All fields retains the existing additions, saved decisions and revision controls.
Switching views preserves drafts. Completing a review stays explicit and blocked
for unchecked values or unsaved drafts. No automatic confirmation, bulk acceptance,
new evidence, permission change, backend rule or database migration is included.
The UI uses the existing mutation lock, ETag and uncertain-response recovery.

Types, field labels/helpers, guided selectors, constants, components and styles
are separated under `apps/web/app/review/`. The existing document page owns the
saved review state and request handling. It is not a full refactor of that page.

G02 backend policy/persistence and G03 combined pair workspace remain. Current
scope and Choose/Use revision controls are still present. Existing quotation
documents, old comparisons and access rules remain. Dashboard/roles are separate
unapproved proposals. No .NET/Python implementation changed in G01.

## Evidence and review

Exact before files and a 1,133-file hash baseline are under
`outputs/guided-review-20261012`. Diff/preservation evidence uses this captured
working tree, including pre-existing uncommitted work, rather than Git HEAD.

The first focused UI run passed 95 of 97 tests. Two new assertions incorrectly
expected Arabic text plus numbers to be one DOM text node. They now assert the
complete blockquote text, preserving the exact bilingual evidence requirement.
There was no production change for that assertion repair. The initial result is
retained in `ui-first.json`. A subsequent 13/15 run exposed an asynchronous
return-link assertion and a number present in both value and source. Those
assertions now await the return context and check both numbers plus the exact
source quote. TypeScript also caught unsupported test-query options and a fixture
nullability issue; both were fixed. No business assertion was weakened.

Final guided rerun: **15/15 pass** in `ui-guided-recheck.json`. Together with the
unchanged regression passes, `latest-ui-results.json` records **99 distinct latest
passes across focused runs**, not one clean 99-test invocation. Preview contracts
pass **17/17**, with no skips. TypeScript and affected ESLint rechecks exit **0**.
These are fresh UI/static results; no .NET, Python or real database test is claimed.

The Progress and Quality Reviewer assessed the design read-only. It required an
explicit exclusion after I can't tell, saved-response-only progress, clear gaps,
safe drafts and protection against reconfirming an original machine value over a
saved correction. These requirements are implemented with focused tests. The
independent code review found no material production defect. Its material
verification finding (test/TypeScript failures) was fixed and rechecked by the same
reviewer. No material issue remains from that bounded assessment. Final lint,
documentation and preview receipts are recorded in
`outputs/guided-review-20261012/gates.json` and `review.md`.

Browser automation could not initialize: the helper exited with
`helper_unknown_error: setup refresh had errors`. No browser visual pass is claimed.
Both SVG diagrams were rendered and inspected without clipping. The guide
illustration is a diagram, not a screenshot. Read-only HTTP checks found the sample
invoice and a healthy preview; they do not establish browser behavior. Existing extraction/OCR,
live identity, complete integration, report/export and deployment gaps remain.

The preservation check found seven intended changes to existing files, 11 new
source/documentation files, zero missing baseline files and zero unexpected
baseline changes. Exact diffs were opened using VS Code's reuse-window command.
Nothing was committed, deleted or applied to a retained database.

### Focused commands

From the root, using the installed Node runtime:

```powershell
.node/node.exe apps/web/node_modules/vitest/vitest.mjs run app/review/guided-review.test.tsx app/review-experience.test.tsx app/page.test.tsx app/invoices/workflow.test.tsx app/review-selection.test.tsx app/document-navigation.test.tsx app/document-simplification.test.tsx --root apps/web --maxWorkers=1
.node/node.exe apps/web/node_modules/vitest/vitest.mjs run app/review/guided-review.test.tsx --root apps/web --maxWorkers=1
.node/node.exe apps/web/node_modules/typescript/bin/tsc --project apps/web/tsconfig.json --noEmit --incremental false
.node/node.exe --test tools/ui-preview/fixtures.test.mjs tools/ui-preview/supplier-invoice-workflow.test.mjs
```

Affected ESLint ran from `apps/web` against `app/page.tsx app/review` with
`--max-warnings 0`. Two final lint launcher attempts used incorrect runtime paths
and did not run ESLint; `eslint-verified.log` is the successful actual recheck.
Documentation uses the existing VitePress build into this batch's separate
`site` directory. No production app build or full demo was run.

## Short manual check

Use the local synthetic preview at `http://127.0.0.1:3410/`. It uses the actual
components with simulated storage and sample documents, not real extraction or
database persistence.

1. Open Invoice to review.txt, Start review, then Check one value at a time. Expect
   one suggested value beside its source and no edit boxes.
2. Change a value, choose a reason, switch to All fields and back. The unsaved draft
   should remain. Save it deliberately; only then should the next value appear.
3. Choose I can't tell, then Cancel. Nothing should save. Repeat and explicitly
   Leave this value out. Expect an unavailable-value notice, never a complete match.

From a saved pair, Open invoice review should choose guided mode automatically
when the review is active. User results have not been reported. Follow the
[workflow guide](../workflow-review.md) for the current steps and remaining work.
