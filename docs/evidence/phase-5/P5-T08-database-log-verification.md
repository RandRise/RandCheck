# P5-T08 database-log verification

**Date:** 2026-09-08. Application-side privacy increment; server-log privacy gate remains open.

## Outcome

Three new tests exercise real PostgreSQL persistence and capture application
database logs. Successful indexing/retrieval/history/evidence and an expected
uniqueness conflict passed initially. A controlled conversion error failed:
PostgreSQL put the synthetic input into its primary error message, and EF's
query-failure logger copied that message into application logs.

The production `AddInfrastructure` registration now supplies EF with a
`DatabasePrivacyLoggerFactory`. For every error/critical event, or any event
carrying an exception, it discards the original message formatter, structured
state, and exception object. It emits a fixed diagnostic with the original
category, event ID and severity. Non-exception events below Error retain their
normal EF telemetry. This applies to all EF contexts created through this
registration, not just cited endpoints; it does not change stored data, queries,
exception propagation, transaction behavior, or API contracts.

This is a deliberate diagnostic tradeoff: database errors remain countable and
alertable by event/category, but unrestricted raw error messages and stack traces
are not sent through this application logger. The previously added cited API
boundary still returns safe error responses. Direct diagnostic listeners/APM,
manually constructed contexts, independently configured driver loggers, and other
exception logging outside this EF factory are not covered.
Non-error SQL telemetry still requires parameterized queries and disabled
sensitive-data logging; this factory does not sanitize arbitrary literal SQL,
user-controlled SQL identifiers, or externally supplied logging scopes.

All three live database checks passed after this change. The full .NET suite
passed **385 tests, 0 failures, 0 skipped**: 333 non-persistence and 52 PostgreSQL.

## Verified scenarios

`CitedDatabaseLoggingTests.cs` adds three cases to the existing isolated
PostgreSQL fixture:

- Actual private Python indexing and PostgreSQL lexical retrieval, same-key
  replay, fresh-scope saved history, source listing, exact page quote, and
  reviewed-fact reason. Synthetic filenames, page text, question, request key,
  and review reason reach their intended results but not captured application
  logs. A real EF command-executed event must be present.
- Two real persisted retrieval runs with the same organization/actor/request
  key. The second save returns `ConstraintConflict`; application error telemetry
  remains present without exposing the synthetic request key.
- A parameterized test-only integer conversion of synthetic nonnumeric input.
  The original `PostgresException.MessageText` demonstrably contains that input
  even though error-detail and parameter logging are disabled. Application error
  events no longer contain it. This probe is not a user-facing query endpoint.

Capture includes formatted messages, structured fields/string arrays, exception
text, and supplied scopes, with application logging at Trace. Tests assert that
EF sensitive-data logging, Npgsql `Include Error Detail`, and Npgsql `Log Parameters`
remain disabled. Capture starts after fixture seeding and does not claim privacy
of test setup/migration diagnostics.

Ten infrastructure unit cases verify exception stripping at all six log levels,
error/critical stripping without an exception, preservation of ordinary command
telemetry, and ownership of the underlying logger factory. An intentionally
unformattable state/formatter proves stripped diagnostics are not evaluated.

## PostgreSQL server-log finding — not fixed

The same synthetic marker appeared in the isolated server's own log:

- Conversion failure: primary `ERROR` message included the invalid input.
- Uniqueness conflict: `DETAIL` included the offending request-key value.

The observed local PostgreSQL **16.4** settings were:

```text
log_destination = stderr
logging_collector = off
log_min_messages = warning
log_min_error_statement = error
log_error_verbosity = default
log_statement = none
log_min_duration_statement = -1
log_parameter_max_length = -1
log_parameter_max_length_on_error = 0
```

`pg_ctl -l` directs this cluster's stderr to
`tmp/phase5-postgres-20260907/server.log`. This file is separate from captured
application telemetry. The synthetic marker is `private-database-value-4197`;
matching lines preserve the evidence without exposing customer information.
The log was not erased, truncated, or reconfigured to make the test pass.

Npgsql's error-detail option concerns client exception detail and can contain
sensitive data; it does not govern PostgreSQL's own log. Its parameter-logging
option is also separate. See [Npgsql connection settings](https://www.npgsql.org/doc/connection-string-parameters).
PostgreSQL documents separate controls for error statements, verbosity and bind
parameters, and warns that logged statements can expose sensitive information.
Disabling bind-parameter logging alone is not a claim of sanitized server error
messages. See [PostgreSQL 16 logging controls](https://www.postgresql.org/docs/16/runtime-config-logging.html).

Before real-invoice deployment, choose and verify a server-log policy covering
restricted operational access, protected storage/export, retention/deletion, and
any redaction before logs reach broader sinks. Redaction in a later sink does not
remove data already written to the original server file. Reducing error verbosity
does not guarantee that primary error text is private. Do not silently suppress
all server errors as a substitute for this decision. No global server logging,
Windows permissions, retention policy, or production infrastructure was changed
in this increment. Until that gate is addressed, keep these acceptance runs on
synthetic data and do not describe database logging as fully private.

## Verification record

The baseline report `tmp/phase5-test-results/phase5-database-logs-baseline.trx`
records 2 passes and 1 privacy failure. The focused hardened report
`phase5-database-logs-hardened.trx` records 3 passes, 0 failures. They are retained
separately; failed exploratory runs are not final passing evidence.

The full regression uses `phase5-database-logs-final*.trx` in the same directory.
It passed 111 domain, 88 application, 52 infrastructure, 4 architecture,
78 API, and 52 PostgreSQL cases. This is 10 new non-persistence cases and
3 new PostgreSQL cases over their respective prior counts. The PostgreSQL
report ends in `20260908223522.trx`; the API report ends in `20260908223604.trx`.
Database tests took approximately 6 minutes 12 seconds and include the full
existing migration/constraint/isolation/retrieval suite, not just the new checks.
Debug solution build passed with **0 warnings and 0 errors**. The documentation
build passed using the approved Windows path-resolution sandbox fallback.

## Environment and reproduction

The existing isolated PostgreSQL cluster was verified stopped before this run.
Sandboxed startup failed with Windows restricted-token error 87; the approved
startup outside the sandbox succeeded on **127.0.0.1:25434**. No Docker restart
or new runtime/dependency installation was needed. Private Python tests use the
existing `.venv/Scripts/python.exe`, ephemeral loopback ports, and test-only
worker tokens; helpers stop their own processes.

The fixture recreates and later drops **only `document_intelligence_test`**.
The retained `phase5_e2e_20260908` acceptance database is separate and must not
be reset. Use the cluster startup command in the [continuation checkpoint](../../continuation.md), then:

```powershell
$env:TEST_POSTGRES_CONNECTION = 'Host=127.0.0.1;Port=25434;Database=postgres;Username=document_intelligence;Password=local_development_only'
$env:PHASE5_PYTHON = (Resolve-Path '.venv/Scripts/python.exe').Path
dotnet test apps/backend/UaeDocumentIntelligence.sln --no-restore --logger 'trx;LogFilePrefix=phase5-database-logs-final' --results-directory tmp/phase5-test-results
```

The connection password above is the existing local fixture value, not a live
provider credential. After the full test run, a read-only database listing
confirmed `document_intelligence_test` had been dropped by fixture teardown,
while `phase5_e2e_20260908` remained. No other client sessions were connected.
The deleted database contained disposable synthetic test data and can be
recreated by the fixture; no application/acceptance database was removed.
The isolated cluster was then stopped with the approved `pg_ctl -m fast -w stop`.
A follow-up `pg_ctl status` reported no server running, and its log confirmed
shutdown at 22:37:30 +04 on 2026-09-08. The synthetic error log remains available.

## Remaining limits

No migration, extraction/OCR production change, frontend change, paid request,
generated answer, real-invoice accuracy claim, production patch-level/security
certification, or Phase 5 completion is introduced. The full PostgreSQL suite
is rerun here; browser/web and complete Python/OCR acceptance are not. Verbose
URL/query metadata from the [previous checkpoint](P5-T08-workflow-log-verification.md),
server-log privacy, deployed telemetry/access/retention, admission controls,
model grounding/injection defense, and wider acceptance remain open.
