# Project baseline recheck - October 11, 2026

Rand requested fresh verification before further modifications, then added Python
organization to the refactoring scope. This turn runs checks and records findings;
it does not implement the proposed dashboard, workflow, roles, or Python refactor.

## Result

No confirmed functional regression was found in the exercised application paths.
This is a qualified local baseline, not a complete integration or release sign-off.
A web test failed in the full run and passed unchanged in isolation. Broader Python
lint has existing findings. Five pinned OCR runtime cases remain unexecuted.

| Check | Fresh result |
| --- | --- |
| .NET domain | 210 passed |
| .NET application | 410 passed |
| .NET API | 314 passed |
| .NET infrastructure | 222 passed |
| .NET architecture | 4 passed |
| Web full Vitest suite | 325 passed, 1 failed, 0 skipped |
| Retention file, focused follow-up | 6 passed, including the initial failure |
| Review-request Node tests | 3 passed |
| Synthetic preview adapter | 17 passed |
| TypeScript and whole-web ESLint | Passed |
| Initial Python pytest run | 497 passed, 1 failed, 6 skipped; 298 subtests passed |
| Python fixture runtime follow-up | 7 passed in the separate existing fixture interpreter |
| Python native PDF follow-up | 1 passed with its runtime gate explicitly enabled |
| Python CI lint rules | Passed: E9,F63,F7,F82 over apps/api/app and tools |
| Broader Python lint | Failed: 175 findings across application and test files |

.NET total is **1,160 passing tests**, no skips. Each project compiled its needed
assemblies using the installed SDK and existing restored packages. No full solution
or Next production build was run.

The latest web result covers all 326 distinct cases, but there was no single clean
full-suite run: retain the initial failure and focused follow-up separately.
Six follow-up cases overlap the original run and must not be added to 326.

Python has **499 distinct passing parent cases** across the appropriate runtimes:
491 application cases, seven fixture cases, and one native PDF case. The original
497 includes six fixture passes; the seven-case rerun adds only the previously
failing case. The native follow-up replaces one initial skip. Five OCR cases remain
unexecuted. Do not add the 298 subtests to the parent-case count; pytest's XML
includes them in its larger raw total. The Windows interpreter is Python 3.11.9,
not the documented enterprise Python 3.11.16 baseline.

## Findings and disposition

Review was self-review; no independent reviewer or subagent was used.

1. **Web timing sensitivity remains open.** The retention test named
   `requires an explicit confirmation and the hold version to release` failed
   after 5.31 seconds in the full run. Its JSON error retains a Vitest
   `STACK_TRACE_ERROR` creation stack. Duration is consistent with the default
   five-second timeout; the report alone does not establish the underlying cause.
   The entire retention file passed in 1.17 seconds of test time on an isolated
   rerun with the same assertions and default timeout. No test or production fix
   was made. Explicit confirmation and expected-version checks were retained.
2. **Python fixture failure was a runner/environment mismatch.** The first broad
   invocation ran the legacy generator with Pillow 12.3.0 and reportlab 3.6.13.
   It requires Pillow 9.4.0 and reportlab 3.6.12. All seven fixture cases passed in
   `tmp/phase2-fixture-runtime-20260910`, without installing packages or changing
   saved fixtures. Exact native PDF evidence also passed in the application runtime.
3. **Broader Python lint needs a separate cleanup batch.** Findings include 70
   import-order issues, 24 dictionary-construction suggestions, 19 nested-context
   suggestions, and other style/review items. They are not 175 proven runtime bugs.
   `main.py:40` repeats `provider_timeout` with the same 504 value, so that duplicate
   does not change current responses. No automatic lint fixes were applied.
4. **Python test discovery needs attention when organizing files.**
   `tools/run_python_regression.py` uses unittest discovery; function-style pytest
   tests such as the supplier-invoice verification cases are not discovered by
   that runner. This recheck used pytest for the broad application run, so those
   cases were exercised. Preserve that coverage when improving the CI runner and
   moving modules. Do not treat the existing unittest runner as the entire suite.
5. **Browser check could not run.** The preview was stopped. The browser tool
   failed twice during initialization with `trusted Node process exited
   unexpectedly; kernel reset`. No preview server was started and no visual pass
   is claimed. Automated preview tests use synthetic responses.

## Preservation and evidence

Evidence lives in `outputs/project-recheck-20261011`:

- `verify.ps1`, `followup.ps1`: exact commands, sequential heavy checks.
- `gates.json`, `followup-gates.json`, `python-ci-lint.json`: exit codes and timing.
- Five .NET TRX files, web JSON, Python XML, manifests, and separate text logs.
- `python-lint.json`: complete broader lint findings.
- `baseline.json`, `preservation.json`: all 1,120 pre-existing Git-visible files
  unchanged after tests. This report and the later continuation note are the only
  documentation additions/updates made afterward; final preservation is recorded
  separately.

Earlier historical S3/refactor receipts were not overwritten or counted as fresh
execution. Existing test assertions, permissions, audits, retries, Arabic source,
English-digit rules, unreviewed-value protections and old comparisons are unchanged.
No database was accessed, migrated, or provisioned by this recheck. No package
restore/install, full demo, paid model call, or deployment was performed. No owned
application or test process remains running.

Real PostgreSQL persistence, extractor-to-backend integration, pinned OCR runtime,
live identity/recovery, Next production/browser behavior, report/export, deployment
and user acceptance remain separate gaps. Native PDF evidence is a narrower pass;
it does not establish general Arabic/scanned extraction quality.

## Next Python organization batch - proposed scope

Python is included in Rand's requested professional organization. Keep related
code together in purposeful modules rather than creating a file per tiny function.

1. Start with the API entry point: separate request/response models, error response
   policy and handlers, then route/service responsibilities. Keep application
   assembly easy to read and preserve private-service authorization.
2. Split `local_extraction.py` (1,344 lines) in a later bounded batch: constants and
   page models, PDF/OCR adapters, field rules, and source-evidence helpers. Preserve
   time limits, model hashes, coverage warnings and original Arabic evidence.
3. Group tests with their features, retain regression cases, and correct test
   discovery so pytest functions remain in the maintained verification path.

Before each implementation batch, capture the current source, show its before/after
VS Code diff, review imports and behavior, run the affected checks, and give a short
manual checkpoint. No Python file moves have been made in this verification turn.
Dashboard, Add documents / Check details / See results, and Owner/Staff remain
unapproved proposals.

## Optional two-minute manual check

When ready, start the existing `Start preview.cmd` and open Invoice checks. Create a
sample check, save Completed invoice and Completed purchase order, then refresh.
Both documents should remain saved; review revisions should not be chosen
implicitly and comparison should remain blocked until its requirements are met.
Use Open invoice review and its return link to check navigation back to that same
case. This uses synthetic data and is not real integration acceptance. Stop it with
`Stop preview.cmd` when finished.
