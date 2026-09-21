# Disposable local demo

This Phase 8 increment runs the current application with fictional English invoice
data, local extraction and the existing **Development identity**. No external account
or paid provider is needed. Phase 7 Arabic/bilingual support remains pending.
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
single-invoice and three-document reconciliation fixtures with their provenance
and license records. `source-inventory.json` records
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

## Walkthrough

1. Upload `samples/phase-2/generated/native-two-page-invoice.pdf` from the **copy**.
   This is a fictional, two-page English invoice. The source/license register
   identifies CC0 synthetic content and the included Bitstream Vera font notice.
2. Wait for local extraction and open the document. Inspect extracted fields and
   page evidence. The machine total is `1,050.00`.
3. Start review. Correct total to `1155.00` with a synthetic demonstration reason,
   confirm remaining machine fields and complete review. This deliberately chosen
   correction demonstrates provenance; it is not an accuracy label.
4. Stop and restart using the commands below. Reopen the document and its history:
   completed review and effective total persist; machine total stays `1,050.00`.

For a repeatable HTTP verification, start with an empty demo and run these stages.
Pause between stages to inspect the same real document in the browser:

```powershell
./tools/verify-local-demo.ps1 Empty
./tools/test-local-demo-guards.ps1
./tools/verify-local-demo.ps1 Upload
./tools/verify-local-demo.ps1 Review
./tools/verify-local-demo.ps1 Restart
```

`journey.json` stores the synthetic result and the full pre-restart review workspace.
Restart verification compares that workspace exactly after removing/recreating the
containers. It does not use a database fixture or mocked extraction service.
The guard check temporarily changes the copy's ownership/configuration and creates
one disposable probe volume, verifies that reset refuses each mismatch, then
restores the original bytes and removes its probe. Run it only on an empty demo.

## Reconciliation walkthrough (existing Phase 4 feature)

Use a **fresh demo copy** for this separate walkthrough; do not run the earlier
single-invoice `Upload`/`Review` stages in it. After `up`, run inside the copy:

```powershell
./tools/verify-demo-reconciliation.ps1 Prepare
```

This uploads the three unchanged CC0 synthetic PDFs in
`samples/phase-4/procurement-v0.1.0/`, uses local extraction, confirms the machine
fields and completes their review revisions through the existing API. Automatic
confirmation here prepares a known fictional fixture; it is not a recommendation
to accept machine results without reviewing real documents. Sample hashes are
checked before upload, and partial progress is retained if preparation fails.

1. Open [Reconcile documents](http://127.0.0.1:3308/reconciliation).
2. Create a case named **Synthetic procurement walkthrough**.
3. Select and pin the completed review for each role: `synthetic-quotation-Q-260801.pdf`,
   `synthetic-purchase-order-PO-260801.pdf`, and `synthetic-tax-invoice-SYN-STD-260801.pdf`.
4. Keep the default tolerances and select **Run comparison**. Expect nine findings.
   Supplier, AED currency and the first line match; the second line and totals
   deliberately differ. The interface lists exceptions, so matching fields do not
   produce finding cards.
5. Inspect **AMT-002**: purchase order **8925.00**, invoice **8820.00**, absolute
   difference **105**, tolerance **0.01**, with the source pages shown.
6. Enter the reason below on AMT-002, then select **Accepted**:
   `Synthetic demo: reviewed the AED 105 difference; no payment approval.`
   The original finding stays visible alongside the reasoned decision.
7. Copy the case UUID shown in the case summary and verify the stored result:

```powershell
./tools/verify-demo-reconciliation.ps1 Verify -CaseId '<case UUID>'
```

Verification checks the exact pinned review revisions, all nine expected finding
identities, both source pages/quotes for every finding, the matching fields and
the retained decision on a fresh API read. `reconciliation-journey.json` stores
the result inside this disposable copy. This is one focused demonstration of the
already completed Phase 4 feature, not a repeat of Phase 4 acceptance or an accuracy
claim. The page currently keeps the selected case in memory; refreshing it returns
to case creation, although the case remains stored and can be read by its UUID.

Finish with `./tools/local-demo.ps1 reset` to remove this copy's disposable volumes.
Keep the source copy and receipt if you want to inspect the evidence afterward.

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

The original increment verifies native PDF extraction, review and restart/reset
behavior. The separate reconciliation walkthrough exercises the existing Phase 4
feature with its unchanged synthetic pack. Neither re-accepts OCR accuracy,
Arabic, real-world reconciliation accuracy, cited-answer quality,
real Entra, TLS, production container hardening or CI. Dependency downloads use the
repository's current locks/ranges; this is workflow reproducibility, not a claim that
every future rebuilt image will be byte-identical. Repository license/publication
review and the wider Phase 8 delivery checklist remain separate work.

