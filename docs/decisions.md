# Decision log

This page records product decisions separately from implementation details. A decision should include its reason and the consequence for the roadmap.

| ID | Date | Decision | Reason | Status |
| --- | --- | --- | --- | --- |
| D-001 | 2026-08-06 | Use Next.js for the product UI | Demonstrates React while providing a production application structure | Accepted |
| D-002 | 2026-08-06 | Use ASP.NET Core for the public enterprise API | Highlights backend, security, workflow, and persistence engineering | Accepted |
| D-003 | 2026-08-06 | Isolate AI processing in Python/FastAPI | Keeps OCR, extraction, RAG, and evaluation close to the Python AI ecosystem | Accepted |
| D-004 | 2026-08-06 | Do not use “Bayaan” as the product name | The name is crowded in regional AI and business markets | Accepted |
| D-005 | 2026-08-07 | Make the first MVP English-first | Reduces early OCR and evaluation complexity while preserving Arabic as a meaningful later phase | Proposed |
| D-006 | 2026-08-07 | Treat document reconciliation as the primary differentiator | Creates more business value and stronger portfolio evidence than generic PDF chat | Proposed |
| D-007 | 2026-08-07 | Use VitePress for planning documentation | Keeps the source in Markdown while providing fast local navigation and review | Accepted |
| D-008 | 2026-08-07 | Use PostgreSQL as the canonical persistent database with EF Core Code First migrations | PostgreSQL is the production target; SQLite behavior is not sufficient migration evidence | Accepted |
| D-009 | 2026-08-07 | Treat the current prototype database and synthetic data as disposable | The prototype has no migration history and contains no client or production data worth importing | Accepted |
| D-010 | 2026-08-07 | Seed one development organization and system actor for local workflows | Phase 1 needs structural ownership and attribution without pretending authentication already exists | Accepted |
| D-011 | 2026-08-07 | Require an actor for every review action and a reason for correction, addition, or clearing | Review history must remain attributable and explain material changes while confirmations may remain reason-free | Accepted |
| D-012 | 2026-08-07 | Separate document-processing and human-review states while preserving the existing API through DTO mapping | Database workflows need distinct lifecycles without coupling the browser contract to EF entities | Accepted |
| D-013 | 2026-08-07 | Use snake_case PostgreSQL table and column names from the initial migration | A consistent physical naming convention avoids a later destructive rename migration | Accepted |
| D-014 | 2026-08-07 | Structure the ASP.NET backend as a modular monolith using Clean Architecture boundaries, Vertical Slice organization, and DDD-lite | This preserves a practical single deployment while making domain, application, infrastructure, and feature responsibilities explicit | Accepted |

## Decisions still needed

- Final product name
- Exact first user persona
- Final MVP document types
- Initial discrepancy rules and tolerances
