# Administration workspace proposal

October 26, 2026. **Original proposal, followed by approval of batch 1.**

Update: the user accepted this direction and authorized batch 1 with "Yes, Proceed."
Batch 1 is implemented and the user accepted its sample review. Batch 2 (Activity
history) is now approved and implemented, with its manual review open. Batch 3
(Document storage) remains planned. The original proposal and
its evidence below are retained as the design record. No live account provisioning
was authorized. See [batch 1 verification](../testing/admin-people-20261026.md).

### Original proposal status
The user approved inspecting the current admin pages and preparing a simple
layout using fictional details. No account or permission changes are authorized
by this design review. The local workspace remains Staff.

## The main idea

One **Administration** item in the sidebar opens **People & access**. Across the
top are three section links styled as tabs:

**People & access · Activity history · Document storage**

You arrive at useful work immediately. Choose a person, change their access and
save. Changes happen on that screen. The other tabs show recorded activity and
file-keeping controls. They are separate tasks, not steps that must be completed
in order. Keep the current grey theme, Dark/Light switch and labelled outline icons.

![Proposed Administration layout with fictional people; not a working screen](/workflow-review/admin-workspace-proposal-20261026.svg)

## What the code already does

| Area | Current behavior | Design consequence |
| --- | --- | --- |
| Access | Only Admin can manage members, read organization activity and use retention controls. | Show the single Administration entry to Admin only; keep server checks. |
| People | Name, Staff/Admin, active/revoked state and versioned changes. List capped at 500. | Start with the people list and an editor beside it. Do not present the capped list as an organization-wide total. |
| New people | Display name and a Microsoft Entra account ID are required; tenant comes from the current organization. | Use **Add person**, not Invite. Explain the account ID requirement in the form; it cannot be silently replaced by email. |
| Activity | Newest first, 100 events per page, bounded offset. Saved actor/target IDs and before/after details. | Readable event summaries, expandable evidence and Newer/Older controls. No invented actor names or unsupported search filters. |
| Storage | Retention settings, holds, quarantine cleanup and protected document deletion. | Separate everyday settings from removal controls. Preserve preview, confirmation, versions, blockers and recovery. |
| First Admin | Private operator setup creates the organization and first Admin. | No public signup, first-visitor promotion or self-service role switch. |

Sources: `apps/web/app/organization/page.tsx`, `audit/page.tsx`,
`retention/page.tsx`, `retention/document-deletion.tsx`, and Infrastructure's
`EnterpriseMembershipService.cs`. Current code uses Staff/Admin; the old Phase 6
contract's separate data-role/admin flag describes an earlier design.

## People & access: first screen

- Heading: **Administration**, with the company name below.
- First tab: **People & access**. One **Add person** button above the list.
- Each row shows **Name · Role · Access · Edit access**. Say **Access removed**
  for a revoked membership; keep its earlier work and record visible.
- Selecting a row opens an editor alongside the list. On a narrow window, it
  moves below the selected person, receives focus and has a clear close action.
- The editor shows the person's name and current role. Choose **Staff** or
  **Admin**. Explain that Admin also manages people and file-removal settings.
- Ordinary role changes use **Save changes**. Promotion uses **Grant Admin access**.
  Removing access retains its explicit confirmation. The final active Admin
  cannot be removed or demoted. Avoid a generic Delete user button.
- Show save errors beside the editor; keep unsaved choices visible. A conflict
  asks the user to reload the changed member before another save. Successful
  writes use the returned version; a lost response requires recovery.

For the first implementation, show the existing loaded member list with its
500-record limit disclosed. If search/paging is added later, it must query the
backend across accessible members; a search over a capped subset must not pretend
to find everyone. Email invitations, directory lookup and global member totals
are separate proposals, absent from this sketch.

## Activity history

Use **What happened · Who · When · Result**. Translate known action codes into
short phrases such as “Access changed”; retain original codes and saved IDs in
**Details**. Use a recorded name only when the data supports it. Unknown actors
remain identifiable without invented names. Show timestamps with an explicit
display timezone and retain UTC in details; use English digits and preserve Arabic.

Keep the existing Newer/Older paging and append-only records. Distinguish no
events from a failed load. Explain that this covers supported system/workflow
events, not every visit or action. Invoice comparison history stays in its existing
workspace; this tab does not become another route to the same invoice screens.

## Document storage

Explain the page as “Choose how long files are kept and protect records.”

1. **How long to keep files:** show the actual saved policy. Blank currently means
   keep indefinitely. Setting a period only flags files for review; it does not
   automatically delete them.
2. **Protect files from removal:** explain a hold in plain language. Keep the
   scope, reason, active holds and explicit release action. Releasing a hold does
   not delete a file or override other blockers.
3. **Removal controls:** clearly separate abandoned-upload cleanup from stored
   document deletion. Preserve inspection, confirmation and history. Explain why
   a file is blocked. Deployment-disabled controls stay disabled.

This is not a new storage-usage dashboard. No invented capacity charts, automatic
deletion, retention defaults or removal of historical documents is proposed.

## Navigation and preview boundaries

Reuse `/organization`, `/organization/audit` and `/organization/retention` so old
bookmarks, refresh and Back still work. Replace the three admin sidebar links
with one Administration entry, active across these routes. Section links provide
the one consistent path between admin tasks; remove redundant footer shortcuts.
Use native links with current-page semantics rather than simulated tab widgets.

The sketch uses Example Trading and fictional people. It is an illustration,
not a screenshot, live account, or working form. A later interactive prototype
must be isolated from the retained app/API and labelled **Sample Admin view**.
It must not change the current Staff identity or enable real grants/removals.
Real Admin integration will need an explicitly prepared test identity/environment.

## Proposed batches after layout approval

| Batch | Deliverable | Acceptance checks |
| --- | --- | --- |
| 1 | Single admin entry, shared header/section navigation, People list/editor. Separate component, hook, service and types as needed. | Staff denied; Admin sees correct section; old routes/Back/refresh work; create/edit/revoke/version-conflict and last-Admin protection retained; no duplicate navigation. Review both themes and narrow width. |
| 2 | Plain-language Activity history with expandable details. | Correct tenant and order; unknown actor fallback; exact saved evidence; paging, error and empty states; Arabic and English digits. |
| 3 | Grouped storage settings, protection holds and removal controls. | Loading/failure never shows a false policy; versions and explicit confirmations retained; blockers and deployment flags honored; retries cannot silently repeat deletion. Use disposable fixtures for destructive-path tests. |

At each meaningful batch: focused tests, independent Progress and Quality Reviewer,
fixes and recheck, VS Code diffs, guide update, then a short manual check. No broad
rebuild is needed for this proposal. The export work and the 52 final acceptance
scenarios remain separate; neither is completed by an admin design change.

## Decision for the user

Review the single Administration entry and the three sections, with People &
access opening first. If this direction is accepted, start with batch 1 only.
The diagram is a layout sketch; final spacing and responsive behavior still
require the implementation's browser checks. No product acceptance is recorded.

## Proposal checks

The Progress and Quality Reviewer checked the brief against the current code and
found no remaining issues. A clarity finding was fixed by showing **Active access**
in the sketch. The SVG was rendered and inspected: no text outside its canvas,
runtime errors or application requests. This checks the illustration only, not
responsive UI, API integration or a real Admin account.

All 1,292 baseline files remain: only three existing documentation files changed,
with this brief and its SVG added. All 284 checked documentation links resolve.
The proposal, picture and workflow-guide diff are open in the existing VS Code
window. No application tests or rebuilds were run for this planning-only batch.
Evidence: `outputs/admin-design-20261026/`. Layout approval remains pending.
