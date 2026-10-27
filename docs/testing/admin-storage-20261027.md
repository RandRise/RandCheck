# Document storage verification

October 27, 2026. N18 is part of the delegated local-completion work. This is
agent-executed verification, not new user acceptance or production verification.

## Changed behavior

Settings and protection sit side by side on desktop and stack on small screens.
File status and removal are optional expandable sections. Blank settings keep
stored files indefinitely and disable unfinished-upload cleanup. Saving settings
never starts deletion. Original backend permissions and preservation rules stay.

The independent reviewer found uncertain mutation replies, refresh draft loss and
a last-page boundary regression. Replies now prove the intended mutation before
success is shown. Drafts keep their original version; loading newer settings
requires explicit discard. The last allowed page accepts the backend's
continuation flag while stopping requests beyond offset 10000. `Retained` is
shown as **Keep for now**. Deletion recovery also blocks paging until refreshed.

## Fresh evidence

- 31 web checks: `vitest run app/organization/retention` (3 files).
- Deferred removal checks cover pending request/remount without replay or read-ahead,
  failed recovery staying locked, fresh confirmation and shared parent/child locks.
- Two Node preview-adapter checks: sample version conflict, persistence/isolation and protected-upload cleanup before and after explicit release.
- TypeScript and scoped ESLint pass. An initial React ref-render lint failure was
  fixed with render state; it was not suppressed.
- Eight browser checks: actual components with labelled fictional data; Dark and
  Light desktop/narrow layouts; draft refresh/save; protection/release; keyboard
  expansion; cleanup confirmation; sample reopen; real Staff denial at port 3310.
- Zero network API writes in that browser run. Removal was simulated in a new
  disposable browser profile. Retained documents and the running preview were
  not reset. Initial harness failures (no eligible sample upload and reload
  timing) were corrected before the final passing receipt.

Receipts and captured baseline: `outputs/local-completion-20261027/storage/`.
The real Admin/database storage path remains for the isolated local security
batch. Earlier backend retention/deletion results are historical, not rerun here.
The browser checks do not prove filesystem deletion or production sign-in.

## Short optional manual checks

1. Open **Administration → Document storage**. Switch themes and narrow the window.
   Settings, protections and expandable removal controls should remain readable.
2. Change a setting, then Refresh. Entries must remain. If another Admin changed
   saved settings, choose whether to discard your entries and load those settings.
3. In the fictional preview only, add/release protection and preview cleanup.
   A separate confirmation is required; release does not delete a document.

Sample: <http://127.0.0.1:3430/organization/retention>. Its storage examples remain
in this browser, separate from the sample server's existing People & access data.
