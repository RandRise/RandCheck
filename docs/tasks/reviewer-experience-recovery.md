# Reviewer experience recovery — phases and task gates

Authorized to begin by Rand on September 26, 2026. Scope follows the
[recovery plan](../design/reviewer-experience-recovery-plan.md), including role
guidance, consistent typography/buttons, action-repeat review, and case categories.
These R-prefixed phases span the existing product phases; they do not replace or
retroactively relabel Phase 0–8 acceptance evidence. Existing Arabic work is retained.

## October 2 simplification implementation

Rand authorized the [simplification audit](../reviews/2026-10-02-simplification-audit.md)
recommendations and three coordinated agents: implementation, independent review
and bug fixes, and testing/integration. The coordinator owns this ledger. Existing
uncommitted work is preserved; implementation proceeds through the task gates below.

| Increment | Outcome | Status |
| --- | --- | --- |
| Startup consistency | Current English extractor version agrees across local profiles; UI preview and real integration demo have clear entry points | Complete — independent source review; configuration/provider/preview checks in [run evidence](../testing/simplification-20261002.md) |
| R4-T05b | Safe document/upload/processing/question retries and deliberate review revisions | Complete — independent review and [fresh focused/real PostgreSQL verification](../testing/simplification-20261002.md) |
| Documents | Needs review/In progress/Reviewed list state, role-appropriate next actions, simpler field controls | Complete — independent review and [summary/role/UI verification](../testing/simplification-20261002.md) |
| Questions | Named document/case context, reviewed answers by default, advanced retrieval/identifiers outside the everyday path | Complete — independent review and [private history/exact source/UI verification](../testing/simplification-20261002.md) |
| Feature organization | Smaller workflow modules for touched features; optional pilot registration has a clear boundary | Implemented — independent review and automated gates passed; October 3 sampled browser checks pass, full acceptance remains separate |

Each increment requires independent review and verification of its affected roles
and real integration contracts before dependent implementation begins. The existing
R4-T05a comparison behavior and Arabic evidence work remain dependencies. This
sequence does not silently complete the R5/R6 tasks or make live identity, paid
processing, or cloud deployment part of the account-free portfolio milestone.
Test and integration receipts are recorded in the [testing evidence for this run](../testing/simplification-20261002.md).

## Rule for every task

Finish a task before beginning the next implementation task:

1. State its user outcome, acceptance conditions, dependencies, and files/contracts
   at risk. Record whether work is a prototype, real UI, API, or migration.
2. Implement the smallest complete increment, keeping the fast preview available.
3. Run meaningful focused checks and record commands/results. Use the real backend
   for contracts it owns; simulated preview passes cannot prove integration.
4. Have an independent agent review the change when available. Record findings and
   fix blockers; rerun checks affected by a fix. Read-only audits may run concurrently
   within the current task, but dependent implementation tasks remain sequential.
5. Check integration impact: routes/deep links, authentication and role changes,
   API payloads, request keys/ETags, persistence/migrations, audit, source evidence,
   Arabic/mixed-direction text, and preview parity. Mark each relevant item verified,
   unchanged with reason, or blocked. An unverified affected integration is not green.
6. Record the result and residual limits in the task ledger. Only then start the next
   task. Failed checks hold the task open. External dependencies stay explicit.

No blanket full-stack rebuild after every CSS edit. Choose checks from the actual
impact; include cross-feature regressions at the shared-frame and workflow milestones.

Rand's September 30 requirement: test each affected workflow as Operator, Reviewer,
Viewer and administrator-only, plus combined administration/document roles. Include
denied access and role-change cleanup. Do not treat an administrator flag as document
access, and record which checks used the real API versus synthetic preview data.

## Phases and tasks

| Phase | Task | Deliverable / exit condition | Status |
| --- | --- | --- | --- |
| R0 · Fast testing | R0-T01 | One-action Docker-free actual-component preview; restart, sample state, update and ownership checks | Implemented; [verification](../testing/ui-preview-verification.md) predates this new review gate |
| R1 · Baseline | R1-T01 | Independent UI/API audits, action and capability inventory, reproducible baseline checks, integration risk map | Complete — [baseline evidence](../testing/recovery-baseline.md) |
| R1 · Baseline | R1-T02 | Prevent stale document-scoped responses from overwriting a newly selected review; delayed-response regression tests and independent review | Complete — [review and integration evidence](../testing/recovery-review-context.md) |
| R2 · Design choice | R2-T01 | Interactive work-list/review proposal with two restrained visual directions, light/dark modes, role help and case-form examples; tested and independently reviewed | Complete — [verification](../testing/recovery-design-proposal.md) |
| R2 · Design choice | R2-T02 | Record Rand's choice after seeing the preview, including any requested adjustments | Complete — September 30: A, blue accents; dark approved, light refinement deferred |
| R3 · Shared interface | R3-T01 | Theme tokens and reusable typography/buttons; all interaction states and stored appearance preference | Complete — [verification](../testing/recovery-shared-appearance.md) |
| R3 · Shared interface | R3-T02 | Shared role-aware navigation/account frame across actual screens; preserve login/callback/session gates | Complete — [verification](../testing/recovery-shared-navigation.md) |
| R3 · Shared interface | R3-T03 | In-workspace role guidance, dismiss/reopen behavior, and permission-change behavior | Complete — [verification](../testing/recovery-role-guidance.md) |
| R4 · Continuous work | R4-T01 | Document/case list and stable reopen/deep-link flow with necessary tenant-scoped API queries | Complete — [verification](../testing/recovery-reopen-work.md) |
| R4 · Continuous work | R4-T02 | Focused document review, context evidence and clear save/finish states; preserve review history and concurrency | Complete — [verification](../testing/recovery-focused-review.md) |
| R4 · Continuous work | R4-T03 | Comparison/questions in shared context with pinned-version correctness and saved-result recovery | Complete — [verification](../testing/recovery-comparison-questions.md) |
| R4 · Continuous work | R4-T04 | Case reference/category contract, migration/backfill, creation form and filters | Complete — [verification](../testing/recovery-case-metadata.md) |
| R4 · Continuous work | R4-T05a | Comparison action rules, case-creation retry identity, explicit replacements/reruns/revisions and integration tests | Complete — [verification](../testing/recovery-comparison-repeat-actions.md) |
| R4 · Continuous work | R4-T05b | Document review/upload/processing and question repeat rules, safe retries and deliberate revisions | Complete — October 2 [verification](../testing/simplification-20261002.md) |
| R5 · Security experience | R5-T01 | Real signed-out/denied/expired/revoked journeys and discoverable authorized administration | Local fixes/review/59 focused checks passed October 5; fresh browser/live identity journey evidence pending |
| R5 · Security experience | R5-T02 | Context activity and readable/filterable audit where supported by scoped APIs | Pending R5-T01 |
| R5 · Security experience | R5-T03 | Authenticated real-application walkthrough and role/action/audit proof | External Entra configuration/test accounts must be checked; mocks cannot close this task |
| R6 · Acceptance | R6-T01 | Integrated regression, both themes, accessibility/RTL and first-time role walkthroughs | Pending R5 |
| R6 · Acceptance | R6-T02 | Resolve observed blockers, update guides and evidence, record actual completion and remaining external limits | Pending R6-T01 |

Early baseline work includes identity configuration readiness. Do not wait until R5
to discover its prerequisites. Do not silently activate a production deployment.
The existing account-free preview and local security contract remain useful while
live identity verification is unavailable.

## Task records

### R1-T01 — Baseline and integration inventory

- Scope: read-only audits and baseline regression; no product behavior changes.
- Reviewers: independent workflow/API and UI/identity agents.
- Checks: 71 UI tests and 7 request/preview checks passed; [inventory and results](../testing/recovery-baseline.md).
- Integration: map the current contracts before choosing route/component changes.
- Exit: reviewed inventory with priority, evidence, affected integrations, and tests;
  baseline results recorded, with unrelated existing limitations preserved.

### R1-T02 — Keep asynchronous responses in their document context

- Trigger: two independent source audits identified unguarded review/processing
  responses after document selection changes. Prioritized before visual work.
- Scope: frontend request ownership/state only; preserve endpoints, payloads, ETags,
  backend authorization, append-only history and committed mutation semantics.
- Tests: an independent agent owns delayed-response regression tests; root implements
  the fix. A separate agent reviews the final implementation and integration impact.
- Result: 10 new regressions pass; full UI suite 81/81 across 9 files; TypeScript
  and focused lint pass. Independent review's two blockers were fixed and re-reviewed.
  [Integration record](../testing/recovery-review-context.md) closes this task.

### R2-T01 — Visual/workflow proposal

- Scope: proposed, isolated preview of the intended experience, clearly distinguished
  from the existing actual-component preview and from implemented API features.
- Required examples: My work, source/detail review, comparison, questions, activity,
  role help, and reference/title/category creation. Two appearance directions use the
  same behavior and font/color discipline so the comparison is fair.
- Verification: interactive behavior, theme/direction switching, keyboard/focus,
  narrow layouts, duplicate-submit behavior in the sample flow, and independent review.
- Integration: prototype must not change real API/auth behavior or silently send
  proposed operations to real endpoints. Document what subsequent implementation
  must connect. Palette selection remains Rand's choice after inspection.
- Result: ten independent prototype tests, strict component TypeScript, seven
  request/fixture checks, independent review, and desktop/mobile browser inspection
  passed. [Detailed evidence and integration limits](../testing/recovery-design-proposal.md).

### R2-T02 — Chosen direction

- September 30: Rand explicitly selected **A — blue accents** after approving the
  dark colors. Light mode remains available, with aesthetic refinement deferred.
- This closes the palette-choice dependency. It does not declare the whole
  proposed workflow implemented or security integrations verified.

### R3-T01 — Shared appearance foundation

- Scope: real UI theme tokens, consistent font/heading color, shared button/input
  states, and a remembered dark/light control; same wrapper in the fast preview.
- Dark is the initial default. Storage failure retains a working session preference;
  switching appearance preserves child state and unsaved forms.
- Risk checks: CSS cascade across route styles, hydration, blocked browser storage,
  cross-tab settings, and callback isolation. No authentication or API changes.
- Result: 10 new appearance tests; full UI suite 91/91, seven request/fixture checks,
  TypeScript, focused lint and production build pass. Independent review and browser
  checks completed; [evidence and remaining limits](../testing/recovery-shared-appearance.md).
- Next: R3-T02 replaces the phase navigation/frame and redundant headings. The old
  layouts and literal uppercase labels are not considered redesigned by this task.

### R3-T02 — One navigation and account frame

- Outcome: every authorized work area is discoverable in one sidebar, with exact
  active-page indication. Administration remains independent of document permissions.
- Simplified page headings and removed phase labels, step banners and duplicate
  organization tabs. Contextual case-question and audit links are preserved.
- Scope: page presentation, role-scoped links, stable child composition and appearance
  control placement. No transport, backend, token or mutation changes.
- Gate: full 111-test UI suite passed; then two additional composed-appearance tests
  passed within the 22-test navigation suite. Seven request/fixture checks, TypeScript,
  focused lint, production build and independent review passed. Mobile comparison
  forms and combined administration navigation checked at 390px without page overflow.
  [Integration evidence](../testing/recovery-shared-navigation.md).

### R3-T03 — Role guidance inside the workspace

- Scope: reopenable contextual instructions in the actual frame, with steps suited
  to the active role/page. Describe current capabilities without promising R4 features.
- Acceptance: help never unmounts a working form, close returns focus, navigation
  updates guidance, role/organization changes dismiss stale guidance, revoked access
  removes it. Callback/sign-in contain no private workspace guide.
- Independent test author and reviewer are separate from implementation. Validate
  guide behavior, browser layout and existing permission/session integration.
- Result: 13 new guide tests; full UI suite 126/126 across 12 files, seven request/
  fixture checks, focused lint, production build and independent review pass.
  Desktop/mobile guide layout and keyboard dismissal verified. No API or data
  contract changes. [Integration evidence](../testing/recovery-role-guidance.md).
- September 30 follow-up: expanded every guide with English/Arabic responsibilities,
  detailed tasks and limits, plus a responsive translated workflow diagram. Independent
  review and 20 guide tests pass; full suite 133/133, lint and production build pass.
  Arabic desktop/mobile and English/light checks pass with the case draft preserved.
  No backend or permission changes; extension evidence is in the same record.

### R4-T01 — Find and reopen work

- User outcome: find saved comparison cases and documents, reopen by URL, refresh
  without losing the selected record, and use browser Back/Forward between selections.
- Scope: real UI, tenant-scoped paginated case-summary query, preview adapter and
  bilingual guide updates. No migration or changes to mutation payloads/history.
- Acceptance: cases can be created, listed and reopened; document upload/selection
  updates its URL; invalid/missing links never silently open a different record.
  Late reads/writes cannot replace a newer selection, including after Back.
- Risks: provider reset boundaries, case ETags, selection request ownership, tenant
  filtering, list pagination, Arabic names and preview parity. Independent frontend
  regression author/reviewer and backend implementer; root reviews backend query.
- Gate passed: 150 UI tests, eight request/fixture checks, 118 API/security tests,
  eight application tests and one actual PostgreSQL query isolation/order/count test.
  Independent review, TypeScript/lint/build and desktop/mobile browser flow pass.
  [Evidence and limits](../testing/recovery-reopen-work.md). Next R4-T02.

### R4-T02 — Focused review and safe drafts

- Delivered progress, Needs review filter, keyboard focus navigation, actual page/
  quote evidence, saved-versus-draft labels and explicit completion guidance.
- Preserves sibling drafts and same-context stale-save drafts; blocks completion
  while edits remain; locks editing during writes; supports failed-load retry and
  visible recovery if another reviewer completes the session.
- Gate passed: 166 full-suite UI tests, final focused 18/18 (two added afterwards),
  eight request/fixture checks, lint, TypeScript, production build, independent
  review and desktop/390px browser verification. No backend contract changes.
- [Evidence and limits](../testing/recovery-focused-review.md). Next R4-T03.

### R4-T03 — Comparison/questions context and each-role verification

- Saved comparison runs and question results reopen by URL with their original
  values/evidence/index versions. Current case/composer selections are distinct.
- UI follows real Read/Review/Ask permissions; every document role and independent
  administrator flag tested. Saved question results remain actor-scoped, including
  in the corrected sample adapter. Role changes clear private workspace state.
- Gate passed: 200 UI tests, 118 API/security/contract tests, 11 request/fixture
  checks, lint, production build/TypeScript, independent review and role-by-role
  browser checks. Desktop/mobile and light/dark checks pass.
- [Evidence and boundaries](../testing/recovery-comparison-questions.md). Next
  R4-T04 structured case references/categories. Preview remains running without
  Docker; one necessary fixture restart reset temporary sample changes.

### R4-T04 — Structured references, categories and safe metadata

- Delivered server-assigned organization/year references, free-form bilingual names,
  categories, ETag-protected metadata editing and tenant-scoped server filters.
- Deterministic legacy backfill preserves UUIDs/titles/history; concurrent allocation
  and immutable references are database-enforced. Title text is excluded from audit.
- Gate passed: 217 UI, 13 request/fixture, 134 API/security, one audit-operation,
  eight application and seven actual PostgreSQL tests. No backend skips. Independent
  review, lint, production build/TypeScript and role/mobile/theme browser checks pass.
- [Evidence and rollout boundaries](../testing/recovery-case-metadata.md).
  Preview 3410 remains running after one fixture restart. Disposable PostgreSQL stopped;
  no retained database migration/deployment. Next R4-T05 repeat-action rules.

### R4-T05a — Comparison retry protection and deliberate revisions

- R4-T05 split into comparison and document/processing increments to retain per-task
  test/review gates. See the action-repeat contract for classified rules and remaining gaps.
- Durable creation intent, no-op pins/identical decisions, explicit replacement/rerun/
  revision controls, and stale/uncertain reload recovery shipped together with migration21.
- Gate passed: 237 UI, 15 request/fixture, 141 API/security/audit, 12 application and
  nine distinct real PostgreSQL checks. Concurrency recovery defects discovered by
  tests/review were fixed and affected checks rerun. Lint, build/TypeScript, independent
  reviews and all-role/mobile/theme browser checks pass.
- [Evidence](../testing/recovery-comparison-repeat-actions.md). Preview remains running;
  disposable PostgreSQL stopped. Next R4-T05b; no retained database deployment.

### R4-T05b — Document and question retry protection

- User outcome: retry an uncertain upload, processing request, or question without
  silently creating another operation; revise a saved review decision deliberately.
- Scope: real UI/API, migration22, immutable actor/organization-scoped upload and
  processing receipts, existing question identities, and exact review-action no-ops.
- Acceptance: rapid clicks submit once; exact retries return the original operation
  even after completion; changed inputs conflict; acknowledged operations allow a
  new intent; uncertain review writes require a read reload and preserve drafts.
- Integration risks: transaction/file compensation, current authorization, deleted
  or ineligible source tombstones, bounded byte hashing, ETags, atomic audit,
  restricted database grants, Arabic drafts, and late-response ownership.
- Gate passed: independent backend/frontend review, 97 UI checks, 16 request/fixture
  checks, 36 application checks, 124 API/security/audit/review checks, 11 distinct
  actual PostgreSQL checks, TypeScript and focused lint. Fixture corrections and
  product findings were resolved and affected checks rerun; the counts do not add
  overlapping reruns. PostgreSQL used a new isolated cluster and synthetic data.
- Migration22 is required for durable keyed processing/upload retries. Legacy
  callers without a key preserve their existing semantics. No retained application
  database was migrated. [Fresh evidence](../testing/simplification-20261002.md).

### October 2 — Documents simplification (complete)

- User outcome: see which documents need review, are being reviewed, or have a
  completed review; understand the next permitted action without opening every item.
- Planned scope: lightweight tenant-scoped work-list projection and a simpler
  Documents presentation. Keep extraction status separate and preserve the existing
  detailed document API for other consumers. No migration is planned for this step.
- Acceptance: summary selection matches the real review workspace; older completed
  reviews remain identifiable during reprocessing; list loading does not fetch each
  document's evidence or make individual review calls; list refresh preserves drafts.
- Risks: active older reviews, partial/failed replacement extraction, empty fields,
  role permissions, selected-record URLs, late responses, and preview parity.
- Gate passed: independent source review, three real PostgreSQL checks, 66 UI
  checks, 114 API permission/audit checks, 16 request/fixture checks, TypeScript and
  focused lint. Projection is one detached summary query; old review/current
  extraction and all role/administration combinations are verified. Initial test
  fixture and label expectations were corrected with narrow reruns.
- Comparison tolerances move into a Settings disclosure in this same visible
  simplification increment; defaults, validation, saved snapshots and deliberate
  comparison/retry rules remain unchanged.
- Comparison disclosure gate passed independent review, 60 distinct focused UI
  checks and lint. Native browser visibility/validation is reserved for the final
  settled journey check. [Evidence and limits](../testing/simplification-20261002.md).

### October 2 — Questions simplification (complete)

- User outcome: ask from named completed reviews or a comparison context, use
  reviewed answers by default, and reopen recent saved questions without copying IDs.
- Planned scope: exact document/review context lookup, bounded actor/organization
  saved-result summaries, Questions controls and preview parity. Preserve existing
  preparation permissions, evidence-search routes and immutable saved-source versions.
- Acceptance: sources outside the first list page can be linked exactly; recent
  result names are private to their actor/organization and honor source eligibility;
  refresh/Back and repeat-request identities keep their existing guarantees. Advanced
  IDs and search algorithms remain available through secondary controls.
- Gate passed: independent backend/UI review, four distinct real PostgreSQL checks,
  58 affected UI checks, 128 API logging/permission/audit checks, 17 request/fixture
  checks, TypeScript and focused lint. Exact older review lookup beyond the first
  source page, private bounded history, current source eligibility and supported
  saved-result reopening are verified. Pagination ownership, valid uppercase links
  and truthful source context findings were fixed and affected checks rerun.
- Native browser journey acceptance is reserved for settled source; the current
  browser connector has failed to bind a tab even with no heavy test job active.
  This is an explicit unverified environment limit, not a browser pass.

### October 2 — Feature organization (implemented; browser acceptance pending)

- User outcome: trace the ordinary journeys through smaller feature modules and
  read normal backend registration without optional model-pilot orchestration.
- Planned scope: Questions workflow hook, document processing/intent helpers, and
  explicit optional-pilot registration at existing standalone pilot call sites.
  Keep page rendering, current prepared semantic retrieval, schema/history and
  unresolved spending holds; pilot settings stay disabled.
- Acceptance: normal public handler graph resolves without pilot coordinators;
  existing standalone pilot checks opt in explicitly; request ownership, role
  cleanup, draft recovery and stored-source version behavior remain unchanged.
- Risks: DI resolution, private Python client registration, default-deny recovery,
  feature hook lifetimes, existing history and docs entry paths. Architecture and
  relevant broader backend regressions, full UI checks/build, docs build and final
  browser journey checks follow settled source. Unchanged Python OCR is outside
  this refactor; real identity and cloud deployment retain their separate gates.
- Source gate passed: independent workflow, default graph and documentation
  reviews; core public handlers resolve without optional pilot coordinators, and
  explicit pilot registration retains disabled processing and denied recovery.
  Final automated regression/build receipts are recorded in the
  [run evidence](../testing/simplification-20261002.md). Native browser acceptance
  remains unverified because the final connector inventory has no enabled surface.
- Fresh broad regression: 1,001 non-persistence backend checks, 38 selected actual
  PostgreSQL checks, 277 distinct UI checks and 17 request/preview checks pass.
  The Release solution and both changed standalone pilot tools compile with zero
  warnings/errors. Stale schema inventory, async test timing, Viewer presentation
  expectations and local pilot build assets were corrected without weakening
  production rules or timeouts. Full TypeScript, zero-warning ESLint and the Locked
  production Next build pass; exact pre-build generated configuration bytes were
  restored. The VitePress build passes with dead-link validation enabled after
  unpublished repository-source links were converted to verified code paths.
  Native browser acceptance remains pending; it is separate from this implementation.

### October 3 — Bounded browser follow-up

Rand requested simple actions instead of a full demo. The available in-app browser
passed sampled preview role gates, exact completed-review Questions handoff, named
saved-result refresh/reopen, native hidden-Settings validation, one Back navigation
and a light phone-width Arabic layout. Account/theme/viewport were restored.
[Evidence and screenshots](../testing/simplification-20261002.md#october-3-short-browser-acceptance-check-passed-for-sampled-actions)
supersede the connector-unavailable boundary for these sampled checks. Full R5/R6
and real identity/OCR acceptance are not closed by synthetic preview actions.

### October 5 — R5-T01 access recovery increment (local gate passed)

- Outcome: access checks cannot restore older permissions after a newer response;
  stalled session reads expose safe recovery; pending membership saves keep their
  original editor/target. Real UI changes, with no API or migration change.
- Acceptance: reverse-order session responses cannot roll back grants; stale
  failures cannot discard a newer session; initial session reads have a deadline
  and clear private state before retry; membership controls and duplicate submits
  remain locked until the owned save finishes.
- Dependencies/risks: secure transport invalidation, focus/timer/manual rechecks,
  sign-out/unmount cleanup, Strict Mode, session identity reset, membership ETags,
  callback exclusion and synthetic preview parity. Independent review and focused
  regressions precede completion; short browser checks remain separate from live
  identity proof. Existing broad October 2 gates are not rerun by default.
- Source review confirms MSAL's installed redirect bridge preserves its original
  request URL. No additional return-path storage/helper is needed for that journey.
  Explicit `/signin` retains the existing role-based home fallback; live proof
  remains unverified.
- Final local gate: independent source review, 59 distinct focused tests, full
  nonincremental TypeScript and affected-file zero-warning lint pass. No broad
  build/demo or database run. [Evidence and remaining boundaries](../testing/recovery-access-20261005.md).
- The fresh browser connector failed to bind the preview tab. Checked local env/
  process identity settings are absent; external Entra configuration/test accounts
  remain unknown. Full R5-T01 and dependent R5/R6 acceptance remain pending.
