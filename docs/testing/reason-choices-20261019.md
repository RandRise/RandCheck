# Reason choices — October 19, 2026

## Approved scope and behavior

Rand approved choices for the required review reason to reduce typing. A shared
`review/reason-input.tsx` component and `review/reason-options.ts` constants supply
appropriate suggestions for adding a missing field, changing/clearing an extracted
value, and clearing an earlier added field. The guided view already has suggestions.

No reason is preselected. Suggestion buttons use `type="button"` and only update
the draft. Other clears a preset, preserves an already-custom explanation and
focuses the input. Users can also edit the reason directly. The current Add/Save/
Clear action records that reason through the existing API. Choosing a reason does
not submit a request or replace missing source evidence. Permissions, ETags,
uncertain-save handling, existing records and Arabic text remain unchanged.

## Focused verification and review

Fresh results: **68/68 tests pass**, no failures/skips, across `page`,
`document-simplification`, `review-experience`, `review/guided-review` and
`document-types`. Run `vitest run` with those five files and `--maxWorkers=1` from
`apps/web`. New cases check exact preset/custom reason payloads, no writes on
selection, empty Other rejection, Arabic text preservation, correction/clear,
earlier added fields and disabled choices during a pending save.

`tsc --project tsconfig.json --noEmit --incremental false` and ESLint on the five
affected TypeScript files both exit 0. Independent source review found no material
issue. Browser layout and manual acceptance remain pending; these are UI tests,
not fresh .NET/database integration verification.

Baseline: 1152 existing files. Before copies, diffs, review findings and receipts
are in `outputs/reason-choices-20261019`; test evidence is `web-tests.log`,
`typescript.log`, and `eslint.log`. No backend files or data were changed. No
preview restart, reset, dependencies or broad application build were needed.

Workflow guide source and roadmap records are updated. Source links are checked;
the earlier static guide is not rebuilt. Existing diagrams do not depict these
normal-form controls and remain accurate. Dashboard/role proposals, payment-term
placement, supplier checks and G02/G03 remain separate.

## Short manual check

1. In All fields, find Add a missing field. Click **Shown on the document but
   missed.** It should fill Reason without saving.
2. Click **Other — write my own**. Write an explanation or choose the preset
   again. Use **Discard new field** if you do not want to save anything.
3. When saving a test addition or change, check that the selected reason appears
   with its saved decision. No sample reset is necessary.
