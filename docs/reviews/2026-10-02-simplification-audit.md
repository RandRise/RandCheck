# Project simplification audit

Reviewed October 2, 2026, against the current working tree, including its existing
uncommitted changes. This is a decision report, not an implementation change or a
new full-stack acceptance certificate.

The project can be made substantially easier to use and maintain without a rewrite.
The best opportunities are to remove technical decisions from everyday screens,
make review progress visible before opening a document, separate page behavior into
small feature units, and distinguish the supported product from optional experiments.
The existing service boundaries, evidence history, and security controls serve real
purposes and should remain.

Code references below use paths from the repository root. Open those files in your
editor when following the evidence; the documentation site publishes the guides.

## What was checked

- Source tracing through upload, durable extraction, review, comparison, questions,
  permissions, protected sources, administration, and retention entry points.
- Application registration, provider selection, database access, startup profiles,
  test entry points, current task records, and delivery requirements.
- Read-only browser inspection of Documents, Questions, and Compare documents in
  the actual-component synthetic preview, including the Viewer document screen.
- Fresh frontend, Python application, and all five non-persistence .NET test suites.

No product source, schema, retained data, permissions, or existing task status was
changed. Browser inspection used synthetic responses; it does not prove real OCR,
database persistence, or Microsoft sign-in. Fresh PostgreSQL, OCR-container,
production-build, dependency-audit, and real Entra checks were outside this pass.

## Findings and decisions

### 1. The document list does not explain what work remains

**Observed:** unfinished and completed reviews both display `Ready`. The list uses
extraction status, while review progress is loaded separately for the selected
document. A user must open documents to discover which need attention. The Viewer
screen also shows disabled upload/reprocess controls and the instruction “Start
review” despite not offering that action.

**Simplify:** show a separate, derived review state in the list: Needs review,
In progress, or Reviewed, alongside processing failures where relevant. Provide a
clear next action for the selected item. Keep upload controls for Operators and
use role-appropriate inline guidance. Preserve separate processing/review states
internally, including an older completed review while a newer extraction runs.

Use a document-summary query rather than fetching each review individually.
`DocumentRepository.ListQuery()` currently loads all extraction runs, fields, and
evidence for the organization; a summary projection would also separate list data
from workspace data. Pagination can follow the actual list requirements.

Evidence: document screen (`apps/web/app/page.tsx`),
document repository (`apps/backend/src/UaeDocumentIntelligence.Infrastructure/Persistence/DocumentRepository.cs`).

### 2. Questions asks the user to operate internal machinery

**Observed:** the page exposes a case UUID input, explicit source preparation,
question mode, keyword/vector/hybrid selection, and a saved retrieval ID input.
The heading promotes reviewed answers, but the initial mode is evidence search.
Saved-result URLs and the comparison-to-question link already work; they should
be reused. Semantic search requires an already prepared query for the selected
sources and can reject an otherwise reasonable new question.

**Simplify first:** make reviewed-field answers the default, carry the selected
document/case into the page, and show named sources with a plain-language
preparation action. Keep keyword evidence search as a clearly labeled alternative.
Move vector/hybrid choices into an experimental area, shown only when applicable.
Replace UUID entry as the normal reopening method with an account-scoped recent
results list; preserve existing links and restrict history to its current owner.
That list requires a new backend query, not just a cosmetic frontend change.

Preparation must still require Reviewer/Operator permission. A Viewer must not
silently gain source-preparation permission through a simplified button.

Evidence: questions screen (`apps/web/app/questions/page.tsx`),
public question routes (`apps/backend/src/UaeDocumentIntelligence.Api/CitedRetrievalEndpoints.cs`),
semantic workflow (`apps/backend/src/UaeDocumentIntelligence.Application/CitedQuestions/CitedSemanticRetrieval.cs`),
reviewed-answer persistence (`apps/backend/src/UaeDocumentIntelligence.Infrastructure/Persistence/CitedReviewedQuestionStore.cs`).

### 3. Review and comparison still expose avoidable technical decisions

**Observed:** adding a missing field requires a field key, position, and value
type. Comparison exposes five absolute tolerances before the first run. The
document page offers reprocessing next to ordinary review. These are valid
capabilities, but they make routine work harder to distinguish from exceptions.

**Simplify:** use named supported fields with inferred types and a line selector;
keep custom field entry in an advanced section. Start comparisons with the existing
defaults and put tolerance adjustments under Settings. Move reprocessing and
technical provider details into secondary controls. After completion, offer a
direct route to questions or a related comparison without forcing invoice-only
work into a case.

Keep explicit review completion, reasons for changes, immutable completed reviews,
versioned case sources, and deliberate revisions. Do not automatically confirm
high-confidence fields or silently replace a saved comparison's evidence.

Evidence: review controls (`apps/web/app/page.tsx`),
comparison controls (`apps/web/app/reconciliation/page.tsx`),
review invariants (`apps/backend/src/UaeDocumentIntelligence.Domain/Model/ReviewSession.cs`).

### 4. Page files own too many responsibilities

**Observed:** Documents, Compare documents, and Questions contain 16, 26, and 20
`useState` declarations respectively, plus request-generation refs, loading,
mutation, recovery, routing, DTO definitions, and rendering. The count alone is
not a defect; their combined responsibilities make each flow difficult to trace.
The Python local extractor similarly combines PDF reading, OCR, orientation,
field/line parsing, evidence construction, and runtime checks in about 1,070 lines.

**Simplify:** move each page's workflow into a feature-specific hook/controller
with explicit states, leaving the route to compose the list, editor, and evidence
panels. Put feature DTOs and API calls next to that workflow. Extract common
request/error handling only where semantics match. Split local extraction into
document reading/OCR and field/evidence parsing behind the existing provider API.

Preserve late-response protection, draft retention, ETags, request identities, and
exact evidence offsets. Do not add a new state-management library or a universal
workflow framework to accomplish this. Refactor one feature at a time using the
existing behavioral tests.

Evidence: the three page files above and
local extractor (`apps/api/app/local_extraction.py`).

### 5. Optional AI experiments are mixed into normal application setup

**Observed:** reviewed answers use a deterministic fact engine; public question
routes do not invoke the paid answer-generation coordinator. The default service
registration nevertheless contains generation/embedding coordinators, spending
ledgers, unknown-cost holds, and recovery services. Generation and embedding
settings default to disabled; some recovery interfaces deny all callers by default.

**Simplify:** separate the public reviewed-answer/evidence path from explicit pilot
registration and organize pilot tools as experiments. Keep one deployable .NET
backend. This provides a smaller normal dependency graph and a clearer reading path.

**Do not bulk-delete `Cited*` files.** That name covers active indexing, retrieval,
citations, reviewed answers, persisted history, and experimental generation.
Semantic retrieval also uses previously prepared embedding data. Any later removal
needs a caller/data dependency audit and must preserve existing records, migration
history, and any unresolved spending holds. Do not enable paid features as part of
simplification.

Evidence: registration (`apps/backend/src/UaeDocumentIntelligence.Infrastructure/DependencyInjection.cs`),
question workflow (`apps/backend/src/UaeDocumentIntelligence.Application/CitedQuestions/CitedQuestionWorkflow.cs`),
generation coordinator (`apps/backend/src/UaeDocumentIntelligence.Application/CitedQuestions/CitedGenerationCoordinator.cs`),
embedding settings (`apps/backend/src/UaeDocumentIntelligence.Application/CitedQuestions/CitedEmbeddingIngestion.cs`).

### 6. Startup choices have drifted and serve different purposes

**Confirmed source mismatch:** root `docker-compose.yml` expects adapter
`local-1.2.0`; the current English extractor reports `local-1.3.0`, and the .NET
client rejects mismatched versions. The isolated demo profile expects `local-1.3.0`.
The root profile was not launched in this audit; the mismatch is established from
configuration and validation code. Its Python environment says `mock`, but the
durable route selects the explicit provider requested by .NET, so that variable
alone does not establish which worker extractor executes.

**Simplify:** present two supported choices: UI preview for interface work and real
local demo for integration. Give each one documented entry point. Repair or
explicitly retire the legacy root setup from the normal onboarding path. Remove
the old A/B design proposal from the preview's main navigation once its useful
design reference is archived. Do not merge synthetic responses with real security
or persistence: the two environments prove different things.

Evidence: [root Compose](../../docker-compose.yml),
[demo Compose](../../tools/demo/compose.yml),
provider validation (`apps/backend/src/UaeDocumentIntelligence.Infrastructure/Services/PythonExtractionClient.cs`),
[preview guide](../ui-preview.md).

### 7. Documentation and completion criteria need reconciliation

**Observed:** shared navigation, role help, case reopening, and explicit comparison
revisions already exist. The preview guide still says comparison lacks reopening.
The architecture page describes an earlier security state. The README mixes current
capabilities with superseded implementation checkpoints.

The September 15 portfolio decision makes real Entra optional for that milestone.
The later reviewer-experience ledger requires a real Entra walkthrough for its R5
acceptance. These are different scopes that currently look like competing finish
lines. Neither requirement should be silently deleted or marked complete.

**Simplify:** publish one current capability/status table and distinguish portfolio
delivery from real-identity acceptance. Keep historical evidence linked as history.
Preserve current Arabic work and its experimental limits; freeze further scope
expansion during simplification. Reuse the existing recovery ledger rather than
creating another competing program of phases.

Evidence: [portfolio decision](../design/github-portfolio-delivery.md),
[current recovery tasks](../tasks/reviewer-experience-recovery.md),
[architecture](../architecture.md), README (`README.md`).

## Recommended execution order

1. **Establish the current baseline.** Preserve the existing working tree, reconcile
   startup instructions and the adapter mismatch, and record separate completion
   milestones. Use a low-concurrency test command on this machine.
2. **Finish the already identified retry rules.** Continue R4-T05b for upload,
   processing, review decisions, and questions. Upload currently creates a new
   document/run/job for every accepted submission; a trace ID is not retry identity.
   Keep the comparison retry behavior already completed in R4-T05a.
3. **Simplify Questions and document status.** Remove normal UUID/search-algorithm
   decisions, show meaningful review status, and carry context between screens.
   Reduce advanced controls in review/comparison as those flows are touched.
4. **Refactor behind the settled behavior.** Extract feature workflows, separate
   pilot registration, and split the local extractor. Avoid simultaneous schema,
   security, framework, and UI rewrites.
5. **Verify the resulting journey.** Test permitted/denied roles, refresh/Back,
   uncertain retries, saved history, Arabic evidence, and the real disposable demo.
   A first-time user should finish review and comparison without copying an internal
   ID or selecting a retrieval algorithm. The owner should be able to trace one
   action through UI, handler, and persistence without reading pilot code.

The first product changes should therefore be Questions and document work status,
after the baseline/retry checks. The audit does not support removing reconciliation,
review history, tenancy, or the durable queue, or replacing the technology stack.

## Fresh verification results

| Check | Result and limit |
| --- | --- |
| Frontend component tests | 237/237 passed with `vitest run --maxWorkers=1`; 19 files. Two earlier default-parallel runs each failed one different asynchronous test. Do not represent those earlier runs as green. |
| Review request tests | 3/3 passed with Node's test runner. |
| Synthetic preview adapter | 12/12 passed; does not prove the real API. |
| .NET Architecture / Domain / Application / Infrastructure | 4 / 111 / 365 / 220 passed, with no skips. |
| .NET API | Initial sandbox run: 283 passed, 3 certificate tests failed. All 23 source-key tests then passed outside the restricted token, including the 3 failures. Across the five projects, all 986 distinct non-persistence tests passed across these runs; this was not one unrestricted full-suite run. |
| Python application group | 338 passed, no skips, using the existing Windows development environment. OCR/native/legacy-fixture groups were not rerun. |
| Browser inspection | Current-component preview: Operator Documents/Questions/Compare entry screens and Viewer Documents. Read-only inspection, not all-role end-to-end acceptance. |

The certificate failure matches the previously documented restricted-token
PFX-import issue in [source-key evidence](../evidence/phase-6/P6-source-keys-20260915.md).
It is not evidence that the source-protection feature should be removed. Test
reruns did not change timeouts, assertions, source code, or security checks.

Commands used the repository Node/.NET runtimes via
`tools/use-project-runtimes.ps1`, `dotnet test --no-restore` for each non-persistence
project, the focused certificate rerun with `--no-build --no-restore`, and
`.venv/Scripts/python.exe tools/run_python_regression.py --group application`.
These results support incremental simplification; they do not establish production
readiness or replace the remaining integration acceptance work.
