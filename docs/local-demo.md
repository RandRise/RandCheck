# Disposable local demo

Run the invoice and purchase-order workflow with fictional documents, local
extraction and the existing **Development identity**. No external account or paid
provider is needed. The October 27 source uses the supplier-invoice-v1 profile
(local-1.6.0), with bounded English/Arabic OCR. Current end-to-end acceptance is
recorded in the [completion ledger](tasks/local-completion-20261027.md); configuration
alone does not prove OCR accuracy.
See the [verification report and screenshots](evidence/phase-8/P8-local-demo-20260916.md)
for observed results and host-specific startup limitations.

## Prerequisites and first start

Use Windows with PowerShell **7**, Docker Desktop running Linux containers and Docker
Compose plugin (`docker compose`; tested 5.3.1). Reserve loopback ports **3308**
(browser) and **8308** (API). Initial image,
NuGet, PyPI and npm downloads require internet access and several GB of disk space.
PostgreSQL and Python use an internal Docker network with no published ports.
The API also joins a separate browser-facing bridge with the web server. Only
`127.0.0.1:3308` and `127.0.0.1:8308` are published. The browser/API bridge permits
outbound access; Python has no external route or paid-provider credential.

From the repository root:

```powershell
$demo = ./tools/prepare-local-demo.ps1
Set-Location $demo
./tools/local-demo.ps1 up
```

Open [the local workspace](http://127.0.0.1:3308). Keep `127.0.0.1` in the URL:
the API permits that exact browser origin. The banner explicitly shows Local
development and Development identity. This is a single trusted-machine demo.
Do not put real documents into it or expose it through a proxy/tunnel.

The source copy lives under `outputs/local-demo/<random-id>`. It includes current
uncommitted implementation files, selected with an explicit allowlist, plus the
current supplier invoice/PO samples and retained historical fixtures with their
provenance and license records. `source-inventory.json` records
every copied file's SHA-256. It excludes retained data, private environment files,
Git history, build outputs and existing dependencies. It never exports plain HEAD.
Prepare a new copy to pick up subsequent source changes; existing copies are checked
for source changes before startup. This is a runtime source inventory, not the final
publication inventory for the repository.

Startup builds pinned-version .NET/Node/Python images, starts a fresh PostgreSQL
volume, runs the dedicated migration container, seeds only the fixed Development
organization/actor, then starts the API, local extraction worker and Next.js dev
server. The migrator has a fixed disposable database connection and accepts no
operator-supplied database. Restart reapplies pending migrations idempotently.
Fresh-volume initialization has a bounded 180-second health-check grace period.
TCP readiness can end it early; a Unix-socket-only initialization server cannot.
No host .NET/Node installation is needed for this Docker path. For any host builds
or tests, first run `tools/install-project-runtimes.ps1` and dot-source
`tools/use-project-runtimes.ps1` in that shell.

## Current workflow: add, check, read results

1. Open **Home** and start an invoice check. Upload `english-invoice.pdf` and
   `english-po.pdf` from `samples/supplier-invoice/v0.1.0/` in the source copy.
   Choosing each file uploads it; there is no second upload button. One file alone
   does not create a case. Both accepted documents belong to one check.
2. Wait for extraction. In **Check details**, read each value beside its document
   evidence. Confirm it only when it agrees with the printed document. Correct a
   mistake using the reason choices. Check the invoice, then the purchase order.
3. These samples both total **1260.00 AED** and contain two items. They omit some
   comparison details, such as units and amount bases. Leave information that the
   documents do not establish unconfirmed; never invent values to obtain a pass.
4. Select **See results**. The backend compares those exact saved reviews. Read
   which checks passed, differed or could not be checked. For this incomplete
   sample, **Checks incomplete** is a valid result, even when the totals agree.
5. Record a decision where useful. The decision explains your handling of a
   finding; it does not change missing checks into passed checks.
6. Download a readable **HTML report** or **CSV spreadsheet** from the results.
   Both use the same captured saved version. Reopen the files and inspect amounts,
   evidence, missing checks and decision history. The HTML report can be printed.
7. Use `down` and `up` below. Reopen the saved check: reviewed versions, results
   and decisions must remain. Do not use `reset` when you want to keep this demo.

The `.txt` files in the sample pack test parser helpers. They are not the local
PDF/OCR walkthrough. New reviews use invoice/PO only. Old quotation documents,
reviews and comparisons remain supported as historical data.

## Verification tools and historical evidence

`test-local-demo-guards.ps1` checks ownership and reset refusals on an empty demo.
`verify-local-demo.ps1` is the retained single-invoice upload/review/restart probe;
its `Empty` stage checks an empty initial/reset store. `verify-demo-reconciliation.ps1`
is a **historical Phase 4 three-document probe**, not instructions for creating a
new review. Its old nine-finding expectations must not be used as acceptance of
the current invoice/PO policy. Keep its fixtures and receipts for compatibility.

All scripted confirmations concern known fictional fixtures. They never authorize
automatic confirmation of real documents. Agent-operated browser acceptance and
historical evidence are identified separately in the completion records.

## Stop, diagnose and reset

Run these commands **inside the same isolated copy**:

```powershell
./tools/local-demo.ps1 status
./tools/local-demo.ps1 logs
./tools/local-demo.ps1 health   # Check database health and crash/recovery history.
./tools/local-demo.ps1 down     # Stop/remove this project's containers; retain volumes.
./tools/local-demo.ps1 up       # Restart with persisted uploads and database.
./tools/local-demo.ps1 reset    # Stop and delete ONLY this demo's database/upload volumes.
./tools/local-demo.ps1 up       # Fresh migration and empty document store.
./tools/verify-local-demo.ps1 Empty
./tools/local-demo.ps1 down
```

Reset requires a matching copy-path/Compose ownership marker, checks Docker project
labels and refuses unexpected volumes or mounts. It never runs Docker prune, deletes
host directories, drops an existing host database or touches another Compose project.
The source copy, inventory, logs and journey receipt remain after reset. To run a new
automated journey, prepare a new copy; receipts are not silently overwritten.

If startup fails, inspect the last 100 lines with `logs`, then use `down`. A build
failure is not a successful launch. Check Docker availability, free ports and initial
download access. The migration container must exit zero before the API starts.
Failed extraction is visible in the UI. API responses carry `X-Request-ID`; raw logs
remain private working artifacts. Curate only synthetic evidence into documentation.

Startup also checks the database container's health, restart/OOM status and logs.
An abnormal database process exit or automatic crash recovery fails startup even if
HTTP requests subsequently succeed. `health` repeats this check and preserves dated
receipts and database logs under the copy's `diagnostics/` directory. Normal first-run
initialization shutdown is allowed. This detects failures; it does not establish the
cause of the two earlier unexplained process exits or guarantee future stability.

For repeat cold-start evidence, run `./tools/verify-demo-reliability.ps1` from the
original repository root. It runs three sequential trials, each in a new source
copy with fresh volumes, exercises upload/review, observes database/API health for
at least five minutes, verifies restart persistence and resets only that trial.
Use `-Cycles 2` for two trials. Logs and receipts remain under ignored `outputs/`.
Keep the demo ports free and avoid running container test suites concurrently on
memory-constrained hosts. This is a bounded reliability check, not a load test.

## Boundaries

The demo uses the existing development scanner, fixed identity and disposable database
owner; it is not an enterprise deployment or malware-scanner certification. Next.js
runs in development because the production browser correctly rejects Development
identity. Locked/Entra defaults and enterprise controls are unchanged.
Original-file download requires Entra plus enforced storage, so it is unavailable
in this demo; inspect the supplied sample locally instead. Image builds run
sequentially to reduce peak memory demand; first startup can be slow on an 8 GB host.

The September 16 report above is historical verification of its then-current
source. The current invoice/PO, OCR, local security and clean-source results are
tracked separately in the completion ledger. None establish accuracy on arbitrary
supplier layouts, real Entra integration, production readiness or hosted CI.
Dependency downloads use the repository's current locks/ranges; this is workflow
reproducibility, not a promise of byte-identical future images. Publishing the
repository remains a separate action.
