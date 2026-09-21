# P5-T08 — Durable pilot spending ledger

**Date:** 2026-09-09  
**Scope:** PostgreSQL accounting component and synthetic tests. No paid transport,
credentials, premium billing, generated-answer UI or public ledger endpoint.

**Follow-up:** the later [internal answer bridge](P5-T06-T08-answer-bridge-verification.md)
connects this component to fake-provider tests. The results and remaining work
below describe the ledger-only checkpoint, not the later integration state.

## Outcome

The pilot now has one persisted USD 8 admission allowance shared across
organizations, within the user's USD 10 future test budget. Creating a new
organization cannot create another pilot allowance through this component.
This is not yet an enforced limit on real API spending: all paid dispatch is
still disconnected and the live-readiness gates remain open.

`ICitedGenerationLedger` is registered in Infrastructure but has no HTTP route
or background caller. Its reserve and dispatch methods both require an explicit
server-controlled enablement value. No runtime configuration enables them.
Tests call these methods directly with synthetic rates and costs.

## Persistence and workflow

Migration `20260908210639_AddCitedPilotSpendingLedger` adds only:

- `cited_pilot_budgets`: one database-constrained singleton, initially USD 8,
  accumulated settled cost and a reconciliation-required flag.
- `cited_spend_reservations`: organization/actor/operation identity, request
  fingerprint, complete rate snapshot, model/version, rate expiry, token maxima,
  reserved amount, actual amount and state.
- `cited_spend_events`: ordered, attributed state-transition records. Application
  `SaveChanges` rejects updates/deletes of existing event records.

Every operation uses a fresh context and transaction. A PostgreSQL row lock on
the singleton serializes admission and transitions; a partial unique index also
prevents multiple pending reservations. The budget preflight calculation runs
inside that lock before reservation and event insertion commit together.

| State/action | Accounting behavior |
| --- | --- |
| Reserve | Save the worst-case allowance before any dispatcher can proceed |
| Replay the same operation | Return the original record; changed inputs conflict, terminal records never restart |
| Mark dispatched | Exactly one competing caller receives `true`, after commit; all later attempts receive `false` |
| Cancel before dispatch | Release a still-Reserved hold; cannot cancel a dispatched/uncertain call |
| Mark uncertain | Keep the full allowance reserved; no automatic timeout release or retry |
| Settle | Atomically add verified actual cost, close the hold and append its audit event |
| Actual cost exceeds quote | Record the actual cost and freeze further admission for reconciliation |

Reservation checks the rate validity window and dispatch rechecks its expiry
against database time. An expired pre-dispatch reservation can be explicitly
cancelled; expiry is not authority to erase an unknown charge.

Amounts use decimal microdollars. Settlement rejects negative, over-precision,
out-of-range or unreferenced amounts instead of allowing database rounding.
Identical settlement replay does not debit twice; conflicting amount/reference
replay is rejected. Reapplying the migration does not reset existing spending.

## Organization and privacy boundary

Reads and mutations require the reservation's organization and actor from the
server execution context; the actor must exist in that organization and remain
active. Foreign-organization, foreign-actor and inactive-actor attempts fail
without returning the reservation. Database composite foreign keys enforce
actor/organization consistency. There is no public cross-organization balance API.

Stored request fingerprints and model/rate metadata contain no invoice body or
question. Settlement evidence references must be opaque internal references,
not pasted invoices, secrets or raw provider errors. Database failures become
fixed application errors without the original exception attached. Existing EF
privacy logging remains in use; this does not sanitize PostgreSQL's own logs.

This is structural ownership enforcement, not completed login/RBAC. Future
settlement must be called only by trusted usage reconciliation or an authorized
operator, never from a user's submitted cost value. The evidence-reference
string alone does not prove a provider charge. Operator approval, provider usage
verification and ledger-administration UI are not implemented here.

## Verification

The 12 focused PostgreSQL tests passed. They cover disabled/invalid calls,
fresh-service-provider replay, changed-key rejection, eight concurrent identical
reservations, competing organizations, eight competing dispatch attempts,
uncertain-cost holds, settlement replay/conflict, shared ceiling, organization/
actor checks, overrun freeze, expiry, database constraints and event immutability.
Injected event-write failures verify that dispatch and settlement/balance updates
roll back with their audit record. Pre-cancelled requests do not create holds.

The focused report is `tmp/phase5-test-results/phase5-spend-ledger-focused.trx`.
The first test compilation attempt had a helper argument-order mistake; it was
corrected before the focused test run. A migration alignment check against stale
startup binaries initially reported changes; rebuilding the API and rerunning
the check confirmed no pending model changes. No extra migration was generated.

The full .NET solution regression passed: **411 tests, zero failures/skips**.

| Project | Passed |
| --- | ---: |
| Domain | 111 |
| Application | 102 |
| Infrastructure | 52 |
| Architecture | 4 |
| API | 78 |
| PostgreSQL persistence | 64 |

Reports: `tmp/phase5-test-results/phase5-spend-ledger-final*.trx`. The full run
also verifies a valid different actor in the same organization is denied, and
reapplying migrations does not reset settled spending. Debug builds passed with
zero warnings/errors; the rebuilt EF startup reports no pending model changes.
Python, OCR and frontend/browser suites are not claimed as newly rerun; the full
database regression includes the existing private Python integration tests.
The documentation production build passed using the approved Windows host
fallback, with a non-blocking bundle-size warning. `git diff --check` passed;
Git also reported its existing LF-to-CRLF normalization notices.

## Environment after the PC restart

Only the existing isolated PostgreSQL cluster was needed and restarted:
`tmp/phase5-postgres-20260907`, loopback `127.0.0.1:25434`. Docker, the persistent
web UI, public API and Python development server were not required or started.
The migration is exercised only in `document_intelligence_test`, which the
fixture recreates and drops. The retained `phase5_e2e_20260908` acceptance database
is separate and was not migrated or reset. Final read-only checks confirmed the
retained database still exists, the disposable test database was dropped by its
fixture, and no other client connections remained. The test database can be
recreated by the fixture; the retained invoice evidence was preserved.
PostgreSQL is left running for continued work. A sandboxed process-status check
could not see the process; the approved host-level status check and SQL server
address/data-directory queries confirmed the correct cluster on loopback :25434.

## Remaining gates and next step

- Connect the ledger to the private answer adapter with fake-provider integration
  tests before any live transport. Only the committed dispatch winner may call
  a provider; never automatically retry after an ambiguous commit or API outcome.
- Measure the complete prompt/schema/evidence token count before dispatch,
  bound transport output, verify authoritative usage/cost and charge retries
  explicitly. Add embedding-specific accounting against the same allowance.
- Preserve the logical operation ID across job retries. No browser-supplied
  model, rate, cost or fresh operation ID may bypass server admission policy.
- Test actual dispatcher/process termination and lost-response windows. Current
  restart evidence uses new service providers/connections plus durable state and
  injected transaction failures; it is not a full OS/crash fault-injection suite.
- Recovery of stranded holds and clearing an overrun freeze requires verified
  operational reconciliation. No automatic release or public reset is provided.
- This allowance covers one canonical database and calls routed through this
  ledger. Other API clients, restored older backups, cloned databases or privileged
  direct SQL writes are outside that guarantee. Reconcile provider billing after
  recovery; do not restore/reset the ledger to obtain fresh allowance.
- Authentication, raw-log access/retention, production backup/recovery/privacy,
  real-model quality and broader Phase 5 acceptance remain tracked in the
  [roadmap](../../roadmap.md). No Phase 5 completion is implied.
