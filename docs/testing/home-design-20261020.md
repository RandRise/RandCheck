# Approved Home design implementation

October 20, 2026. D01 follows Rand's acceptance of the Mosaic-inspired preview
and explicit approval to apply it to the working Home page.

## Scope and implementation

Home now has a grey/purple Start here panel, a document-pair illustration,
three ordered step tiles and a saved-review list with one destination per row.
Plain saved/closed-history labels do not imply a match or complete checks.

`apps/web/app/invoices/components/invoice-home.tsx` retains the workflow hook and
its request handlers. `home.css` scopes layout and colour to Home and its enclosing
workspace frame. The mounted-screen selector matters: Next can retain route
stylesheets after navigation. No global theme choice is overridden. Existing light
mode uses readable light surfaces. Unused earlier Home rules were removed from
`journey.css` after a dependency search.

Permissions, identity isolation, account/help/sign-out controls, old bookmarks,
loading/error/empty states, pagination, refresh and safe creation retries remain.
No backend, storage, comparison policy or retained-preview data changed.
Workflow/results/library redesigns are separate future batches.

## Findings and fixes

Self-review found the broad Home button-outline selector overriding the approved
primary border. It now targets account controls and Home button defaults
separately; the primary selector wins. The final browser check asserts its
computed lavender border. No business logic was changed to obtain passing tests.
Independent review found no material code/design issue. It caught a guide note
that called all pictures illustrations; that note now distinguishes illustrations
from the actual sample-preview screenshot. The reviewer independently verified
six expected changes and four additions against 1206 captured source files, with
none missing or unexpectedly changed.

## Fresh verification

Evidence, captured baseline, diff and screenshots:
`outputs/home-implementation-20261020`.

- Project Node with `node_modules/vitest/vitest.mjs run` on `home.test.tsx`,
  `journey.test.tsx`, `home-routing.test.tsx`, `workspace-navigation.test.tsx`,
  `workspace-guide.test.tsx` and `appearance.test.tsx`: **75 passed**.
  The five new Home tests cover neutral history/Arabic labels, loading/empty
  states, refresh failure and keyboard retry, pagination/deduplication and
  denied creation access. Existing tests cover uncertain creation-key reuse,
  unfinished reviews, stale saves, navigation, roles, Help and appearance.
- `node_modules/typescript/bin/tsc --noEmit --incremental false`: passed.
- Scoped ESLint on the changed component and new test: passed.
- Eleven declared colour pairs pass: text at least 4.74:1, tested control borders
  at least 3.14:1. The browser separately checks the computed primary colours.
- `inspect-home.mjs`: dark/light at 1440, 420 and 360px, no horizontal overflow,
  runtime exceptions or application writes; one start action and one destination
  per row. Keyboard Tab reaches Start with a visible 3px outline. Navigating to
  Document library restores its original palette.
- A DOM-only long Arabic/mixed name and long English-digit reference fits at
  360px. It is a temporary layout fixture, not saved application data.

The initial runtime-loader command was blocked by PowerShell script policy and
fell through to system Node 20, which failed with EPERM. It ran no successful
checks. The recorded passing runs use the project's Node executable directly.
Headless Edge used a disposable profile and closed after checking. No broad
application rebuild or preview restart was needed.

These are focused tests and synthetic-preview checks, not real identity, OCR,
export, deployment or backend comparison acceptance. N04/G02/G03 gaps remain.
The static preview's earlier tests are separate historical evidence.

## Short manual check

1. Open Home. Check that Start here is clear and your saved reviews remain.
2. Open one saved review with Continue, then return using Home. Confirm the
   expected review opens and Home keeps the new design.
3. Switch Appearance to Light and back to Dark. Narrow the window and check
   that labels and buttons remain readable. Try Tab to see the focus outline.

Working-page manual acceptance remains pending.
