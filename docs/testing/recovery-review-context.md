# R1-T02 — Document response ownership

September 26, 2026. Corrective task inserted before visual work following the
[baseline audit](recovery-baseline.md).

## Change

Every document selection now has a generation. Review, processing, history and
mutation responses can update the visible workspace only while they belong to
that selection. A return from A to B to A is a new visit, not ownership by an old A
request. Switching clears the previous document's drafts/reasons/history link.
Concurrent review writes are prevented while one is pending on that selection.

A completed mutation invalidates background reads that may contain pre-commit
values, protecting both the saved value and its ETag. An upload that succeeds after
a selection change still updates the queue and resets its submitted form, while
preserving the user's newly selected document. Server-side mutations are not
cancelled or replayed just because the user changes the visible document.

## Tests and review

- Independent test author reproduced eight response races against the original
  implementation, with assertion failures. All eight passed after the first fix.
- Independent code reviewer found two integration problems: background uploads
  disappeared from the queue, and same-document refreshes could replace a committed
  correction. Both were fixed and two more tests added. All ten focused tests pass.
- Independent second review found no remaining blocker in the scoped changes.
- Existing document-page tests: 9 passed. TypeScript passed. Focused ESLint passed
  without warnings after cleanup. Full UI regression result is recorded in the ledger.
- Browser checked the actual-component preview: English and Arabic document
  selection renders the corresponding fields and values. The earlier empty-data
  preview scenario was changed temporarily for this check; sample records were not reset.

## Integration impact

| Area | Disposition |
| --- | --- |
| HTTP endpoints, mutation payloads, roles and tenant enforcement | Unchanged; no backend/identity code edited. |
| ETags and stale-write recovery | Verified with delayed-response tests, including next request using the committed ETag and no reload of an abandoned review on 412. |
| Review start/correct/complete | Verified while switching documents; old messages/responses cannot disable or overwrite the new selection. |
| Upload and processing | UI response coordination changed; tests verify queue refresh preserves selected details, stale processing is ignored, and native form reset is invoked after successful upload. Actual worker/OCR code and upload body are unchanged. |
| Original evidence and audit history | No server contract or record changes; mutation attribution still belongs to its original document. History download response can no longer attach to an abandoned selection. |
| Arabic and mixed text | Existing content/direction helpers preserved; regression suite and browser selection check cover rendering. |
| Production routing and persistence | No route/schema changes. No database migration required. |

No full Docker/OCR or real Entra run was needed for this frontend-only correction.
The jsdom upload test verifies the native reset call because user-event replaces
file-input properties; actual browser file delivery remains the separate historical
limitation in the continuation notes. This task does not claim to resolve it.
