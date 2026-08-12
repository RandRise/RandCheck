# UAE Enterprise Document Intelligence Platform

This is a portfolio-grade document review and reconciliation platform for UAE businesses. Its enterprise API is ASP.NET Core, its private AI service is Python/FastAPI, and its review workspace is Next.js. The product name is intentionally undecided while Phase 0 is under review.

## Review the plan first

The product plan is a local VitePress site sourced entirely from Markdown:

```powershell
cd docs
npm install
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
- 184 passing .NET tests: 81 domain, 18 application, 36 infrastructure, 29 PostgreSQL persistence, 4 architecture, and 16 API
- Python discovery: 71 total, with 66 passed and 5 expected host runtime-gated skips; those 5/5 pass in the pinned Docker runtime
- Deterministic synthetic Phase 2 fixtures with versioned labels, source/license records, and byte-for-byte container reproducibility; these are contract fixtures, not accuracy evidence ([P2-T03 verification](docs/evidence/phase-2/P2-T03-synthetic-fixture-verification.md))
- Zero-warning/error .NET Release build, no pending EF model changes, valid Compose configuration, and passing Next.js tests/lint/build
- Reproduced review result: machine total `1050.00` unchanged, effective total `1155.00`, added `payment_terms = Net 30`, completion and reload persisted

### P2-T06 durable workflow

Migration `20260811131151_AddDurableExtractionQueue`, asynchronous `202` upload, PostgreSQL `extraction_jobs`, the .NET worker, authenticated private Python route, scoped storage/cleanup, fencing/cancellation/reprocess primitives, and Activity/Meter hooks are complete. Real local Compose demonstrations reached `Ready` for one synthetic native PDF and one synthetic scanned PDF. See the [P2-T06 verification](docs/evidence/phase-2/P2-T06-queue-worker-verification.md).

## Quick start

Prerequisites: Python 3.11+, .NET 8, Node.js 20+, and Docker Desktop.

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

See the [product specification](docs/product-spec.md), [architecture](docs/architecture.md), and [roadmap](docs/roadmap.md).

## Data and claims

The bundled documents are synthetic and contain no personal or client data. Current fixtures verify deterministic contracts and failure behavior; they do not prove production English OCR or Arabic/bilingual accuracy. Authentication and tenant-aware authorization, production OCR, Arabic support, RAG, reconciliation, cloud deployment, client usage, extraction accuracy, time saved, and financial impact are not claimed.
