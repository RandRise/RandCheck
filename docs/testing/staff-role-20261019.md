# Staff role — October 19, 2026

## Approval, scope and dependencies

Rand approved merging Viewer, Reviewer and Operator into one everyday role and
delegated the name. Staff uses the existing Operator capabilities: viewing,
uploading/reprocessing/cancelling, checking values, comparisons and decisions.
Administration remains a separate grant. Available downloads remain available;
this batch does not implement the planned reviewed-results export.

Inspection covered frontend permission checks, backend route classification,
membership services, enum/storage constraints, organization setup, role guidance
and preview controls. Operator already includes Viewer and Reviewer powers. Reusing
that contract avoids a database migration or rewriting historical audit strings.
Staff is the product-facing name, not a new API value. Backend creation/update APIs
still accept legacy roles for compatibility. This is one normal account-setup role,
not deletion of the backend role system or a conversion of existing memberships.

New accounts start with Choose document access; Staff and No document access are
the available choices. An existing limited account retains its role in the editor
and can be changed to Staff only through an explicit save. The form explains the
extra rights and labels that action Grant Staff access. Administrator-only and
revoked accounts retain their separate states. Pending-write locks, expected
versions, conflict reloads, revocation confirmation and last-administrator backend
protection remain. No live membership inventory or account migration was performed.

Shared labels and role descriptions are in `security/document-roles.ts`.
English/Arabic guidance and preview account labels are updated. Old restricted
test identities remain available; preview data was not reset or restarted.

## Fresh checks and review

- Web: **93/93**, seven focused files: `organization/membership-save`,
  `organization/staff-access`, `security/document-roles`, `security/security`,
  `security/workspace-guide`, `security/workspace-navigation`,
  `security/session-recovery`. Run Vitest with those files and `--maxWorkers=1`.
- Backend: **21/21** role-matrix cases, using
  `dotnet test apps/backend/tests/UaeDocumentIntelligence.Api.Tests --no-restore --filter FullyQualifiedName~Role_matrix_is_enforced_for_every_route`.
  These inspect all classified routes for the tested permissions; they do not
  establish live identity or real-database membership migration coverage.
- TypeScript `--noEmit --incremental false` and ESLint on the nine affected web
  TypeScript files exit 0. Five documentation sources and four new links pass
  content/link checks. No full application or guide build was run.
- Independent source review and final documentation/preservation recheck found
  no remaining material issue. No specialist was needed.
- The existing preview returns HTTP 200 for the Staff role module and updated
  test-account labels. This is read-only module delivery, not a browser test.

The first web run had two stale role-heading assertions; both were updated to the
new explicit limited-access wording. All other assertions, including denied access,
draft preservation and stale-write behavior, remain. The initial backend launch
hit PowerShell script policy and then a global SDK mismatch. Running the installed
project `.dotnet/dotnet.exe` with its project runtime fixed the launch; no machine
execution policy or runtime configuration was changed. Vitest also emits its
existing advisory about a future Vite config-loader default.

The baseline has 1156 existing file hashes. Captured before copies, the exact batch
diff, failed receipts and current results are retained in the output folder.
Current preservation check: 11 existing files changed, four added, none missing;
all changed existing files have verified before copies.
VS Code reuses the existing window for before/after review. No .NET/Python source,
permissions, database rows or history were changed. Static guide rebuild and
browser/manual acceptance remain pending; source guide checks are recorded
separately. Existing workflow illustrations do not depict role names; the role
section now contains a small Staff/Administration diagram.

## Short manual checks

1. With administration access, open Add member. Document role should offer Staff
   and No document access, with no choice selected. Close without saving if testing
   a real account list.
2. Open an existing Viewer or Reviewer. Its limited access should remain selected.
   Choose Staff and read the added-rights message, then Close to avoid granting it.
3. Staff should see upload and review controls. An existing Viewer should still be
   unable to edit; an administration-only account should still lack document access.
   Use labelled sample accounts for this check; do not reset the sample data.
