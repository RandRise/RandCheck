# Local development setup

The verified local stack uses PostgreSQL, ASP.NET, the private Python mock/local providers, and Next.js without paid credentials. P2-T06 completes the durable queue/worker path for covered synthetic native/scanned invoices.

## Prerequisites

- Docker Desktop
- .NET 8 SDK with the `dotnet-ef` tool
- Python 3.11 or newer
- Node.js 20 or newer

## Local ports

| Service | URL or port |
| --- | --- |
| Next.js | `http://localhost:3000` |
| ASP.NET API | `http://localhost:8080` |
| Python/FastAPI | `http://localhost:8001` |
| PostgreSQL | `localhost:55432` |

## 1. Configure and migrate PostgreSQL

From the repository root:

```powershell
Copy-Item .env.example .env
docker compose up -d db
$env:MIGRATION_DATABASE_URL='Host=localhost;Port=55432;Database=document_intelligence;Username=document_intelligence;Password=local_development_only'
dotnet ef database update --project apps/backend/src/UaeDocumentIntelligence.Infrastructure --startup-project apps/backend/src/UaeDocumentIntelligence.Api
```

The application does not run `EnsureCreated`. EF Core Code First migrations own persistent schema changes.

## Inspect the database with DBeaver

Create a PostgreSQL connection with these local-development values:

| Setting | Value |
| --- | --- |
| Host | `localhost` |
| Port | `55432` |
| Database | `document_intelligence` |
| Username | `document_intelligence` |
| Password | `local_development_only` |
| Schema | `public` |

After connecting, expand the database and refresh **Schemas → public → Tables**. If the tables are not visible immediately after a migration, right-click **public** or **Tables** and choose **Refresh**.

Useful tables:

- `documents`
- `extraction_runs`
- `extraction_jobs`
- `extraction_pages`
- `machine_field_values`
- `field_evidence`
- `extraction_warnings`
- `review_sessions`
- `review_actions`
- `__EFMigrationsHistory`

These credentials are committed solely for local development. Do not reuse them for a deployed database.

## 2. Start the private AI service

```powershell
python -m venv .venv
.venv\Scripts\Activate.ps1
pip install -e ".[dev]"
uvicorn app.main:app --app-dir apps/api --reload --port 8001
```

The mock provider is deterministic. The pinned local provider processes synthetic native/scanned English fixtures; host discovery intentionally skips five runtime-gated tests unless that OCR runtime is present. Azure credentials are not required.

## 3. Start the ASP.NET API

In a second terminal:

```powershell
dotnet run --project apps/backend/src/UaeDocumentIntelligence.Api
```

Development configuration seeds one fixed local organization and system actor. This provides attribution for local workflows; it is not authentication or tenant authorization.

The example environment keeps `ExtractionWorker__Enabled=false` and `StorageMaintenance__Enabled=false` for explicit host control. P2-T06 requires the same development secret in `AiService__WorkerToken` and `WORKER_SHARED_SECRET`, a shared upload root, and migration `20260811131151_AddDurableExtractionQueue`. Do not enable the worker against an unmigrated database.

## 4. Start the Next.js workspace

In a third terminal:

```powershell
cd apps/web
npm install
npm run dev
```

Open `http://localhost:3000`. Verified P2-T06 fixtures include `samples/phase-2/generated/native-two-page-invoice.pdf` and `samples/phase-2/generated/scanned-two-page-invoice-300dpi.pdf`.

## Compose alternative

After the database migration has been applied:

```powershell
docker compose up --build
```

`docker compose config --quiet`, the current AI/backend image builds, and synthetic native/scanned Compose workflows pass. See [P2-T06 verification](./evidence/phase-2/P2-T06-queue-worker-verification.md) for exact image identities and results.

## Verification commands

```powershell
dotnet build apps/backend/UaeDocumentIntelligence.sln -c Release --no-restore
dotnet test apps/backend/UaeDocumentIntelligence.sln
python -m unittest discover -s apps/api/tests -v
docker compose config --quiet
$env:MIGRATION_DATABASE_URL='Host=localhost;Port=55432;Database=document_intelligence;Username=document_intelligence;Password=local_development_only'
dotnet ef migrations has-pending-model-changes --project apps/backend/src/UaeDocumentIntelligence.Infrastructure --startup-project apps/backend/src/UaeDocumentIntelligence.Api
cd apps/web
npm test
npm run lint
npm run build
cd ../../docs
npm run build
```

See the [P1-T08 acceptance verification](./evidence/phase-1/P1-T08-acceptance-verification.md) for the final Phase 1 results and limitations.

See [P2-T06 verification](./evidence/phase-2/P2-T06-queue-worker-verification.md) for final queue/worker totals and honest local limitations.
