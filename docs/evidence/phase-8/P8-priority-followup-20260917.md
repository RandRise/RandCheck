# Phase 8 priority follow-up — September 17, 2026

This local follow-up investigates the two unexplained database process exits,
adds explicit failure detection, corrects the language-support claim and reviews
publication candidates. Verification was closed out on September 19 with emphasis
on the important local checks, as requested by the user.
No source commit, push, external account or publication is authorized by this work.

## Result

The local demo passed twice: fresh setup/migrations, synthetic upload and extraction,
append-only review, exact persistence after container recreation, and guarded reset.
Services use loopback only. See the [runbook](../../local-demo.md),
[screenshots](P8-local-demo-20260916.md) and [structured receipt](P8-priority-receipt.json).

The final isolated CI source `8e7fa1227ac2` records 1,785 passing tests: 1,365 backend,
64 browser/request, 351 Python/tool and five database-health cases. Release and
Locked browser builds, browser lint, migration checks and the documentation build
passed. This includes the final crash-test harness change. The run was interrupted
during dependency audits and has no runner completion receipt; it is **not an
all-green CI run**. Those audits were not repeated. Its remaining owned container
and networks were removed on September 19, with a separate cleanup receipt.

The historical database exit-code-2 cause remains unproven. Startup now tolerates
the separately reproduced slow initialization and rejects a database that crashed
and recovered. Phase 7 Arabic/bilingual support, license selection, hosted CI and
clean-HEAD/publication acceptance remain pending.

## Final safety check — September 19

All ten task-owned project inventories are empty: no containers, networks or named
volumes remain, and demo ports 3308/8308 have no listeners. Source copies, images,
evidence and caches remain. The retained public-table fingerprint exactly matches
the baseline SHA-256 `06c07f7f05eb9d20a7ceb561d43821414427bd79d0453e170a65ae41132c7549`.
The retained cluster was initially stopped. It was started on loopback for the
read-only comparison, completed recovery of its prior interruption and was cleanly
stopped afterward. No reset or migration was applied to retained data.

The fresh Locked-browser/source/history/selected-log scan covered 1,258 files with
26 exact reviewed findings, zero unresolved findings and zero errors. This scan
preceded final report edits; it is not a repository-wide publication clearance.
The final documentation edits were checked directly; no duplicate build or suite
was started after the user narrowed verification.

## Database investigation

The retained logs identify abnormal exits at `2026-09-16T15:24:15Z` and
`2026-09-16T15:35:42Z`, followed by PostgreSQL crash recovery. These occurred
minutes after the final server started, not during its normal temporary-server
initialization shutdown. The original logs and failed attempts remain intact.

PostgreSQL 16.14's [backend SIGQUIT handler](https://github.com/postgres/postgres/blob/REL_16_14/src/backend/tcop/postgres.c)
exits with code 2; its [connection-startup code](https://github.com/postgres/postgres/blob/REL_16_14/src/backend/postmaster/postmaster.c)
also documents that behavior. This makes a signal-related exit a plausible lead,
not a proven diagnosis. The old logs do not identify a signal sender or establish
memory exhaustion. The available rotated Docker VM logs begin after those events;
the host log excerpts inspected at their timestamps do not establish a cause.
The earlier no-OOM observations remain observations, not proof against all resource
problems. The current Docker VM reports four CPUs and about 3.76 GiB of memory.

The launcher previously accepted successful HTTP readiness even if the database
had crashed and recovered. `tools/demo/database-health.ps1` now rejects abnormal
process exits, crash reinitialization, automatic recovery and PANIC, and checks
running/health/restart/OOM state. Startup invokes it before reporting success;
`local-demo.ps1 health` repeats it. Dated receipts and raw database logs remain in
the ignored source copy. It accepts normal initialization shutdown. Five regression
cases cover normal initialization, exit code, signal, recovered crash and PANIC;
both historical failed logs also trigger rejection.

This change detects a failure instead of hiding it. It does not fix or establish
the historical root cause. No security gate, database durability setting, memory
allocation or global Docker configuration was relaxed.

The repeat investigation found a separate, reproducible initialization-budget
failure. In copy `46c5e22bfa20`, the database container started at 08:31:00 UTC;
the final TCP server became ready at 08:32:49. Compose had already exhausted its
startup checks and rejected the dependency. The saved database log contains normal
initialization and no abnormal exit/recovery; the final diagnostic check was healthy.
The host sample reported 481,168 KiB free physical RAM out of 8,251,192 KiB while a
separate npm install was in progress. This observation does not prove the historical
exit-code-2 cause.

The demo now allows a 180-second initial health-check grace period, retaining the
TCP probe and post-start crash-history rejection. Docker's [health-check semantics](https://docs.docker.com/reference/dockerfile/#healthcheck)
allow an early successful probe to end that grace period. This accommodates slow
initialization without declaring an unready database usable. The subsequent trials
run without a concurrent host dependency installer.

## Local verification

The first diagnostic copy, `b51db8b8670c`, used the unchanged demo configuration.
Upload, local extraction, append-only review and restart persistence passed, with
healthy database observations beyond five minutes from container startup. Its
owned volumes were reset. Repeated trials use the new launcher in fresh source
copies; `tools/verify-demo-reliability.ps1` records their outcomes and cleanup.

The first instrumented trial, `be1a95e3b775`, passed 322.7 seconds of observation
and ten API probes, then restart persistence and cleanup. Its second cycle,
`3d350c30a9a4`, failed before service startup with BuildKit's `forwarding Ping: no
such job`; Docker's VM log corroborates the build-stage error. A separate retry
produced the initialization-budget failure described above. Both failed receipts
are preserved with successful owned-project cleanup; neither is relabelled as passed.

The final CI stage results are summarized above; raw receipts remain in ignored
outputs. Earlier failures are preserved and are not relabelled as passing.

The combined attempt in snapshot `d6c134b92f18`, project `uaedi-ci-8fd9c7bc4e10`,
passed workflow checks, the five health cases, a zero-warning/error Release build
and the first 932 backend tests. Its PostgreSQL suite reported four
recovery crash-checkpoint failures (`recovery_store_unavailable`) and one child
generation checkpoint wait timeout. It is not an all-green result. The captured
database log window shows no abnormal process exit or automatic recovery. Production
deadlines and assertions have not been relaxed. After those five
failures, the owned backend test container was deliberately stopped to prioritize
diagnosis. Exit code 137 records that stop, not an observed OOM event. The remaining
tests were not completed; the attempt's receipt remains `passed: false` with
`cleanupPassed: true`. Its disposable database and network were removed.

The diagnostic-only cold-process probe `uaedi-probe-a22c36096222` reproduced three
recovery failures and one pass. Exception-type/stack tracing captured operation
cancellation at the existing ten-second deadline, before the requested commit
checkpoint; it did not expose database error values. The initialization variant
`uaedi-probe-37d381d63285` passed all eleven recovery/generation crash cases with no
skips. Across 33 child processes, EF model/connection initialization took 3.07 to
15.37 seconds; no recovery cancellation was recorded in that variant.

The test harness now initializes EF and opens/closes its guarded disposable
connection before invoking the operation under test. Setup writes no data. Its
parent process allows 60 seconds, covering bootstrap and the coordinator's existing
45-second bound. Recovery's ten-second deadline, generation's operation limits,
kill checkpoints, durable-state checks, accounting/audit assertions and no-redispatch
checks are unchanged. Diagnostic tracing is not included in the implementation.
This isolates crash-atomicity verification from process initialization. Cold recovery
calls can still hit their strict deadline on this constrained host; no cold-start
latency or production-readiness claim is made. The final source's complete backend
suite subsequently passed, including all 433 PostgreSQL integration cases.

The final two-trial run passed on the revised startup configuration:

| Isolated source copy | Healthy observation | API probes | Restart and cleanup |
| --- | ---: | ---: | --- |
| `054e4a8ac7d1` | 314.6 seconds | 9 | Passed |
| `64dc5c57b9ad` | 331.2 seconds | 10 | Passed |

Each trial started with fresh disposable volumes, verified an empty store, uploaded
the fictional English invoice, extracted it locally, completed append-only review,
observed database/API health and recreated the containers. The complete review
workspace and source bytes persisted. Final health checks found no abnormal process
exit or crash recovery. Both guarded resets removed only the owned database/upload
volumes; source and evidence remain. The raw receipt is
`outputs/demo-reliability-90c1b7ef12d1.json`. Two short local trials do not establish
long-term reliability or resolve the historical cause.
Both 297-file demo source inventories still match the current workspace exactly;
the later backend test-harness changes are outside the demo's source allowlist.

The optional host browser install first hit sandbox network-denial errors. Its
network-enabled retry eventually completed in 16 minutes with zero reported npm
vulnerabilities, but no host browser build is used as final evidence. CI can now
export its newly built Locked browser output with `-ExportBrowserReview`, for a
private scan without a duplicate host build. Export is off by default and excluded
from the hosted artifact allowlist.

## Publication preparation and wording

The UI now states that English review is available and Arabic/bilingual support
is planned. This [real browser capture](05-english-scope-20260917.jpg) shows the
revised statement with synthetic reviewed data. Historical screenshots remain
unchanged and retain their English-only disclaimer.

The initial credential scan examined 1,223 files across current text candidates,
reachable history, selected demo logs and the prior Locked browser build. It found
zero unresolved findings/errors after 26 exact existing review decisions. A later
scan uses the fresh Locked browser output from the final isolated CI source. One
browser finding moved to a new artifact path; its complete file hash, rule and line
exactly match the prior reviewed artifact. The new exact-path decision preserves
that review without a broad suppression. Final counts are in the structured receipt.
The inventory separately found two documentation server logs eligible
for Git inclusion. `*.log`, `*.trx` and generated `output/` packs are now ignored;
all underlying files are preserved. Reviewed sample inputs remain in `samples/`.

See the [publication review](../../security/publication-review.md) for source/sample
selection, provenance and outstanding license/PII review. No project license was
selected on the user's behalf. An inventory or pattern scan alone is not full
publication clearance. OS/image vulnerability review, dependency notices, hosted
CI and fresh-checkout acceptance remain outside the completed local evidence.
