# Supplier invoice checking product plan

October 11 update: the [simpler workflow and delivery plan](simple-invoice-workflow-20261011.md) governs future dashboard, flow and role work. The plan below preserves the earlier product foundations and evidence requirements; its previous instruction to retain three everyday role choices is superseded by a two-role proposal with an explicit permission-migration review. No permission change has been applied.

Planning baseline: October 6, 2026. The bounded S1 extraction, S2 comparison and S3 saved invoice/PO workflow batches pass their local automated gates; their manual results remain unreported. S3 also passes independent source review and a short synthetic-preview browser smoke. Inbox/review, report/export and final integration remain pending. The first release is a focused prototype for a person checking a supplier invoice against a purchase order. Customer access is optional during prototype development; customer demand and business benefits remain assumptions.

The promise is: **Upload an invoice and its purchase order, see the discrepancies, verify the evidence, and export the reviewed result.**

The complete journey is **Add invoice and PO → verify extracted values → inspect differences → record decisions → export**. Preserve existing source, tests, reviewed history, permissions and safe retries. The [implementation tasks](../tasks/supplier-invoice-product.md) define the sequence and completion gates.

## Product scope

The first release handles one invoice and one PO in the same currency, using a small set of documented English and Arabic/English PDF layouts. It compares the invoice against the complete PO. Partial invoicing, uncertain document scope, unsupported layouts and incompatible currencies need explicit attention or unavailable checks. They cannot receive a complete agreement result.

Arabic text remains in scope; numeric values in the first-release profile use English digits (`0` through `9`). Arabic-number inputs retain their original evidence and require explicit review or correction. Existing historical Arabic-number capability and tests stay intact, with no automatic conversion promised by this new profile.

October 12 scope update: quotation is excluded from new reviews; existing quotation work stays accessible. The new invoice and PO policy has no missing quotation warning. Preserve the existing quotation, PO and invoice comparison as a secondary legacy workflow, with its original versioned results. Goods receipt, delivery verification and payment approval are outside this release. Describe the new workflow as invoice and PO checking.

The prototype should answer four questions at every step: which invoice is open, what needs attention, which action comes next, and whether the action was saved. Customer-facing screens use business terms; technical identifiers remain available in details where needed.

## Reuse and changes

| Area | Reuse | Change for the first release |
| --- | --- | --- |
| Home | Durable document list, processing and review summaries, existing case references | Invoice inbox with one next action per item and access to existing work |
| Documents | Upload, processing, cancel and retry protection, source files, page evidence | Continuous invoice and PO selection, saved before either review is finished |
| Review | Confirm, correct, remove, unfinished drafts and completed review revisions | Uncertain and missing required values first; unchecked values stay unchecked |
| Comparison | Typed checks, immutable runs, reviewed snapshots, tolerances | Versioned pair policy, explicit coverage and compatible currency and unit checks |
| Decisions | Saved finding decisions, reasons, actor, time and append-only history | Plain labels, remaining decision count and a report that explains each decision |
| Export | Source download and review history infrastructure | A comparison CSV derived from one selected run and a captured decision version |
| Access | Backend role permissions, organization isolation, ETags and audit | Simple role descriptions and appropriate actions in the invoice workflow |
| Other work | Saved Questions, administration, advanced settings and older cases | Secondary navigation with existing work still reachable |

Implementation anchors are `apps/web/app/page.tsx`, `apps/web/app/reconciliation/page.tsx`, `apps/backend/src/UaeDocumentIntelligence.Application/Documents/DocumentWorkItem.cs`, `apps/backend/src/UaeDocumentIntelligence.Application/Reconciliation/ReconciliationHandlers.cs`, `apps/backend/src/UaeDocumentIntelligence.Domain/Model/ReconciliationRuleEngine.cs` and `apps/api/app/local_extraction.py`.

## Inbox and saved workflow

Show supplier, invoice number, amount and currency when available, plus a clear next action. Keep extraction progress separate from the user's review state. Suggested review states are **Add PO**, **Check values**, **Inspect findings** and **Decisions recorded**. Show **Processing** or **Processing failed** as processing information. The report carries a separate coverage label when some checks could not be completed.

An item can be reopened after refresh, Back navigation or leaving the application. Persist invoice and PO draft references before reviews are complete; these references are distinct from the exact completed review revisions used by a comparison. Reuse existing case storage where suitable and add only the necessary contract and persistence changes.

Allow upload or selection of an existing document according to current permissions. Offer a direct route to each required review. Changing a document or completing a newer review offers a deliberate new comparison; old reports retain their original sources and decisions. A stale or uncertain save offers reload and preserves the draft without replaying a write.

Documents that are not invoices and legacy comparison cases remain reachable through secondary existing-work navigation. Home labels must not hide or misclassify that work.

## Extraction and review

Prioritize uncertain values, absent business fields and values needed by comparison. Keep all other extracted values reachable. Prioritization never confirms a field: finishing review still requires explicit handling of unchecked extracted fields under the existing review contract.

Define a small required-field profile for the supported checks. Finishing extraction review does not establish that every required business field or invoice row exists. Report missing coverage separately, including absent line tables. Keep the original extracted value and source excerpt distinguishable from a corrected value.

Use English digits, a dot decimal separator and correctly grouped commas in the supported numeric profile. Define decimal precision explicitly. Preserve the source text alongside the normalized value. Arabic-number forms and ambiguous or malformed formats require review; permissive parsing must not turn them into a plausible checked amount. Apply the same policy to extracted and corrected values.

Arabic-only PO classification and ordinary invoice and PO table extraction need representative fixture proof. Current Arabic presentation and parsing are useful foundations; current line extraction evidence does not support a general Arabic line-checking claim. Manual additions also lack page evidence today. Any manual fallback needs saved page or region provenance and a clear reviewer-entered label. An unsupported layout remains an explicit limitation.

## Comparison correctness

Introduce a new versioned invoice and PO policy. Keep old rule versions readable and leave historical runs unchanged. A completed comparison describes the checks it performed and their coverage, rather than certifying that the invoice is correct.

The first release must resolve these existing hazards:

- Rule set 1.0 always expects a quotation, PO and invoice. Pair checking needs its own role requirements.
- Monetary subtraction currently continues after a currency mismatch. Suppress incompatible monetary checks and explain why they were unavailable.
- Missing line sets can produce no line findings. Record absent or incomplete line coverage explicitly.
- Duplicate line identities can become unique again when a third occurrence is encountered. Keep every ambiguous identity excluded from automatic matching.
- Exact normalized item codes or descriptions do not establish translated line equivalence. Unmatched or ambiguous lines remain visible for review.
- Quantity differences need compatible units. Price comparisons need compatible price bases. Missing compatibility information limits those checks.
- A partial invoice compared with a whole PO can yield a legitimate difference. Scope uncertainty requires attention; this release has no cumulative invoicing model.

Present each check as **Difference found**, **Could not check**, or **No difference within completed checks**. Describe incomplete coverage near the result, including zero findings. Preserve original values and show direction, amount or quantity, unit and tolerance where the comparison is valid. Do not sum overlapping quantity, price, tax, line-total and document-total findings into a savings or exposure figure.

## Findings and decisions

Each finding shows its subject, PO value, reviewed invoice value, difference, unit or currency, tolerance, and both source references. Open the cited page or source evidence directly. For a corrected value, show that the reviewer changed it and keep the original excerpt visible without implying that the excerpt contains the corrected value.

Map plain language to the existing finding decisions and preserve their meaning: resolved, accepted difference and dismissed finding each require a reason. A resolved decision records the reviewer's statement; it does not prove that a supplier changed an invoice. Revisions retain earlier decisions and actors.

**Decisions recorded** means every displayed finding has a saved decision. It does not imply full coverage, delivery confirmation or payment approval. Coverage gaps remain visible even after decisions are recorded. Avoid adding a separate approval system or reusing case closure as evidence that review is complete.

## First export

Ship one CSV export alongside the on-screen report. It contains report metadata, reviewed comparison values, findings, coverage gaps and recorded decisions. Use explicit row types so matched checks and unavailable checks remain distinguishable from findings. Include case reference, selected run, rule version, complete tolerance settings, source document and completed review revisions, generation time and the captured decision sequence or cutoff.

Build the report and export from the selected run's frozen document and field snapshots. Current case pins must never supply the sources for an older run. Decision revisions after export create a later report version. Export is read-only and does not start processing, evaluate rules or record a decision.

Capture a coherent server-read report version containing the selected run and its case and decision version. Bind the visible report and CSV to that same version. Separately fetching latest decisions or applying only a timestamp cutoff across split queries cannot establish consistency. On sign-out or account or access changes, clear private report state and pending download artifacts under the existing access recovery rules.

Preserve Arabic text in UTF-8, quote multiline evidence and reasons correctly, and protect spreadsheet readers from formula injection in supplied text. Check actual downloaded bytes and reopen the file. User usefulness of CSV remains an assumption; a formatted PDF and accounting-system formats can follow feedback.

## Roles and access

Keep the existing Operator, Reviewer and Viewer permissions and the separate administrator flag. Use short descriptions of what each role can do; no new role framework or permission expansion is needed. Administrators without document permissions cannot view invoice contents or export them.

Use current organization authorization, audit, version checks and request keys for every new endpoint or write. Preserve safe retry behavior for uploads, processing, draft pairing and decisions. Retained database migrations require a separate deployment step; disposable integration checks do not migrate a real installation.

Questions, retrieval controls and administration remain available as secondary destinations. Tolerances are inspectable from a report, with advanced controls away from the everyday review path.

## Solo validation

Start with legally usable synthetic fixtures and known expected results. Reuse frozen Phase 4 and Phase 7 samples where they fit, then add only missing cases. Record the supported layout, language, expected fields and rows, known differences, source pages and permitted review actions. The minimum matrix includes:

| Fixture | Expected outcome |
| --- | --- |
| Complete matching invoice and PO | No difference within completed checks; explicit coverage |
| Known price or quantity mismatch | Correct direction, unit and difference; cited evidence |
| Missing PO or required value | Clear next action or unavailable check; no complete agreement |
| Arabic or mixed-language uncertainty | Visible uncertainty, faithful correction and evidence |
| Currency mismatch or incompatible unit | Unavailable affected checks; no misleading subtraction |
| Duplicate lines or reordered lines | Ambiguity stays visible; valid matching survives reordering |
| Partial invoice or unsupported table | Scope or extraction limitation remains explicit |
| Reprocessed or replaced document | Old report still uses its original review snapshots |

Self-review tests correctness and flow, and remains separate from user usability and market validation. Keep a list of assumptions: who experiences the problem, whether checking is currently manual, whether evidence reduces investigation effort, and whether the export fits their handoff.

If one accountant, bookkeeper or purchasing employee becomes reachable, ask them to describe a recent check, then attempt one short task with a permitted redacted pair or synthetic example. Observe whether they find the difference, understand its evidence and decision, and can use the exported result. Note confusion, correction effort and their current alternative. Feedback is optional for prototype completion; customer benefit and demand remain unvalidated until real evidence supports them.

## Future options

Structured UAE e-invoice imports, duplicate invoice detection, cumulative invoicing and accounting-system integrations are later opportunities. The current PDF workflow is not an e-invoicing compliance service. Company revenue thresholds alone do not identify a suitable customer; existing systems and manual exception work need investigation. No accredited provider integration, goods receipt, payment automation or enterprise rollout belongs in this prototype release.

## Completion and existing limits

The smallest complete release includes the saved pair workflow, accurate supported checks, explicit coverage, evidence-backed decisions and a real reopened export. A renamed home screen or a standalone pair endpoint does not complete it. Supported Arabic layouts must pass the same evidence and coverage conditions; otherwise the release remains limited and the bilingual promise stays open.

Use focused domain, application, API, UI and disposable persistence checks according to each task's impact, then a short integrated acceptance pass. Preserve existing tests; broaden verification only when shared contracts or failures justify it. Synthetic preview results cannot establish real backend integration or live identity acceptance.

Rand will manually test at meaningful milestones. The latest October 6 instruction supersedes stopping after every simple change. Complete related changes, automated verification and independent review in a bounded batch, then provide a short usable check before advancing to the next substantial stage. Internal fixes and documentation edits do not each require a stop. Documentation review alone cannot establish product behavior.

The [recovery ledger](../tasks/reviewer-experience-recovery.md) retains R5 and R6 status. [October 5 evidence](../testing/recovery-access-20261005.md) passes the local access gate; fresh browser and live identity acceptance remain open. [October 3 samples](../testing/simplification-20261002.md) retain their original bounded scope. The interrupted October 6 browser attempt provides no new acceptance pass. Representative Arabic and mixed table accuracy, actual history-file delivery, and deployment readiness remain separate gaps; this plan does not close them.
