# R2-T01 — Design proposal verification

September 26, 2026. Result: complete as a design-choice prototype, independently
reviewed. Production visual direction still requires Rand's choice (R2-T02).

Open [the proposal](http://127.0.0.1:3410/design-preview) while the lightweight
preview is running. The preview toolbar also links to it. Style A uses neutral
surfaces with blue actions; Style B uses warmer neutral surfaces with muted green
actions. Both support light/dark appearance, one font family and heading color,
the same controls, and the same sample workflow.

## Scope and evidence

- Worklist, document search/status filters, source/detail review, correction
  reasons, completion, comparison, questions, activity, contextual role guides,
  administrator-only view, and automatic case-reference/title/category example.
- Ten independent Vitest tests pass (15.95 seconds). They cover stored appearance,
  corrections and original evidence, completion, role restrictions, guide focus,
  draft preservation, unique case references/rapid submission, worklist search,
  comparison/decision continuity, and save/cancel keyboard focus.
- Strict standalone TypeScript check passed for the proposal component.
- Focused component/test ESLint completed without file diagnostics; the reused
  Next configuration emits root-level React/pages discovery notices because these
  files belong to Vite tooling. Test configuration passed Node syntax checking.
  An initial broad lint attempt incorrectly applied Next's Babel configuration to
  the tooling `.mjs` file; it was replaced with the appropriate focused checks.
- Seven existing request/fixture checks passed after preview integration.
- Documentation production build passed (77.81 seconds); only the existing large
  bundle advisory remains. The first sandboxed attempt could not read ancestor
  paths for esbuild; the approved external-sandbox run completed.
- Browser inspection covered A light worklist, A dark document review, B dark
  review/help, and B light at 390 × 844. Mobile worklist, source/detail, help and
  case form were inspected; mixed Arabic source and field values remain readable.
  Found and fixed horizontal overflow from absolutely positioned accessible labels
  inside the scrollable table. Recheck: document width 375 at viewport width 390
  (scrollbar occupies the remainder). Desktop viewport restored after testing.
- Browser warning/error log was empty at final inspection. The existing actual-app
  preview still renders and its original empty-workspace scenario was restored.

Commands, from the project root:

```powershell
./.node/node.exe apps/web/node_modules/vitest/vitest.mjs run --config tools/ui-preview/proposal-tests.config.mjs
./.node/node.exe apps/web/node_modules/typescript/bin/tsc --noEmit --jsx react-jsx --module esnext --moduleResolution bundler --target es2022 --allowSyntheticDefaultImports --skipLibCheck --strict --typeRoots apps/web/node_modules/@types tools/ui-preview/design-proposal.tsx
./.node/node.exe --test apps/web/app/review-request.test.mjs tools/ui-preview/fixtures.test.mjs
```

## Independent review and fixes

The test author and code reviewer were separate from the implementer. Review
identified unfinished corrections being discarded through another field or
navigation, finishing with an unfinished edit, missing focus restoration, and
comparison results disappearing on document reopen. Fixed these, added regression
coverage, and received a clear final code review. The mobile overflow fix changed
only the containing block of the table and was browser-verified.

## Integration impact

| Area | Result |
| --- | --- |
| Routes | `/design-preview` exists only in the tooling preview; existing production routes unchanged |
| APIs, ETags, request keys, persistence | Unchanged; proposal never calls the API. State is local and resets on reload; only namespaced appearance preferences persist |
| Authentication and roles | No identity/provider changes; simulated role controls demonstrate intended presentation only |
| Audit | Local sample activity only; no real audit event or durability claim |
| Source evidence | Clearly illustrative source; original amount remains visible after a sample correction |
| Arabic | Mixed-direction sample visually checked; no change to extraction or question contracts |
| Preview isolation | Separate route/root; prefixed styles; no production imports from tooling; existing adapter tests pass |

## Limits and next tasks

Comparison output is a fixed, explicitly labeled example and does not evaluate
corrections or real rules. Case reopening, category filtering, uploads, actual
member changes, authentication, audit durability and backend connectivity are
subsequent implementation work. Identical revised-decision resubmission can still
append a sample activity entry; R4-T05 owns the final repeat policy. This proposal
does not close any backend or production acceptance gate. No full accessibility
certification or real history-download test is claimed.

Next: record the user's appearance choice before R3 shared-interface implementation.
