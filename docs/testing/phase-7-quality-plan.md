# Phase 7 quality plan

Keep canonical-text, OCR, reviewed-answer and browser results separate. The
required fixtures in `samples/phase-7/language-v0.2.0/` are deliberately
small development examples. They do not establish general Arabic support.

Latest [actual upload/worker and reconciliation report](../evidence/phase-7/P7-upload-workflow-20260922.md):
English 8/8 and mixed native/scanned 8/9 strict fields, correct assessed numeric
values, saved-answer replay and original citations. Reconciliation RTL and saved
Arabic reasons are visually/API verified. Subsequent-upload polling regression
is fixed. History download delivery is still unverified despite successful API
responses and a new explicit save link. The 34 focused UI tests are not proof of
browser file delivery. Keep these claims separate.

Rand clarified that numeric fields use Western digits and standard date/amount
formats. Arabic-Indic source numeral extraction is optional. The v0.1.0 cases and
failed image receipts remain unchanged; reproduce them by adding
`--dataset samples/phase-7/language-v0.1.0/cases.json` to either evaluator.
Both evaluators now default to v0.2.0. Its text receipt passes 9/9 cases. The
[standard-numeric OCR/RTL report](../evidence/phase-7/P7-standard-numerics-and-rtl-20260921.md)
records the same-image fast/best comparison and remaining mixed-layout failures.
A changed dataset or rendering profile is not an engine improvement.

## Agreed priority after the synthetic OCR refinement

Zero failures on real-world documents is not a required release promise. Report
critical numeric errors separately from text/diacritic mismatches, missing fields,
and extraction failures. A `Partial` result requiring review is not automatically
a failed user workflow. Do not loosen existing exact-match goldens to improve a
score; preserve them and describe the practical impact of each mismatch.

The next evidence is realistic layouts and saved corrections/answers/citations,
not repeated tuning to make a tiny fixture set perfect. The fictional native and
scanned two-page PDF pack in `samples/phase-7/pdf-v0.1.0/` provides a reproducible
workflow regression. Both PDFs share content and cannot estimate real-world
failure rates. They test header/totals/payment fields; table line-item extraction
is not scored. See the [PDF workflow report](../evidence/phase-7/P7-pdf-workflow-20260922.md).

Run its persistence checks only against a newly created disposable database
container. Set `TEST_POSTGRES_CONNECTION` explicitly to that container, never to
a retained database: the fixture drops/recreates `document_intelligence_test`.
Set `PHASE5_PYTHON` to the project Python executable, then run:

```powershell
dotnet test apps/backend/tests/UaeDocumentIntelligence.Persistence.Tests/UaeDocumentIntelligence.Persistence.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~Arabic_pdf_review_answers_and_original_evidence_survive_reopening
```

This checks persisted application behavior using frozen real extraction, not
HTTP upload, worker ingestion, browser rendering or semantic retrieval quality.

The subsequent [saved browser and line-price check](../evidence/phase-7/P7-browser-and-line-prices-20260922.md)
uses the real frontend/API/database with the guarded seeder in
`tools/phase7-browser-seed/`. The new missing-field form supports numeric types
and row positions; line fields retain the extractor's one-based indices. The
browser audit verifies saved history, added prices without invented quotations,
Arabic payment corrections and original page evidence. It does not replay upload
or extraction workers. History download delivery and reconciliation RTL are still
separate open checks. Ordinary tables currently score 0/6 structured fields in
both frozen PDF forms; do not mistake the manual price fallback for OCR coverage.

## Reproduction

From the repository root, using the existing Python environment:

```powershell
.venv/Scripts/python.exe tools/phase7_evaluate.py --output outputs/phase-7/text-new.json
.venv/Scripts/python.exe -m pytest apps/api/tests/test_arabic_text.py apps/api/tests/test_local_extraction.py apps/api/tests/test_due_date_extraction.py -q
.venv/Scripts/python.exe tools/run_python_regression.py --group native
. tools/use-project-runtimes.ps1
dotnet test apps/backend/tests/UaeDocumentIntelligence.Application.Tests/UaeDocumentIntelligence.Application.Tests.csproj --no-restore -p:NuGetAudit=false --filter FullyQualifiedName~CitedTypedFactsTests
.node/node.exe apps/web/node_modules/vitest/vitest.mjs run --root apps/web --maxWorkers 1 --no-file-parallelism
```

The ordinary Python command skips the explicitly gated native/OCR checks. The
native runner covers its one test separately. Earlier English OCR goldens are
not part of the new 36-case comparison and must not be counted as freshly passed.

For image evaluation, build the service and derive the font/model evaluation
image. The recorded first run reused the September 15 verified service image;
a fresh build has its own dependency/image identity and needs a new receipt.

```powershell
docker build -f apps/api/Dockerfile -t uaedi-phase7-service .
docker build -f tools/phase7-ocr.Dockerfile --build-arg RUNTIME_IMAGE=uaedi-phase7-service -t uaedi-phase7-ocr .
New-Item -ItemType Directory -Force outputs/phase-7
docker run --rm --network none --mount "type=bind,source=$((Get-Location).Path),target=/workspace,readonly" --mount "type=bind,source=$((Get-Location).Path)/outputs/phase-7,target=/evidence" --workdir /workspace -e PYTHONDONTWRITEBYTECODE=1 uaedi-phase7-ocr python tools/phase7_ocr_evaluate.py --output-dir /evidence/ocr-new
```

No database, provider key or service port is needed. Both evaluators refuse to
overwrite evidence. Review PNGs visually before drawing conclusions from OCR.
The comparison reports failures rather than throwing solely because accuracy is
poor; an output report is not a passing acceptance receipt.

The current renderer defaults to `--render-profile isolated-v3`, preserving whole
Latin phrases as well as numeric runs. Use `--render-profile isolated-v2` for
the September 21 comparison. Add
`--render-profile legacy-v1` to reproduce historical RTL date ordering; it is not
an acceptable source for new ISO-date accuracy claims. Each report records its
profile, dataset, font and image hashes. Use `OMP_THREAD_LIMIT=1` on this host.

For the candidate comparison, download the official 4.1.0 `ara.traineddata` from
`https://raw.githubusercontent.com/tesseract-ocr/tessdata_best/4.1.0/ara.traineddata`
into a local ignored output path. Verify the SHA-256 from the report above, mount
it read-only at `/usr/share/tesseract-ocr/5/tessdata/ara.traineddata`, and add
`--arabic-model best` with a new output directory. The extractor now supports
the best pin as an explicit opt-in and records its identity in provider metadata;
the English/fast/block defaults remain unchanged.

For the September 22 targeted refinement, add `--layout-profile lines-v1 --modes eng+ara`.
Use `--dataset samples/phase-7/language-v0.3.0/cases.json` to include the three
additional cases. The first nine source/expected cases are unchanged. The report
scores actual pipeline fields and warnings, not a second parsing of its output.
Retain failures and report rendering changes separately from OCR changes.

The equivalent application settings are `OCR_LANGUAGE=eng+ara`,
`OCR_ARABIC_MODEL=best`, `OCR_LAYOUT_PROFILE=lines-v1`, with the same verified
model mount. A default image contains the fast model and will reject the best
setting unless the exact best file is supplied. Arabic OCR still requires review.

For frontend-only RTL reproduction, run `.node/node.exe tools/phase7_workspace_preview.mjs`,
then launch Next on `127.0.0.1:3327` with
`NEXT_PUBLIC_API_URL=http://127.0.0.1:8327`, `AUTH_MODE=Development`, and
`NEXT_DIST_DIR=.next-phase7-rtl`. The adapter supplies independent synthetic
review and citation fixtures. Do not count its responses as extraction, persisted
history, authorization, or answer-engine acceptance. Use the real browser to
inspect Arabic corrections and ISO dates in highlighted canonical evidence.

## Acceptance still required

All raw quotes must match UTF-8 source slices. Model pins and missing-model failures
must be tested. Arabic OCR remains explicitly experimental and produces degraded
review warnings even with high engine confidence. Corrected numeric review values
must satisfy the existing typed-fact format; arbitrary Arabic prose is not coerced
into numbers. Confirmed machine facts use their normalized values while original
page evidence stays unchanged.

Before claiming Phase 7 complete: independently evaluate standard Western numeric
fields with Arabic labels/text and mixed reading order; add native/multipage and realistic layout coverage;
visually demonstrate the real review/citation UI; evaluate multilingual embeddings
and cross-language semantic retrieval separately. Do not infer embedding quality
from the bounded Arabic question grammar or spend provider funds implicitly.
