# N03: upload, menu and purchase-order feedback

October 20, 2026. Fixes requested after Rand's manual testing. Acceptance of the
new behavior is still pending.

## Cause and approved changes

The file picker only selected a file; a second Upload button sent it. Choosing a
file now starts the upload. Cancelling the picker does nothing. A normal failed
upload offers Retry upload using the same file and operation key. If the server
accepted the file but saving its selection failed, the file remains accessible
in Documents and no upload retry is offered. A conflict requiring a separate
upload blocks the picker until the user explicitly chooses that action.

Other tools links were inline elements inside a disclosure, so their padded
areas could overlap. A grid wrapper now places them on separate rows. The
disclosure stays a block on narrow screens, with links beneath its heading.

Read-only inspection of the affected retained preview check found an invoice
saved in the PurchaseOrder slot. The server's `preview_error` was a valid type
rejection. The UI had allowed that wrong choice and explained it too late.
Known wrong types are now filtered out of new saved-file choices. A retained
wrong selection stays visible with a clear warning, and continuation is blocked.
Changing the selection is explicit; no file is relabelled or silently replaced.
Unknown types still require backend validation.

## Review and preservation

The separate Progress and Quality Reviewer identified three safety issues:

- The post-upload callback could use an older document-list snapshot. It now
  checks the upload receipt's type before saving the selection.
- A late type result could unmount an open review. The warning now keeps that
  workspace mounted and blocks continuation, preserving unsaved edits.
- A chooser event could bypass a required separate-upload decision. Both the
  disabled picker and upload handler now enforce that gate.

Tests cover those findings. Final independent recheck found no remaining material
issue in this bounded batch. The reviewer independently verified all 435 baseline
files: 13 intended existing changes, no missing or unexpected changes, and 3 added
files. Final source hashes, fresh evidence, documentation and narrow-screen
rendering were checked. This clearance does not replace manual acceptance.
The baseline is `outputs/workflow-fixes-20261020/before`, with hashes and a scoped
diff. No existing data, review, history, permissions, backend policy or useful
tests were deleted. Port 3410 was not restarted or reset. No application build,
deployment or additional persistent service was introduced.

## Verification

Receipts are under `outputs/workflow-fixes-20261020`.

- The initial web selection passed 115/116; one new test asserted phase 2 before
  initial URL restoration finished. It now waits for Show results before checking
  that it is disabled. The focused journey rerun passed 20/20.
- New component tests exercise automatic upload, cancellation, rapid choices,
  uncertain retries without browser hashing, accepted-file selection failure,
  explicit separate uploads and disabled access.
- **22/22 isolated preview checks passed**, including the reported wrong-PO
  rejection without state/review changes, followed by an explicit correct-PO
  selection and successful synthetic comparison.
- The final focused selection passed **116/116 web tests**, with no skips.
  TypeScript and affected ESLint exited 0, with no lint warnings.
- Desktop and 420px browser captures verify one picker per file and no second
  normal Upload button, the early type warning and separate Other tools rows.
  Final measured rectangles place both menu links below Other tools, with a 5px
  gap and no overlap at both widths. Browser Back/Forward/Return and keyboard
  focus also passed, with zero writes to the application origin. Two unrelated
  Windows browser integration requests were counted separately.
- Five documentation files passed 201 local-link checks; the SVG is valid.
  The guide wording and diagram are updated in source. This small documentation
  change did not require rebuilding VitePress. The existing Vite configuration
  advisory remains; it is not a failing test.

Relevant upload, workflow and menu CSS before/after diffs were opened in the
existing VS Code window. Screenshots, receipts and the captured batch diff are
in the output folder.

The isolated comparison uses the preview's v1 simulation, not the real v2 rule
engine. The retained preview still cannot upload/OCR real files or validate live
identity, the v2 backend policy, export or deployment. Automatic upload is covered
by component tests against a controlled API; a real upload remains an integration
check. Earlier N02 and S3 results remain historical evidence.

## Short manual check

1. In a working upload environment, choose an invoice file. It should start
   uploading immediately, with no second Upload click. Cancelling should do nothing.
2. Expand Other tools on a wide and narrow window. Each link should have its own
   row beneath the heading.
3. In the affected preview check, use Change documents. Under Purchase order,
   expand the saved-file choice, select **Completed purchase order.txt**, then
   choose **Use this purchase order**. Check the invoice and PO explicitly before
   Show results. The old invoice file and its review should remain in Documents.

This is a bounded N03 repair within G02/G03; overall integration and manual
acceptance remain open. Other design proposals remain separate.
