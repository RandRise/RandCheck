# Named fields without custom settings — October 19, 2026

## Approved scope

Remove Custom field settings from the normal review screen. Keep named field
choices, existing custom values and historical reviews. Do not change permissions,
backend APIs, audit history or comparison evidence rules. Moving Payment terms is
still a proposal; G02/G03 remain separate work.

## Change and preservation

The addition form no longer offers Custom field, Field key, Field position or
Value type. The selected catalog field supplies the type at submission. Document
fields use occurrence 0; item fields retain a visible Line number. Switching field
choices resets the line number. Obsolete custom-control state and CSS are removed.
Existing values render through the same review cards and history paths as before.

The source baseline records 1151 existing files. Current before copies, review diff,
test logs and preservation hashes are in `outputs/named-fields-20261019`. Earlier
uncommitted work is preserved. No backend/data mutation or service restart is needed.

## Verification

Focused tests cover named-field type/position inference, line-to-header switching,
Arabic values/reasons, ETags, unsaved drafts, disabled actions, guided review and
saved custom values in active/completed reviews. The obsolete custom-creation test
was replaced with preservation checks; existing payload and safety assertions stay.
Independent review caught an unsupported `exact` option in a new test query; it was
removed before static verification. The final focused run passed **63/63 tests**
across `page`, `document-simplification`, `review-experience`, `review/guided-review`
and `document-types`; TypeScript and affected ESLint both exited 0. The initial
63-pass run is retained separately and is not added to this count. Run with the
installed Node runtime: `vitest run` for those five files with `--maxWorkers=1`,
`tsc --project tsconfig.json --noEmit --incremental false`, and `eslint` on the four
changed TSX files, from `apps/web`. Receipts are `web-tests-final.log`,
`typescript.log` and `eslint.log` in the output folder.
Manual/browser and real backend integration acceptance remain
separate from these UI checks.

The workflow guide source was updated. Existing diagrams do not depict the removed
controls and remain accurate. Documentation validation is limited to the changed
source and links (4 pages and 4 new links/anchors pass). The previously built static
guide is not rebuilt or restarted in this UI batch; use the updated Markdown source.
No full application rebuild is required.

## Short manual check

1. In an active review, choose **All fields** if needed. Find **Add a missing field**.
   There should be no Custom field choice or Custom field settings.
2. Choose **VAT**, then **Line quantity**. Only the item field should ask for a
   line number. Use **Discard new field** if you do not want to save anything.
3. If you have an older custom value, confirm it still appears in its saved review.

Do not reset sample data. This screen change can load through the existing preview;
its simulated backend and the separately implemented AED gate are unchanged here.
