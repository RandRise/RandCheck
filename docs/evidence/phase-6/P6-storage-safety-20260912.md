# Storage security verification — September 12, 2026

Rand authorized continuing Phase 6. This increment implements the
[storage admission and malware screening contract](../../design/phase-6-storage-safety.md).
It does not complete Phase 6 or establish production readiness.

## Results

| Check | Result |
| --- | --- |
| Application regression | 337 passed, zero skips, including receipt forwarding and failed-admission compensation |
| Infrastructure regression | 157 passed, zero skips, including 34 new storage, scanner, receipt and worker checks |
| API regression | 153 passed, zero skips, including five storage environment-mode cases |
| PostgreSQL integration | Eight passed, zero skips: seven new admission/worker failure cases and the existing audited-upload compensation case |
| Retained database | All 34 table count/content fingerprints match both the previous checkpoint and this increment's before/after snapshots |
| Spending / migrations | 464 spend events, 153 reservations and 15 migrations unchanged; no paid calls or new migration |

There are 655 unique passing checks. Earlier runs are not added to the total.
The entire PostgreSQL suite, Python and browser suites were not rerun; their prior
evidence remains separate. Browser code and its existing snapshots are unchanged.
Evidence is in `outputs/phase-6/storage-safety-20260912`, including four TRX reports,
retained fingerprints and machine-readable verification.

## What was verified

- Staged bytes remain outside final source paths until clean scanning completes.
  Failed scans remove their owned stage. Existing destinations, including a file
  created during scanning, cannot be overwritten or removed by failed promotion.
- Declared-size overruns stop after one excess byte without scanning. Locked modes
  reject before reading. Hash verification detects changes made during a scan.
- A real loopback TCP fixture verifies clamd command/chunk framing and exact bytes.
  Complete clean replies pass. Detection, errors, duplicate or incomplete results,
  alternate framing, oversized replies, timeout and disconnect block admission.
  Caller cancellation is preserved. Remote and unconfigured scanners refuse input.
- Missing or altered receipts, mismatched hash/size/policy, mismatched actor or
  organization and unbound audit contexts reject before database access.
- A signed-token HTTP upload against disposable PostgreSQL admits clean files and
  binds clean-scan audit to the caller, document, hash and size. Infected/unavailable
  results create no document/job/source. Forced clean-scan audit insertion failure
  rolls back the document/job and compensates the promoted source. Independent
  request-failure audit remains available. Events and error responses omit private
  source content, original filenames and raw scanner text.
- Enforced workers check stored path/bytes/hash and rescan before extractor dispatch.
  Infected, unavailable and tampered fixtures make no extractor call and preserve
  the source. A clean fixture reaches the existing completion flow.
- Real restricted PostgreSQL worker logins record safe System audit reasons.
  Scanner unavailability terminates after three attempts; malware and integrity
  failures terminate immediately even if a caller supplies a retryable flag.
- API hosts default storage to Locked in production, reject Development there,
  accept explicit Enforced and preserve the Testing development default.

PostgreSQL fixtures recreate only `document_intelligence_test`, with one runner.
Unique worker roles are removed in test cleanup. The original Phase 5 database is
queried read-only. No retained actor remapping, model enablement or spending change.
.NET uses `-p:NuGetAudit=false` for offline metadata lookup; this is not a current
dependency advisory assessment.

## Limits and next work

Scanner outcomes are injected in HTTP/worker tests; protocol tests use a synthetic
TCP peer. No real ClamAV daemon, signatures, EICAR detection, scanner configuration
or definition freshness was verified. Deployment must handle uninspectable content
and scan limits. Filesystem permissions/encryption and privileged local races were
not deployment-tested. Crash-leftover staging cleanup, original-file authorized
access/expiring links, retention/deletion, live Entra/TLS, remaining tenant mutations,
API runtime grants and rate limits remain open.

The contract links official ClamAV protocol and scanning guidance. Existing browser
snapshots remain in `outputs/phase-6/browser-20260911`; this increment changes backend
behavior only and does not claim new browser screenshots.
