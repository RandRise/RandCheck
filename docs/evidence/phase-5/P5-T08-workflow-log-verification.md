# P5-T08 workflow-log privacy checkpoint

**Date:** 2026-09-08. Provider-free increment; Phase 5 remains in progress.

## Findings and changes

The expanded logging tests found two gaps beyond the earlier request-body checks:

1. The real typed private HTTP client exposed a synthetic worker token and
   response header content when its default logging was enabled at Trace level.
   `RedactLoggedHeaders` masked the formatted message, but this installed .NET 8
   implementation still supplied the raw values to structured logging sinks.
   The regression capture checks both representations, including string arrays.
2. Unexpected repository/service exceptions could leave the cited endpoint's
   safe error path. A message containing synthetic private text propagated out
   through the test host rather than becoming a safe API problem response.

The private cited client now removes **only its own default HTTP-client loggers**
and emits event 5110 with a fixed internal operation, success/failure outcome,
HTTP status when available, and elapsed milliseconds. It records no headers,
body, exception object, caller URL, filename, question, or worker token. Other
HTTP clients and application logging are not globally disabled. The wire token
and body are still sent correctly; the transport stub verifies this.

The cited middleware now catches unexpected errors from the downstream endpoint
pipeline and returns `500 cited_request_failed` with a generic message. Event
5108 contains the fixed error code/status, not the original exception. If the
response has already started, it logs `cited_response_aborted` and aborts instead
of appending an error document. Client cancellation is not converted to 500.
Existing workflow-specific safe errors retain their existing status/code.
Original request streams are restored in all tested downstream outcomes.

This deliberately trades raw exception diagnostics in this boundary for safe
failure events. Debugging must use opaque identifiers and controlled reproductions;
do not re-enable unrestricted exception/body logging with customer data.

## Verification scope

**323 non-persistence .NET tests passed, 0 failed, 0 skipped:** 111 domain,
88 application, 42 infrastructure, 4 architecture, and 78 API tests. This is
16 new cases over the previous 307-test checkpoint. PostgreSQL was filtered
out, not counted as passing or skipped. Raw final solution reports are
`tmp/phase5-test-results/phase5-workflow-logs-final*.trx`; its API report ends
in `20260908215237.trx`. The separate API confirmation report is
`phase5-workflow-logs-api-confirmed.trx` after the final assertion cleanup.

The API confirmation passed all **78 cases** again. Debug solution build passed
with **0 warnings and 0 errors**, and the documentation build passed using the
approved Windows sandbox path-resolution fallback. No dependency upgrade,
migration, persistent service startup, or paid call was required. Existing
loopback boundary tests stopped/disposed their temporary hosts.

`CitedWorkflowLoggingTests` has 14 new cases using the real ASP.NET pipeline,
application handlers, domain objects, and registered private HTTP client. Only
persistence/workspace ports and external HTTP transport are replaced with
in-memory fixtures. No database, Python service, paid model, or real customer
document is used by these new tests.

- Successful question and raw retrieval POSTs, same-key replay without another
  private call, question/raw history GETs, source listing, exact page evidence,
  and reviewed-fact provenance return expected private fixture text to the
  authorized test response while keeping it out of captured logs.
- No-hit refusal and its saved history retain the question without logging it.
- Private-service rejection, malformed JSON response, transport exception,
  and cancellation/timeout translation return safe 502/503/504 responses. The
  cancellation case injects a cancellation exception; it is not a wall-clock
  measurement of the private client's 30-second deadline.
- Unexpected failures during request lookup, lexical execution, saved-run
  loading, page loading, and source listing return safe 500 responses. A failed
  lexical execution still marks the run `retrieval_interrupted` in the fixture.
- Two metadata cases distinguish the shipped log-level configuration from
  an explicitly verbose capture. Both exclude payload/credential markers.

Two additional boundary tests cover downstream client cancellation and a response
that has already started. The latter uses HTTP feature doubles, not a network
test of connection-abort behavior. The earlier stream-restoration test now
expects the safe 500 contract and asserts its observations outside middleware.

Log capture inspects formatted messages, raw structured fields, string-array
values, exception text, and supplied scopes. Successful private operations must
emit the safe event; HTTP default log categories for that client must be absent.
Unexpected failures must emit their fixed failure code. Tests do not pass merely
because no logger was attached.

### Development iterations

The first run exposed an incomplete test fixture (missing required field
evidence), which was corrected. The actual baseline then had 13 failures and
1 pass. Adding safe API failure handling and formatted header redaction left
11 failures and 3 passes: raw structured header values still leaked. This led
to the client-specific safe telemetry change. These failed exploratory reports
are retained separately and are not counted as final passes.

## Known metadata limitation

With the shipped log levels (`Microsoft.AspNetCore: Warning`), the successful
test request's synthetic URL/query metadata marker is absent from capture.
Enabling verbose ASP.NET request diagnostics makes that marker visible. The
accepted `X-Request-ID` is intentionally returned to the caller, as before;
correlation identifiers and URL/query strings are **not confidential storage**.
The verbose test characterizes this limitation rather than claiming it is fixed.

Do not place invoice text, tokens, or personal information in URLs, query
parameters, or request IDs. Arbitrary debug logging, proxy access logs, diagnostic
listeners/APM, and deployed log-sink configuration require a separate privacy
review. This increment does not rewrite global logging settings or correlation
contracts.

## What remains unproved

- The in-memory stores do not establish privacy of real EF/PostgreSQL command or
  exception logs, especially if sensitive-data logging or database error detail
  is enabled. Dependency logs emitted before an exception reaches middleware
  cannot be erased by the new boundary. Index creation/chunking success was not
  rerun through this fixture; it shares the same private client implementation.
- Body-read/host failures before endpoint execution, other API families, real
  network abort behavior, retention, production authentication, admission/rate
  limits, and concurrent-request resource use remain separate gates.
- No frontend, OCR/Python processing, real-invoice evaluation, generative-model
  grounding/injection defenses, spend controls, or Phase 5 completion are proved.

See the [prior boundary checkpoint](P5-T08-request-boundary-verification.md),
[threat model](../../security/phase-5-rag-threat-model.md), and
[continuation checkpoint](../../continuation.md).

## Reproduce

```powershell
$env:PHASE5_PYTHON = (Resolve-Path '.venv/Scripts/python.exe').Path
dotnet test apps/backend/UaeDocumentIntelligence.sln --no-restore --filter 'FullyQualifiedName!~UaeDocumentIntelligence.Persistence.Tests' --logger 'trx;LogFilePrefix=phase5-workflow-logs-final' --results-directory tmp/phase5-test-results
```

PostgreSQL is deliberately excluded. Earlier PostgreSQL, web, native-PDF browser
acceptance, and full Python/OCR results are historical, not rerun claims here.
