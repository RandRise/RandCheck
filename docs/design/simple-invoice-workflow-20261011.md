# Simple invoice workflow and delivery plan

> **October 20 update:** Rand approved the numbered navigation and simple Home.
> G02/G03 source slices are implemented; manual and live integration checks remain
> open. [Current plan](one-review-navigation-20261020.md) and
> [verification](../testing/navigation-flow-20261020.md) supersede earlier
> unimplemented navigation wording below. Visual redesign and export stay separate.


> **October 19 A02 approval:** Rand clarified that only Staff and Admin should
> remain, with no separate administration setting. This is implemented in source;
> existing accounts require a reviewed migration before deployment. No real data
> was migrated. [Current implementation and checks](../testing/two-roles-20261019.md).
> Owner and dashboard proposals below remain unapproved.

> **Earlier October 19 A01 decision (superseded by A02):** Rand approved one combined everyday document role,
> named Staff. Administration remains a separate grant. New-account setup now uses
> Staff with the existing Operator permissions; existing restricted accounts keep
> their limits unless an administrator explicitly grants Staff. No role migration
> or bulk promotion was performed. The Owner account type below remains a proposal.
> [Implementation and verification](../testing/staff-role-20261019.md).

> **October 12 approval update:** Rand approved the low-effort review direction
> discussed in the conversation. The [new bounded plan](low-effort-review-20261012.md)
> now governs that work: guided values, backend-managed checks and one pair flow.
> It supersedes the proposed routine full/partial scope question below. G01 is only
> the first value-checking batch. This older document remains proposal/history for
> the dashboard, roles and other unapproved changes; it is not blanket authorization.

October 11, 2026. Rand wants a comparison flow that a first-time user can understand, fewer everyday roles, a redesigned home screen, and visible code review at every step. This planning reset governs future UI and role work. Existing S1–S3 implementation and evidence remain useful foundations; passing tests do not establish that the interface is easy to use. No application or permission changes are made by this plan.

## Product experience

Use adult, plain language at roughly a fifth-grade reading level. The main journey has three stages: **Add documents → Check details → See results**. Decisions and download belong within results. Each screen has one main next action, a clear back action, and an honest saved/saving/failed indication.

1. **Add documents.** Two labelled areas: Invoice (what the supplier charged) and Purchase order (what you ordered). Allow upload or an existing document. Generate a useful check name; renaming is optional. Save through the existing durable workflow. Only say Saved after an acknowledged save; interrupted saves offer recovery without repeating unknown writes.
2. **Check details.** Keep the documents in one workspace. Put uncertain and missing values first, with their source beside them and all other values reachable. Do not confirm any value merely because it has high confidence or the user clicks Next. Ask “Does this invoice cover the whole order?” with Yes, No and I'm not sure; do not preselect Yes. After explicit review completion, **Compare these documents** should bind the reviewed revisions the user just inspected and evaluate them. Keep revision IDs and technical controls in details. If newer values appear or only part of the operation succeeds, explain what is saved and request a deliberate recovery action.
3. **See results.** Show Differences found, Could not check, or No differences within completed checks. Keep coverage gaps visible. Each difference shows the two values, signed difference and evidence. Offer the existing reasoned decision meanings in plain language. Download the reviewed result from the same frozen report and decision version shown on screen. A recorded decision is not payment approval or proof that a supplier changed an invoice.

October 12 scope update: new reviews use an invoice and purchase order only. Quotations are not an optional new-review step. Existing quotation documents, reviews, comparisons and audit history remain reachable. The dashboard, three-stage and role proposals below are still unapproved. Goods receipt and payment approval are out of scope; do not call this standard three-way matching.

## Home screen proposal

Start with a clean invoice list and a prominent **Check an invoice** button. Use restrained styling, readable spacing and clear text. No decorative statistics are needed for the first version. Each row shows supplier, invoice number, amount when known, and a next action such as Add order, Check details, or See results. Show extraction progress separately from review and difference status; Completed must never imply every value was verifiable.

Keep recent results in the same list. Put older document tools, legacy comparisons and Questions under More tools. Keep account administration in Settings. Avoid displaying case IDs, rule versions or internal revision terminology in the everyday path. Preserve Arabic text and direction; display supported numbers with English digits.

The first mockup must show populated, empty, loading, failed and incomplete states, keyboard focus, narrow-screen layout and one Arabic/English example. Evaluate it with a short task before implementing the new dashboard.

## Role proposal

Default to two everyday account types: **Owner** and **Staff**. Staff can upload, review, compare, decide and export within their organization. Owner can do that work and manage people and settings. A solo setup needs only the owner experience; inviting staff is secondary.

The current backend has Viewer, Reviewer and Operator document permissions, plus a separate administrator flag. None, signed out and revoked are access states, not additional business roles. The preview account selector also contains test combinations, which should not look like normal product setup.

For new authorized accounts, Staff corresponds to the existing operational capabilities and Owner additionally has administration. Existing Viewer/Reviewer/admin-only accounts cannot be silently relabelled into broader access. Before reducing stored roles, inventory memberships and service identities, define an explicit migration and rollback, and review allowed and denied actions. Preserve existing restricted accounts until their mapping is deliberately resolved. Hide test account controls from the everyday product experience while retaining them in test tooling. This plan does not grant permissions or claim that reducing labels alone removes backend roles.

## Source review findings

These are verified design findings from the current source, not new runtime bug claims:

- `apps/web/app/invoices/page.tsx:225` requires a check name before choosing documents. Make the generated name editable later.
- `apps/web/app/invoices/page.tsx:235` explains internal draft/revision distinctions in the main workflow. Preserve the distinction in storage and simplify the visible action.
- `apps/web/app/invoices/page.tsx:248` exposes raw review IDs; lines 254–255 require separate Choose and Use actions for each document. Replace that ceremony with an explicit comparison of the exact reviews just inspected, including stale-version and partial-save recovery.
- `apps/web/app/invoices/page.tsx:261` uses Invoice scope. Keep the business question, using plain language and an explicit unknown option.
- `apps/web/app/security/enterprise-provider.tsx:129` makes Documents, Compare documents and Questions peer destinations, while the new pair flow is not the primary navigation entry. Make the invoice list the starting point.
- `apps/backend/src/UaeDocumentIntelligence.Api/Security/EnterprisePermissions.cs:66` separates document access from administration. Review this boundary before changing role names or assignments; removing a UI control alone is not an authorization change.

## Delivery sequence and review gates

| Step | Concrete deliverable | Code and design review | Focused check |
| --- | --- | --- | --- |
| 1 Current flow audit | This plan, source findings and new-chat handoff | Inspect the current workflow and permission files in VS Code | Planning only; do not count old test results as fresh execution |
| 2 Screen design | One small clickable prototype of home and the three stages | Check wording, action hierarchy, missing/error states and Arabic layout | Rand locates the next action and interprets a difference without coaching |
| 3 Home and navigation | Implement the approved invoice list using existing data | Show the exact changed-file diff; preserve older work and links | Empty/list/error states, keyboard use, reopen and access boundaries |
| 4 Guided comparison | Replace manual draft/revision ceremony with the reviewed three-stage flow | Review exact-review binding, truthful saved state, unchecked data and recovery | Missing order, wrong document, stale review, refresh, double click and lost response; affected real API/database checks |
| 5 Role simplification | Two default account types plus an explicit existing-account migration decision | Show permission matrix and backend/UI diff before any persisted role migration | Allowed/denied actions, admin-only and existing restricted access, revocation and organization isolation |
| 6 Results and export | Clear differences, evidence, decisions and one CSV | Review frozen sources, coherent report/decision version and export safety | Known difference, unavailable check, older result, concurrent decision, UTF-8 and downloaded file contents |
| 7 Short integration | One representative supported pair through the complete path | Review unresolved findings and evidence limits | A short real integration/browser exercise plus targeted failure cases |

Steps 3–7 replace the previous future UI delivery sequence; they reuse S1–S3 foundations rather than restarting extraction or comparison. The first implementation increment is the approved home/list with working resume links. It is not yet the complete product.

For every implementation step: define the user outcome, capture the current baseline, make a bounded change, show its exact before/after diff in VS Code, perform independent code and design review, fix material findings, run the affected tests, and give Rand a short manual checkpoint. Report files changed, review findings, fixes, test evidence and remaining limitations. Distinguish a self-review from an independent review. Group small fixes within the step; do not stop for every edit. Keep compilers and heavy tests sequential and browser actions short.

## Preserved evidence and limits

S1 extraction and S2 comparison passed earlier local gates. S3 passed 324 distinct focused checks on October 6: domain 25, application 59, API 66, actual PostgreSQL 20, UI 137 and preview adapter 17, plus static checks and independent source review. The browser smoke used actual UI components with synthetic storage and comparison. These are historical receipts, not October 11 test runs or real-user acceptance. Rand's earlier manual results remain unreported.

Preserve the dirty working tree, existing tests, audit history, organization isolation, safe retries, original evidence and old comparisons. Retained migrations 23/24 and role provisioning remain pending unless separately verified. The new extractor is opt-in; explicit basis/tax-currency coverage and general Arabic/scanned extraction remain limited. R5/R6 live identity/recovery, original-file hashing/history delivery, full integration, report/export and deployment gaps remain open. Customer demand and benefits remain assumptions; use synthetic or permitted redacted documents and seek a short accountant/purchasing-staff task if someone becomes reachable. Solo usability checks are useful but do not establish market demand.

See [the existing product tasks](../tasks/supplier-invoice-product.md), [S3 evidence](../testing/supplier-invoice-S3-20261006.md) and [the new-chat message](../handoffs/simple-invoice-new-chat-20261011.md).
