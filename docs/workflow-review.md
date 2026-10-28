---
title: Workflow review room
description: Understand the RandCheck invoice workflow, compare proposed changes, and review one change at a time.
pageClass: workflow-review
outline: [2, 2]
lastUpdated: false
---

<div class="review-hero">
  <p class="review-kicker">PROJECT REVIEW · EDITION 35 · OCTOBER 27, 2026</p>
  <h1>From an invoice<br>to a clear answer.</h1>
  <p class="review-lead">What does the supplier charge? What did we order? What can we safely check? Let’s walk through the work, then study how to make it easier.</p>
  <div class="review-badges"><span>Current work explained</span><span>Proposals clearly marked</span><span>Test results recorded</span></div>
  <a class="review-start" href="#the-two-documents">Start the walkthrough ↓</a>
</div>

::: info What this page is
This guide shows how the product works now. The main flow stays on one page:
**Add invoice + PO → Check details → See results**. You can open extra details
when you need them, then return to the same check.

The system uses the exact reviews you saved. It shows what passed, what differed
and what it could not check. A decision explains what you will do; it never turns
a missing check into a pass. You can download a readable report or spreadsheet.

The library shows 10 files at a time. Search by filename, supplier or the number
printed on the document. Older quotations and their history remain available.
Use **Appearance** to switch between Dark and Light.

The agent has checked fresh fictional files through the real local system, plus
bounded Arabic and Admin scenarios. These checks do not prove every supplier
layout or real Microsoft sign-in. Pictures say whether they show a sample preview
or the real local system. Future ideas stay labelled as proposals; Owner is not
an approved role. [Read the current evidence and limits](testing/local-delivery-acceptance-20261027.md).
:::

## What we checked with fresh files

We uploaded a new made-up invoice and its purchase order. We checked each value
against its printed source, then asked the system to compare the saved reviews.
It found **6 passed checks, 0 differences and 5 checks it could not finish**.
The totals agreed, but missing item details meant it could not claim a full match.

We recorded that we had asked for the missing information. The counts stayed the
same. Both downloads kept that result and decision. Processing the invoice again
did not rewrite the old review or comparison. Retrying a lost reply did not create
a second processing request. A broken PDF showed an error and could not become a
finished review.

![Actual local results for freshly uploaded fictional invoice and purchase order](/workflow-review/fresh-pair-results-20261027.png)

This is an actual local-system screenshot using fictional documents. The agent
performed these checks; this does not claim accuracy on every supplier layout or
acceptance by you. [Read the dated verification](testing/local-journey-20261027.md)
or [run the disposable local workflow](local-demo.md). Final independent review cleared this batch. The three-step workflow itself is unchanged.

## What the language and Admin checks tell us

A scanned English sample kept all nine expected header values. Two versions of
one mixed Arabic/English sample kept eight of nine. One small Arabic mark was
missed. We corrected it on screen and checked that the old text stayed in the
evidence. Other unchecked values stayed unchecked. Arabic reads right to left;
numbers still use English digits in both themes.

These files still need review. Their document type was not clear enough for the
system to choose safely. Passing a few sample checks does not mean it can read
every supplier's invoice perfectly.

We also checked Admin changes against the real local database using temporary
test accounts. The last Admin cannot remove their own Admin access. Conflicting
edits ask for a refresh. Removing access blocks the next request. A lost save
reply does not cause the change to be repeated. Activity history keeps the record.
The temporary accounts and database are separate from your documents.

[Read the checks and their limits](testing/local-language-security-20261027.md).
Real Microsoft sign-in remains a separate production check. The invoice workflow
and its diagrams have not changed in this verification batch.

## Know where you are

The sidebar now uses small icons beside the same text labels. In an invoice check,
the numbered phases show your progress:

- **Purple — Current step:** this is where you are working now.
- **Green with a check — Files saved / Reviews saved:** that setup is saved.
- **Grey — Next step:** work ahead. Existing results may instead say **Saved results available**.

Opening an earlier step makes it current again. Green does not mean the invoice
matches the order. Read **Differences found**, **Checks incomplete**, or
**All required checks passed** in the results. Merely reading results does not
complete your decisions or change the checks.

![Illustration: saved files, current value review and upcoming results](/workflow-review/phase-progress-20261026.svg)

The local test identity is **Staff**, so it does not show administration links.
An authorized **Admin** now sees one **Administration** entry. It opens People &
access, with Activity history and Document storage as section links. The People
layout is accepted in the sample review. Activity history and Document storage are now built and agent-checked. Local Admin browser/API checks now pass with an offline test identity. Real Microsoft sign-in remains unverified.

**Which download?** Review history still has its technical JSON record. In **See results**,
choose a **Readable report (HTML)** or **Spreadsheet (CSV)** and select **Download
results**. These files use the same captured comparison and decision version, with
a useful case/comparison filename. Open HTML in your browser to read or print it.

For the final test with new files, use the [scenario checklist](/testing/final-user-acceptance).
It separates ready checks from export, real-account and processing prerequisites.
The [N13 evidence](/testing/navigation-progress-20261026) records tests and remaining limits.

The [N14 security maintenance](/testing/dependency-security-20261026) updates the
web framework and affected dependencies. The invoice workflow stays the same.
The web dependency audit is clear, automated browser checks pass and independent
review found no material issues. Manual acceptance remains open. This does not complete the
planned export or real-account acceptance work.

## Keep a readable copy of results

**Built and locally checked on October 27; final independent evidence check pending.**
Stay in **See results**. Choose a file type, then **Download results**.

- **HTML report:** read the outcome, compared amounts, differences, missing checks
  and decisions. Open the file in your browser; Print can save a PDF.
- **CSV spreadsheet:** use the saved rows in a spreadsheet. Quoted text and Arabic
  are kept. A protective apostrophe stops document text from running as a formula.

Both files use one saved version. If someone changes the case or records another
  decision, refresh results before downloading again. Older files stay unchanged.
The file name includes the case reference and comparison number.

A decision never makes an unchecked value correct. The report separates confirmed,
corrected and added values from unchecked, cleared or older values whose review
state was not recorded. Cleared text stays visible as history, not an approved value.
Invoice and PO values keep their actual roles; earlier quotation records remain readable.

![Illustration: both file types use one backend snapshot](/workflow-review/results-export-20261027.svg)

![Download controls in the real local workflow with synthetic reviewed documents](/workflow-review/results-export-20261027.png)

![Opened HTML report from the real local backend using synthetic documents](/workflow-review/readable-report-20261027.png)

[Read the export checks and optional manual list](/testing/results-export-20261027).
The final files have been downloaded and reopened. Fresh uploads, worker processing
and language acceptance are the next checks; saved-fixture export does not prove them.

## Administration: People & access

**Batch 1 sample review accepted on October 26.** The real local
account remains Staff. You can try the separate **Sample Admin view** at
[the local preview](http://127.0.0.1:3430/organization). It uses fictional people;
its saves never change the retained workspace. Restarting that sample clears only
its own made-up changes. Other sample destinations are limited to this review.

One **Administration** entry opens **People & access**. The other section links
are **Activity history** and **Document storage**. These are different tasks, not
steps to finish in order. Activity history is now built below. Document storage is now built below.

![Administration navigation and the three actions for changing access](/workflow-review/admin-navigation-20261026.svg)

1. **Choose a person.** The list shows names, roles and whether access is active.
   Removed access stays visible. The list shows up to 500 people, not a total for
   the whole company. Account IDs are inside the editor's Account ID control.
2. **Set access.** Staff works with documents. Admin also manages people and system
   settings. On a wide screen, the editor is beside the list. On a narrow screen,
   it follows the list; choosing a person moves focus and scrolls to the editor.
3. **Save and read the message.** Admin grants are clearly named. Removing access
   needs confirmation. The final active Admin cannot be removed or demoted.
   Refreshing the list keeps an unsaved edit. If a save result is unclear, reload
   and inspect the current access before choosing a new change. No save is replayed
   automatically. A reload is not proof that an interrupted request has finished.

**Add person** still needs a name and Microsoft Entra account ID from your IT
contact. It grants application access; it does not send an invitation. No role is
chosen for you. This screen does not create the first Admin; private operator
setup remains separate.

![Actual Dark People screen using fictional sample data; not a real Admin session](/workflow-review/admin-people-dark-20261026.png)

![Actual Light People screen using fictional sample data; not a real Admin session](/workflow-review/admin-people-light-20261026.png)

Only the navigation and People screen changed in the first batch. The existing invoice
workflow, backend permission checks, documents and recorded history remain.
[Read the verification and short manual checks](/testing/admin-people-20261026).
The [original layout proposal](/design/admin-workspace-20261026) is retained;
The storage redesign, invitations and member search remain separate.

## Activity history: read a saved record

**Built and checked; your manual review is open.** Open **Activity history** from
Administration. This page is for reading. It does not change access, documents or
comparison decisions.

![How to read an activity record](/workflow-review/admin-activity-reading-20261026.svg)

- **What happened:** a clear label for a known action. An older or unknown code
  says **Other recorded activity**; its original code is in Details. Older
  quotation comparisons keep their history and use the general word Comparison.
- **Who:** the saved actor type and ID. The current records do not contain a
  person's name. We say **Person - name not recorded**, rather than guessing.
  A repeated ID such as 222222 is still an ID; its shape does not prove a role.
- **When:** UAE time (UTC+4), with English digits. Details keeps the original
  timestamp and the UTC time. Missing or invalid times are labelled unavailable.
- **Result:** **Successful**, **Not allowed** or **Failed**. An unfamiliar result
  is labelled clearly. A successful action does not mean the invoice matched.

Open **Details** to read the original codes, IDs, reason, and saved Before/After
values. Arabic stays as saved. Nothing in this reading view edits the record.

Use **Older** and **Newer** to move through up to 100 events per page. **Refresh
activity** returns to the newest page. If a page fails, **Try again** retries that
same page. A failed read does not mean there are no records. This view stops at
page 101; that limit does not delete older records. New events can move entries
between pages, so this is not a frozen export or a complete log of every visit.

![Dark Activity history screen using fictional sample data](/workflow-review/admin-activity-dark-20261026.png)

![Light Activity history screen using fictional sample data](/workflow-review/admin-activity-light-20261026.png)

Try [Sample Activity history](http://127.0.0.1:3430/organization/audit).
It adds four read-only sample scenarios alongside existing sample events. The
running preview was not reset. Earlier sample-only action codes still appear as
unknown, with their exact saved details available. No real workspace writes run.
[Read the checks and short manual list](/testing/admin-activity-20261026).
Real signed-in Admin integration remains open; the real local Staff account is
still denied this area. The Document storage batch is described below.

## Administration: Document storage

**Built and agent-checked on October 27.** You asked the implementation agent to
finish the remaining work and test it. That removes the need to wait for your
manual review after each batch; it does not turn sample checks into live evidence.

- **Keep files:** blank means keep without a time limit. A number marks when a file
  can be checked. Saving does not remove anything.
- **Protect files:** protect all files, or a specific file. Protection stays until
  an Admin confirms its release. Release does not delete the file.
- **Removal controls:** open only when needed. Preview first, then confirm. The
  backend checks the saved version, permissions and protection again. Reviewed
  or cited history can block removal.

If a response is unclear, stop and refresh the controls and history. Your settings
entries survive Refresh. If saved settings changed, explicitly discard your
entries to load the new version before saving again.

![Illustration: storage settings, protections and explicit removal checks](/workflow-review/admin-storage-20261027.svg)

![Dark Document storage screen using fictional sample data](/workflow-review/admin-storage-dark-20261027.png)

![Light Document storage screen using fictional sample data](/workflow-review/admin-storage-light-20261027.png)

[Sample Document storage](http://127.0.0.1:3430/organization/retention) uses isolated
browser examples. Existing sample people and events are retained. The real local
identity remains Staff. [Read the checks and optional manual list](/testing/admin-storage-20261027).
Real Admin/database storage integration remains for the isolated security batch.

<div class="review-route">
<a href="#the-two-documents"><strong>1</strong> Understand the job</a>
<a href="#the-workflow-today"><strong>2</strong> Follow today’s steps</a>
<a href="#changes-to-review"><strong>3</strong> Study the changes</a>
<a href="#our-review-record"><strong>4</strong> Review each batch</a>
</div>

**Reading key:** **Built** means code exists. **Checked** means a named test was run. **Proposed** means an idea is waiting for your review. **Planned** means work remains. These words do not mean the same thing.

## The two documents

A **purchase order**, or **PO**, says what your business asked to buy. An **invoice** says what the supplier wants you to pay. Our job is to compare them and show the source of each answer.

<figure class="review-picture">
  <a href="/workflow-review/two-documents.svg" target="_blank" rel="noopener"><img src="/workflow-review/two-documents.svg" width="1040" height="550" alt="Illustrated purchase order for 10 chairs at AED 100 and invoice for 10 chairs at AED 110. The example difference is AED 100 before tax." /></a>
  <figcaption>Picture 1 · Made-up example. Open any picture to see it at full size.</figcaption>
</figure>

Imagine we order **10 chairs** at **AED 100 per chair**. The supplier bills **AED 110 per chair**. If both documents mean the same kind of chair and the same price basis, the price is **AED 10 higher per chair**. For these 10 chairs, that is **AED 100 more before tax**.

That is a reason to look closer. It does not prove an overcharge. There may be an agreed change. We need the documents and a person’s decision.

::: warning Do not guess the missing facts
This example assumes the same item, unit, currency and before-tax price basis, plus complete checked rows. The real app must check those facts. If one is missing, it must say **Could not check**. Some current extracted documents still lack this information.
:::

**New reviews use 2 documents: an invoice and a purchase order.** A quotation is an earlier price offer. It is no longer an optional step or document choice for new checks.

### Where earlier quotation work goes

Nothing is deleted. Open **Document library** in the side menu and select **Show earlier quotations** to find an earlier quotation and its saved review. Open **Other tools → Earlier cases and history → Saved cases** to find an earlier case, its results, evidence and decisions. Its quotation controls remain so you can finish or follow up on that existing case. Access still depends on your current permissions.

<figure class="review-picture">
  <a href="/workflow-review/new-and-existing.svg" target="_blank" rel="noopener"><img src="/workflow-review/new-and-existing.svg" width="1040" height="600" alt="Two separate paths: new checks use only an invoice and purchase order; earlier quotation documents, reviews, results and audit records stay accessible without being converted or deleted." /></a>
  <figcaption>Change Q01 · Current scope. New invoice work and earlier quotation work stay separate.</figcaption>
</figure>

| Before | After this change |
| --- | --- |
| A new case from Compare documents opened a quotation-based workflow. | Use **Start with my files** on Home for the invoice/PO workflow. Earlier case links remain supported. |
| Invoice document menus also listed known quotation files. | Known quotations are not offered as new choices. A quotation already saved in a draft stays visible with a message asking for a replacement. |
| The upload area said Add a document. | It says **Add an invoice or purchase order**. PDF, PNG, JPEG and TXT still work, up to 10 MB. |
| A quotation-number field was offered in every review. | It is offered for quotation documents or reviews that already contain that field. Saved fields are kept. |
| This guide called quotations optional. | The new path is invoice and PO only. Earlier quotation work has its own clearly marked path. |

**About uploads:** the picker checks file format, not business meaning. The system identifies the contents after upload. An unknown file stays available for human review; that does not prove it is an invoice or PO. The existing server checks still reject a known wrong document type when you try to pin its completed review. No new upload-content blocking is claimed.

**If an earlier save lost its reply:** retry with the same name and category. The app uses the same save request to avoid making a second case. This may bring back an earlier quotation case. Older connected tools can still use quotation support. Removing that support would need a separate change.

Checking received goods and approving payments are outside this product’s current scope.

## AED only: one currency, less work

**Approved October 12 · implemented in source · manual check pending.**

For a new comparison in **Invoice checks**, both documents must say AED. Check
the currency just like any other value. There is no exchange-rate service, money
conversion or daily job.

| What the backend finds | What happens |
| --- | --- |
| Both saved reviews have one checked AED currency with source text. | The comparison can start. Other missing facts may still limit the checks. |
| Either document uses another currency. | A message says this check supports AED only. No new result is saved. Use a document issued in AED; do not rename a foreign amount as AED. |
| A currency is missing, left out, unclear or lacks source text. | A message asks you to check that document. The system does not guess AED. |

**Your work stays:** uploads, checked values, old results and audit records are
kept. You can still open an older result that used another currency. Starting a
new comparison for that pair now needs AED. Earlier legacy comparison tools keep
their existing rules for compatibility; this gate applies to the saved invoice/PO
workflow. AED on the document does not prove that its tax values can be checked.

**Small manual check:** on Invoice checks, read the AED message. With checked AED
documents, try the comparison. With a non-AED or unverifiable currency, expect a
clear message and no new result; reopen the saved review to confirm it is kept.
The currently running sample preview has not been restarted, to preserve its
document list. It can show the new wording, but its existing simulated backend
does not yet use this gate. Fresh automated preview tests do. Use the updated .NET
app for the backend check; this is not recorded as a manual pass.

[Change details and focused verification](/testing/aed-only-20261012).
Supplier checks, richer visual design and reviewed export remain separate. The combined workspace is now described below.

## The workflow today

<span class="review-status built">BUILT IN THE CURRENT CODE · INTEGRATION GAPS REMAIN</span>

Start at **Home**. Find **Start here**, then choose **Start with my files**. This opens file selection; it does not create a check. Choose or upload both files, then press **Continue to review**. That action creates the check and saves the pair. To reopen saved work, use **Continue** beside it. Each check has its own reference, so you can find it again. You do not need to name a case.

<figure class="review-picture">
  <a href="/workflow-review/workflow-overview.svg" target="_blank" rel="noopener"><img src="/workflow-review/workflow-overview.svg" width="1040" height="740" alt="Start here leads to one page showing all 3 phases: Add documents, Check details and See results. Optional Details opens a reading view with Return to workflow. Moving between phases does not confirm values." /></a>
  <figcaption>Diagram 1: one workflow page. The open phase holds the work; the other phases stay visible.</figcaption>
</figure>

| Step | What you do | What the system does |
| --- | --- | --- |
| **1. Add documents** | Choose the invoice and PO; each new file uploads when selected. Or expand **Use a saved invoice/purchase order instead** and select each file. Press **Continue to review** once both are selected. | Creates the check and saves both file choices. One file alone creates no check. No values are confirmed and no comparison runs. Known wrong types and quotations are not offered for that slot. |
| **2. Check details** | Check the invoice against its source. Save your decisions and complete its review. Choose **Continue to purchase order**, then check and complete the PO review. | Keeps both documents in this workspace. Saves the exact completed invoice review you saw. No separate revision picker is needed. |
| **3. See results** | Choose **Show results** after checking the PO. Read differences, gaps and source evidence. Record a decision with a reason. | Saves the exact PO review you saw, then asks the backend to compare. Evidence and decision history stay here. |

**New check:** there is no name form or separate save button for each file. A saved reference identifies the check. **Existing check:** Change documents still lets you save a changed choice, then use Continue to check values. Earlier reviews and results stay available.

**If preparation stops:** keep this browser tab and choose **Retry preparation**. It uses the same request and checks what was saved. The server saves the check and its two links separately, so an interrupted save can leave a partial draft. Retry resumes the missing links; it does not overwrite changed choices. If a file is now unavailable or has the wrong type, Retry recovers the check without attaching it. **Open saved check** lets you inspect the recovered work. No values are confirmed by recovery.

<figure class="review-picture">
  <img src="/workflow-review/pair-first-20261022.png" width="1440" alt="Sample preview: selected invoice and purchase order beside each other, Continue to review beneath them, and the remaining two phases visible below." />
  <figcaption>N06 ? Sample-data preview, not live backend acceptance. Both files are selected; no check has been created yet.</figcaption>
</figure>

All 3 phase headings stay on the page. Scroll to see the whole job. The current
phase says **You are here** and opens its work beneath the heading. Completed
work shows a short saved summary. Use **Change documents**, **Continue checking**
or **View results** when that action is available. The highlighted next button
continues the work in this same page.

**Details is optional.** It opens a separate reading view for the files, current
saved reviews or saved results. Choose **Return to workflow** to return to the
same phase and document. Reading Details does not confirm values or start a
comparison. Save or discard edits before opening it. A refresh keeps the saved
case, but can require you to inspect the invoice again before running new checks.
Current review details may differ from the frozen values in an earlier result.

Use **Home** in the side menu to return to saved checks. **Document library** opens the
library; it is no longer repeated inside the workflow. Save edits before leaving;
a warning lets you stay if something is unsaved. You cannot leave during a save.
Closed checks show saved work without editing controls.

**Document library is your file shelf.** Search for a saved file and select it.
Its details open on the same page. You can download the original, read saved
values and evidence, or download review history. Values are read-only here.
There is no upload form, comparison shortcut or Questions link.
Use Home in the side menu to add files or continue an invoice check.
Earlier quotations are hidden from the list at first. Select **Show earlier
quotations** to include them. This only changes the list: no files, reviews or
history are deleted. A saved link can still open a quotation. Hiding it while it
is open keeps its reading view available. The file count shows how many match
your search and visibility choice. Processing details keeps recovery tools.
On a wide screen, the file list sits beside the reading area. Saved values open
after you choose a file, with each value in its own card. On a phone, the list
and details stack. Opening a card does not confirm or change a value.

**Find a file without its internal ID:** enter part of its filename, supplier
name, printed invoice number or PO number, then press **Search**. The backend
searches the full library that your account can access. Choose **All**,
**Invoices** or **Purchase orders** to narrow the list. Results show **10 files
per page**, newest first. Use **Previous / Next** to move through them.
**Clear search** removes the words while keeping your type and quotation choices.

Search uses the values shown by the current review. Saved corrections replace
extracted values, and cleared values do not match. An unchecked extracted value
can help find a file, but finding it does not confirm it. If a supplier or number
is missing, search by filename instead. An active review may use an earlier
extraction; the file card explains that. Changing search or page keeps the open
file's details available, even if it is outside the new result list.

<figure class="review-picture">
  <img src="/workflow-review/library-search-20261023.png" width="1440" alt="Sample preview of the library showing filename/supplier/number search, type selection, quotation option and Previous/Next page controls beside preserved quotation details." />
  <figcaption>N08 - Retained sample-preview screenshot. Real query verification is recorded separately.</figcaption>
</figure>

**What changed:** the .NET library endpoint returns a page of 10 summaries.
The retained sample preview simulates this behavior in the browser so existing
sample work stays intact. Its screenshots do not prove real server paging.
Focused PostgreSQL/API tests verify the real query separately. Very large data
volumes have not been benchmarked; substring search and counting can still cost
more as the library grows. No search service or scheduled job was added.

<figure class="review-picture">
  <img src="/workflow-review/library-filter-20261023.png" width="1440" alt="Sample preview of the Document library with Show earlier quotations unchecked. A quotation opened through its saved link remains readable beside the filtered list." />
  <figcaption>N07 · Sample-preview screenshot. Hiding a list entry keeps its saved work accessible.</figcaption>
</figure>

<div class="review-figure">
  <img src="/workflow-review/document-library.svg" width="1040" height="490" alt="Home starts or continues the invoice workflow. Document library opens saved files and their details in place. The library is optional and does not confirm values." />
</div>

**Earlier cases and history has 3 reading steps:** find a case, choose a saved
comparison, then read its evidence and decisions. Search and filters stay above
the saved case cards. The secondary **Recover an earlier request** section
only retries a matching request kept in this browser for your account. It keeps
the original name, category and request key. It cannot start a fresh empty case. For a new invoice check,
use Home. If a case has no saved comparison yet, there is no result to read.

**Ask about documents also has 3 steps:** choose documents, ask a question, then
read the answer and evidence. From a case, choose **Use this case's documents**
to use its saved invoice and PO together. There is no need to find them in a long
list. Older cases keep their saved documents, including earlier quotations.
These are the reviews currently linked to the case; an older comparison may use
different reviews. Opening the page does not prepare or confirm anything.

For a separate document, choose **Choose other documents**, search by filename,
then choose a completed review and **Add source**. Search checks all accessible
completed reviews. Only 10 reviews appear on each page; **Previous reviews** and
**Next reviews** replace that page instead of growing the list. Already selected
documents stay selected while you search. Only these selected versions are
used. Write your question and choose the answer or search button. The answer
appears below; saved questions follow it. Missing facts stay unanswered.
Opening a saved answer keeps its original sources even if today's selection
is different. **Use saved sources** deliberately copies those sources into a
new question. It does not change the old answer.

<figure class="review-picture">
  <img src="/workflow-review/reading-tools-20261022.svg" width="1040" height="360" alt="Two reading paths. History: find a case, choose a saved comparison, read evidence and decisions. Questions: choose saved sources, ask a question, read the answer and evidence. Opening a page does not complete a check." />
  <figcaption>D03 · Reading guides, not check-completion indicators. Missing checks are never a complete match.</figcaption>
</figure>

**If the wrong file was already selected:** a message explains the document type.
For example, an invoice cannot fill the purchase-order slot. Use **Change
documents**, choose the actual PO and save that choice. The original file,
its review and earlier results are kept. Unknown file types still need checking;
the backend remains responsible for accepting a completed source.

**If upload fails:** Retry upload appears when retrying the same request is safe.
If the file was accepted but selecting it failed, use the saved file instead of
uploading it again. A conflict that requires a separate upload offers an explicit
**Start a separate upload** choice.

**If a reply is lost:** choose **Check saved progress** (or **Reload results** for
a result decision). This reads what reached the server; it does not repeat the
write. A review pinned before a later failure remains saved.

**If you change a document:** earlier results stay as they were. Check both sides
again to create new results. **Check this value** uses the saved invoice field,
not the PO row number. If the document or extraction has changed, the app explains
that the earlier evidence belongs to another version instead of selecting a
possibly unrelated row.

### Reading your results

The results stay in step 3. The backend uses the exact saved invoice and PO reviews
you checked. Later edits do not change an earlier comparison.

| Outcome | What it means |
| --- | --- |
| **Differences found** | At least one checked value differs. Read missing checks too. |
| **Checks incomplete** | Some needed information or evidence is missing. This is not a match. |
| **All required checks passed** | Every required check ran and passed for these saved versions. This does not approve payment. |

**Passed + Differed + Could not be checked = Total checks.** A finished comparison
can still contain differences or gaps. For example, 4 + 1 + 2 = 7 checks.

Each check shows **Invoice** and **Purchase order** values, with the saved page and
supporting text when available. Passed checks are in a fold-out list. A missing
check says what is missing and offers one review action within this workflow.
If the document itself lacks the information, ask the supplier; do not guess it.

Choose a reason that fits the situation, or choose **Other** and write your own.
**Record follow-up**, **Accept difference** (or **Acknowledge missing check**) and
**No action needed** record your decision. They do not make a missing check pass.
Earlier decisions remain in history. Old comparisons without full check records
say that counts were not recorded. Sample simulations stay labelled.

<figure class="review-picture">
  <img src="/workflow-review/results-stage.svg" width="1040" height="660" alt="Three workflow steps and three result outcomes. Passed, differed and missing checks add up to the total. Decisions remain separate." />
  <figcaption>N09 - Illustration with made-up counts, not a live result.</figcaption>
</figure>

[Results findings, verification and short manual checks](/testing/results-stage-20261023).
Focused tests and read-only browser checks are recorded in the linked report. Independent recheck found no material issues. Your manual acceptance and the documented live integration checks remain open.

### What the backend checks now

You no longer choose whole order, part of the order or not sure for new checks.
The backend compares the facts these two documents can support.

- If the order lists 100 chairs and the invoice lists 40, it can report those
  quantities and a difference of -60 when the item and units are checked.
- It can compare the price per chair when currency and price bases are usable.
- It does not call a smaller invoice total wrong just because fewer items were billed.
- Whole-document amounts need complete, trusted item coverage and equal checked
  quantities for the same items, plus usable amount bases and currency.
- Missing evidence stays **Could not check**. Matching visible numbers do not fill a gap.

This does not prove that 60 chairs remain to invoice, that goods arrived, or that
payment is approved. The new rules do not yet calculate the earlier illustrative
quantity-times-price impact. Older results retain their original rules and scope.
The reviewed report and CSV are still planned.

[Implementation, findings and verification](/testing/navigation-flow-20261020).
The [earlier multi-page diagram](/workflow-review/today-flow.svg) is retained as
history, not current instructions.

::: details What “reviewed” really means
A person must confirm or correct a value before it can support a check. The value also needs a usable type and real source evidence. A high confidence score, a completed document, or clicking Next must never confirm an unchecked value. A manually added value without the needed source evidence may still be unavailable for comparison.
:::

## G01: less typing when checking values

**Approved direction · first slice implemented · manual review pending.**

Before, each value came with edit boxes, even when it was correct. Now, opening a
review from a saved invoice pair selects a one-value view. Start the review if it
has not started. Values with reading warnings come first. Every other unchecked
value still needs your decision; high confidence does not approve it.

<figure class="review-picture">
  <a href="/workflow-review/guided-values.svg" target="_blank" rel="noopener"><img src="/workflow-review/guided-values.svg" width="1040" height="680" alt="Guided review illustration: one invoice total beside its source, with Keep this value, Change it and I can't tell. Leaving a value out requires a separate explicit choice." /></a>
  <figcaption>G01 · Illustration of the implemented controls, using made-up data. This is not a browser screenshot.</figcaption>
</figure>

- **Keep this value:** save that you checked this value. No typing needed.
- **Change it:** show the value and reason boxes. Pick a short reason or write your own, then save.
- **I can't tell:** first read what leaving it out means. Only **Leave this value out** saves that decision. Cancel saves nothing. The original value stays in history.

After a saved decision, the next value appears. A failed save keeps you on the
value. If the reply is lost, reload the saved review before deciding what to do.
The app does not repeat that write for you.

**All fields** shows saved decisions and the existing add/revise tools. Switching
views keeps drafts. Unsaved text is not promised to survive a page refresh. Missing
values and values you left out stay visible, and may prevent some checks. Completing
a review saves that you finished your decisions; it does not say the documents match.
Source text comes from returned page quotes. A highlighted PDF picture is not built
in this batch.

**October 20 update:** the numbered workspace and factual backend rules now
remove the routine scope/version choices for new checks. The earlier G01 slice
remains part of step 2. Live integration and manual acceptance remain open.
Visual redesign and Owner remain separate proposals. Staff and Admin are covered
below. [Current plan and acceptance criteria](/design/one-review-navigation-20261020).

## Adding a missing value: fewer settings

**Approved October 19 · implemented in source · manual check pending.**

In **All fields**, **Add a missing field** now offers named choices such as
Invoice number, VAT and Payment terms. Choose a field, enter its value, and choose
a reason or write your own. For an item field, also choose its line number.

| Before | Now |
| --- | --- |
| A Custom field choice and Custom field settings exposed internal names, positions and value types. | Those controls are removed. The app sets the value type and document-field position for you. |
| Saved custom values could already appear in a review. | They still appear with their saved decisions. Earlier reviews and history are kept. |

Adding a missing value does not create a new check or supply missing source
evidence. Such values can still be unavailable for automatic comparison. Payment
terms remain in their current place; moving them into Other details is only a
proposal. This change does not finish the combined invoice/PO flow.

**Short check:** open an active review and choose All fields if needed. Under Add a
missing field, confirm that Custom field settings is gone. Choose VAT, then Line
quantity: only the latter should ask for a line number. You can discard the draft
without saving. If an older review has a custom value, check that it is still shown.

[Change record and verification](/testing/named-fields-20261019).
The existing workflow diagrams still apply: the main review steps have not changed.

## Reason choices: less typing

**Approved October 19 · implemented in source · manual check pending.**

The normal review screen now has a **Choose a reason** list:

| What you are doing | Choices |
| --- | --- |
| Adding a missing field | Shown on the document but missed. |
| Changing or clearing an extracted value | Read incorrectly by the system. / This value is not on the document. / I cannot verify this value. |
| Clearing a field someone added | Added by mistake. / I cannot verify this value. |

Each list also has **Other — write my own**. Choose it to open **Your explanation**
and write a short reason. With a listed reason, there is no extra box to fill in.
Nothing is selected for you. Choosing a reason does not save a decision. Use
**Add field**, **Save correction** or **Clear** when ready. The saved reason joins
the review history. It does not replace the source evidence needed for comparison.

Choosing Other clears a preset so you can write your explanation. If you already
wrote your own reason, it stays. The guided one-value view keeps its existing
suggested reasons. Keeping an extracted value unchanged still needs no reason.

**Short check:** open the reason list under Add a missing field and choose a reason.
It should stay selected without saving. Choose Other to write your own explanation.
Discard the draft to clear it. A reason joins the saved review only after you save.
[Dropdown verification and limits](/testing/reason-dropdown-20261019).

The styling is kept simple for the future redesign. That larger design change has
not started in this batch. The main workflow and its diagrams are unchanged.

## Reading an honest result

<div class="review-card-grid">
  <article class="review-card"><span class="review-status attention">DIFFERENCE FOUND</span><h3>Two usable values differ.</h3><p>Example: AED 100 per chair on the order and AED 110 on the invoice. Read the source and decide what to do.</p></article>
  <article class="review-card"><span class="review-status pending">COULD NOT CHECK</span><h3>A needed fact is missing.</h3><p>Example: one price is per chair, but the other unit is unclear. Do not guess that the amounts mean the same thing.</p></article>
  <article class="review-card"><span class="review-status built">NO DIFFERENCE IN THIS CHECK</span><h3>The checked values agree.</h3><p>This statement applies to that check. Other checks may still be missing or incomplete.</p></article>
</div>

These are plain-language explanations of the rule outcomes. Some current screen wording is more technical and still needs design work.

**Coverage** means how much of the job the system could check. If 3 checks pass and 2 cannot run, we have **3 completed checks and 2 gaps**. We do not have a complete match.

A difference can appear in a unit price, line total and document total at the same time. Do not add those numbers together and call them savings; they may describe the same issue more than once.

| Decision label today | Plain meaning for the reviewer | What it does not prove |
| --- | --- | --- |
| Resolved | “I have dealt with this finding.” Record what you did. | It does not prove the supplier changed the source invoice. |
| Accepted | “I know about this difference and accept it for this review.” Say why. | It does not approve a payment. |
| Dismissed | “I do not think this finding needs action.” Say why. | It does not turn a missing check into a completed check. |

## Why old results must stay the same

A saved comparison is like a dated copy of the facts used for that check. Later edits belong to a new comparison. Decisions have their own history, so you can see who changed a decision and why.

<figure class="review-picture">
  <a href="/workflow-review/saved-history.svg" target="_blank" rel="noopener"><img src="/workflow-review/saved-history.svg" width="1040" height="450" alt="Comparison 1 keeps invoice AED 110 and order AED 100. A later corrected invoice at AED 100 creates comparison 2. The first comparison is preserved." /></a>
  <figcaption>Diagram 2 · Changing today’s values must not rewrite yesterday’s result.</figcaption>
</figure>

Keep Arabic names and source text as written. For supported numbers, use English digits: **0 1 2 3 4 5 6 7 8 9**. For example: <bdi lang="ar" dir="rtl">شركة النور</bdi> · <bdi>AED 1,100.00</bdi>. An unclear number needs review; the original evidence must stay available.

## Changes to review

<span class="review-status proposed">BUILT AND PROPOSED · EACH CHANGE HAS ITS OWN STATUS</span>

The aim is fewer confusing choices and a clearer next step. The safeguards above still apply. We can change the screen without changing what counts as a trustworthy result.

### A. Earlier screen proposal — superseded by the numbered flow

<div class="review-three-stages">
  <article><span>01</span><h3>Add documents</h3><p>Put the invoice and its order together.</p></article>
  <article><span>02</span><h3>Check details</h3><p>Read the values beside their source.</p></article>
  <article><span>03</span><h3>See results</h3><p>Study differences, decide, then download when export is ready.</p></article>
</div>

| Earlier workflow | Earlier proposal (history) | What must stay safe |
| --- | --- | --- |
| You name a check before choosing documents. | Start with the documents. Suggest a useful name that you can change. | Give each saved check a stable identity so it can be reopened. |
| Review, choosing a revision and using it are separate tasks. | Put review in one clear place. After explicit review completion, use **Compare these documents** to bind the exact reviewed versions. | Never confirm unchecked values or silently switch to a newer version. Explain partial saves and ask for a deliberate recovery action. |
| “Invoice scope” needs explanation. | **Superseded direction:** let the backend report supported quantities and prices, without asking the user to guess full/partial intent. Implemented in version 2 for new checks; see above. | Preserve old policies/results. Missing coverage must still limit checks; do not infer remaining invoice balances. |
| Results use technical wording and several controls. | Lead with differences and missing checks. Keep extra details expandable. | Keep source evidence, coverage limits, older results and decision history. |

**Review question:** Can a first-time user find the next step without learning what a revision or a rule version is?

### B. Home: implemented structure, later visual design

Home now offers Start here, Start with my files and a list of saved checks with Continue. Current rows show the saved name and reference. Rich supplier/amount summaries and the visual treatment in the picture below are still proposals. The initial name is Invoice check; its reference distinguishes it from other checks.

<figure class="review-picture">
  <a href="/workflow-review/proposed-home.svg" target="_blank" rel="noopener"><img src="/workflow-review/proposed-home.svg" width="1040" height="660" alt="Proposed invoice-list home, labelled not built. Sample rows show Add order, Check details and See results. It includes an Arabic supplier name with English digits." /></a>
  <figcaption>Picture 2 · A rough screen illustration to discuss. These are not working buttons.</figcaption>
</figure>

The main button could be **Check an invoice**. A row could show the supplier, invoice number, known amount and next action. Older tools could sit under **More tools**, with account controls under **Settings**.

Before building this design, review these states too:

- **Nothing added:** explain the first step without empty charts.
- **Still reading a document:** show progress without calling the review complete.
- **A request failed:** keep the work and explain how to recover.
- **Values are missing:** say what cannot be checked.
- **Small screen or keyboard:** keep the next action easy to reach.
- **Arabic and English together:** keep names readable and numbers clear.

**Review question:** Can you find your saved check and its next step easily? The richer list illustration is not a completed screen.

### C. Only two roles: Staff and Admin

**A02 approved October 19 · built in source · account migration and manual acceptance pending.**

Choose **Staff** for people doing invoice work. Choose **Admin** for people who
also manage the system. There is one role list and no extra administration switch.

| Role | What the person can do |
| --- | --- |
| **Staff** | Upload documents, check values, compare invoices with orders, record decisions, ask source-based questions and use existing downloads. |
| **Admin** | Everything Staff can do, plus manage users, read audit history and manage retention and deletion controls. |

<figure class="review-picture">
  <img src="/workflow-review/two-roles.svg" width="1040" height="400" alt="Staff handles document work. Admin has the same document tools plus user and system settings. Disabled accounts cannot sign in." />
  <figcaption>A02 · Role diagram. Active or disabled is account status, not a third role.</figcaption>
</figure>

**Disabled means no access.** Disabling a person keeps their earlier work and audit
record. At least one active Admin must remain. Choosing a role does not save it;
you must use the save button. A changed account must be reloaded before a stale
save can be retried. Old role names are rejected by the new API.

**What happens to existing people?** First we list each account and explain what
access would change. You review that list before conversion. Former Viewer and
Reviewer accounts would gain document powers, so we do not convert them silently.
Former administration-only accounts would gain document access as Admin. Disabled
accounts stay disabled. An earlier account with no access stays disabled.
If the account list changes after review, the migration stops and needs a new review.
The original audit records stay as they were; new records explain the conversion.

**Current limit:** the configured database is offline. No existing accounts were
converted. The running sample preview keeps your documents and shows the two role
labels, but user management there is blocked until the server is updated. Do not
reset that preview to test roles. The full reviewed-results export is still planned.
Owner and the earlier visual dashboard concepts remain unapproved; the simple Home navigation is now approved.

[Verification, earlier approach and short manual checks](/testing/two-roles-20261019).

### D. Add a report you can trust

The planned report brings checked values, differences, evidence, decisions and missing checks together. CSV is a simple table file you can open in a spreadsheet. Its download should use the same saved version as the screen.

If someone changes a decision while you download, the report must still be one consistent record. Older results must remain downloadable as older results. Arabic text, English digits, line breaks and spreadsheet-safe values need focused checks.

**Review question:** What must be in the download for you to use it at work? This report is still planned.

## Organizing the code behind the screens

Refactoring means moving code into clear places while keeping the same behavior. Think of sorting a desk: the paperwork is easier to find, but its meaning stays the same.

<figure class="review-picture">
  <a href="/workflow-review/system-map.svg" target="_blank" rel="noopener"><img src="/workflow-review/system-map.svg" width="1040" height="470" alt="System map: the web screen sends requests to the .NET service. .NET uses repository interfaces to reach stored records and calls Python for document extraction. Python returns suggested values and evidence for human review." /></a>
  <figcaption>Diagram 3 · Each part has a clear job. This is a simple map, not a full deployment diagram.</figcaption>
</figure>

| Part | Its job, in plain language | Where the organization stands |
| --- | --- | --- |
| **Web / Next.js** | Show screens, collect your choices and explain what happened. | The invoice feature is split into components, a workflow hook, services, types, constants and helper functions. Other large pages still need work. |
| **.NET** | Check access, apply business rules and save work safely. | The first batch moved 40 existing types into clearer files. The existing repository pattern is retained. Other features remain for later batches. |
| **Python** | Read document text, suggest values and return source evidence. | Organization is requested. No Python refactor has been applied yet. Start with models and error handling, then separate extraction work in a later batch. |
| **Database** | Keep documents, reviewed values, comparisons and history. | Preserve existing records and access rules. File cleanup does not authorize deleting this data. |

**What is `IRepository`?** In .NET, a repository interface is a list of allowed storage jobs. A handler (code for one task) asks for a job, such as loading a case, without needing to know the database details. `IReconciliationRepository` and `IDocumentRepository` already do this. Their implementations use EF Core, the database tool, to read and save records. We are making the pattern easier to find, not adding a second storage layer.

For each code batch, we will show the actual before/after diff in VS Code, inspect the changes, fix findings and run focused tests. Related functions can share a useful module; one file for every tiny function would make the project harder to navigate.

**Cleanup rule:** remove files that are safe to regenerate and no longer needed. Keep useful tests, source evidence, audit history, backups and saved comparisons. A test that passed once is still useful next time.

## What has been checked

<span class="review-status checked">OCTOBER 11 BASELINE · NOT USER ACCEPTANCE</span>

The last project recheck found no confirmed functional regression in the paths it tested. Here is what that statement covers:

| Area | Recorded result | Limit to remember |
| --- | --- | --- |
| .NET | 1,160 tests passed. | The real PostgreSQL integration suite was not rerun. |
| Web | 325 of 326 passed in the full run; all 6 tests in the failing file passed on an unchanged focused rerun. TypeScript and web lint passed. | The first failure remains recorded as a possible timing issue. There was no single clean full run. |
| Python | 499 distinct parent cases passed across the correct runtimes. | 5 pinned OCR runtime cases were not run. Broader lint found 175 cleanup/review items. |
| Browser | The browser helper failed to start. | No fresh visual acceptance was claimed. Preview tests use made-up data. |

The older S3 batch’s **324 checks** are a separate historical result. Do not add them to this baseline. Tests tell us about the cases they cover; they do not tell us that the interface is easy to use.

Real sign-in, complete extraction-to-backend integration, general Arabic/OCR quality, report/export and deployment still have open gaps. The current supplier extractor also lacks some facts needed for full comparisons. A good-looking screen cannot close those gaps.

[Read the exact baseline report and findings →](/testing/project-recheck-20261011)

## Our review record

### October 26: easier case questions and clearer actions

Page actions such as **Ask about this case's documents** now look like buttons.
They keep normal link behavior. Headings and descriptions sit on separate lines,
including inside **See results**. Buttons and nearby text have clear gaps.

You can ask about the invoice, the PO, or a comparison of both. For a saved case,
one deliberate action uses its documents together. Finding a separate document
uses filename search and small pages, so you do not scroll through thousands of
reviews. Search does not silently select a document or change a saved answer.

<figure class="review-picture">
  <img src="/workflow-review/case-questions-20261026.svg" width="1040" height="490" alt="Two ways to choose documents: use a saved case's documents together, or search filenames and choose from 10 reviews per page. Both lead to asking a question and reading the answer with evidence." />
  <figcaption>N12 · Current question flow. Preparing documents is an explicit action, not confirmation of unchecked values.</figcaption>
</figure>

<figure class="review-picture">
  <img src="/workflow-review/case-questions-20261026.png" alt="Real local test app with the saved synthetic invoice and purchase order selected together for a question. No answer is claimed in this screenshot." />
  <figcaption>Live local app · Fictional reviewed documents and Development identity. This does not verify real sign-in or uploads.</figcaption>
</figure>

101 focused web tests, 2 PostgreSQL checks and 14 API checks pass. Live case-source
preparation and review search pass; the saved comparison and decisions stayed
unchanged. Manual acceptance is pending. See the [N12 report and short checks](/testing/questions-polish-20261026).

### October 23: who recorded a decision?

Decision history now calls the known local test account **Development user (test
identity)**. The long ID is still saved; point to the label with a mouse to see it.
Other users still show their saved ID until the backend supplies a reliable name.
We never assume that an older decision belongs to the person using the app now.

This changes the wording only. The decision, reason, result and access rules stay
the same. 40 focused tests pass; the live visual check could not start its browser,
so your manual check remains open. [N11 evidence and short check](/testing/actor-label-20261023).

### October 23: a larger library and a real saved result

The steps stay the same: **Add invoice + PO → Check details → See results**.
This batch checks those steps; it does not add another screen or change your work.

We tested a list with **10,000 made-up files**. The real library showed 10 files
at a time and found a supplier beyond the first page. In a separate check, the
real app sent the exact saved invoice and PO reviews to the backend. It showed
**12 passed, 1 differed and 0 missing checks**. Recording a decision and reopening
the result kept that difference. A decision did not turn it into a match.

<figure class="review-picture">
  <img src="/workflow-review/verification-stages-20261023.svg" width="1040" height="620" alt="Verification stages: made-up large list checked; real library API and database checked; real browser and prepared reviewed pair checked; two native PDF partial extractions checked separately. Full upload processing, scans and real sign-in still need checks." />
  <figcaption>N10 · Test coverage diagram, not a screenshot or proof of a complete upload flow.</figcaption>
</figure>

**What this proves:** the tested parts can work together with prepared test data.
**What it does not prove:** that every uploaded or scanned file works, or that
the system is ready for many people at once. The first API request took about
5 seconds. Later searches were quicker. Choosing files for a new invoice check
still loads the full list; that screen needs its own scale improvement.

133 focused Python checks also passed. Two real PDF extraction requests kept
their missing-information warnings. No value was silently accepted. Real sign-in,
scanned Arabic files, the complete upload-and-processing path and exports still
need checks. Your manual acceptance is also pending.

See the [N10 evidence and three short manual checks](/testing/scale-integration-20261023).
The retained sample preview was not reset. It was unreachable after the interrupted
session; the isolated live test app has its own startup instructions in that record.

### October 22: clearer reading pages and button spacing

The library now gives saved values more room. History puts saved cases first,
with search and filters grouped together. Questions places source selection
beside the question on wide screens, then shows the answer and saved questions
below. Phone layouts stack in reading order. Recovery and evidence buttons have
space around their text. The numbered guides explain the order; they never
mark a value as checked or a comparison as complete.

<figure class="review-picture">
  <a href="/workflow-review/library-layout-20261022.png" target="_blank" rel="noopener"><img src="/workflow-review/library-layout-20261022.png" alt="Sample-preview Document library with a file list beside saved quotation value cards. Evidence and processing sections are expanded for inspection." /></a>
  <figcaption>Document library · Sample data. Sections are expanded to show retained evidence and recovery tools.</figcaption>
</figure>
<figure class="review-picture">
  <a href="/workflow-review/history-layout-20261022.png" target="_blank" rel="noopener"><img src="/workflow-review/history-layout-20261022.png" alt="Sample-preview Earlier cases and history page with three numbered reading steps, grouped filters and saved case cards." /></a>
  <figcaption>Earlier cases and history · Sample data, with the older named-check form kept under a secondary disclosure.</figcaption>
</figure>
<figure class="review-picture">
  <a href="/workflow-review/questions-layout-20261022.png" target="_blank" rel="noopener"><img src="/workflow-review/questions-layout-20261022.png" alt="Sample-preview Ask about documents in Light mode, with source and question panels, followed by the answer area and saved questions." /></a>
  <figcaption>Ask about documents · Sample data. An empty answer area does not prove that a question can be answered.</figcaption>
</figure>

[D03 findings, checks and short manual review](/testing/workspace-layout-20261022).
Implementation is authorized; visual and manual acceptance remain open.
The administrator dashboard is the next separate design stage.

### October 22: choose Dark or Light

Rand accepted Home's look. The same grey and purple colours now continue through
the workspace. Choose **Appearance → Light** for pale grey and white, or
**Appearance → Dark** for charcoal. The browser remembers your choice when it
can save preferences. Switching colours keeps your open work and unsaved typing.

The layout and three steps stay the same. Warning and error messages keep their
own colours and text. There is no new service or daily background task.

<figure class="review-picture">
  <a href="/workflow-review/home-light-20261022.png" target="_blank" rel="noopener"><img src="/workflow-review/home-light-20261022.png" alt="Light Home theme with white cards, pale grey background and purple start action. Sample-preview screenshot." /></a>
  <figcaption>Light theme in the retained sample-data preview. <a href="/workflow-review/home-dark-20261022.png">View the matching Dark screenshot</a>. These are app screenshots with sample data, not live integration evidence.</figcaption>
</figure>

91 focused tests and twelve browser views passed. The existing reviewer found
no material issues in this batch; no extra specialist agents were created. Try the
[short theme checks](/testing/themes-20261022). Acceptance of these shared themes
and live integration remain open.

### October 20: the approved Home design is now in the app

Rand approved the grey and purple preview and asked us to apply it.
Home now puts **Start here** in a clear purple panel. The three cards explain the
steps. Below them, each saved review has one **Continue** or **View results**
button. “Saved work” still does not mean the documents match.

Refresh, loading messages, errors, light mode, Help and account controls remain.
The pages after Home keep their current design for now. The workflow itself
has not changed.

<figure class="review-picture">
  <a href="/workflow-review/home-implemented-20261020.png" target="_blank" rel="noopener"><img src="/workflow-review/home-implemented-20261020.png" alt="Implemented Home screen in dark grey and purple, with Start here, three steps and saved reviews. Captured in the sample-data preview, not a live production account." /></a>
  <figcaption>Working Home in the retained sample-data preview. This screenshot does not verify real backend comparisons.</figcaption>
</figure>

75 focused tests and desktop/phone browser checks passed. Please try the short
[manual checks](/testing/home-design-20261020) before we call the working design
accepted. Live integration and the sample-results limitation remain open.

### October 20: Mosaic-inspired Home preview

Rand found the grey version too plain and chose Mosaic as a design reference.
The [Home preview](/proposals/home-dark.html) now uses dark grey, purple accents
and clearer cards. Start here stands out. The three steps show what comes next.
Saved reviews stay in their own area, with one action for each review.

This is a picture made with code and example data. Its buttons do not work.
Preparing this preview is approved; its final look still needs Rand's review.
The working app and workflow have not changed. Four screen widths were checked;
that does not mean real document checks or live integration have passed.
[Scope and evidence](/design/home-dark-refinement-20261020).

### October 20: layered greys

Rand rejected amber and requested grey shades. The
[static proposal](/proposals/home-dark.html) now uses a light grey start button,
darker grey secondary buttons and quiet grey navigation. The layout stays the
same. This revision awaits review; the working application is unchanged.

### October 20: try muted amber buttons

Rand approved the dark grey background and asked to replace teal buttons.
The [static proposal](/proposals/home-dark.html) now uses muted amber accents.
The layout and grey surfaces are unchanged. This accent is awaiting review;
the working application has not changed.

### October 20: charcoal grey with a teal accent

Rand liked the Home layout and asked to change the colours. The
[static proposal](/proposals/home-dark.html) now has dark grey surfaces and teal
buttons and selection accents. The layout stays the same. This colour revision
is awaiting review and has not changed the working app.

### October 20: review the dark Home design

Rand chose to keep the dark workspace and refine it. The first proposal gives
Start here more focus and makes saved checks easier to scan.
[Open the static design proposal](/proposals/home-dark.html) and
[read its scope](/design/home-dark-refinement-20261020).
The sample rows are illustrative. This layout is awaiting review; the application
has not changed. The simplified workflow stays the same.

### October 20: a simpler Document library

Rand asked for fewer routes and a clearer library. The page now finds saved files
and opens their details in place. Uploading and editing invoice values stay in the
main workflow. Original files, quotation history, evidence and processing recovery
remain available. The duplicate preview shortcut menu is removed.
[N05 findings, verification and manual check](/testing/document-library-20261020).
Manual acceptance remains pending.

### October 20: fix the sample result error

Both saved reviews were present, but the sample server expected an older request.
The preview now sends the format it understands. You do not need to fill in a
scope field. Its results still use older sample rules and show missing checks.
They do not prove that all real checks passed.
[N04 evidence and short manual check](/testing/evaluation-preview-20261020).
Your saved work stays in place. Manual acceptance remains pending.

### October 20: one upload action and earlier file-type checks

Rand's manual testing found duplicate upload controls, overlapping Other tools
links, and an invoice saved in the PO slot. Choosing a file now uploads it.
The expanded menu uses separate rows. File choices filter known wrong types;
existing wrong choices show an early warning without changing the saved data.
[N03 findings, fixes and verification](/testing/workflow-fixes-20261020).
Manual acceptance remains pending.

### October 20: the full workflow on one page

Rand asked to remove confusing repeated links and see the whole job together.
Home now guides the start. All 3 phases remain visible, with the active work
opening in place. Optional Details has one Return button. The sidebar provides
one Home route and keeps historical tools in an expandable group.
[N02 changes, review findings, checks and limits](/testing/workflow-overview-20261020).
This refines navigation; it adds no new backend rules or account permissions.
Manual acceptance remains pending.

### October 20: one saved check, 3 steps

Approved and implemented in source: simple Home, numbered workspace, inline
review/results and factual backend policy. Independent review found navigation,
save-token, closed-case and source-version issues; fixes and focused regression
checks are recorded in the [batch evidence](/testing/navigation-flow-20261020).
Manual acceptance and full live integration remain pending. Earlier records below
are retained and are not fresh verification.


This section is the place to return after each meaningful change. Keep earlier entries. Add a new entry that says what changed and links to its evidence. Tell me your feedback in our conversation, and I will add it here. Updating this page does not record your approval by itself.

| Record | What happened | Your review |
| --- | --- | --- |
| **D03 · October 22** | Library layout, button spacing, History and Questions reading guides. [Evidence](/testing/workspace-layout-20261022). | Implemented; visual/manual acceptance pending. Admin dashboard follows later. |
| **D01 · October 20** | Dark Home refinement proposed with a static mockup. | Dark direction approved; layout review and implementation pending. |
| **N05 · October 20** | Simplify Document library and remove competing links. [Evidence](/testing/document-library-20261020). | Implementation approved; manual acceptance pending. |
| **N04 · October 20** | Fix Show results in the retained sample preview; keep incomplete-check warnings. [Evidence](/testing/evaluation-preview-20261020). | Error reported; repair implemented, manual acceptance pending. |
| **N03 · October 20** | One upload action, menu spacing and early wrong-type guidance. [Evidence](/testing/workflow-fixes-20261020). | Fixes requested after manual feedback; acceptance pending. |
| **N02 · October 20** | Full workflow overview, upload-first guidance and optional read-only Details. [Evidence](/testing/workflow-overview-20261020). | Implementation requested; manual acceptance pending. |
| **B01 · Invoice code organization** | The first web feature was split into clearer files. [Changes and checks](/testing/invoice-refactor-20261011). | Feedback on the structure is still open. |
| **B02 · .NET and safe cache cleanup** | 40 types were separated; existing repositories and behavior were retained. [Changes and checks](/testing/net-refactor-cleanup-20261011). | Feedback on the structure is still open. |
| **B03 · Baseline recheck** | Fresh checks recorded before more edits. [Results and limits](/testing/project-recheck-20261011). | No manual acceptance recorded. |
| **R01 · First guide, October 11** | Current work and possible changes were explained with pictures. Documentation only. | No manual acceptance recorded. |
| **Q01 · Quotations, October 12** | New checks use an invoice and PO. Earlier quotation work stays accessible. [Dependency review, changes and checks](/testing/quotation-workflow-20261012). | Scope requested by Rand. Manual review is still open. |
| **G01 · Guided values, October 12** | One value beside its source; edit only when needed; explicit exclusion with gaps kept visible. [Changes and checks](/testing/guided-review-20261012). | Direction approved. Manual acceptance pending. |
| **P1 · Python organization** | Existing API models and safe error handling now have separate files. [Checks and boundaries](/testing/python-organization-20261027). | Implemented and independently cleared; focused checks pass on Windows and Linux. The workflow is unchanged. |
| **N01 / G02-G03 · October 20** | Numbered workspace and factual v2 policy implemented in source. [Evidence](/testing/navigation-flow-20261020). | Manual and live integration acceptance remain open. |
| **A01 · Earlier Staff naming, October 19** | Superseded by A02; earlier evidence retained. [Checks](/testing/staff-role-20261019). | Direction approved. Manual acceptance pending. |
| **A02 · Staff and Admin, October 19** | Exactly two roles in source; guarded account conversion. [Checks](/testing/two-roles-20261019). | Approved. Real migration and manual acceptance pending. |
| **Earlier proposals · Visual redesign, Owner, export** | This row preserves the original options. The later dark/light design and S5 readable report/CSV are now implemented and checked. | Owner remains unapproved; export is no longer planned work. See the newer records above. |

### How we study one change

<div class="review-cycle"><span>Explain</span><span aria-hidden="true">→</span><span>Discuss</span><span aria-hidden="true">→</span><span>Change a small part</span><span aria-hidden="true">→</span><span>Check it</span><span aria-hidden="true">→</span><span>Record the result</span></div>

1. **Before:** show today’s behavior with one example.
2. **Proposed change:** explain what will be easier and what must stay safe.
3. **Your choice:** record your actual feedback and the agreed scope. A test pass is not your approval.
4. **Implementation:** show the code and real diff in VS Code. Review bugs and design problems.
5. **Verification:** record focused tests, fixes, remaining gaps and a short manual check.
6. **After:** update the picture and explanation. Keep the old record linked so we can compare.

::: details Reusable change card — copy for the next batch
**Record ID and date:**

**Status:** Proposed / Agreed scope / In progress / Checked / User review complete. Use only the status supported by the record.

**Problem:** What makes the current task hard?

**Before:** What happens now? Link the old picture or source snapshot.

**After:** What will change? Label a mockup as a mockup until it is built.

**Stays the same:** Which permissions, history, evidence and review rules must survive?

**Review decision:** What did Rand actually say, and when? If no answer exists, write Pending.

**Code and diff:** Which files changed? Link the saved before/after record.

**Findings and fixes:** Who reviewed it? Name self-review honestly. What was found and fixed?

**Tests:** What ran, on which date, and with what failures or skips?

**Manual check:** Give 2 or 3 short steps and the expected result.

**Open points:** What is still unverified or waiting for a decision?
:::

## Short check for Q01

1. Start a fresh check. Open its document choices. You should see invoice and PO slots, with no quotation slot or known quotation choice.
2. Open an earlier quotation document, then an earlier saved case. Its review, source evidence, old result and decision history should still be reachable. Do not change any values for this check.
3. Read the picture above. Does it make the difference between new work and earlier work clear?

These are manual checks for you to perform. They are not recorded as passed.

## Historical manual checklist: one page and optional details

1. From Home, choose **Start with my files**, or Continue existing work. All 3 phases should be visible. Add the files in phase 1; the saved-file alternative should expand only when requested.
2. Use **Continue to check values**. The working area should open in phase 2 on the same page. Open an available **Details** view, then **Return to workflow**. Your phase and document should be kept; reading must not confirm values.
3. In results, read an older comparison through Details and return. The workflow should still show its original selected result. Use Home to reopen saved work. Historical quotation work remains under Other tools.

These checks are **pending your manual review**. The retained port 3410 preview
can show updated navigation and sample results through a compatibility adapter,
but its older simulated backend cannot validate the new rules. Results keep
missing-check warnings; you do not need a scope field. Do not reset it.
This paragraph records the earlier preview checkpoint. The newer S5, S6 and L1
records above now cover local extraction, exports and bounded local identity
checks. Live Microsoft identity and deployment remain separate, unverified tracks.

::: details Source map for checking this guide
Current workflow: `apps/web/app/invoices/components/`, `hooks/use-invoice-workflow.ts`, and `apps/web/app/reconciliation/page.tsx`.

Access rules: `apps/backend/src/UaeDocumentIntelligence.Api/Security/EnterprisePermissions.cs`.

Rule limits: [invoice/PO rule contract](/design/invoice-po-rule-contract). Future work: [supplier invoice tasks](/tasks/supplier-invoice-product).

Original ideas: [simple-workflow proposal](/design/simple-invoice-workflow-20261011). Its implementation wording is still a proposal; the later continuation notes preserve Rand’s review requirement.

Code organization: [web refactor](/testing/invoice-refactor-20261011), [.NET refactor](/testing/net-refactor-cleanup-20261011). Latest baseline: [project recheck](/testing/project-recheck-20261011).
:::




## Short check for G01

Use the [local synthetic UI preview](http://127.0.0.1:3410/). These are sample
values and simulated saves, not real extraction or database evidence.

1. Open **Invoice to review.txt**, Start review, then **Check one value at a time**. Expect one value beside its source, with no edit boxes.
2. Choose **Change it**, edit the value and pick a reason. Switch to **All fields**, then back. The draft should remain. Save it deliberately.
3. On another value choose **I can't tell**, then Cancel: nothing should save. Try it again and choose **Leave this value out**. The gap should remain visible; the app must not claim a complete match.

For current pair entry, use step 2 in the saved check. Its guided view opens for an active review. The old G01 scope/version controls have been replaced by the numbered flow; explicit value decisions and review completion remain required.
