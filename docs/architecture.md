# Architecture

This page separates the **current prototype** from the **target portfolio architecture**. Planned components should not be mistaken for implemented capabilities.

## Current implemented foundation

```text
Browser / Next.js ---> ASP.NET API ---> Application ---> Domain
                           |                 ^
                           v                 |
                     Infrastructure --------+
                       |          |
                       v          v
                 PostgreSQL   Python/FastAPI
                  :55432       mock extractor
```

The .NET solution now has four production projects:

| Project | Implemented responsibility |
| --- | --- |
| Domain | Document, extraction, evidence, and review entities plus invariants |
| Application | Feature use cases, DTO projection, queries, and dependency interfaces |
| Infrastructure | EF Core/Npgsql, repositories, storage, development identity/seed, and the Python client |
| API | HTTP endpoints, composition, request IDs, CORS, and development Swagger |

The frontend consumes explicit ASP.NET DTOs rather than EF entities. ASP.NET validates and stores uploads, persists the document/extraction aggregate through EF Core, invokes the private AI service, and returns page-aware fields. PostgreSQL is canonical and is exposed locally on host port `55432`; the initial migration and database contract are exercised by PostgreSQL persistence tests.

The current local extractor is deterministic and works with the bundled synthetic text fixture. It is a service-boundary demonstration, not production OCR or an accuracy claim.

Architecture tests enforce that Domain has no outer-layer or persistence dependencies, Application has no Infrastructure/API/EF dependencies, and Infrastructure does not reference API.

The upload workflow persists the document and running extraction attempt atomically before calling the provider. Request cancellation writes a terminal cancelled run using an independent bounded compensation token; provider timeout becomes a safe failed document result. Storage removes generated partial files after cancellation/failure, external request IDs are bounded before persistence, provider-returned extractor identity is retained, and aggregate collections expose read-only wrappers. These behaviors are covered by the final application, infrastructure, domain, and API contract regressions.

## Implemented human-review workflow

ASP.NET exposes a review workspace plus start, action, and completion endpoints. Review actions are append-only and the application projects machine, latest decision, and effective values separately. Strong ETags protect mutations: clients send `If-Match`, successful writes rotate the token, and stale writes fail without overwriting newer work.

The Next.js workspace consumes this contract to confirm, correct, add, clear, complete, and reload review state. Machine values remain visible and immutable beside reviewer-effective values. PostgreSQL migration `20260811073607_AddReviewActionValueTypeConstraint` adds the persisted corrected-value-type check.

## Key decisions

- **Two deliberate service boundaries:** ASP.NET demonstrates enterprise workflow/security strengths; Python isolates the changing AI ecosystem.
- **Provider boundary:** `ExtractionProvider` keeps paid Azure OCR/LLM services replaceable and testable.
- **Synchronous first slice:** simplest observable end-to-end proof. Background RabbitMQ/Celery work is deferred until processing warrants it.
- **Evidence-aware fields:** every field carries confidence and source page even before full bounding-box citations.
- **Generated storage keys:** original filenames are display metadata only; paths cannot escape the upload directory.
- **No implicit AI claims:** the mock provider parses a documented synthetic format and identifies itself in stored results.
- **Practical .NET structure:** the public backend remains one deployable modular monolith, organized with Clean Architecture boundaries, Vertical Slice features, and DDD-lite domain modeling.
- **Code First persistence:** ASP.NET owns the PostgreSQL schema through reviewed EF Core Code First migrations.

## ASP.NET application approach

The ASP.NET backend is a **modular monolith**, not a collection of premature microservices. It remains one deployable application while its code is separated into clear boundaries:

- **Domain:** entities, value objects, invariants, and domain behavior without infrastructure dependencies.
- **Application:** use-case orchestration and interfaces needed by a feature.
- **Infrastructure:** EF Core, PostgreSQL, file/object storage, AI-service clients, and other adapters.
- **API:** HTTP endpoints, authentication integration, DTO mapping, and composition.

Features are organized as **Vertical Slices** so an upload, extraction, review, or later reconciliation use case keeps its command/query, validation, handler, DTOs, and tests close together. **DDD-lite** means modeling important document, extraction, and review invariants explicitly without introducing patterns that do not solve a current domain problem.

The Python/FastAPI AI service remains a separate private service because model-facing document processing changes independently from the enterprise workflow backend. This does not turn every ASP.NET module into a network service.

## Target architecture by responsibility

| Component | Responsibility |
| --- | --- |
| Next.js | Upload, case list, document viewer, human review, discrepancy resolution, cited questions |
| ASP.NET Core | Public API, workflow, authorization, tenancy, validation, audit, persistence orchestration |
| Python/FastAPI | OCR normalization, extraction providers, RAG, evaluation, model-facing logic |
| PostgreSQL/pgvector | Transactional state, structured results, evaluation metadata, vector retrieval |
| Object storage | Original documents and derived page assets |
| RabbitMQ or managed messaging | Background jobs, retries, dead-letter handling |
| OpenTelemetry stack | Logs, metrics, traces, correlation, latency and failure measurement |

## Production target

Next.js on a managed web host; containerized ASP.NET and private FastAPI on Azure Container Apps; Azure Database for PostgreSQL + pgvector; Blob Storage; Azure Document Intelligence; Azure OpenAI through managed identity; Service Bus/RabbitMQ-compatible jobs; Entra ID/OIDC; Key Vault; OpenTelemetry to Application Insights.

## Security evolution

Current controls include size/type validation, generated storage keys, safe provider errors, structural organization relationships, and organization-scoped repository queries. Local requests use a fixed development organization/system actor; this is not authentication or a production tenant boundary. Before real documents: malware scanning, private object storage, encryption, authenticated tenant-scoped access, RBAC, audit events, secret management, retention policies, signed downloads, rate limiting, and a DPIA/threat model appropriate to the organization.

## Architecture principles

- The browser calls only the ASP.NET public API.
- AI providers remain replaceable through explicit interfaces.
- Local development does not require paid credentials.
- Machine output and human corrections remain separate.
- Evidence is part of the data model, not a UI-only decoration.
- Tenant authorization is enforced at the data-access boundary.
- Metrics describe measured behavior only.

See the [database design and ERD](./data/database-design.md), [P1-T04 database evidence](./evidence/phase-1/P1-T04-database-verification.md), and [P1-T08 acceptance verification](./evidence/phase-1/P1-T08-acceptance-verification.md). Phase 1 is complete; later enterprise, AI, and deployment capabilities remain roadmap work.
