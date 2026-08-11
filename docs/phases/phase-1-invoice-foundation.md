# Phase 1 — Invoice foundation

**Status:** Complete  
**Outcome:** A reviewer can upload one English UAE invoice, inspect extracted values, and correct them.

## Planning artifacts

- [Database design and ERD](../data/database-design.md)
- [Phase 1 task ledger](../tasks/phase-1.md)
- [Database pre-implementation review](../reviews/P1-T02-pre-implementation.md)
- [P1-T04 verification evidence](../evidence/phase-1/P1-T04-database-verification.md)
- [Human-review pre-implementation review](../reviews/P1-T05-human-review-pre-implementation.md)
- [P1-T08 acceptance verification](../evidence/phase-1/P1-T08-acceptance-verification.md)

## Approved database foundation

- PostgreSQL is the canonical persistent database.
- EF Core Code First migrations own schema evolution.
- The prototype database and its synthetic data are disposable.
- Local development seeds one organization and system actor.
- Every review action is attributable; correction, addition, and clearing require a reason.
- Processing and review states remain separate behind explicit API DTOs.
- The PostgreSQL schema uses snake_case naming.
- The ASP.NET backend evolves as a modular monolith with Clean Architecture boundaries, Vertical Slice organization, and DDD-lite modeling.

## User journey

1. The reviewer uploads a supported invoice.
2. The system validates the file and records metadata.
3. The AI service extracts required fields.
4. The API saves the machine result.
5. The UI displays values, confidence, and page evidence.
6. The reviewer confirms or corrects each field.

## Verified implementation

- The ASP.NET solution is split into Domain, Application, Infrastructure, and API projects.
- EF Core Code First has an initial Npgsql migration for the approved PostgreSQL model.
- PostgreSQL runs locally on host port `55432`; development seeding creates the local organization and system actor.
- The human-review API supports review creation, field decisions, completion, reload, and strong ETag concurrency.
- The Next.js UI separates machine and effective reviewed values and supports confirm, correct, add, clear, complete, and reload interactions.
- Automated verification passes 100 .NET tests: 53 domain, 15 application, 11 PostgreSQL persistence, 4 architecture, 15 API, and 2 infrastructure tests.
- Five frontend tests, frontend lint/build, one Python unittest, and the VitePress production build pass.
- The .NET Release build passes with zero warnings/errors, EF reports no pending model changes, and the Compose definition validates.
- Local PostgreSQL is at the latest migration, including `20260811073607_AddReviewActionValueTypeConstraint`.
- A live synthetic-invoice workflow persisted revision 1, preserved machine total `1050.00`, projected reviewed total `1155.00`, added `payment_terms = Net 30`, rotated ETags, completed, and reloaded successfully.

These results complete the approved Phase 1 boundary. They do not establish production OCR, Arabic quality, authentication/tenant authorization, RAG, reconciliation, or deployment readiness.

## Completed task sequence

| Task | Outcome | Status |
| --- | --- | --- |
| P1-T05 | Human-review contract and review | Complete |
| P1-T06 | ASP.NET review workflow and backend coverage | Complete |
| P1-T07 | Next.js correction/confirmation UI and frontend coverage | Complete |
| P1-T08 | Phase 1 acceptance verification and evidence | Complete |

Integration Guardian final verdict: **APPROVAL-READY; no blockers**.

## Included

- PDF, PNG, JPEG, and safe text fixture support
- File size and content-type validation
- Generated storage identifiers and file hashing
- Supplier, invoice number, dates, currency, subtotal, VAT, total, and TRN
- Processing status and failure information
- Document list and review screen
- Machine output separated from reviewer corrections
- Foundational organization keys and database-enforced relationship integrity
- Request IDs and processing-duration hooks
- Unit and API integration tests

## Excluded

- Quotations, POs, and contracts
- Production OCR claims
- RAG or document chat
- Arabic extraction
- Autonomous approval
- Authentication, memberships, permissions, and tenant-aware authorization enforcement

## Acceptance criteria

- [x] A supported sample follows the full upload-to-review path.
- [x] Unsupported or oversized files are rejected safely.
- [x] Metadata, extracted fields, and review state persist and reload from PostgreSQL.
- [x] The UI clearly distinguishes machine output from confirmed/corrected values.
- [x] Each extracted field includes page provenance when available.
- [x] Failure states are visible without exposing sensitive internals.
- [x] Tests cover successful upload, validation, persistence, AI-service failure, review concurrency, and completion.
- [x] Database constraints reject cross-organization relationships; tenant-aware read authorization remains deferred to Phase 6.

## Portfolio evidence

- [Architecture](../architecture.md)
- [P1-T04 database verification](../evidence/phase-1/P1-T04-database-verification.md)
- [P1-T08 acceptance verification](../evidence/phase-1/P1-T08-acceptance-verification.md)
- API, persistence, domain, application, infrastructure, and frontend tests
- Documented security choices and explicit limitations
