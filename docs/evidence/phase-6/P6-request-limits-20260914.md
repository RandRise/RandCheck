# Request and resource limit verification

September 14, 2026. The [request-limit contract](../../design/phase-6-request-limits.md)
records defaults, ordering, bounded state, body limits and deployment constraints.

## Results

221 distinct checks pass: the complete API suite (196) and 25 PostgreSQL cases.
Eight body-boundary cases were rerun after tightening timeout attribution; they pass
and are already included in the 196 API cases. There are 27 new API cases and three
new PostgreSQL cases in this increment. Earlier restricted-login database/tenant
checks contribute the remaining 22 PostgreSQL cases. No checks were skipped.
The documentation build and whitespace checks pass.

| Artifact | Scope |
| --- | --- |
| `api-final.trx` | All existing API contracts plus request-budget, body and configuration checks |
| `body-final.trx` | Final body limits, real chunked Kestrel traffic, trailing-slash uploads and cancellation/timeout attribution |
| `persistence.trx` | Three request-limit scenarios plus 22 restricted-login permission/tenant regressions |
| `verification.json`, `unique-tests.txt` | Deduplicated final totals and test names |
| `retained-before.txt`, `retained-after.txt` | Read-only full-row fingerprints for 34 retained tables |
| `docs-build.txt` | Documentation build output |

Artifacts are in `outputs/phase-6/request-limits-20260914`. PostgreSQL tests ran
serially against the disposable `document_intelligence_test` on loopback port 25434.
Private CLI projects compiled with that suite. No schema migration, deployment restart,
browser UI change or paid provider invocation was needed. NuGet auditing was disabled
for offline test commands; this does not claim a fresh dependency audit.

## Verified boundaries

- Monotonic window expiry, atomic multi-budget admission, parallel concurrency bounds,
  exact-once lease release, bounded partition saturation and safe expiry reclamation.
- Default enforcement outside local profiles and startup refusal for disabled enterprise
  limits or invalid configuration. Local compatibility tests retain the disabled profile.
- Ingress refusal precedes database grant verification and unread cited bodies. Forged
  forwarding/identity headers cannot choose a different partition; normalized IPv4 and
  IPv4-mapped addresses share a budget. Separate peers still share process capacity.
- Server-bound actors have separate budgets and share their organization's budget.
  Signed-token PostgreSQL tests use actual memberships with a restricted API login.
  Revocation is checked before an exhausted actor budget; window expiry restores capacity.
- Work exhaustion blocks query body reads while ordinary document reads remain available.
  Source GET downloads count as work, and a throttled download does not scan or release
  the source. A valid grant can be retried successfully after the limiter window expires.
- A throttled upload creates no files, document/job rows, scanner calls or workflow audit
  entries. Retrying after window expiry admits and audits exactly one new upload/job.
- Known/unknown-length JSON and actual chunked Kestrel bodies observe total byte caps.
  Exact limits pass and one extra byte fails; upload-route trailing slashes preserve the
  correct allowance. Stalled reads return 408, client cancellation propagates, and a
  later workflow deadline is not relabeled as a body timeout. Original streams are restored.

## Corrections and limits

The initial unknown-length JSON fixture returned 400 because directly assigning a
TestServer body did not provide a readable HTTP request body. It now sends an actual
streamed HttpContent, with independent wire-level Kestrel coverage. Initial results are
retained in `limits-initial.trx` and `body-recheck.trx`; final successful cases supersede
them. Timeout attribution was tightened to require a cancellation observed during a
body read, instead of treating every later workflow cancellation as an input timeout.

Counters are per process, reset on restart and are not a distributed quota. Partition
capacity and shared budgets can cause legitimate requests to receive 429 under load.
Metrics export, edge limits, trusted-proxy configuration and workload measurement
remain deployment work. Durable queue/storage capacity admission is next. Full Phase 6
acceptance still requires the outstanding deployment, recovery and operational controls.
