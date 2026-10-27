# R4-T02 — Focused document review

September 30, 2026. UI-only change; the shared production page is also served by
the lightweight preview at http://127.0.0.1:3410/.

## Outcome

- Review progress counts extracted values with decisions. Missing placeholders and
  confidence warnings do not incorrectly block completion.
- All fields / Needs review filtering and keyboard-focus navigation make remaining
  work reachable. A disappearing saved row returns focus to the navigation control.
- Original evidence shows every returned page and quote beside the values. Missing
  evidence is explicit; there is no invented page-one fallback.
- Extracted, saved and unsaved values have distinct labels. Confirm keeps the AI
  value; edited values require Save correction. Clear excludes the effective value.
- Saving a field preserves other fields' value/reason drafts and the new-field form.
  Completed reviews cannot silently skip unsaved values, reasons, or form settings.
- Saves lock editing until the response. Stale writes reload the current ETag while
  preserving drafts only for the same document/session/extraction/field identity.
- Failed loads can be retried in place. If another reviewer completes the session,
  remaining local drafts stay visible for copying or discarding, separate from the
  saved decisions. Drafts are in-memory and do not survive a browser reload.

## Verification

- Full UI suite: 166/166 tests across 15 files, serial execution with 15-second
  per-test timeout. Existing document navigation and late-response checks pass.
- Final focused suite: 18/18 after adding initial-load retry and unstarted/no-ETag
  regressions. These two additions followed the full-suite run; all 168 distinct
  UI tests were exercised. Final TypeScript check passes after the retry adjustment.
- Review request adapter and preview fixture suite: 8/8; ETag rotation, stale writes,
  history, completion, permissions and comparison/questions fixtures remain intact.
- Focused ESLint, TypeScript and production Next build pass.
- Independent test author and backend-contract reviewer checked draft scoping,
  completion semantics, source DTOs and history preservation. Review findings about
  completed-elsewhere drafts and failed stale refreshes were fixed and tested.
- Browser: saved one sample field while another retained its edited value and
  reason; completion stayed disabled; discard cleared only the draft; repeated
  navigation moved focus between pending fields. Checked desktop evidence layout
  and stacked layout at 390px without horizontal overflow. Original dark theme and
  viewport restored; temporary tab closed. No preview service restart or Docker.
- Screenshot: `outputs/ui-preview/focused-review.png`.

## Integration boundary

No API contract, database, authentication, permission, review history, or decision
payload changes. The existing If-Match adapter remains in use. Backend semantics
were reviewed; backend tests were not rerun for this UI-only task. The local preview
uses synthetic data and does not establish real identity/backend end-to-end proof.
History-download manual testing remains deferred to Rand. Unrelated Arabic/Phase 7
work is preserved. Next task: R4-T03 comparison/questions with shared context and
pinned-version correctness.
