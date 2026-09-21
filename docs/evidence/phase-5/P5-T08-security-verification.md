# P5-T08 provider-free security checkpoint

**Date:** 2026-09-08
**Status:** Provider-free backend controls and one synthetic full-stack browser workflow verified.
This is a subset of P5-T08, not Phase 5 acceptance or a security certification.

## Changes and security boundary

Saved result content previously remained publicly readable after its source
index was retired, even though source opening rejected that index. Public
history GET and same-key question/retrieval POST replay now recheck **every
frozen source index**, including sources with no hits. A missing or non-Ready
index returns 409 `evidence_unavailable` before assembling the response's hits.
The immutable database audit snapshot, refusal IDs, and hit records are retained.
This deliberately tightens the earlier history-read contract.

Index replay now uses an untracked lookup so a long-lived scope cannot return
its cached Ready entity after another scope retires it. Raw retrieval-history
GET has a 15-second deadline, matching the question-history read bound.

All cited routes use `Cache-Control: no-store`, including invalid route IDs and
malformed request bodies. This policy runs before endpoint parameter binding.
It reduces HTTP cache exposure; it is not server-side deletion, browser-memory
erasure, or an atomic revocation protocol.

## Tests completed

- **331 .NET tests passed:** 282 non-persistence (111 domain, 88 application,
  42 infrastructure, 4 architecture, 37 API) and 49 PostgreSQL tests.
- Six new API cases verify malformed-body errors do not echo a supplied marker
  and that malformed bodies/invalid history or citation routes are no-store.
- Two new persistence cases cover both retirement and quarantine: positive and
  no-hit history, raw retrieval GET, same-key replay, new-key retrieval, and
  same-scope index replay all reject ineligible sources. Database audit hits,
  refusal ID, and run count remain unchanged.
- One new persistence case sends hostile source text and questions through the
  real private Python planner/indexer and PostgreSQL retrieval. Direct and
  zero-width instruction text, a false system label, HTML, an external URL,
  an invented hit ID, and SQL-shaped question text remain inert evidence.
  Results stay in the selected document, no generated answer is created, the
  invented hit cannot be opened, and document/run records remain intact.
- Existing cross-organization/actor, corpus filtering, forged/swapped citation,
  quarantine-open, exact-byte, and safe-rendering tests remain part of their
  respective earlier suites. These new cases are not added twice to the total.
- .NET Debug build passed with zero warnings/errors. No schema change was made.
- All 20 web checks passed again (17 component tests and 3 contract-script tests).
- EF reported no pending model changes.
- Documentation build and scoped whitespace checks passed. No web production
  source or Python application source changed in this checkpoint; web lint/
  production build and the complete standalone Python/OCR suites were not rerun.

TRX results: `tmp/phase5-test-results/phase5-security*.trx`.
Persistence uses the designated disposable `document_intelligence_test`
database on the isolated cluster, not the application's database.

## Full-stack verification

The checked-in `tools/phase5_evidence_acceptance.ps1` targets
only a loopback API and uploads the existing synthetic standard-services PDF,
confirms extracted fields for fixture setup, completes its review, indexes it,
searches, opens exact evidence, reloads/replays, and requests a no-hit refusal.
It preserves existing output reports and does not reset or delete databases.
Scripted confirmation is not a human accuracy review.

The real API run passed all 20 assertions, including response cache policy.
The invoice reached `Ready`, with 8 extracted fields, 3 lexical hits, exact
page evidence containing the synthetic AED 8,820.00 amount, stable POST replay,
read-only history, and a persisted no-hit refusal. The API report is retained
at `outputs/phase-5/security-20260908/full-stack-api.json`.

The actual Next.js browser then pinned that completed review, submitted its
own question through ASP.NET/Python/PostgreSQL, opened highlighted bytes 0–970
of page 1, opened reviewed-fact provenance, obtained a no-hit refusal, and
reloaded the original saved question. After a scoped test-only SQL quarantine
of the verified synthetic index, source opening cleared the prior panel and
history reload cleared the prior hits, both with a safe error. No public
quarantine administration endpoint was invented for the test.

Database inspection afterward confirmed 2 Retrieved runs, 2 completed refusal
runs, 6 hits, 2 refusal answers, and 0 factual claims, with the index Quarantined.
Browser observations and exact IDs are retained in
`outputs/phase-5/security-20260908/browser-verification.json`. These are real
services, not the previous display-fixture helper. The source view is still
canonical text, not a PDF image. The browser check used the desktop viewport;
the earlier narrow-screen fixture check was not counted as a new real-stack run.

The first setup attempt sent a generic multipart content type and received the
expected 415. The helper now explicitly sends `application/pdf`. The host Python
runtime then returned `ocr_unavailable` because approved extraction dependencies
were absent. These failures are retained as setup findings, not successful
acceptance. They did not justify weakening the extractor's runtime checks.

Docker image startup became unresponsive. A separate Python 3.11 environment
under `tmp/phase5-native-runtime-20260908` successfully installed the repository's
declared dependencies, including all five pinned extraction packages. A download
certificate-chain error was resolved by propagating `PIP_USE_FEATURE=truststore`
to pip's build subprocess; TLS verification stayed enabled and no certificate
store was changed. This follows [pip's system-certificate guidance](https://pip.pypa.io/en/stable/topics/https-certificates/).
The successful native-PDF path used this environment, not the stalled container.
It does not establish Tesseract scan/OCR runtime acceptance.

### Reproduction environment

Use an isolated, newly named synthetic database. This run used
`phase5_e2e_20260908` on localhost:25434, separate from the regression fixture's
`document_intelligence_test`. Apply the existing nine migrations to that new
database only. Enable development identity seeding and the extraction worker.

Temporary API settings were `ConnectionStrings__Database` for that database,
`AiService__BaseUrl=http://127.0.0.1:8007`, `Storage__UploadDirectory` pointing to
the absolute repository `tmp/phase5-e2e-20260908/uploads` directory, and
`Cors__Origins__0=http://127.0.0.1:3004`. Python used the same absolute
`STORAGE_SHARED_ROOT`, `EXTRACTION_PROVIDER=local`, and a matching test-only
worker secret (`WORKER_SHARED_SECRET` in Python and `AiService__WorkerToken` in
ASP.NET). `ASPNETCORE_ENVIRONMENT=Development`, `DevelopmentData__Seed=true`,
and `ExtractionWorker__Enabled=true` enable the existing fixture identity and
worker. No `.env` or application configuration file was changed.

Start Python with the isolated interpreter and
`-m uvicorn app.main:app --app-dir apps/api --host 127.0.0.1 --port 8007 --no-access-log`.
Start the real API with `dotnet run --no-build --no-restore --no-launch-profile
--project apps/backend/src/UaeDocumentIntelligence.Api --urls http://127.0.0.1:8086`.
Set `NEXT_PUBLIC_API_URL=http://127.0.0.1:8086` in the web terminal only and run
`npm run dev -- --hostname 127.0.0.1 --port 3004` from `apps/web`.

Run `./tools/phase5_evidence_acceptance.ps1 -PrivateBaseUri http://127.0.0.1:8007
-ReportPath outputs/phase-5/NEW-RUN/full-stack-api.json` from the repository root.
The script requires a new output path and both services healthy before upload.
It creates new synthetic documents; it does not reuse the now-quarantined
acceptance index or overwrite a previous report.

For regressions, build the .NET solution, set `PHASE5_PYTHON` to the existing
`.venv/Scripts/python.exe`, set `TEST_POSTGRES_CONNECTION` to the isolated
PostgreSQL server, then run `dotnet test apps/backend/UaeDocumentIntelligence.sln
--no-build --no-restore`. The fixture explicitly recreates only its designated
test database, so never point it at valuable data with that database name.

### Temporary environment cleanup

Final service cleanup is recorded in the [continuation checkpoint](../../continuation.md).
The browser tab, Next.js, ASP.NET, native Python, and isolated PostgreSQL were
stopped; none of their test ports were listening afterward. Docker did
not return from startup or the named temporary-container removal request;
the container's existence/cleanup was initially unconfirmed. Rand later approved
a Docker Desktop restart. Normal restart and Docker-only process/WSL recovery
were attempted; intermediate checks reported startup timeouts/API 500 errors.
After the extended startup period, the engine recovered: `docker info` returned
version `29.6.2` and two successful exact-name listings confirmed the temporary
container absent. Cleanup is now complete.
No Docker data reset, volume/image prune, or global WSL shutdown was performed.
Only `phase5-e2e-ai-20260908` was targeted for removal, with read-only
source/synthetic-upload mounts and loopback port publishing.

## What remains unproved

- There is no generative model in this path. Inert text tests do **not** prove
  prompt-injection resistance, entailment, grounded answers, or reliable refusal
  when nonempty retrieved evidence is insufficient. No heuristic injection
  scanner/quarantine classifier was added or claimed to solve that problem.
- No production authentication/RBAC, comprehensive tenant security, retention,
  log-capture audit, distributed cache invalidation, or atomic concurrent
  revocation/erasure guarantee is established here.
- Whole canonical-page text highlighting is not a PDF image/polygon viewer or
  evidence of OCR accuracy. Real invoices and vector/hybrid comparison remain
  separate evaluation gates.
- This checkpoint uses no paid model calls. The USD 10 future API-test budget
  does not configure credentials, select a provider, or enforce spending.

## Continue without repeatedly asking about context

Rand authorized automatic checkpoint/handoff judgement on 2026-09-08. A
read-only checkpoint agent found the prior contracts and review sufficient to
continue in this task. It cannot observe the main task's context usage or force
compaction. Codex's model-default automatic compaction remains unchanged; see
the [official configuration reference](https://learn.chatgpt.com/docs/config-file/config-reference).
No recurring automation, setting change, or separate user task was created.
Keep current changes, verification commands, temporary services, and next steps
in the review/checkpoint before any future handoff.
