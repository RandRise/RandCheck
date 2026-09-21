# UAE Enterprise Document Intelligence Platform

**Delivery target:** an independent GitHub portfolio project with a reproducible
local demo, documented architecture, security tests and screenshots. No domain or
cloud account is required for completion. The [portfolio delivery plan](docs/design/github-portfolio-delivery.md)
tracks the remaining work; hosted deployment is optional.

**Phase 6 local security complete — September 16, 2026:** [Acceptance evidence](docs/evidence/phase-6/P6-local-acceptance-20260916.md) covers membership, roles, tenant isolation, audit, protected sources and local recovery. The default remains Locked. Real Entra and production infrastructure remain unverified.

**Phase 8 local demo and CI:** the [disposable demo guide](docs/local-demo.md) provides current-source isolation, loopback startup, migrations, shutdown and guarded reset. See [verification and screenshots](docs/evidence/phase-8/P8-local-demo-20260916.md) for the English invoice journey. Local CI passes 1,780 tests plus builds, lint, migrations and scoped dependency audits; see the [CI guide](docs/testing/ci.md) and [verification report](docs/evidence/phase-8/P8-ci-local-20260916.md). Phase 7 Arabic support, hosted CI proof and publication review remain pending.

**September 19 priority follow-up:** two fresh demo trials pass upload, extraction,
review, restart persistence and safe reset. The updated source passes 1,785 tests
and its builds; its combined run was interrupted during dependency audits, so it
is not a new all-green CI result. See the [follow-up report](docs/evidence/phase-8/P8-priority-followup-20260917.md).

**September 21 reconciliation walkthrough:** the existing Phase 4 feature is now
demonstrated with three synthetic documents, nine expected discrepancies and a
retained reviewer decision. See the [walkthrough and screenshots](docs/evidence/phase-8/P8-reconciliation-demo-20260921.md).

**September 11, 2026:** [Phase 5 development acceptance](docs/evidence/phase-5/P5-development-acceptance-20260911.md) is complete for bounded reviewed answers, case selection and prepared-query semantic evidence search. Full regression and snapshots are recorded; optional model answers remain disabled. Earlier implementation bullets below are historical checkpoints.

This is a portfolio-grade document review and reconciliation platform for UAE businesses. Its enterprise API is ASP.NET Core, its private AI service is Python/FastAPI, and its review workspace is Next.js. The product name is intentionally undecided while Phase 0 is under review.

## Review the plan first

The product plan is a local VitePress site sourced entirely from Markdown:

```powershell
./tools/install-project-runtimes.ps1
. ./tools/use-project-runtimes.ps1
cd docs
npm ci
npm run dev
```

The planning site covers the [product specification](docs/product-spec.md), [delivery phases](docs/phases/index.md), [roadmap](docs/roadmap.md), [architecture](docs/architecture.md), and [decision log](docs/decisions.md). Phase status does not authorize implementation by itself.

## Current verified foundation

- Upload PDF, PNG, JPEG, or text documents (10 MB limit)
- ASP.NET modular monolith with Domain, Application, Infrastructure, and API boundaries
- EF Core Code First model with verified Npgsql migrations
- Tenant-ready document, extraction-run, evidence, and review-history schema
- Verified mock and local native/OCR providers behind the private Python provider interface
- Explicit API DTOs preserving the document-review UI contract
- Append-only human review with correction, confirmation, addition, clearing, completion, reload, and ETag concurrency
- Next.js review UI that keeps machine and effective reviewed values visibly separate
- P5-T06/T08 prompt preflight: complete versioned application prompt, measured-count receipt and reservation/dispatch binding; 362 non-persistence .NET, 40 targeted PostgreSQL and 66 Python cited checks passed. Counter units are synthetic bytes, not verified GPT tokens; paid generation remains disabled ([verification](docs/evidence/phase-5/P5-T06-T08-prompt-preflight-verification.md)).
- P5-T06/T08 internal answer bridge: ledger-to-Python fake-provider integration, committed dispatch protection, synthetic accounting and citation checks without publication; 435 .NET tests and 51 Python cited tests passed. Live generation remains disabled ([verification](docs/evidence/phase-5/P5-T06-T08-answer-bridge-verification.md)).
- P5-T08 durable spending-ledger component: shared USD 8 pilot allowance, atomic reservation/dispatch, duplicate protection and uncertain-cost holds; 411 .NET tests passed including 64 PostgreSQL tests. Paid dispatch remains disconnected ([verification](docs/evidence/phase-5/P5-T08-spending-ledger-verification.md)).
- P5-T08 provider-free checkpoint: 331 passing .NET tests (282 non-persistence and 49 PostgreSQL), 20 web checks, and one real-stack synthetic native-PDF browser flow including quarantine/history denial; semantic answer validation and full Phase 5 acceptance remain outstanding ([verification](docs/evidence/phase-5/P5-T08-security-verification.md)).
- P5-T08 request-boundary increment: 64 KiB cited-body limit, bounded streaming reads, safe JSON failures and rejection-log privacy tests; 307 non-persistence .NET tests passed (25 new API cases). Invoice uploads remain at 10 MiB. PostgreSQL/web results above were not rerun for this increment ([verification](docs/evidence/phase-5/P5-T08-request-boundary-verification.md)).
- P5-T08 workflow-log increment: private-client safe telemetry, generic unexpected errors, and successful/replayed/history/evidence payload privacy checks; 323 non-persistence .NET tests passed. Real database logging and deployed metadata/sink privacy remain unverified ([verification](docs/evidence/phase-5/P5-T08-workflow-log-verification.md)).
- P5-T08 live database-log increment: EF error/exception privacy guard and real PostgreSQL/Python workflow checks; 385 .NET tests passed (333 non-persistence, 52 PostgreSQL). PostgreSQL server logs still contain synthetic error values, so server-log deployment policy remains an open gate ([verification](docs/evidence/phase-5/P5-T08-database-log-verification.md)).
- Python discovery: all 110 tests pass across their declared pinned runtimes, including the local OCR runtime and the intentionally separate Pillow 9.4 fixture-generator runtime
- Deterministic synthetic Phase 2 fixtures with versioned labels, source/license records, and byte-for-byte container reproducibility; these are contract fixtures, not accuracy evidence ([P2-T03 verification](docs/evidence/phase-2/P2-T03-synthetic-fixture-verification.md))
- Zero-warning/error .NET Release build, no pending EF model changes, valid Compose configuration, and passing Next.js tests/lint/build
- Reproduced review result: machine total `1050.00` unchanged, effective total `1155.00`, added `payment_terms = Net 30`, completion and reload persisted
- Phase 3 offline evaluator and comparator with versioned schemas, exact-match/precision/recall/correction/failure/latency/cost metrics, field/layout/scan/provider breakdowns, and configurable regression thresholds
- Phase 4 reconciliation workspace and backend with reviewed-source pinning, immutable runs, explainable header/line rules, configurable tolerances, page evidence, and append-only finding resolutions
- Phase 5 provider-independent cited-question persistence: frozen indexes/corpora, exact page spans, ranked hits, claims, citations, and refusal history; embedding storage and semantic answer orchestration remain outstanding ([P5-T03 verification](docs/evidence/phase-5/P5-T03-persistence-verification.md))
- Phase 5 deterministic Python chunking, ASP.NET source reconstruction, stable citation-span IDs, and disabled-by-default embedding interface; the synthetic pack retains all 46 evidence labels within chunks ([chunking contract](docs/design/phase-5-chunking-contract.md)).
- Phase 5 on-demand index/evidence APIs and English PostgreSQL keyword retrieval: 45/46 labelled evidence items and all evidence for 23/24 answerable questions retrieved in the top five; tiny synthetic corpus only, no generated answers or vector/hybrid comparison ([lexical contract](docs/design/phase-5-lexical-retrieval-contract.md)).
- Phase 5 strict citation-proposal validation and provider-free no-hit question/refusal API. Structural citation checks do not establish semantic claim support, and nonempty evidence does not automatically become an answer ([validation checkpoint](docs/evidence/phase-5/P5-T06-validation-verification.md)).
- Phase 5 `/questions` evidence workspace with completed-review pinning, saved-result reload, visible refusal, and authorized canonical-text highlighting; no generated answers or PDF image viewer ([workspace checkpoint](docs/evidence/phase-5/P5-T07-workspace-verification.md)).
- Versioned synthetic UAE quotation/PO/invoice pack and live-stack acceptance: 3 completed reviews, 9/9 exact expected findings, and a retained reasoned resolution; these are development results, not real-world accuracy claims

### P2-T06 durable workflow

Migration `20260811131151_AddDurableExtractionQueue`, asynchronous `202` upload, PostgreSQL `extraction_jobs`, the .NET worker, authenticated private Python route, scoped storage/cleanup, fencing/cancellation/reprocess primitives, and Activity/Meter hooks are complete. Real local Compose demonstrations reached `Ready` for one synthetic native PDF and one synthetic scanned PDF. See the [P2-T06 verification](docs/evidence/phase-2/P2-T06-queue-worker-verification.md).

## Quick start

Use the [disposable local demo](docs/local-demo.md) for the Phase 8 walkthrough.
It includes the current source, automatic migrations, local PDF extraction, review,
restart persistence and safe reset, with browser/API ports bound to loopback.
Windows PowerShell 7 and Docker Desktop (Linux containers) are required:

```powershell
$demo = ./tools/prepare-local-demo.ps1
Set-Location $demo
./tools/local-demo.ps1 up
```

Open [the local workspace](http://127.0.0.1:3308). Use only the included synthetic
PDF. The guide covers verification, shutdown and reset. The following manual setup
is retained as historical developer context; the default Compose file is not the
isolated demo profile.

Prerequisites: the [reviewed runtime baseline](docs/design/phase-6-runtime-baseline.md)
(.NET SDK 8.0.425 / runtime 8.0.31, Node 24.21.0, and the Python 3.11.16 service image),
plus Docker Desktop. On Windows, run the bootstrap and dot-source the runtime selector
above in every build/test shell. System runtime installations are not replaced.

PostgreSQL is exposed on host port `55432`. See the [local setup guide](docs/setup.md) for the complete port map, development-seed behavior, and limitations.

Start PostgreSQL and apply the Code First migration:

```powershell
docker compose up -d db
$env:MIGRATION_DATABASE_URL='Host=localhost;Port=55432;Database=document_intelligence;Username=document_intelligence;Password=local_development_only'
dotnet ef database update --project apps/backend/src/UaeDocumentIntelligence.Infrastructure --startup-project apps/backend/src/UaeDocumentIntelligence.Api
```

### Inspect PostgreSQL with DBeaver

Create a PostgreSQL connection using these development-only values:

| Setting | Value |
| --- | --- |
| Host | `localhost` |
| Port | `55432` |
| Database | `document_intelligence` |
| Username | `document_intelligence` |
| Password | `local_development_only` |
| Schema | `public` |

After migrations run, refresh **Schemas → public → Tables** in DBeaver. Useful tables include `documents`, `extraction_runs`, `machine_field_values`, `review_sessions`, `review_actions`, and `__EFMigrationsHistory`. These credentials are strictly for local development and must not be reused in deployed environments.

Start the private AI service:

```powershell
Copy-Item .env.example .env
python -m venv .venv
.venv\Scripts\Activate.ps1
pip install -e ".[dev]"
uvicorn app.main:app --app-dir apps/api --reload --port 8001
```

In a second terminal:

```powershell
dotnet run --project apps/backend/src/UaeDocumentIntelligence.Api
```

In a third terminal:

```powershell
cd apps/web
npm install
npm run dev
```

Open `http://localhost:3000`. ASP.NET API docs are shown at its `/swagger` URL in development; private AI docs are at `http://localhost:8001/docs`.

After the database is migrated, the application services are configured for Compose:

```powershell
docker compose up --build
```

`docker compose config --quiet`, the current AI/backend image builds, and synthetic native/scanned end-to-end flows are verified. These local synthetic demonstrations are not production or accuracy claims.

## Verification

```powershell
python -m unittest discover -s apps/api/tests
dotnet test apps/backend/UaeDocumentIntelligence.sln
cd apps/web
npm test
npm run lint
npm run build
cd ../../docs
npm run build
```

The completed Phase 1 result is recorded in the [P1-T08 acceptance verification](docs/evidence/phase-1/P1-T08-acceptance-verification.md), with the earlier database checkpoint preserved in [P1-T04 evidence](docs/evidence/phase-1/P1-T04-database-verification.md). The completed local Phase 2 outcome is recorded in [P2-T08 acceptance verification](docs/evidence/phase-2/P2-T08-acceptance-verification.md), with durable workflow details in [P2-T06 verification](docs/evidence/phase-2/P2-T06-queue-worker-verification.md).

Phase 3 is complete for the approved development-only boundary. The live
Phase 2→Phase 3 bridge, generated UAE benchmark comparison,
review cues, action analytics, and history export are recorded in the
[Phase 3 development acceptance verification](docs/evidence/phase-3/P3-T07-development-acceptance-verification.md).

Phase 4 has passed its technical acceptance gate. The reconciliation workspace,
synthetic procurement truth set, split-runtime regression matrix, and live API
scenario are recorded in the
[Phase 4 acceptance verification](docs/evidence/phase-4/P4-T06-T08-acceptance-verification.md).
The roadmap remains in progress until Rand formally approves phase completion.
All reported UAE-pack metrics are synthetic regression results, not real-world
extraction-accuracy claims.

See the [product specification](docs/product-spec.md), [architecture](docs/architecture.md), and [roadmap](docs/roadmap.md).

## Data and claims

The bundled documents are synthetic and contain no personal or client data. Current fixtures verify deterministic contracts and failure behavior; they do not prove production English OCR or Arabic/bilingual accuracy. Authentication and tenant-aware authorization, production OCR, Arabic support, RAG, reconciliation, cloud deployment, client usage, extraction accuracy, time saved, and financial impact are not claimed.
