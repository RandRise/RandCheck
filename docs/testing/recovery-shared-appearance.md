# R3-T01 — Shared appearance foundation

September 30, 2026. Rand approved Style A (blue accents) in dark mode and deferred
light palette refinement. This task applies its shared styling to the actual web
components, independently of the separate workflow proposal.

## Changes

- `apps/web/app/appearance.css`: dark/light semantic colors, one heading font/color,
  common buttons and inputs, focus/hover/pressed/disabled states, and shared surfaces
  for review, reconciliation, questions, sign-in, membership, audit and retention.
  Status/error colors remain meaningful. CSS-driven uppercase transformations are
  removed; literal old labels/layouts await the navigation/workflow tasks.
- `appearance.tsx`: accessible Appearance selector, default dark, namespaced local
  storage, cross-tab updates, safe memory fallback for denied reads or writes.
  Appearance changes retain the existing child tree and in-progress form values.
- Production layout and actual-component preview use the same provider. The
  authentication callback bypasses it. The separate design proposal is unchanged.

## Task checks

| Check | Result |
| --- | --- |
| Independent appearance tests | 10 pass: default, stored/invalid/cleared preference, cross-tab updates, denied reads/writes, unrelated session storage, draft preservation, callback isolation and hydration |
| Full UI regression | 91 tests across 10 files pass in 49.82 seconds, one worker |
| Request and preview contracts | Seven checks pass, including ETag behavior and synthetic access boundaries |
| TypeScript | `tsc --noEmit --incremental false` passes |
| Focused ESLint | Appearance implementation/tests and root layout pass without diagnostics |
| Next production build | Pass: compilation, TypeScript and all 10 static pages; no Docker/backend needed |
| Independent code review | No remaining provider/integration blocker; corrected low-contrast sign-in wordmark found during review |
| Browser | Dark review, questions, members, audit, retention, sign-in inspected; light review inputs/values readable; unsaved draft survived theme switch; 390px sign-in no horizontal overflow |

Commands from the project root:

```powershell
./.node/node.exe apps/web/node_modules/vitest/vitest.mjs run --root apps/web --maxWorkers 1
./.node/node.exe --test apps/web/app/review-request.test.mjs tools/ui-preview/fixtures.test.mjs
./.node/node.exe apps/web/node_modules/typescript/bin/tsc --project apps/web/tsconfig.json --noEmit --incremental false
```

From `apps/web`, use the project Node runtime with
`node_modules/next/dist/bin/next build` and
`node_modules/eslint/bin/eslint.js app/appearance.tsx app/appearance.test.tsx app/layout.tsx`.

## Integration review

| Contract | Impact |
| --- | --- |
| Routes/deep links | No route changes; same screens and links, appearance survives full preview navigations |
| Identity/roles | Enterprise provider unmodified; appearance outside its gate; callback bypass covered by tests; no token or membership changes |
| API/ETags/request keys | No network code touched; existing regression suite passes |
| Persistence/migrations | Only a browser appearance key added; no database or migration changes |
| Audit/history | No real workflow/audit operation changed; history download remains deferred to Rand |
| Evidence/Arabic | Source text, field contents and bidi rules unchanged; theme styles do not alter their direction or provenance |
| Preview parity | Actual screens import the production appearance provider/CSS; proposal remains separately scoped |

The browser uses synthetic identities/data: this is not proof of real Microsoft
authentication, database durability, OCR or backend security. No paid processing,
real memberships or retention operations were performed. A synthetic review was
started for form-state verification; its temporary draft was restored without saving.

## Remaining work

Saved light mode can briefly show the server's dark default before hydration;
the hydration test proves no mismatch, not zero visual flash. The choice to avoid
adding scripts to the identity path keeps this polish item explicit. Light-mode
palette refinement is deferred at Rand's request. Existing phase navigation,
redundant headings and layout remain for R3-T02/R4; this task closes only the shared
appearance foundation. Dark mode and the preview are ready for inspection at
[the actual-component preview](http://127.0.0.1:3410/).
