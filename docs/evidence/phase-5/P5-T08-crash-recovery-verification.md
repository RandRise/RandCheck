# P5-T08 — Application-process crash and restart

**Date:** 2026-09-09  
**Scope:** Abrupt termination of a separate .NET process around real PostgreSQL
commits, followed by a new process using the same logical operation. Synthetic
invoices and a fake answer adapter only; no paid calls or published answers.

## Outcome

All seven focused crash/restart scenarios passed. Committed reservations,
dispatches and settlements survive abrupt application termination; uncommitted
changes roll back. Restart cannot dispatch an already-dispatched operation or
charge a settled operation twice. No production fix was required by these tests.

The new test harness invokes the existing production coordinator and spending
ledger. It pauses at a named boundary, flushes a content-free checkpoint marker,
and waits. The parent requires that exact marker, kills only its launched child,
waits for confirmed exit, then checks the database from another connection.
Restart means launching another OS process, not rebuilding a service provider
inside the original process. The operation, request and rate snapshot stay fixed.

The test project has an explicit executable entry point; normal `dotnet test`
discovery remains supported. The child accepts only the exact disposable
`document_intelligence_test` database on `127.0.0.1` and a `test-only` provider.
It cannot migrate/reset databases and does not start application workers.
Transaction interceptors exist only in the test process. Production source,
routes, dependencies and schema are unchanged.

## Interruption matrix

| Process killed at | Durable state after termination | Same-operation restart |
| --- | --- | --- |
| Before reservation commit | No reservation/event | May reserve and call once |
| After reservation commit, before return to coordinator | Reserved | May dispatch once |
| Before dispatch commit | Reserved; dispatch event rolled back | May dispatch once |
| After dispatch commit, before return to coordinator | Dispatched; fake adapter not called | Denied; allowance held |
| After fake-adapter acceptance, before returning its result | Dispatched; one fake receipt | Denied; allowance held |
| Before settlement commit | Dispatched; settlement/event/balance update rolled back | Denied; allowance held |
| After settlement commit, before return to coordinator | Settled; cost and event retained | Denied; no second charge |

Each case checks durable state, adapter-call count, event count, settled balance,
cross-organization rejection after restart and absence of a published answer.
Where a dispatched hold remains, cancellation and a new reservation must fail.
For the two cases with known fake acceptance but no settlement, the test invokes
the existing internal settlement method twice with the same synthetic amount
and evidence reference. Only one settlement is recorded; the original operation
still cannot dispatch again. This tests settlement idempotency, not an
implemented trusted operator workflow or real provider reconciliation.

The fake adapter writes a committed operation marker to a test-only table in
the fixture database's separate `crash_probe` schema before pausing/returning.
It does not change the application's `public` schema. The table is not part of the
application schema or migrations. Its receipt is proof of this fake invocation,
not a remote provider's billing statement. Synthetic input/output usage remains
100/50 units at test rates, for USD 0.000025; the reservation is USD 0.001650.

## Verification

- Focused process-failure run: **7 passed, zero failures/skips**.
- Non-persistence .NET regression: **362 passed** (111 Domain, 102 Application,
  67 Infrastructure, 4 Architecture, 78 API), zero failures/skips. Normal test
  discovery also worked with the new test-only executable entry point.
- Combined database regression: **47 passed**, zero failures/skips: 12 ledger,
  15 answer-coordinator, 13 prompt-preflight and seven crash cases. This includes
  the existing private .NET/Python fake-answer round trip. Together with the
  non-persistence suite, **409 unique .NET cases** passed; focused repetitions
  do not increase that count.
- Debug solution build passed with zero warnings/errors. EF reports no pending
  model changes. Docs build passed with the existing non-blocking bundle-size
  warning; `git diff --check` passed with line-ending normalization notices.
- Final seven-case rerun after moving receipts into their own test schema:
  **7 passed**, zero failures/skips (`phase5-crash-recovery-schema-final.trx`).
  Every crash case also invokes the existing exact application schema/constraint/
  index assertion while that separate schema is present. No production source
  changed between the combined regression and this final harness-only rerun.
- The initial harness run failed seven cases: the added interceptor registration
  was not taking effect, and one denial assertion used the wrong error code.
  Explicit test-only options registration and the corrected expected code fixed
  the harness. These were not production defects. The initial report is retained;
  the focused rerun passed all seven cases. A preliminary compile error in the
  harness's asynchronous line-read call was also corrected before testing.
  Review then identified that receipts in `public` would contaminate the
  existing exact-schema test. They were moved into `crash_probe`; the application
  schema assertions were preserved rather than relaxed.

Reports use `tmp/phase5-test-results/phase5-crash-recovery-*.trx`. This is a
targeted PostgreSQL regression, not a new full database/OCR/frontend/browser
acceptance run. Python source is unchanged; the prior 66 Python cited-unit/route
results are historical and are not claimed as newly rerun here.

## What this does not prove

- PostgreSQL remains alive. These are application-process failures, not an OS
  crash, power loss, PostgreSQL restart, disk/WAL failure, backup restoration,
  network partition or cloned-database recovery test.
- The post-commit pause represents the application dying before receiving the
  ledger's return value. It does not inject loss of a PostgreSQL wire-level
  commit acknowledgement. The fake-adapter pause is not a real HTTP/provider
  lost-response test. Prior .NET/Python transport tests remain separate evidence.
- A dispatch committed just before the process dies can hold the allowance even
  when no adapter call happened. This deliberate fail-closed behavior trades
  availability for duplicate-spend protection; restart cannot assume zero cost.
  An accepted call can similarly leave a hold without a saved answer. There is
  no automatic hold release, public reset or result-replay implementation.
- Trusted recovery still needs authenticated operator authorization, verified
  independent usage evidence, safe operator attribution, handling of inactive
  original actors, freeze resolution and restored/duplicated database controls.
  Current settlement is an internal organization/actor-scoped primitive, not
  completed Phase 6 admin/RBAC or an approved recovery service.
- Exact provider counting/rates/usage, embedding accounting, semantic support,
  model-injection evaluation, real invoice accuracy, raw-log/storage/backup
  privacy and generated-answer acceptance remain open. This is not a live
  spending guarantee or completion of Phase 5.

## Reproduction and environment

From the repository root, with the existing isolated PostgreSQL cluster running
on loopback `25434`:

```powershell
$env:TEST_POSTGRES_CONNECTION='Host=127.0.0.1;Port=25434;Database=postgres;Username=document_intelligence;Password=local_development_only'
dotnet test apps/backend/tests/UaeDocumentIntelligence.Persistence.Tests --no-restore --filter Generation_process_crash --logger 'trx;LogFileName=phase5-crash-recovery-focused.trx' --results-directory tmp/phase5-test-results
$env:PHASE5_PYTHON=(Resolve-Path .venv/Scripts/python.exe).Path
dotnet test apps/backend/tests/UaeDocumentIntelligence.Persistence.Tests --no-restore --filter 'FullyQualifiedName~Prompt_preflight|FullyQualifiedName~Generation_|FullyQualifiedName~Ledger_' --logger 'trx;LogFileName=phase5-crash-recovery-db-final.trx' --results-directory tmp/phase5-test-results
dotnet test apps/backend/UaeDocumentIntelligence.sln --no-restore --filter 'FullyQualifiedName!~UaeDocumentIntelligence.Persistence.Tests' --logger 'trx;LogFilePrefix=phase5-crash-recovery-nonpg' --results-directory tmp/phase5-test-results
```

Do not run database suites concurrently: the fixture owns one disposable database.
Only the fixture database is recreated/reset/dropped. The retained synthetic
`phase5_e2e_20260908` database must not be used for fixture resets. No key, funding,
Docker engine or persistent public API/UI is needed for these checks.

Final SQL checks confirmed PostgreSQL remains on `127.0.0.1:25434`, the fixture
removed its disposable database, the retained acceptance database remains
present, and no other database clients remain. Every launched crash child was
waited to exit, including cleanup paths. The disposable data is reproducible
from the fixture; retained synthetic invoice evidence and historical logs were
not removed. PostgreSQL remains running for continued work.

## Next step

The follow-up [read-only assessment](P5-T08-recovery-assessment-verification.md)
defines and tests the recovery authority/evidence boundary; all production
callers remain denied. Transactional execution and operator audit still need
implementation before any privileged operational surface. Continue provider-free
work here; a new task is unnecessary while the saved checkpoint is sufficient.
The broader gates remain unchecked in the [roadmap](../../roadmap.md).
