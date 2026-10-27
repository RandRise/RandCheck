# One workflow page with optional details

October 20, 2026. Rand rejected the previous page/link arrangement and asked for
a visible full process, “Start here”, file upload first, and optional detail pages.
This is the approved refinement; the earlier numbered design is retained as history.

## Design review

The current Home repeats Documents and All checks links already in the sidebar.
Inside a check, top step buttons and bottom Back buttons reach the same content.
Only the active phase is visible, so the user cannot see the whole job.

Use an Operate layout: one vertical sequence with generous separation, plain
instructions and an obvious current action. Reuse the existing light/dark theme.
Do not use more navigation cards, a new dashboard or a new visual theme.

## Flow and batches

1. **Entry and files:** Home says Start here and explains the three phases.
   Remove duplicated in-page library/all-check links. Saved work retains Continue.
   Home is the single saved-check destination; remove the redundant All checks
   sidebar entry and make the brand non-navigating. Historical tools stay in
   an expandable Other tools group with their existing permission checks.
   In the workflow, all phase headings remain visible. Upload is the default;
   choosing a saved document is an expandable alternative in each file slot.
2. **Checking and results:** expand the current phase under its heading. Keep
   completed phases as honest saved summaries. Each inactive phase has at most
   one action to reopen its work. Remove the separate top step-navigation row and
   repeated bottom Back controls. Explicit review decisions/completion stay.
3. **Optional details:** each available phase has one clearly labelled Details
   action. Open a separate reading view with Return to workflow. Store its
   location in the URL (`view=details&details=1|2|3`), retaining the same saved case,
   active step, document and in-memory workspace. This is a separate page view,
   not another required workflow stage. Refresh can reopen it; returning from a
   refresh may require deliberate review reinspection, as before.

Details are read only: file selection/status; current saved document reviews;
saved comparison evidence/history. They never confirm a value, pin a newer
review, record a decision, or advance progress. The main work stays mounted but
hidden while details are open, so a round trip does not clear a checked invoice.
Dirty edits must be saved or explicitly discarded before leaving their phase.
Browser Back/Forward must preserve the same guarantees.
Opening Details while dirty is blocked without discarding the edits. Returning
from Details never discards main-workflow state. Its result selector is local to
that reading view; it must not change the main workflow's selected comparison.

## Acceptance

- All three phases visible together; active work expanded beneath its heading.
- Start/upload guidance, one normal next action, no duplicate page destinations.
- Saved file selection is not labelled a completed review; results are not a match
  when checks are incomplete. Historical results remain clearly historical.
- Details and Return preserve exact case/step/document, checked-review intent and
  ordinary browser history. No writes occur from opening details.
- Existing uncertainty/concurrency/permission/retry/history tests remain valid.
- Independent review, focused web/static checks, rendered desktop/mobile inspection,
  before/after diffs in VS Code, updated guide and a short manual check.

Backend rules, stored data, permissions, export, quotation compatibility and the
running preview's retained data are outside this change. The preview remains
synthetic and cannot validate the new backend comparison policy.
