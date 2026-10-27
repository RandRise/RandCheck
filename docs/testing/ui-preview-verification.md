# Lightweight preview verification — September 26, 2026

Scope: the new Docker-free preview launcher and synthetic adapter. No production
application, identity implementation, database, or extraction code was changed.

## Verified

- Windows PowerShell launcher starts the one-process preview on loopback port 3410.
  A second start reuses it without discarding sample state. Stop shuts it down;
  restart restores the seed data. Both Windows PowerShell (used by the shortcuts)
  and the current PowerShell shell were exercised.
- The first successful launch reported 15.7 seconds; a warm restart reported
  2.5 seconds. These are readiness timings on this PC, not performance guarantees
  or browser-render timings. The server reported 42–68 MB RSS during the first
  walkthrough; this excludes browser memory and any helper process.
- In-app browser rendered the actual review page with five sample documents.
- Started a review, changed the total from 1155.00 to 1200.00 with a reason, saved,
  and refreshed. The reviewed value and reason remained; the machine value was
  unchanged.
- Edited the preview composition's header while the browser was open. It updated
  automatically without manually reloading or restarting; the saved correction
  remained after the automatic page reload.
- Switched to the fictional administrator and opened the actual membership and
  audit pages. Audit showed the simulated correction event. After stop/restart,
  the audit page was empty, consistent with the documented in-memory lifetime.
- Sign out opened the actual sign-in screen. Its simulated sign-in button returned
  to review without contacting Microsoft.
- Four automated adapter tests passed: review correction/concurrency/completion/
  history; role/origin boundaries and reset/scenarios; comparison/resolution/
  questions/evidence; membership/revocation/retention holds. Run with
  `./.node/node.exe --test tools/ui-preview/fixtures.test.mjs`.
- Server JavaScript syntax check and repository diff whitespace check passed.

## Limits

The initial sandboxed launch failed because esbuild could not read ancestor
directories while resolving installed dependencies. Running the local preview
outside the tool sandbox resolved this. The launcher uses the normal user's
environment when double-clicked; it does not modify machine permissions.

This is a component preview with a browser-navigation shim, injected fictional
identity, and synthetic API. It does not verify real Next routing, server rendering,
Microsoft authentication, backend authorization, durable audit, extraction, or
all comparison rules. No full product regression suite was run for this tooling
change. Existing application defects, including the missing comparison reopen
flow, remain visible and are tracked in the recovery plan.

See the [usage guide](../ui-preview.md) and [recovery plan](../design/reviewer-experience-recovery-plan.md).
