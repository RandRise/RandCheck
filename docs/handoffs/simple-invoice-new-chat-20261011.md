# New chat message for the invoice project

> **October 12 update:** This is a historical handoff. Read the latest
> [continuation](../continuation.md) and [low-effort review plan](../design/low-effort-review-20261012.md)
> before using it. Rand approved simpler value checking and one pair flow with
> backend-managed checks. G01 changes value review first; backend scope removal and
> the combined workspace remain. Dashboard and role proposals are still unapproved.

Copy the message below into the new chat in the same project workspace.

---

We are continuing an existing project at:
`C:\Users\Rand-\Documents\Codex\2026-08-06\uae-document-intelligence`

Read `docs/continuation.md`, `docs/design/simple-invoice-workflow-20261011.md`, `docs/tasks/supplier-invoice-product.md` and `docs/testing/supplier-invoice-S3-20261006.md` first. Follow applicable AGENTS.md instructions. Inspect the current working tree before changing anything; there is extensive existing uncommitted work that must be preserved.

My latest direction, October 11, supersedes the earlier UI/role plan: comparison is still too complicated, I dislike the dashboard, and I want fewer unnecessary roles. Design for a first-time adult user with fifth-grade-level language. Plan and design step by step before the next implementation batch. Do not treat passing tests as proof of good usability.

The product is a supplier-invoice checking tool for purchasing/accounting staff. Its promise is: upload an invoice and purchase order, find discrepancies, inspect evidence, record a decision and export the reviewed result. Arabic/English text stays supported within verified limits; use English digits for numbers. October 12 update: new reviews use invoice and PO only; quotations are no longer an optional step. Existing quotation work and audit history must stay accessible. Other design proposals remain unapproved. Goods receipts and payment approval are outside the first version, so do not call it standard three-way matching. Customer demand and benefits remain assumptions; I am working alone and may not have users available yet.

Proposed experience: **Add documents → Check details → See results**, with decisions/export inside results. One main next action per stage. Generate names, save acknowledged progress, and keep exact review versions behind an explicit human comparison action. Hide technical IDs and revision-selection controls from normal use. Never silently confirm unchecked fields, adopt a newer review or represent missing coverage as a match. Make the invoice list the home screen; Questions, legacy tools and advanced settings are secondary. Prototype the dashboard and stages, including empty/error/incomplete states, before implementing them.

Proposed roles: **Owner and Staff**, with a simple solo-owner experience. Current backend roles are Viewer, Reviewer and Operator plus a separate administration flag. Signed-out/revoked/None are states, not extra roles. Review the actual permission matrix and existing-account migration before changing permissions; never silently upgrade restricted or admin-only accounts. Removing labels alone is not a completed role reduction.

Existing foundations to reuse:
- Next.js/React frontend, .NET backend and PostgreSQL persistence, Python extraction service.
- S1: bounded opt-in extraction profile, English-digit rules and source evidence.
- S2: opt-in `invoice-po-1.0` policy with frozen checks, warnings, scope and unavailable coverage; legacy `1.0` remains available.
- S3: `/invoices` saves draft invoice/PO selections before completed review; exact review pins remain separate, stable URLs reopen checks, retries/ETags protect writes, and old reports retain original sources. Do not rebuild these safeguards merely to simplify the UI.

Historical S3 verification on October 6 passed 324 distinct checks (25 domain, 59 application, 66 API, 20 actual PostgreSQL, 137 UI, 17 preview), static checks and independent review. Its browser check used actual components in a synthetic preview; it was not live identity, real upload/OCR or full production acceptance. These results are historical and my manual acceptance remains unreported. Retained migrations 23/24 and role provisioning were not applied. Extractor activation/basis gaps, broader Arabic/scanned accuracy, R5/R6 recovery/live identity, hashing/history delivery, report/export and deployment remain open. Read the receipts rather than assuming these gaps are solved.

Our working method for each meaningful step: agree the user-visible outcome/design, capture the current baseline, implement a small increment, show me the exact before/after code diff in VS Code, review code and design, fix findings, run focused bug/regression/integration checks, then give me a 1–3 minute manual check. Keep a review summary with file links, findings, fixes, tests and remaining risks. Distinguish independent review from self-review. Do not stop for every small edit or run a full demo/broad rebuild unnecessarily. Keep heavy jobs sequential and close only work-owned temporary resources when finished.

VS Code was opened on the project with `apps/web/app/invoices/page.tsx`. Reopen/reuse its window and show the relevant files/diffs as we work. Key review files also include `apps/web/app/security/enterprise-provider.tsx` and `apps/backend/src/UaeDocumentIntelligence.Api/Security/EnterprisePermissions.cs`. Nothing in the application or permissions was changed during the October 11 planning turn, and no fresh application tests were run then.

Start by confirming current repository state and reviewing the proposed dashboard and three-stage flow. The next deliverable is a small design/prototype for review, before dashboard implementation. Keep the existing project and tests intact.
