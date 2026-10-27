# Fast UI preview without Docker

Double-click **Start preview.cmd** in the project folder. It opens
[the preview](http://127.0.0.1:3410) in your browser. Leave it running while testing
changes. Starting it again reopens the same preview without resetting your work.

Double-click **Stop preview.cmd** when finished. You do not need Docker, Python,
.NET, PostgreSQL, or a Microsoft account for this preview.

## What updates automatically

The [archived design proposal](http://127.0.0.1:3410/design-preview) records the
earlier A/B choice. It is linked under **What you can test**, separate from the
current application workflow. Its sample state resets on refresh. The selected
blue-accent design and role guides are already implemented in the actual screens. See its
[verification record](testing/recovery-design-proposal.md).

The preview imports the actual invoice-check, review, comparison, questions, membership, audit,
and retention page components from `apps/web/app`. CSS changes update in place;
component edits automatically reload the page. Saved sample changes survive that
reload. Unsaved form text and page-only selections may reset, so save before editing.

This uses a small Vite development server and synthetic API in one Node process.
It intentionally does not run Next.js server rendering or the real backend.
Changes to the preview server/fixtures or dependencies require stop/start. New
application routes need an entry in the preview's page map. Changes to Next layouts,
middleware, configuration, or server-only components need the real Next environment.

## Short invoice-check checkpoint

Open [Home](http://127.0.0.1:3410/) and choose **Start with my files**, or reopen a saved
check. Choose **Completed invoice** and **Completed purchase order** through the
saved-file choices. Continue to check values, inspect the invoice, then inspect
the purchase order and choose **Show results**. Work stays on the same page.

There is no scope question. A preview-only adapter sends the older sample rules
an Unknown scope so the retained server can return a result without restarting.
The result remains **Synthetic total-only preview**, with missing-check warnings;
it does not run the current invoice/PO policy or establish coverage. Refresh keeps
saved work but requires you to inspect the reviews again before comparison.
Do not reset or restart the preview while you need its saved sample work.
[N04 repair evidence](testing/evaluation-preview-20261020.md).

Earlier S3 steps and results remain historical evidence in the
[S3 verification receipt](testing/supplier-invoice-S3-20261006.md).

## Earlier preview workflows (historical)

These retained steps describe older screens and account labels. New checks use
the invoice/PO flow above; current account choices are Staff and Admin. The
three-document exercise below belongs to earlier quotation work.

1. Open the first invoice, select **Start review**, and correct or confirm fields.
2. Refresh: saved decisions remain. **Reset sample data** restores the starting data
   after confirmation; stop/start also resets it. Nothing is written to the real database.
3. Compare the three documents named **Completed quotation**, **Completed purchase
   order**, and **Completed invoice**. The sample invoice exceeds the order by AED 105.
4. In Questions, choose a named case or add a completed review and ask **What is the total?**
   Reviewed answers are selected by default. After completing a document review,
   **Questions from this review** carries that exact revision. Reopen a question by
   name under **Your saved results**. Other question types deliberately show an
   unsupported/no-evidence result. Semantic search is not simulated.
5. In the preview bar, choose **Administrator + operator**, then **Organization →
   Audit trail** to see simulated changes. Other account choices let you inspect
   reviewer, viewer, administrator-only, revoked, and signed-out screens.
6. Use **Data** to test an empty workspace or unavailable service; return to
   **Sample documents** to recover.

The sign-in button simulates entering the app; it does not contact Microsoft.
Comparison simulates document totals only. Source downloads are synthetic text.
Uploads, reprocessing, OCR, deletion, real permissions, actual audit durability,
and database behavior still need the integration environment. Unsupported API
operations return an explicit error instead of silently reporting success.

All preview tabs share sample data until the server stops. Each tab chooses its own
simulated account. Documents and comparisons reopen from their saved lists and
document/case URLs; saved question results reopen by name or from result URLs. Saved source
versions remain separate from the current composer. These preview behaviors use
synthetic persistence and do not verify the real backend.

## Commands and troubleshooting

From the project folder:

```powershell
./tools/ui-preview.ps1 start
./tools/ui-preview.ps1 status
./tools/ui-preview.ps1 stop
```

Use `start -NoBrowser` to start quietly. The launcher binds only to `127.0.0.1:3410`,
checks readiness, reuses an existing preview for this workspace, and refuses to kill
other services occupying that port. Stop is authenticated with a local ownership
file; it does not stop Docker or other project processes.

The existing project Node runtime and web/docs dependencies must be installed once.
They are already available on the current PC. On a fresh checkout, run the project
runtime installer and select its runtime, then `npm ci --prefix apps/web` and
`npm ci --prefix docs`. Subsequent launches do not reinstall packages.

Logs and temporary ownership/cache files live in `outputs/ui-preview`, which is
ignored by Git. If startup fails, inspect `server.err.log` there. Editing the server
requires a restart; do not edit the ownership file or delete caches while it runs.

To test the synthetic adapter:

```powershell
./.node/node.exe --test tools/ui-preview/fixtures.test.mjs
```

The full [disposable demo](local-demo.md) remains available for integration checks.
Preview success is not a security, extraction, or production-readiness claim.
