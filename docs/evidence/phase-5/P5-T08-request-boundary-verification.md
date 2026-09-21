# P5-T08 cited-request boundary checkpoint

**Date:** 2026-09-08. Provider-free security increment; Phase 5 remains in progress.

## Changes

The four `/api/cited-indexes`, `/api/cited-retrievals`, `/api/cited-questions`,
and `/api/cited-sources` path families now have a **65,536-byte (64 KiB) request
body limit before JSON binding or handler execution**. This includes unknown
subpaths and bodies on methods other than POST. Prefix lookalikes are excluded.

- A declared oversize body returns 413 without being read. Unknown-length and
  chunked bodies are counted as read; application buffering stops at 65,537
  bytes. The bounded buffer is in memory, not a temporary disk file.
- Exactly 65,536 payload bytes are permitted through to normal validation.
  JSON escaping, whitespace, and UTF-8 bytes count toward this limit. The existing
  4,000 UTF-16-character question and 100-source bounds fit within it, including
  a JSON-escaped Arabic question. Those logical bounds are not increased.
- Body reading has a separate 15-second deadline and safe 408 response. Client
  cancellation is propagated, not relabelled as a server read timeout. Existing
  handler deadlines remain separate, not one shared end-to-end deadline.
- Cited responses retain `Cache-Control: no-store`. Request-ID middleware runs
  first so rejected application requests retain correlation headers.
- The three JSON POST endpoints parse explicitly and discard parser diagnostics
  before returning generic 400 responses. This prevents JSON property paths in
  parsing exceptions from entering framework binding logs. JSON content-type
  metadata remains available for endpoint discovery; unsupported types get 415.
- Application rejection event 5108 records only a fixed error code and status,
  not payloads, request keys, credentials, or parsing exceptions.

The invoice `/api/documents` upload path and its configured **10 MiB file limit
are unchanged**. This increment does not harden multipart buffering or change
upload transport limits. Existing host/proxy wire limits are left unchanged,
including any smaller operator-configured limit.

## Verification

**307 non-persistence .NET tests passed; 0 failed, 0 skipped:** 111 domain,
88 application, 42 infrastructure, 4 architecture, and 62 API tests. The API
suite includes 25 new boundary/privacy cases in `CitedRequestBoundaryTests`.
PostgreSQL was excluded by filter and is not counted as passing or skipped here.
Raw reports are `tmp/phase5-test-results/phase5-request-boundary-final*.trx`;
the API report ends in `20260908042725.trx`. The initial failing exploratory
report `phase5-request-boundary-api.trx` is retained and is not the final result.

Debug solution build passed with **0 warnings and 0 errors**. The VitePress
documentation build passed. Its first sandboxed attempt failed with Windows
`EPERM` while resolving the user-directory path; the approved build outside
that sandbox completed. No dependency updates were needed. The final API test
assembly timestamp is later than the last boundary-test source change.

New cases cover declared and streamed oversize, exact-limit bodies, UTF-8 byte
counting, maximum escaped logical inputs, malformed/private JSON keys and
values, invalid GUID values, empty/null JSON, unsupported content type, bounded
read counts, original-stream restoration after failure, route isolation,
preservation of a smaller host limit, read deadline, and client cancellation.
Log capture includes framework/application formatted messages, structured fields,
exceptions, and supplied scopes. Rejection-event assertions make relevant
privacy checks non-vacuous; unsupported-type routing also asserts captured logs.

Two real HTTP/1.1 Kestrel tests verify actual chunked transfer, exact-limit
acceptance and limit-plus-one rejection, no-store, and absence of a synthetic
private body marker from captured logs. These use ephemeral loopback ports and
stop/dispose their server instances. They do not use PostgreSQL or a provider.

### Issues found while testing

The initial test-only Kestrel host used the default Windows Event Log provider,
which failed under this environment's access restrictions. The test now uses
console/captured logs, matching the application's existing explicit logging
setup. No Windows permissions or production logging settings were changed.

The real-server test also caught an exact-limit chunked body being rejected
when the Kestrel host limit was lowered to the payload limit. The final guard
counts payload bytes itself and leaves the independent host limit alone.
An initial unsupported-type test incorrectly expected an application rejection
event even though endpoint content-type routing rejects that request earlier;
its assertions now check the actual 415/no-store and captured-log behavior.
Failed exploratory runs are not counted as passing verification.

## Reproduce

From the repository root, with the existing private chunking Python environment:

```powershell
$env:PHASE5_PYTHON = (Resolve-Path '.venv/Scripts/python.exe').Path
dotnet test apps/backend/UaeDocumentIntelligence.sln --no-restore --filter 'FullyQualifiedName!~UaeDocumentIntelligence.Persistence.Tests' --logger 'trx;LogFilePrefix=phase5-request-boundary-final' --results-directory tmp/phase5-test-results
```

PostgreSQL is explicitly excluded from this increment's regression run. Its
earlier 49 passing tests and the earlier 20 web checks/native-PDF browser flow
are recorded in the [previous checkpoint](P5-T08-security-verification.md),
not presented as rerun here. There are no database/schema, Python production,
or frontend source changes in this increment.

## What this does not prove

This is not a complete logging/privacy audit: successful database/provider
paths, proxy/host logs, URL/query/header redaction, retention, and production
telemetry configuration remain separate checks. The tests use synthetic markers,
not real customer information. Transport failures rejected before this middleware
may use host-generated responses rather than these application contracts.

The bounded per-request buffer/deadline is not admission control, rate limiting,
or distributed denial-of-service protection. Concurrent-request memory use,
connection/header limits, reverse-proxy behavior, HTTP/2/3, multipart uploads,
and model spending controls remain outside this test scope. No decompression
middleware is enabled or evaluated here.

No paid requests, provider credentials, generated answers, semantic-support or
model prompt-injection claims, production authentication, or Phase 5 completion
are introduced. See the [threat model](../../security/phase-5-rag-threat-model.md)
and [continuation checkpoint](../../continuation.md) for remaining work.
