# .NET organization and disposable-file cleanup — October 11, 2026

Rand requested cleanup of unnecessary files and the same professional organization
for .NET, using the repository pattern. This is the first .NET batch: repository
contracts and the invoice/comparison application feature. It does not complete
whole-project refactoring or approve the pending UI/role proposals.

## Repository pattern and organization

The backend already had dependency inversion: application handlers receive
`IReconciliationRepository` / `IDocumentRepository`, while Infrastructure supplies
Entity Framework implementations through scoped dependency injection. That pattern
is preserved and easier to find now:

```text
Application/Reconciliation/Handlers/
    SetReconciliationDraftDocumentHandler.cs
    CreateReconciliationCaseHandler.cs
    EvaluateReconciliationCaseHandler.cs
        -> Application/Abstractions/Repositories/IReconciliationRepository.cs
        -> Infrastructure/Persistence/ReconciliationRepository.cs
        -> AppDbContext
```

The first arrow is the handler's interface dependency. Infrastructure implements
that interface; the application assembly does not depend on Infrastructure or EF.
The interfaces remain specific to their aggregates and retain organization-scoped
operations and explicit save outcomes. Draft retry receipts keep their existing
store interface, shared scoped context, and atomic commit with the case changes.

Moved 40 existing types into named files. Repository interfaces and related
document/reconciliation contracts were extracted from `Abstractions/Ports.cs`.
Reconciliation now separates `Handlers`, `Commands`, `Queries`, `Models`, `Mapping`,
`Validation`, and `Exceptions`. Removed the three old combined source files after
saving their exact current bytes. Namespaces, visibility, signatures, ordering,
default parameters, and behavior remain unchanged.

Added `apps/backend/.editorconfig` for consistent C# spacing and block layout.
Formatting was limited to this batch; cleaned unused imports and added space
between repository operations and helper methods. The remaining storage/extraction
ports and other application/API features still need later organization batches.

Repository implementations, EF queries, dependency injection, audit, permission
checks, database migrations, rule engines, and existing test source are unchanged.
No new generic CRUD layer, dependency package, transaction boundary or save call.

## Cleanup completed

Removed **143 generated cache files, 2,161,830 bytes** (about 2.1 MB), from:

- `.pytest_cache`
- `.ruff_cache`
- `apps/web/node_modules/.vite`
- `apps/api/app/__pycache__`
- `apps/api/tests/__pycache__`
- `tools/__pycache__`

Each target was inventoried before removal. The PowerShell cleanup verified
resolved paths stayed inside this workspace, refused reparse points, rechecked
file counts/hashes and used literal paths. Git-visible files were excluded.
Added `.ruff_cache/` to the root ignore rules to keep regenerated cache files out
of source control.

Passing tests remain useful regression protection. No test source was deleted.
Historical logs/TRX receipts, source snapshots, synthetic fixtures, backups,
retained databases, installed runtimes/dependencies, and reusable build outputs
were retained. Nothing is treated as disposable just because it is under `tmp`
or `outputs`. Those folders include records needed to explain earlier results.

## Review and verification

Review was **self-review**, not an independent reviewer or subagent.

Findings and fixes:

- Repository interfaces were already in use but buried in a mixed `Ports.cs`.
  Extracted those contracts without changing callers or scoped registrations.
- Combined comparison files mixed handlers, commands, response models, mapping,
  validation and exceptions. Gave each named type a separate file/folder.
- Moving declarations copied unused imports. Removed those and checked compilation.
- Current architecture documentation referenced a removed file. Updated its entry
  point and documented the repository dependency path. Historical receipts still
  refer to their preserved historical source; they were not rewritten.
- Reviewed shared-context atomicity, organization/actor scope, creation/retry keys,
  exact review binding, version checks, unavailable coverage and immutable results.
  No behavior change or fresh persistence acceptance is claimed.

A local SDK/Roslyn audit checks every moved declaration's exact token text and
namespace against its saved original: **40/40 equal**. Remaining declarations in
`Ports.cs` also retain their token sequence. This includes string literals and
anonymous JSON property order used in request hashes; it is stronger than simply
removing whitespace before comparing text. Compilation and tests check the resulting
cross-file bindings. The audit uses installed SDK assemblies, with no package feeds.

The initial application baseline passes **44/44**. The final focused application
gate passes **80/80**, the API gate **66/66**, and architecture **4/4**, with no
skipped checks: **150 final tests**. The affected formatting check also passes.
Results are recorded in `outputs/net-refactor-cleanup-20261011/gates.json`.
Baseline tests overlap the final gate and must not be added to its count.

Evidence under `outputs/net-refactor-cleanup-20261011` includes `baseline.json`,
four exact source snapshots, `moved-types.json`, `syntax-equivalence.json`,
`preservation.json`, final TRX/logs, and cleanup plan/result receipts. `verify.ps1`
records the exact focused commands and runs heavy checks sequentially using the
project runtime helper. The initial inventory covers 1,081 pre-existing Git-visible
files. All changes outside the four source containers, `.gitignore`, and the two
updated current-context documents are rejected by the preservation check.
Temporary syntax-audit `bin`/`obj` outputs are removed after final verification;
the small reproducible audit source and its result remain. These newly generated
files are separate from the 143 pre-existing cache files removed above.

No full solution build, new PostgreSQL test cluster, retained database access,
browser demo, paid call, migration or deployment. Focused tests compile their
required projects. Existing live identity, integration, Arabic/OCR, report/export,
deployment and manual-acceptance gaps remain open.

## Two-minute manual source review

VS Code reuses the existing project window. The complete captured-baseline diff
is `before-feature.cs.txt` versus `after-feature.cs.txt`; editable source is open
in separate tabs.

1. Read `Abstractions/Repositories/IReconciliationRepository.cs` and the existing
   `Infrastructure/Persistence/ReconciliationRepository.cs` implementation.
2. Open `Reconciliation/Handlers/SetReconciliationDraftDocumentHandler.cs` and
   see its interface dependencies and explicit save/retry checks.
3. Use the feature folders to find one command, response model, and exception.

User acceptance of the organization remains pending. Extend this convention to
the next substantial feature after that review; preserve the pending design choices.
