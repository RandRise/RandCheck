# N10: scale and live integration checks

October 23, 2026. Approved: test a large fictional library, measure the real API,
then exercise the current two-document workflow through the real browser/API.
Extraction and identity are separate verification stages. No production-readiness
or user acceptance claim follows from a simulation.

## Boundaries and acceptance checks

- Preserve retained preview state, saved documents/history, permissions and the
  existing dirty tree. Baseline: 1,259 files captured before edits.
- Use 1,000 and 10,000 fictional entries; return at most 10 files per page. Search
  must find a supplier or printed number outside the initial page. Check filters,
  empty results, phone overflow, errors, response size and timing.
- Measure actual PostgreSQL/API queries separately from browser simulation. Use
  bounded repeated requests; report the machine/sample limits, not an invented SLA.
- Live workflow: actual Next.js and .NET over loopback, an isolated database,
  exact reviewed invoice/PO IDs, actual outcomes and decision history/recovery.
  Development identity is explicitly not real sign-in verification.
- Keep workers and extra services off until needed. Stop owned test processes
  after verification. Do not restart or reset the retained preview.

## Batch 1: large-library simulation

`tools/verification/library-scale.mjs` runs a temporary headless Edge session using
the actual Document library UI. Only that browser tab uses fictional list responses.
The real preview store is never written. The page carries a list-only simulation
label; generated files are not intended to be opened.

Both sizes passed: 10-file pages, Next, supplier search beyond page 1, filtering,
empty results and 1440/420px layouts. No application writes or runtime exceptions.
Maximum response: 2,294 / 2,295 bytes. Five page-operation samples: 63–316 ms for
1,000 entries, 42–86 ms for 10,000; these include CDP polling and warm-up effects.
Supplier search: 96 / 39 ms. In-memory filter maximum: 1.3 / 8.1 ms.
The faster second dataset is not evidence that more data improves performance.

The first harness attempt did not replace the preview's captured request transport
and timed out; the browser-only harness now installs its explicit list transport.
No product behavior was changed to make the simulation pass.

Evidence: `outputs/scale-integration-20261023/library-scale.json` and four PNGs.
Python Playwright remains unavailable; the existing Edge CDP approach was used.

## Batch 2: real database and API

The installed PostgreSQL 16 test cluster on loopback port 55623 hosts the separate
`uaedi_validation_20261023` database. The API uses a restricted database role.
No retained application database or existing user role was changed. The seeder
checks the cluster path, refuses existing initialization and never drops data.
Its refusal was exercised successfully (`seed-refusal.log`). One reviewed pair
contains 2 documents; the other entries have few fields and little review history.
Filenames are fictional: the seeded records do not provide downloadable originals.

Each dataset received 63 sequential GET requests: 7 scenarios, each with a first
request and 8 warm samples. Results below are milliseconds, rounded; they measure
local API/database work, not multi-user load or production performance.

| Scenario | 1,000: first / warm median / warm maximum | 10,000: first / warm median / warm maximum |
| --- | --- | --- |
| First page | 5171 / 61 / 160 | 747 / 248 / 383 |
| Second page | 92 / 43 / 51 | 277 / 225 / 265 |
| Deep page | 36 / 41 / 54 | 229 / 220 / 873 |
| Supplier search | 162 / 62 / 98 | 515 / 406 / 626 |
| Printed number | 51 / 62 / 89 | 390 / 349 / 483 |
| No match | 53 / 53 / 62 | 340 / 345 / 440 |
| Invoice filter | 41 / 36 / 52 | 170 / 195 / 252 |

All responses respected the 10-file limit, expected totals and no-store caching.
The initial cold request took **5.2 seconds**. This needs attention in a later
production-profile measurement; warm results alone do not establish readiness.
Eight extra sequential supplier searches took 3.2 seconds together. The API
process used about 320 MiB private memory and 356 MiB working set at that point;
this excludes PostgreSQL, Next.js and the browser. It is not total system usage.

Evidence: `api-library-1000.json`, `api-library-10000.json`, `api-resources.json`,
`seed-init.log` and `seed-grow.log` under the N10 output directory.

## Batch 3: live browser and backend

The actual Next.js development server (3310) called the actual .NET API (8310),
which saved to the isolated PostgreSQL database. No intercepted responses or
preview adapter were used. Identity was **Development**, not live sign-in.

- The workflow selected the seeded invoice and PO, continued through both
  documents and displayed the backend's saved result on the workflow page.
- Document, extraction and reviewed-version IDs matched the seeded pair exactly.
- The result was **Differences found**: 12 passed, 1 differed, 0 could not be
  checked, 13 total. Compared values and their saved source text were visible.
- A scenario reason was selected and the difference accepted. The decision did
  not change frozen checks, source versions, saved values or counts.
- An old version token received HTTP 412. Reload preserved the result and decision.
- A separate live Document library check passed 10-file paging and supplier search
  beyond page 1 against all 10,000 records. It called the paged library endpoint
  only, made no application writes and had no runtime exceptions.

Evidence: `live-workflow.json`, `live-workflow.png` and `live-library.json`.
`live-library.png` captures only the top of a small viewport; it is not useful as
evidence of the list layout. An optional desktop screenshot attempt failed at
Edge startup (20-second timeout); no desktop screenshot success is claimed.
The harness now requests a desktop viewport for future runs; that added viewport
setting is syntax-checked only. Completed test batches were not repeated.

**Remaining scale issue:** the invoice picker still uses the full
`/api/documents/work-list`. The library's bounded pages do not fix that separate
screen. No product optimization is included in this verification batch.
The initial Next.js workflow compilation took about 46 seconds in development;
this is not a production build timing. Source uploads, source downloads and human
review confirmation were not exercised: the pair's reviews came from test fixtures.

## Batch 4: native PDF extraction, with limits

Two synthetic English PDFs were sent over HTTP to the actual local Python service.
Both returned the expected document role, saved evidence and **Partial** outcome
with a missing row-coverage warning. A partial extraction is not a complete match.
The raw total `1,260.00` and normalized total `1260.00` were checked separately.
The first probe incorrectly expected the normalized spelling in the raw field;
the harness was corrected against the fixture truth and both probes then passed.

133 focused pytest checks passed in 2.40 seconds. These cover supplier extraction,
native PDF fixtures and text/numeric regressions. They do not prove Arabic OCR.
The host has no available Tesseract executable in its configured PATH; pinned OCR
models, scanned Arabic documents and real identity remain unverified. There was
no combined upload → .NET worker → Python → human review run in this batch.

Evidence: `native-pdf-http.json`, `python.xml`, `python.log`. Native test files
remain under `native-inputs/`; no retained documents were reset.

## Commands and reproduction boundaries

Run from the repository root. The already initialized database must be retained;
do not run `init` again to reset it. Seeder commands were run once per size:

```powershell
.dotnet/dotnet.exe run --project tools/verification/ValidationSeed -- init
.dotnet/dotnet.exe run --project tools/verification/ValidationSeed --no-build -- grow
.node/node.exe tools/verification/library-scale.mjs
.node/node.exe tools/verification/measure-library-api.mjs 1000
.node/node.exe tools/verification/measure-library-api.mjs 10000
.node/node.exe tools/verification/live-workflow.mjs
.node/node.exe tools/verification/live-library.mjs
.venv/Scripts/python.exe tools/verification/native-pdf-probe.py
.venv/Scripts/python.exe -m pytest apps/api/tests/test_supplier_invoice_fixture_integration.py apps/api/tests/test_supplier_invoice_extraction.py -q --junitxml=outputs/scale-integration-20261023/python.xml
```

The 1,000-record API receipt predates `grow`; it cannot be reproduced against the
current 10,000-record database without a separately isolated new fixture. The live
workflow tool creates a synthetic check and decision; it is not a read-only probe.
The native probe requires the Python service, synthetic worker token and isolated
storage configured as described in `native-pdf-probe.py`.

To inspect the retained validation database manually, start only the owned cluster
and validation app; these commands do not seed, reset or start extraction workers:

```powershell
& 'C:/Program Files/PostgreSQL/16/bin/pg_ctl.exe' -D outputs/library-search-20261023/postgres -l outputs/scale-integration-20261023/postgres.log -o '-h 127.0.0.1 -p 55623 -c shared_buffers=32MB -c max_connections=25' -w start
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/verification/start-local-validation.ps1
# When done:
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/verification/stop-local-validation.ps1
& 'C:/Program Files/PostgreSQL/16/bin/pg_ctl.exe' -D outputs/library-search-20261023/postgres -m fast -w stop
```

Wait for the web/API to be ready before opening `http://127.0.0.1:3310/documents`.
This is a local Development-identity fixture, not real accounts or production data.
The Next dev server may regenerate `next-env.d.ts` and `tsconfig.json`; inspect
those generated differences against your current baseline before restoring them.
Docker was unavailable. Its older adapter/quotation launcher was not used as
current integration evidence. No dependency or background service was installed.

## Recovery, review and preservation

Conversation/tool interruptions occurred after the completed evidence was saved.
Those completed batches were not rerun. The final live-library check was resumed.
The test launcher scripts were absent on resumption and were restored from their
captured content. A previous cleanup-loop issue with an empty PowerShell child
list was fixed with an explicit empty-list break; actual bounded cleanup is checked.

Only verification tools and documentation belong to N10. Generated Next settings
are restored to captured pre-batch bytes after shutting down the validation server.
The owned API, Next server and test database were stopped successfully. A process
check found no remaining uvicorn service; ports 3310, 8310 and 8010 were unreachable.
All 1,259 baseline files remain. Only the continuation, task and workflow guide
changed; 14 scoped files were added. Both generated Next files match the baseline.
261 documentation links resolve and the new diagram is valid SVG XML. All new
JavaScript tools passed Node syntax checks, both PowerShell scripts parsed without
errors, and critical Ruff checks passed. Diffs were opened in the existing VS Code
window; the complete scoped diff is `n10-before-after.diff`. The initial independent
evidence review and final recheck found no material issue. The reviewer independently
confirmed baseline hashes and database shutdown logs. Their extra process inspection
was permission-denied, so application cleanup relies on the implementation receipts
in `closure.json`; it is not claimed as independently repeated.
The retained preview was not reset; it was unreachable on resumption, so this
record does not claim it is currently running. No preview restart was attempted.

## Short manual check

After starting the isolated validation app above:

1. Open Document library. Expect 10 entries per page. Search **Validation supplier
   09999** and confirm one result. Clear the search and use Next.
2. Open saved check `fdc5dc8f-8ae6-4a33-85f1-030bc76b5b71` from Home's existing
   work. Expect **Differences found**, 12 passed, 1 differed and 0 missing checks.
3. Read the saved decision and reload. The difference must remain. These fictional
   records have no original files to download; do not treat them as upload evidence.

User manual acceptance is pending. Live identity, Arabic/scanned OCR, full upload
and worker integration, reviewed exports and deployment remain separate open work.
