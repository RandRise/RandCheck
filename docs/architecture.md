# Architecture

The main product workflow is **Add invoice + PO → Check details → See results**.
The library, saved history and document questions support that workflow. The
browser uses the ASP.NET API for all of them. Python handles private extraction and
model-facing preparation; PostgreSQL stores the durable workflow and history.

## Follow one document

![Document processing: upload, store, queue, extract, save evidence, then review](/architecture/document-processing.svg)

Uploading accepts and queues the document; extraction happens afterward. A failed
attempt can retry without blocking the browser request. Review operates on a
successful extraction revision. It preserves the machine values and records human
confirmations, corrections, additions, and exclusions separately.

Completing review locks that revision. A comparison or question uses a specific
completed revision, so a later extraction or correction cannot silently rewrite an
old result. An invoice can be reviewed on its own; a comparison case is only needed
when related procurement documents are being compared.

The current Home flow saves a case only after the user chooses both files and
continues. Uploading one document alone does not create a case. The three stages
stay on `/invoices`; optional Details screens return to that workflow. Historical
document links and quotation comparisons remain readable through compatibility
paths. New invoice/PO checks use the AED-only `invoice-po-2.0` rule set, with no
goods-receipt step or automatic currency conversion.

![Invoice check: choose pair, review, compare exact versions, record a decision, and export](/architecture/invoice-check.svg)

## Where each journey is implemented

Read the screen, then the handler, then its persistence implementation. Start with
these entry points rather than reading every class in the repository.
Code paths are relative to the repository root; open them in your editor to follow
the screen, handler and storage steps.

| Journey | Screen | Backend entry and behavior | Persistent state |
| --- | --- | --- | --- |
| Upload and processing | Home (`apps/web/app/page.tsx`), invoice workflow (`apps/web/app/invoices/page.tsx`) | Upload handler (`apps/backend/src/UaeDocumentIntelligence.Application/Documents/Upload/UploadDocument.cs`), worker (`apps/backend/src/UaeDocumentIntelligence.Infrastructure/Services/ExtractionWorker.cs`), Python extractor (`apps/api/app/local_extraction.py`) | Original file in scoped storage; document, extraction run, job, fields, pages, and evidence in PostgreSQL |
| Human review | Embedded document workspace (`apps/web/app/document-workspace.tsx`), guided review (`apps/web/app/review/`) | Review handlers (`apps/backend/src/UaeDocumentIntelligence.Application/Documents/Review/ReviewHandlers.cs`), workspace projection (`apps/backend/src/UaeDocumentIntelligence.Application/Documents/Review/ReviewWorkspace.cs`) | Review session and append-only actions; effective values are projected from machine output and the latest decisions |
| Compare documents | See results within the invoice workflow; shared result components under `apps/web/app/reconciliation/` | Comparison handlers (`apps/backend/src/UaeDocumentIntelligence.Application/Reconciliation/Handlers`), current rule engine (`apps/backend/src/UaeDocumentIntelligence.Domain/Model/InvoicePurchaseOrderRuleEngine.cs`); legacy engine remains for history | Exact completed reviews; frozen run values/evidence/checks; append-only decisions |
| Report and spreadsheet | `apps/web/app/reconciliation/export/` | `GetReconciliationReportHandler` and `IReconciliationReportRepository`; authorized, version-checked repeatable-read snapshot | Frozen comparison plus one coherent decision snapshot; escaped HTML and formula-protected CSV use that same capture |
| Questions | Questions (`apps/web/app/questions/page.tsx`) | Question routes (`apps/backend/src/UaeDocumentIntelligence.Api/CitedRetrievalEndpoints.cs`), reviewed-answer handler (`apps/backend/src/UaeDocumentIntelligence.Application/CitedQuestions/CitedReviewedQuestion.cs`), answer engine (`apps/backend/src/UaeDocumentIntelligence.Application/CitedQuestions/CitedFactAnswerEngine.cs`) | Frozen source indexes, saved retrieval/answer results, claims, citations, and refusal reasons |
| Access and administration | Shared session (`apps/web/app/security/enterprise-provider.tsx`), organization screens | Permission map (`apps/backend/src/UaeDocumentIntelligence.Api/Security/EnterprisePermissions.cs`), security endpoints | Organization memberships, audit events, retention policies, and preservation holds |

For the storage step, follow upload admission (`apps/backend/src/UaeDocumentIntelligence.Infrastructure/Persistence/UploadQueueStore.cs`)
and the extraction queue (`apps/backend/src/UaeDocumentIntelligence.Infrastructure/Persistence/ExtractionJobQueue.cs`),
then document/review persistence (`apps/backend/src/UaeDocumentIntelligence.Infrastructure/Persistence/DocumentRepository.cs`).
Comparison uses the case repository (`apps/backend/src/UaeDocumentIntelligence.Infrastructure/Persistence/ReconciliationRepository.cs`).
Questions use the reviewed-answer store (`apps/backend/src/UaeDocumentIntelligence.Infrastructure/Persistence/CitedReviewedQuestionStore.cs`)
or evidence retrieval store (`apps/backend/src/UaeDocumentIntelligence.Infrastructure/Persistence/CitedRetrievalStore.cs`).

### .NET repository boundaries and file organization

Application handlers depend on repository interfaces, including
`IDocumentRepository` and `IReconciliationRepository`, in
`apps/backend/src/UaeDocumentIntelligence.Application/Abstractions/Repositories`.
Infrastructure implements those interfaces with Entity Framework and registers
them as scoped services in `DependencyInjection.cs`. Application does not reference
Entity Framework or the infrastructure project. Repository operations retain their
organization scope, explicit save outcomes and concurrency handling.

The reconciliation feature has separate `Handlers`, `Commands`, `Queries`,
`Models`, `Mapping`, `Validation`, and `Exceptions` folders, with one named type per
file. Namespaces and constructor contracts remain stable. Repository implementations
and their transaction boundaries stay in Infrastructure; draft retry receipts still
share the existing context and atomic save. Remaining extraction/storage contracts
in `Abstractions/Ports.cs` and other features can be organized in later batches.

The library uses `/api/documents/library` through its feature service and
`IDocumentLibraryRepository`. Server-side search covers filenames, suppliers and
printed invoice/PO references, returning 10 records per page. Quotations are hidden
by default but can be included. The older work-list endpoint remains for other
workflow consumers. List queries project metadata and status without loading every
document's values, evidence or review history. Review state
and the latest extraction state are separate: an earlier completed review remains
available while a replacement extraction runs or fails.

Questions resolves a document link using its document **and completed review** IDs
through workspace queries (`apps/backend/src/UaeDocumentIntelligence.Application/CitedQuestions/CitedWorkspace.cs`)
and the workspace store (`apps/backend/src/UaeDocumentIntelligence.Infrastructure/Persistence/CitedWorkspaceStore.cs`).
Its recent-result query returns at most 20 supported results for the current actor
and organization, filtering source eligibility before limiting the list. Reading
that list neither prepares indexes nor generates answers. A saved result keeps its
own source versions independently of the current composer selection.

On the frontend, Questions workflow (`apps/web/app/questions/use-questions-workflow.ts`)
owns source loading, URL restoration and request lifetimes; the page renders its
state and controls. Document processing helpers (`apps/web/app/document-processing.ts`)
own file fingerprints, scoped request identities and processing requests.
Supported review fields (`apps/web/app/document-fields.ts`) defines the ordinary
names, types and line-position conventions. These are feature-local modules, so
following a Documents or Questions action does not require a shared orchestrator.

The normal service graph lives in
core registration (`apps/backend/src/UaeDocumentIntelligence.Infrastructure/DependencyInjection.cs`).
Pilot registration (`apps/backend/src/UaeDocumentIntelligence.Infrastructure/CitedPilotRegistration.cs`)
is an explicit extra step for standalone experiment tools and synthetic tests.
The API and worker do not register generation, embedding-ingestion or recovery
coordinators. Core Questions can still read saved results and reuse prepared
semantic queries. Even the extra registration keeps generation and ingestion
disabled and recovery denied until a tool supplies its separately authorized setup.

## What each service owns

| Component | Responsibility |
| --- | --- |
| Next.js | Navigation, source context, drafts, review controls, saved-work reopening, and role-appropriate presentation |
| ASP.NET API | Public HTTP contracts, authentication/permissions, validation, workflow, audit admission, and request limits |
| .NET Application | Feature handlers and DTOs, review/answer projection, and interfaces for persistence and private services |
| .NET Domain | Document/review/case entities, state transitions, invariants, and comparison rules |
| .NET Infrastructure | EF Core/Npgsql, files, durable extraction queue/worker, private Python clients, and operational adapters |
| Python/FastAPI | Private native-PDF/OCR extraction, normalization, evidence validation, chunking/search preparation, and isolated provider experiments |
| PostgreSQL | Canonical durable records, job claims, transactions, version checks, and saved evidence; pgvector supports prepared semantic experiments |

The four .NET projects are layers of one backend deployment. Domain has no
persistence or outer-layer dependencies; Application does not reference
Infrastructure/API/EF; Infrastructure does not reference API. Architecture tests
check those boundaries. A separate message broker is unnecessary for the current
queue: PostgreSQL stores document/run/job state in the same transaction.

## Rules that explain the less visible behavior

- **Machine output and reviewed values are different.** A correction does not
  overwrite the extractor's original value. Reviewer-added fields have review
  provenance, not invented AI confidence or page evidence.
- **Versions matter.** Extraction runs, completed reviews, comparison snapshots,
  and question indexes identify the evidence a result actually used. Opening the
  current document can show a newer revision than an old saved result.
- **A retry and a revision are different.** Concurrency tokens (`ETag`/`If-Match`)
  prevent stale writes. Request identities prevent repeated submission intent
  from creating another result. Deliberate new processing/comparison decisions
  must remain explicit. See the [repeat-action contract](design/action-repeat-contract.md)
  for the implemented boundaries and remaining task status.
- **Processing and reviewing have separate states.** An older successful output
  can stay visible while a replacement extraction runs or fails.
- **A finished review is not a match.** The backend distinguishes passed,
  differing and unperformed checks. It derives reliable facts from reviewed values
  and source evidence; it cannot infer missing commercial facts. A recorded reason
  acknowledges a finding and never changes the underlying check outcome.
- **Questions have different evidence paths.** Reviewed answers use a bounded
  deterministic fact engine. Keyword search returns evidence, not a generated
  answer. Semantic search reuses explicitly prepared queries. Missing or ambiguous
  facts remain unanswered.
- **Optional model experiments are separate capabilities.** Paid generation and
  embedding ingestion are disabled in the normal profile. Their spending and
  recovery records remain historical state; simplifying the interface does not
  authorize spending or deleting them.

## Local environments

Use [UI preview](ui-preview.md) for frequent interface work and the
[disposable local demo](local-demo.md) for real processing/persistence integration.
The preview loads actual page components through Vite with synthetic responses;
its success does not establish Next.js server, OCR, database, or authentication
behavior. The demo builds the current source in an isolated copy, applies
migrations, and uses loopback ports and disposable data.

The [advanced setup reference](setup.md) describes the legacy root Compose/host
path and its limits. The root profile and the disposable demo are distinct
configurations. Normal development does not require paid credentials.

## Security and deployment scope

The default security profile is Locked. The local Development demo uses an
explicit development identity. The Entra profile resolves current organization
membership and enforces Staff/Admin access. Staff handles documents; Admin also
manages people, audit access and storage controls. Migration of older accounts is
explicit and must not silently grant broader access. The private worker
credential authenticates the service and does not replace user authorization.

Local security implementation includes tenant-scoped queries, restricted database
roles, append-only audit, protected source downloads, source integrity/scanning
boundaries, versioned retention/holds, and guarded deletion/recovery. The
[local acceptance](security/phase-6-local-acceptance.md) records verification and
limits. Real identity, scanner/key delivery, TLS, multi-replica behavior, and
production operations require their own target-environment evidence.

The [portfolio delivery decision](design/github-portfolio-delivery.md) requires a
reproducible local project and honest evidence. The later
[reviewer-experience ledger](tasks/reviewer-experience-recovery.md) separately tracks
real-identity acceptance; an account-free demo does not complete that task.

Azure Container Apps, managed PostgreSQL, Blob Storage, Azure OCR/OpenAI, Entra,
Key Vault, and hosted telemetry are possible production targets. Object storage,
a managed broker, and cloud deployment are optional extensions, not prerequisites
for understanding or running the current portfolio workflow.

See the [current simplification verification](testing/simplification-20261002.md)
and [database design reference](data/database-design.md) for supporting details.
