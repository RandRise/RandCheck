# Repository Guidelines

## Project Structure & Commands

- `apps/web/app/`: Next.js screens, feature components, hooks, services, and colocated tests.
- `apps/backend/src/`: .NET API, Application, Domain, and Infrastructure layers; tests in `apps/backend/tests/`.
- `apps/api/app/`: Python extraction and AI services; tests in `apps/api/tests/`.
- `docs/`: VitePress guides, roadmap tasks, designs, handoffs, and verification reports; illustrations in `docs/public/`.
- `tools/`: development utilities; `samples/`: fixtures. Preserve retained data and evidence in `data/` and `outputs/`.

From the repository root in PowerShell, select installed runtimes with `. ./tools/use-project-runtimes.ps1`.

| Command | Purpose |
| --- | --- |
| `npm --prefix apps/web run dev` | Start the web app |
| `npm --prefix apps/web run build` | Build the web app |
| `npm --prefix apps/web run lint` | Run web ESLint |
| `npm --prefix apps/web test` | Run web tests |
| `npm --prefix docs run dev` / `npm --prefix docs run build` | Serve / build VitePress documentation |
| `dotnet run --project apps/backend/src/UaeDocumentIntelligence.Api` | Start the backend |
| `dotnet test apps/backend/tests/UaeDocumentIntelligence.Application.Tests` | Run application-layer tests |
| `.venv/Scripts/python.exe -m pytest apps/api/tests` | Run Python tests |
| `.venv/Scripts/python.exe -m ruff check apps/api --select E9,F63,F7,F82` | Check critical Python lint |

## Planning & Roadmap Checkpoints

Before editing, read `docs/continuation.md`, the relevant `docs/tasks/` entry, and applicable nested instructions; inspect the dirty tree and affected code. Identify the current milestone, dependencies, scope, and acceptance criteria. Separate verified facts from assumptions. Scale planning to complexity; break substantial work into manageable, reviewable batches. Explain significant architectural choices and tradeoffs before implementing them. Continue authorized work; ask only when a missing decision affects scope or correctness.

After each meaningful implementation batch, update the existing task record and continuation checkpoint. Update `docs/workflow-review.md` and affected diagrams when the workflow changes. Report the roadmap position, completed work, remaining work, findings, verification evidence, blockers, and next step. Preserve earlier records. Distinguish proposed, approved, implemented, tested, and manually accepted work; passing tests alone does not complete a milestone with outstanding requirements. Treat unapproved design proposals as proposals.

## Code Organization & Design Patterns

Use the existing architecture and patterns appropriate to each responsibility:

- **Web:** feature folders with separate components, modals, hooks, API services, types, and shared constants. Keep business rules and request handling out of presentation components.
- **.NET:** preserve Domain/Application/Infrastructure boundaries and dependency injection. Use repository interfaces such as `IDocumentRepository` and `IReconciliationRepository` with Infrastructure implementations. Keep persistence out of endpoints; organize handlers, services, models, and contracts into focused files.
- **Python:** keep routes thin; separate request/response models, extraction logic, services, configuration, and reusable helpers into focused modules.

Keep abstractions proportionate to their purpose. Use clear names and blank lines between logical sections. Use 2-space TypeScript and 4-space C#/Python indentation; follow backend `.editorconfig`, ESLint, and Ruff. Use PascalCase for C# types/React components and snake_case for Python. Read `apps/web/AGENTS.md` and relevant installed Next.js documentation before web edits.

## Comments & Non-obvious Logic

Explain why unusual logic exists. Document functions/services with non-obvious inputs, units, defaults, allowed values, side effects, ordering requirements, or retry behavior. Replace unexplained special values with named constants and explain their source or purpose. Use docstrings, XML documentation, or concise comments where they clarify contracts. Keep comments current; avoid narrating obvious statements.

## Testing & Code Review

Use Vitest/Testing Library (`*.test.tsx`), Node tests (`*.test.mjs`), xUnit (`*Tests.cs`), and pytest (`test_*.py`). Test changed behavior and relevant failure cases, permissions, retries, and historical compatibility. Investigate root causes; preserve meaningful assertions rather than weakening tests to obtain passes.

Run focused tests and applicable static checks first. Broaden checks when impact or findings justify it. Use disposable databases for persistence tests. Documentation-only changes need content/link checks, not unrelated application rebuilds.

Before delivery, review the final diff for correctness, design flaws, unintended changes, and regression risks. Fix material findings and rerun affected checks. Identify self-review versus independent review accurately. Record commands, results, failures, skips, and remaining limitations; distinguish fresh verification from historical or synthetic evidence. Never claim an unrun check passed. Show meaningful before/after diffs in the existing VS Code window and provide short manual checks after meaningful batches.

## Progress and Quality Reviewer

Use a separate **Progress and Quality Reviewer** agent at meaningful implementation checkpoints, not after every tiny edit. The implementation agent supplies the approved scope, roadmap milestone, acceptance criteria, captured before/after diff, and test evidence. Preserve the existing dirty tree; review this batch against its captured baseline rather than treating all uncommitted changes as new work.

The reviewer independently:

- Checks the batch against the roadmap, approved scope, dependencies, and acceptance criteria.
- Inspects relevant code changes, design choices, and actual test evidence; distinguishes fresh results from historical or synthetic evidence.
- Identifies bugs, missing tests, unresolved risks, and unsupported completion claims.
- Checks that task records, `docs/continuation.md`, and `docs/workflow-review.md` reflect actual progress, keeping unapproved proposals separate.
- Reports findings by severity with file/evidence references, impact, and required follow-up; states remaining issues and verification limits even when no material finding remains.

Keep the reviewer focused on assessment. The implementation agent owns fixes, documentation updates, and focused verification. After material findings are fixed, return the changed code and evidence to the same reviewer for recheck. Reviewer clearance does not replace user approval, manual acceptance, or outstanding integration checks. If independent review is unavailable, report that limitation rather than describing self-review as independent.

The reviewer may recommend temporary specialist agents only for a concrete need, explaining each specialist's purpose and bounded scope. Ask the user and receive approval before creating them; the reviewer must not spawn them autonomously. Temporary specialists do not require changing `AGENTS.md` unless lasting working rules change. All agents must preserve uncommitted work, permissions, historical data, audit records, safe retries, and tests.

## Preservation, Security & Scope

Preserve uncommitted work, useful tests, audit records, safe retries, and saved comparisons. Inspect dependencies before deleting files; retain historical evidence. Keep changes scoped and justify new dependencies or background services. Avoid unnecessary broad builds and full demos. Use synthetic or permitted redacted fixtures; keep secrets and private document contents out of logs and examples.

Never silently confirm unchecked values, treat incomplete checks as a complete match, or expand permissions. New reviews use invoice/PO; retain earlier quotation compatibility. Keep Arabic text and English digits. Use plain, adult business language in product screens and guides.

## Commits & Pull Requests

Recent history uses `feat:` and `chore:` prefixes with concise descriptions. Keep commits scoped. PRs should explain the problem, resulting behavior, relevant issues, validation, and remaining risks. Include screenshots for visible changes and label illustrative data.
