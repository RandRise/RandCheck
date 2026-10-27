# Disposable Phase 7 browser fixture

This console tool imports the hash-pinned fictional native PDF extraction, opens
a review, confirms eight header fields and manually adds two source descriptions.
The browser must correct the payment wording, add Decimal prices at line positions
1 and 2 and complete the review through the real API. These prepared actions are
fixture setup, not proof of automatic table extraction or browser upload.

The connection is deliberately fixed to loopback port 55438, database
`phase7_browser`, user `ci`, password `disposable_ci_only`. Start a new disposable
pgvector/PostgreSQL container with these values and temporary storage; never
forward that port to a retained database. The tool applies migrations but does
not drop databases or delete existing documents. It requires an empty document
collection unless `--append-fixture` explicitly requests another fictional case.

From the repository root with verified project runtimes:

```powershell
dotnet run --project tools/phase7-browser-seed/Phase7BrowserSeed.csproj -p:NuGetAudit=false -- "$PWD"
```

Source PDFs must already exist in `output/pdf/phase-7-language-v0.1.0/` with the
recorded hashes. Upload storage is isolated under
`outputs/phase-7/browser-storage/`. The tool writes document/review IDs to
`outputs/phase-7/browser-seed.json`, or `browser-seed-final.json` for an explicitly
appended case. The final audit script reads the latter.

Run the real API at `http://127.0.0.1:5327`, pointing to this database/storage, with
Development security, extraction/storage workers disabled, CORS origin
`http://127.0.0.1:3328`, and the private Python service at port 8328. Start Next
in development on port 3328 with `NEXT_PUBLIC_API_URL=http://127.0.0.1:5327` and
an isolated `NEXT_DIST_DIR`. Both private service sides need the same local-only
worker token. Do not load retained environment files or enable paid providers.

After producing the two saved answers in the browser:

```powershell
./tools/phase7-browser-audit.ps1 -PriceResultId <guid> -PaymentResultId <guid>
```

The audit reads stored history, answers and source quotes through the actual API.
It refuses to overwrite an existing receipt. Stop the owned listeners/container
after verification, and remove only the temporary Next type includes introduced
by the isolated development build.

## Actual upload and worker checks

Use `--schema-only` instead of importing a fixture to initialize only migrations
and the development identity in a fresh disposable database. This mode refuses
a populated document collection and writes no fixture receipt. Upload the
existing fictional PDFs through the browser or HTTP and enable the extraction
worker for this scenario. Point both services to a new isolated upload directory;
the Python container mounts it read-only at `STORAGE_SHARED_ROOT`.

The September 22 run used `outputs/phase-7/e2e-20260922-storage`, the existing
`uaedi-phase7-ocr-v1` image with current `apps/api` mounted read-only, and the
hash-pinned `outputs/phase-7/ara-best-4.1.0.traineddata` mounted as the Arabic
model. Set `OCR_LANGUAGE=eng+ara`, `OCR_ARABIC_MODEL=best`,
`OCR_LAYOUT_PROFILE=lines-v1`, `OMP_THREAD_LIMIT=1`, and the backend
`AiService__AdapterVersion=local-1.5.0`. Keep storage maintenance disabled.

`tools/phase7_e2e_audit.py` reads the resulting upload states, machine fields,
saved answers, histories and reconciliation decision. Supply the actual browser
result IDs and a new output filename; it refuses to overwrite a receipt.
The audit does not prove browser download delivery. See
`docs/evidence/phase-7/P7-upload-workflow-20260922.md` for scope and limitations.
