# Phase 6 local acceptance — September 16, 2026

**Accepted for the supported local portfolio contract.** 597 fresh checks pass
with no failures or skips. Backend, production browser and documentation builds pass.

The [claim-to-evidence matrix](../../security/phase-6-local-acceptance.md) maps each
local security criterion to concrete tests and earlier evidence. Completion is for
the independent GitHub portfolio scope. Real Entra, deployed TLS/scanner/storage,
protected key delivery, edge limits and production operations remain unverified.
The enterprise preflight still reports `DeploymentVerified=false`.

## Changes from the preceding checkpoint

- The Development storage cleanup loop now logs a fixed message without exception
  state. A regression drives the real background loop through a synthetic private
  failure and checks the message, structured state and exception argument.
- Git ignores private environment variants and key containers, temporary databases,
  raw logs, downloaded runtimes, generated output and Vite caches. Example environment
  files remain visible. No generated or retained file was deleted. This prevents
  ordinary staging of those directories; explicit force-add remains possible.
- A standard-library scanner reviews current Git text candidates, unique reachable
  historical text blobs, selected logs and a required production client build. It
  checks credential formats, literal assignments and exact private environment
  values. Reports include metadata only. Unreadable/oversize/missing inputs fail.
- The threat model now describes current controls and residual risks, including
  MSAL sessionStorage, lack of tenant RLS against compromised service identities,
  raw PostgreSQL logs and the local retention/recovery limits.

## Credential review scope

The isolated production build is
`apps/web/.next-phase6-acceptance-20260916`. It was built with three synthetic
server-only markers in `OPENAI_API_KEY`, `AZURE_CLIENT_SECRET` and
`PRIVATE_SERVICE_TOKEN`. No provider was contacted. All three markers are supplied
to the scanner again without printing them. The scan covers browser static JS/maps
and prerendered HTML/RSC, not just the source tree.

Log roots are `outputs/phase-6` and `tmp/phase5-postgres-20260907`, limited to `.log`
and `.trx` files. UTF-8 and BOM-tagged UTF-16 are supported. Reachable Git history
contains six commits. Current candidate binary/unknown extensions are listed as
excluded; historical scanning selects text paths only. Other
ignored directories, inaccessible/unreachable Git objects, arbitrary encodings,
private stores and unselected logs are outside this review. This is bounded
credential detection, not proof that every possible secret or private document is
absent. Raw PostgreSQL logs contain known synthetic source/request diagnostics and
remain private; the application logger cannot sanitize them.

Twenty-six candidate matches were individually reviewed: synthetic test credentials,
one test source expression and MSAL protocol field names. Decisions in
`docs/security/phase-6-secret-review-decisions.json` bind scope, path, whole-file
SHA-256, rule and line. A changed file or new match requires review again. There is
no blanket test-directory exception. The scanner has four isolated-repository
tests for detection, output redaction, missing/unreadable inputs and stale decisions.

Initial scans also exposed generated caches and the live PostgreSQL directory as
Git candidates, including non-text database files. Ignore rules fixed that staging
boundary; selected logs remain explicitly scanned. Initial scan reports are kept
locally and superseded by the final review, not presented as passes.

## Fresh results and snapshots

Machine outputs are under `outputs/phase-6/acceptance-20260916`; `verification.json`
records counts, scope and hashes. `secret-review.json` records complete inventory,
zero unresolved findings, 26 reviewed matches and zero read errors.

| Check | Passed | Evidence |
| --- | ---: | --- |
| API regression | 260 | `api.trx` |
| Infrastructure regression, including cleanup privacy | 220 | `infrastructure.trx` |
| PostgreSQL signed-token security walkthrough | 35 | `walkthrough-final.trx` |
| Browser regression | 64 | 61 Vitest + 3 Node checks, `web-tests.log` |
| Python service security | 14 | `python-service-tests.log`; 13 additional subtests excluded from total |
| Credential scanner regression | 4 | `scanner-tests.log` |
| **Total fresh primary checks** | **597** | Historical runs excluded |

Backend build: zero warnings/errors. Browser production build: passed. Documentation
build: passed with the existing large-chunk advisory. Python reports two dependency
deprecation warnings. All 34 read-only retained fingerprints match the previous
checkpoint. No schema change, retained mutation or paid call occurred.

[Actual synthetic audit snapshot](P6-local-acceptance-audit-20260916.json): 14 events,
including four admissions, four denials, two failed requests, two grants, one
membership change and one organization creation. An admission marked Succeeded
only confirms admission; paired business failures remain visible. This is an actual
backend fixture capture, not a browser screenshot. No browser UI changed.
An initial documentation command used a nonexistent script and performed no build;
the declared `npm run build` then passed. An initial PostgreSQL filename filter matched zero tests because the fixture uses
a shared partial class. Only the final method-name walkthrough counts as evidence.

The September 15 [runtime regression](P6-runtime-20260915.md),
[audit coverage](P6-audit-coverage-20260915.md),
[archive checks](P6-audit-export-20260915.md) and
[actual backup/restore drill](P6-local-recovery-20260915.md) remain separate evidence
for unchanged paths. Their counts are not added to this run. No fresh package
advisory audit or deployed-image validation is claimed here.

## Remaining delivery work

Next is Phase 8's clean-checkout local demonstration, followed by curated browser
screenshots, CI and publication inventory. Arabic support remains Phase 7. No
domain/cloud account is required. No deployment, commit, push or publication occurred.
