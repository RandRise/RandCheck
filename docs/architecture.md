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
      PostgreSQL + extraction_jobs     .NET BackgroundService
                  :55432                       |
                                               v
                                  private Python/FastAPI
                                      mock / local
```

The .NET solution now has four production projects:

| Project | Implemented responsibility |
| --- | --- |
| Domain | Document, extraction, evidence, and review entities plus invariants |
| Application | Feature use cases, DTO projection, queries, and dependency interfaces |
| Infrastructure | EF Core/Npgsql, repositories, scoped storage/cleanup, durable queue, background worker, observability hooks, and authenticated Python client |
| API | HTTP endpoints including asynchronous upload and processing primitives, composition, request IDs, CORS, and development Swagger |

The frontend consumes explicit ASP.NET DTOs rather than EF entities. ASP.NET validates and stores uploads, transactionally queues document/run/job state, and returns `202` without invoking the provider in the request lifecycle. A .NET `BackgroundService` claims PostgreSQL work and invokes the authenticated private AI route outside the claim transaction. PostgreSQL is canonical and is exposed locally on host port `55432`.

The P2-T04 local extractor handles synthetic native and scanned English documents behind the private boundary. P2-T06 connects it to the durable workflow through migration `20260811131151_AddDurableExtractionQueue`; PostgreSQL, image builds, and synthetic native/scanned Compose flows are verified.

Architecture tests enforce that Domain has no outer-layer or persistence dependencies, Application has no Infrastructure/API/EF dependencies, and Infrastructure does not reference API.

The upload workflow stores the file, atomically persists document/queued run/job, and compensates storage if the transaction fails. Orphan cleanup is scoped to generated keys and skips reparse points. PostgreSQL-clock claims use `SKIP LOCKED`, lease fencing, heartbeats, bounded retries, cancellation, and reprocess/latest-job primitives. The authenticated Python request verifies storage-key containment, bytes, SHA-256, schema, deadline, provider identity, errors, and normalized results. Activity/Meter hooks expose bounded operational signals. Exact verification and limitations are recorded in P2-T06 evidence.

## Implemented human-review workflow

ASP.NET exposes a review workspace plus start, action, and completion endpoints. Review actions are append-only and the application projects machine, latest decision, and effective values separately. Strong ETags protect mutations: clients send `If-Match`, successful writes rotate the token, and stale writes fail without overwriting newer work.

The Next.js workspace consumes this contract to confirm, correct, add, clear, complete, and reload review state. Machine values remain visible and immutable beside reviewer-effective values. PostgreSQL migration `20260811073607_AddReviewActionValueTypeConstraint` adds the persisted corrected-value-type check.

## Key decisions

- **Two deliberate service boundaries:** ASP.NET demonstrates enterprise workflow/security strengths; Python isolates the changing AI ecosystem.
- **Provider boundary:** `ExtractionProvider` keeps paid Azure OCR/LLM services replaceable and testable.
- **Durable PostgreSQL queue first:** a .NET worker avoids database/broker dual writes and unlocks local processing; RabbitMQ remains deferred until measured scale justifies it.
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

Current controls include size/type validation, generated/scoped storage keys, symlink/traversal rejection, size/SHA verification, a shared-secret private worker route, safe provider errors, structural organization relationships, and organization-scoped repository queries. The worker token is local service authentication, not user authentication or a production tenant boundary. Local requests still use a fixed development organization/system actor. Before real documents: malware scanning, private object storage, encryption, authenticated tenant-scoped access, RBAC, audit events, production secret management, retention policies, signed downloads, rate limiting, and a DPIA/threat model appropriate to the organization.

## Architecture principles

- The browser calls only the ASP.NET public API.
- AI providers remain replaceable through explicit interfaces.
- Local development does not require paid credentials.
- Machine output and human corrections remain separate.
- Evidence is part of the data model, not a UI-only decoration.
- Tenant authorization is enforced at the data-access boundary.
- Metrics describe measured behavior only.

See the [database design and ERD](./data/database-design.md), [P1-T08 acceptance verification](./evidence/phase-1/P1-T08-acceptance-verification.md), and [P2-T06 queue/worker verification](./evidence/phase-2/P2-T06-queue-worker-verification.md). Phase 1 and P2-T06 are complete; later Phase 2, enterprise, and deployment capabilities remain in progress or roadmap work.
