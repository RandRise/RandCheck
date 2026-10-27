# Reviewer experience and security integration recovery plan

Proposed September 26, 2026, following Rand's manual test of the local demo at
`127.0.0.1:3308`. This is a plan, not a record of implemented fixes. Visual direction
will be chosen after previews. Existing uncommitted work remains in place.

**Execution authorized September 26:** work is divided into the
[R0–R6 phases and task gates](../tasks/reviewer-experience-recovery.md). Every task
requires its own tests, independent review, and integration-impact record before
the next dependent implementation task begins. Visual choice remains pending previews.

## Outcome

A first-time reviewer with no technical training can open their work, check a
document against its source, correct a value, finish the review, compare related
documents, and understand the result without learning the project's development
phases. Administrators can find access management and audit history in the same
application. Light and dark modes work throughout the product.

Treat this as a product integration release spanning the existing phases. Keep
phase numbers in engineering documentation, not in the application interface.

## What the repository confirms

This assessment is based on current source and documentation plus the reported
demo address. It is not a fresh browser walkthrough or a new security test result.

| Finding | Evidence | Implication |
| --- | --- | --- |
| Navigation follows implementation phases. | `apps/web/app/page.tsx`, `reconciliation/page.tsx`, and `questions/page.tsx` display phase navigation. | Users must understand the project structure to finish their work. |
| Screens expose engineering concepts and promotional headings. | Examples include “selected corpus,” “pin completed reviews,” “immutable run,” field keys, and large decorative introductions. | Essential actions compete with terminology and decoration. |
| Styling has one palette with extensive hardcoded colors and uppercase transformations. | `apps/web/app/styles.css`, `questions/questions.css`, and `security/security.css`. | A reliable dark mode needs shared theme tokens across all screens. |
| Login and administration exist but are unavailable in the tested demo. | `tools/demo/compose.yml` sets both frontend and API to Development; `enterprise-provider.tsx` skips sign-in and sets `canManageMembers: false`. | The demo does not represent the full product. |
| Hiding the administration link is not the only barrier. | `EnterpriseSecurity.cs` explicitly denies Development access to membership, audit, protected sources, and retention permissions. | Exposing a link or changing a frontend flag cannot safely integrate security. |
| Enterprise screens and APIs already exist. | `/signin`, `/organization`, `/organization/audit`, `/organization/retention`, shared identity provider, and API security endpoints. | Reuse and integrate these capabilities rather than assume they are absent. |
| Live identity integration remains unverified. | [Browser access contract](phase-6-browser-access.md) and [Phase 6 status](../phases/phase-6-enterprise-security.md). | Local security acceptance must be distinguished from a working real-account walkthrough. |
| Comparison work lacks a reopen flow in the current page. | `reconciliation/page.tsx` stores the case in React state and does not initialize it from a route or case list. | Navigating away or refreshing loses the visible case context even though backend records may persist. |

## Proposed application structure

Use one consistent application frame with **My work** as the default destination.
It lists documents and comparison cases with search, understandable statuses, and
a clear next action. **Administration** appears for authorized administrators.
The account menu contains identity, organization, appearance, and sign-out.

Opening an item keeps its context visible. Document review, comparison results,
questions, source evidence, and activity are contextual sections or panels within
that work item. They are not separate phase destinations that require reselection.
Do not force invoice-only work into a three-document comparison case.

Suggested reviewer journey:

1. Open **My work**, showing **Needs review**, **In progress**, and **Completed**.
2. Choose **Review document**. Operators also see **Add documents**; reviewers
   are not given upload permission merely to simplify the screen.
3. See the source beside extracted details. Start with fields needing attention,
   with a visible count and access to all remaining fields.
4. Use **Correct**, **Keep this value**, or **Remove value**, with clear save feedback.
5. Select **Finish review** when required decisions are complete, then **Next document**.
6. If related documents exist, choose **Compare documents** in the same context.
   Missing or unfinished documents offer a direct route to the necessary review.
7. Open a difference or ask a question without leaving the selected document/case.
8. Reopen saved work after refresh or navigation at a stable URL.

The interface should always answer: What am I reviewing? What needs attention?
What should I do next? Was my action saved?

## Additional requirements from manual review — September 26

Rand requested role-specific guidance, consistent headings, improved buttons,
a review of repeatable actions, and help categorizing reconciliation cases.
Guidance and styling are requirements. The action policies and case reference
format below are proposed behavior to validate, not claims that those fixes exist.

### Guidance within each role's workspace

- Add a short **Getting started** panel inside the user's own workspace, with
  a persistent **Help with this page** control. Open help beside the current task
  without losing selections or unsaved work; do not require a separate site/window.
- Explain what the person can do, the next steps, what successful completion looks
  like, and how to recover from common errors. Use plain language and examples
  matching the actual buttons rather than development terminology.
- Make the introduction dismissible and reopenable. Remember dismissal per user
  and guide version. Do not show a blocking tutorial on every visit.
- Derive relevant guidance from current permissions, including combined roles and
  administrator-only accounts. Refresh it when permissions change. Guidance does
  not grant permissions or introduce an unsupported auditor role.

| Access | Guidance in that workspace |
| --- | --- |
| Viewer | Find documents, inspect evidence, open saved results, ask questions using already prepared sources, and request help for actions requiring another role. |
| Reviewer | Start/resume a review, check evidence, correct or confirm values, finish, compare documents, and record or revise a finding decision. |
| Operator | Reviewer guidance plus upload, processing status, cancellation, and when reprocessing is appropriate. |
| Administrator | Manage memberships and permissions, inspect audit records, use retention/holds, and understand that administration and document access are separate. |
| No access or revoked access | Explain the state and how to contact the administrator or use another account. |

**Acceptance:** a new user of each supported role can follow the guide to complete
one permitted task without verbal coaching. Help remains accessible after dismissal
and does not advertise unavailable actions as if the user can perform them.

### Consistent typography and restrained color

- Use one familiar sans-serif font stack for interface text and headings, starting
  with system fonts such as Segoe UI/Arial with suitable Arabic fallback. Avoid an
  additional font download for this PC unless previews demonstrate a clear need.
- Use one heading text color within each theme. Establish hierarchy through size,
  weight, and spacing, not alternating fonts, colored words, serif italics, or
  decorative uppercase headings. The light/dark foreground colors may differ to
  maintain contrast. Source document typography is not restyled.
- Keep a restrained shared accent for actions; reserve status/danger colors for
  meaningful feedback with labels/icons. This does not make every status the same
  color. Show the same typography discipline in both visual-direction previews.

### Professional button styling

Reference research uses official business-product design systems, rather than
marketing landing pages. [IBM Carbon's button guidance](https://carbondesignsystem.com/components/button/usage/)
describes a clear primary action with lower-emphasis alternatives and consistent
variants; its [style reference](https://carbondesignsystem.com/components/button/style/)
provides sizing and state examples. [Atlassian's typography system](https://atlassian.design/foundations/typography)
uses defined text styles and typography tokens. These inform our design; they do
not mandate installing either library or copying its brand appearance.

- Preview one shared button family with comfortable padding, consistent heights,
  modest rounded corners, aligned labels/icons, and restrained borders. Start the
  preview with approximately 40px desktop controls and larger touch targets where
  needed; choose final dimensions after testing the actual screens.
- Give the active task one prominent primary action. Use quieter secondary/text
  actions for alternatives and a distinct danger treatment where appropriate.
  Repeated row actions should not all compete as brightly filled primary buttons.
- Include hover, keyboard focus, pressed, loading, disabled, and success/error
  feedback in the preview. Keep loading buttons stable in width, block duplicate
  submission while pending, and explain unmet prerequisites beside disabled actions.
- Use short verb labels and icons only when they clarify the action. Avoid unlabeled
  icons for essential tasks, gradients, and excessive shadows or animation.

**Acceptance:** headings and buttons follow the same rules across reviewer,
administrator, sign-in, and error screens in both themes. Keyboard focus is visible,
loading cannot trigger duplicate submissions, and the primary action is easy to find.

### Review repeated actions before restricting them

Rand's reported repeated action is not yet identified. Do not record it as a
reproduced defect or make every action single-use. Review both the real backend
and UI; synthetic preview behavior is not evidence of production behavior.

Initial source findings:

- `ReviewHandlers.cs` returns the existing active review on repeated start and
  rejects a new review for an already completed extraction. `ReviewSession.cs`
  permits completion only while active, and permits further field actions while
  active. Existing concurrency checks protect stale writes, not all repeated intent.
- `ReconciliationCase.cs` blocks concurrent running evaluations but supports a new
  run after completion. `ReconciliationFinding.cs` appends subsequent decisions.
  The current reconciliation UI re-enables these actions once a request finishes.
- `CreateReconciliationCaseHandler` creates a new ID for each accepted creation;
  the case constructor validates a required name up to 200 characters. That path
  does not itself deduplicate creation requests or impose a naming pattern.

Audit every mutating control, its permitted state, repeat behavior, error recovery,
and API enforcement. The proposed policy is:

| Action | Proposed repeat behavior |
| --- | --- |
| Start review | Resume the existing active review; never create a second one for the same intended start. |
| Finish review | One successful transition per review revision; show Completed afterward. A later correction needs a supported explicit revision flow, not silent unlocking. |
| Confirm/correct/clear a field | Block an identical repeat or unchanged save; allow a deliberate revised decision while the review remains editable, preserving history. |
| Create a case / submit an upload | One result per submission intent; retries must not create another case/job. Do not permanently ban another case with the same title or assume identical filenames are duplicate documents. |
| Link a reviewed document | One current link per case role; selecting the same document/revision again has no new effect. Replacement must be explicit and preserve previous run snapshots. |
| Run comparison | Prevent overlapping runs and accidental reruns with unchanged inputs. Show the saved result by default; permit a deliberate rerun for changed sources, rules, or tolerances, and define any exceptional rerun policy. |
| Resolve/accept/dismiss a finding | Show the current decision after saving. Use an explicit Change decision action with a reason for revisions; prevent identical repeated submissions. |
| Reprocess / cancel processing | Reprocess only when eligible and with no conflicting active job; cancellation has one state transition per job. Retrying a failed operation remains possible. |
| Membership, retention and holds | Disable unchanged saves; prevent duplicate active grants/holds with the same scope and intent. Revocation/release changes a particular record once, with an explicit later reactivation/new hold where allowed. |
| Search, view, refresh, download | Remain repeatable. For questions that create saved results, distinguish retry of the same request from an intentional new question/run. |

Use both immediate UI pending guards and backend state/concurrency checks. Where
creation or retry needs duplicate protection, define a stable request key scoped
to the authenticated organization/actor and operation, with a payload fingerprint,
atomic result recording, and a documented lifetime. The same key/payload should
recover the original result; different payloads must not silently reuse it. Check
existing request-key support before adding a second mechanism. Do not automatically
retry an uncertain mutation unless its contract supports safe recovery.

**Acceptance:** test rapid double-click, repeated submit after success, refresh/back,
two tabs/users, stale versions, and timeout followed by retry. There is one intended
business effect, while necessary audit attempt records remain intact. Legitimate
changes and recovery paths still work. Keep completed history immutable.

### Reconciliation case references and categories

Recommended proposal: separate a stable case reference, readable title, and
structured categorization. Do not require users to type or memorize a complex
name pattern, or infer categories by parsing free-text names.

- Generate a reference such as **REC-2026-000123** on the server, unique within
  the organization and stable when the title/category changes. This format is a
  preview candidate. Define year/timezone and numbering scope before implementation;
  allocate safely under concurrent creation and allow gaps rather than renumbering.
- Keep the title editable and human-readable. Offer a suggestion such as
  **Sample Trading · Office supplies · September 2026** when those details are
  available; let the user accept or change it. Do not require supplier information
  before documents have been linked. Preserve Arabic and mixed-language titles.
- Start with an optional **Category** selection and an **Uncategorized** default.
  Propose a short category list in the preview. Reuse supplier/date/status metadata
  for filters where available rather than adding several compulsory form fields.
  Add department/project tags only if an actual workflow needs them.
- Search by reference/title and filter by category/status/date, with tenant-scoped
  server queries. Keep the existing UUID as the internal identity. Add a reviewed
  schema/API migration for references/categories; preserve existing titles and
  links and define safe reference backfill for existing cases.
- Keep category separate from review/comparison status. Renaming or recategorizing
  a case must not change its evidence or historic comparison results; track the
  metadata change through the supported audit contract.

**Acceptance:** users can create a case without knowing a pattern, find it by its
reference, and filter cases by category. Concurrent creation cannot allocate the
same reference within an organization. Renames and categorization preserve identity
and history. Existing cases remain discoverable after migration.

## Delivery order and acceptance

### 0. Make frequent testing practical on the existing PC

Added after Rand requested a way to test every modification without repeatedly
starting Docker and the full environment. This is the first implementation task,
before the redesign.

The current disposable demo snapshots the source, checks its hashes, and invokes
four image builds on `up`. Its web container contains copied source, not a live
mount of the working tree. Keep that reproducible acceptance environment, but
provide a separate daily development loop.

- Provide one launcher for a Docker-free UI preview, loading the actual application
  page components from the working tree and a lightweight synthetic-data adapter.
  Reuse appropriate parts of existing preview scripts rather than build a separate
  imitation of the interface. Dependencies are installed once, not on every launch.
- Automatically update the browser after supported UI edits. Restart only when
  configuration or dependency changes require it, with a clear explanation.
- Supply realistic document, review, comparison, question, error, and empty states.
  Simulated sign-in, role, and audit screens must be explicitly labeled as previews
  and isolated from real authentication and production builds.
- Persist synthetic changes across browser refresh where needed to test the user
  journey; provide an explicit reset and document whether restart preserves them.
  Never send preview actions to the real API or modify retained project data.
- Give the user one stable local URL, automatic browser opening, readiness/errors,
  and a simple stop action. Check occupied ports and clean up only owned processes.
- Keep services running during a testing session. For integration checks, provide
  a separate launcher that reuses existing services/data and rebuilds only what
  changed. Start extraction/OCR only when the scenario needs it; verify any worker
  or service configuration required for a reduced integration environment.
- Measure first and subsequent startup time and memory use on this PC. Do not
  promise a fixed startup time before measuring component compilation and fixtures.

| Testing mode | Intended use | What it proves |
| --- | --- | --- |
| Fast UI preview, no Docker | Layout, colors, themes, navigation, wording, and simulated workflows after each edit | Actual interface behavior against synthetic responses; not extraction accuracy, database durability, or security enforcement |
| Real integration environment, reused between edits | API changes, persisted reviews, comparisons, real login/authorization, and audit records | Behavior of the real services enabled for that scenario |
| Full disposable acceptance environment | Milestone verification including upload and OCR | Reproducible integrated behavior for the tested build |

**Exit:** Rand starts the preview with one action, tests a UI edit without Docker
or rebuilding images, and sees the change at the same URL. The launcher reports
unsupported scenarios honestly. Real API and security changes receive integration
checks even when their screen design was first reviewed in preview mode.

**Implementation note, September 26:** the [preview launcher](../ui-preview.md)
uses Vite and the existing application components, with a navigation shim and
injected fictional identity. This avoids the Next.js server's overhead for daily
UI checks. Next routing/server rendering and real backend integration still require
their own checks. No production authentication code was changed. A separate reduced
real-integration launcher remains future work; the existing full demo is retained.

### 1. Establish the actual baseline and integration gaps

- Walk through the current demo from a clean browser session and record each
  dead end, unnecessary transition, misleading status, and missing recovery path.
- Map each proposed user action to an existing API and permission. Check list,
  filtering, case reopening, source preview, and activity needs; add missing
  contracts explicitly instead of assuming this is a CSS-only change.
- Include the repeated-action inventory above, separating confirmed protections,
  reproduced duplicate effects, and proposed changes to legitimate repeat behavior.
- Record the exact source/build used by the demo so later testing cannot silently
  evaluate an older disposable copy.
- Maintain separate status for backend implementation, UI availability, and
  end-to-end verification. Preserve the historical Phase 6 evidence with its limits.

**Exit:** a reproducible issue list with priorities and a capability/route matrix.
Loss of saved work, authorization errors, and actions falsely reporting success
take priority over visual polish if reproduced.

### 2. Preview the simplified workflow and choose the visual direction

- Produce two representative visual directions: neutral surfaces with a restrained
  blue accent, and softer neutrals with minimal accent color. These are candidates,
  not a chosen palette.
- Show each direction in light and dark mode using the same realistic document,
  review fields, warning, source panel, and navigation layout for fair comparison.
- Include the work list, active review, and account/administration entry. Show how
  comparison and questions fit into the same context in a clickable walkthrough.
- Use sentence case, readable body text, clear focus states, compact page titles,
  and short action labels. Remove decorative uppercase headers and redundant
  numbered section labels. Preserve meaningful acronyms such as VAT and AED.
- Keep technical details under a secondary details control when they are useful.
- Preview the role guidance panel, unified heading/button styles, and the proposed
  case reference/title/category form before choosing the final design.

**Exit:** Rand can inspect both themes and choose a direction after seeing the
previews. The click path is understandable before applying styling across the app.

### 3. Implement the shared frame, themes, and reusable controls

- Create shared tokens for background, surface, text, border, accent, focus, and
  success/warning/error states; migrate all current page-specific palettes.
- Offer **Light**, **Dark**, and **Use device setting**. Honor the device setting
  initially and persist an explicit choice through reload and navigation.
- Apply the theme before the initial visible render and cover sign-in, access
  errors, administration, dialogs, inputs, tables, and evidence panels.
- Keep original document colors intact in previews; adapt the surrounding viewer.
- Replace duplicate navigation, oversized hero sections, and phase labels with the
  shared frame. Use semantic status labels with icons/text as well as color.
- Apply the single-font/single-heading-color rules and shared button states above.

**Exit:** both themes cover all routes without unreadable controls or a wrong-theme
flash. Keyboard navigation and 200% zoom remain usable. Check normal text contrast
at 4.5:1 and large text/essential control boundaries at 3:1 as project targets.

### 4. Complete one continuous reviewer workflow

- Build the work list and stable document/case routes. Restore saved selections
  from authorized server data; keep return navigation predictable.
- Add in-workspace role guidance and the reviewed case reference/category contract.
  Implement the approved action-state rules in the UI and real API together.
- Extract the current large pages into shared workflow components while preserving
  existing review, concurrency, evidence, and permission behavior.
- Present the current reviewed value clearly; keep original extracted values and
  correction history accessible. Do not hide unresolved fields or silently confirm
  them to shorten the workflow.
- Open the relevant source page beside the selected field. Reuse protected source
  access; scope any new preview endpoints and verify access before rendering.
- Replace raw field keys and value-type choices in ordinary forms with labeled
  field choices and appropriate inputs; retain advanced fields only where needed.
- Preserve explicit start/finish review semantics where required by the backend,
  while making them the obvious next action rather than extra navigation.
- Carry the selected documents into comparison and questions. Prepare eligible
  reviewed sources through authorized operations and keep the exact review version
  used visible in details. A changed review must never silently alter an old result.
- Move tolerance configuration into advanced comparison settings with clear defaults.
- Add clear pending, saved, failed, stale-data, empty, and retry states. Preserve
  unsaved edits through panel changes; warn before leaving if they would be lost.
- Keep Arabic and mixed-language rendering intact. Document Arabic content support
  separately from full Arabic interface translation, which is not assumed here.

**Exit:** an invoice can be reviewed without leaving its workspace; a comparison can
be completed without repeatedly selecting the same sources; saved work reopens on
refresh; conflicting changes cannot silently overwrite another reviewer's work.

### 5. Make authentication, permissions, and audit part of the product

Begin configuration discovery during step 1 so this does not become a late blocker.

- For the authenticated application, start signed-out users at sign-in and return
  them to their intended authorized work after login. Clearly handle no membership,
  insufficient permission, expired sessions, revoked access, and sign-out.
- Surface **Members and access**, **Audit history**, and **Retention** under
  Administration according to the existing independent administration grant.
- Give reviewers authorized document activity in context. Organization security
  events remain restricted; any new document-scoped activity API needs its own
  permission and tenant checks rather than broadening the existing audit endpoint.
- Show understandable action, person, time, outcome, and change summaries in audit
  history. Keep technical IDs in details. Add useful filters only with supporting
  server queries; filtering one loaded page must not masquerade as a full search.
- Preserve append-only records and distinguish attempted actions from successful
  business changes. State the supported event scope; Microsoft identity-provider
  sign-in logs are not automatically part of the application's audit history.
- Retain an explicitly labeled account-free workflow demo. Explain that security
  features require the authenticated environment, with a clear supported entry path.
- Provide a documented authenticated walkthrough using the actual application,
  API, database, and identity configuration. UI fixtures may support preview/testing
  but do not count as successful real authentication.
- Verify web/API configuration alignment, tenant/application registrations, redirect
  configuration, initial administrator provisioning, and test-role memberships.
  A configured Entra tenant and test accounts are external prerequisites. Track them
  explicitly if unavailable; do not mark real sign-in verified from mocked tests.

**Exit:** a real test account signs in, reaches the correct role-specific workspace,
performs an authorized action, and an administrator finds its expected audit record.
Denied actions and role revocation are verified at the API as well as in the UI.
Development mode remains restricted; it is not turned into an administration bypass.

### 6. Accept the integrated product through real user tasks

| Scenario | Required result |
| --- | --- |
| First-time reviewer | Completes review of a prepared invoice without spoken guidance; understands the next action and save state. |
| Operator uploads a document | Sees progress, can recover from supported failures, and reaches the review without hunting for it. |
| Correct and finish | Corrected value and reason survive reload; original extraction remains inspectable. |
| Resume work | A saved document or case reopens from the work list and a direct URL after refresh. |
| Compare three documents | Review prerequisites and differences are understandable; evidence opens in context; decisions persist. |
| Ask a question | Current sources carry through; supported answers and unavailable answers remain distinguishable. |
| Account access | Signed-out, Viewer, Reviewer, Operator, administrator-only, and revoked users see appropriate behavior. |
| Audit history | Authorized administrator can locate the action and inspect its outcome; unauthorized accounts are denied. |
| Light/dark appearance | All routes and states are checked in both modes, including reload and device preference changes. |
| Accessibility and language | Keyboard, focus, zoom, narrow screens, Arabic text, and mixed-direction values remain usable. |
| Role guidance | Each supported role can complete a permitted task with its own in-workspace instructions and reopen help. |
| Consistent controls | Headings use one font family and one color per theme; buttons share styles, hierarchy, and interaction states. |
| Repeated actions | Double-clicks, stale tabs, and retries cannot duplicate a business effect; deliberate revisions and eligible reruns remain possible. |
| Case categorization | Automatic references stay unique/stable, category filters work, and existing cases survive migration. |

Use targeted component tests for the new workflow/theme behavior, browser tests
against the actual local stack, and authorization/persistence tests for changed
API contracts. Keep existing regression checks appropriate to the touched areas.
Screenshots and test totals alone do not establish usability: conduct the first-time
reviewer exercise and fix observed blockers before calling this complete.

## Scope and completion record

Reuse working extraction, review history, comparison rules, and evidence validation.
This plan does not introduce paid model generation, payment approval, a new identity
provider, or a production deployment. New list/query/preview needs must be estimated
after the capability inventory; a schedule before that would be speculative.

Deliver the issue inventory, theme/workflow previews, implementation increments,
updated walkthrough, and acceptance evidence. Record unfinished external identity
verification explicitly. The lightweight UI preview launcher is implemented; the
next deliverables are the baseline walkthrough and visual previews, including the
additional requirements above. The final palette remains open until those previews
are seen. These plan additions do not claim that the product changes are implemented.
