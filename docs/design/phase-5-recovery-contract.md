# Phase 5 spending recovery boundary

**2026-09-10 policy amendment:** The user accepted a small development-pilot
uncertainty instead of further billing investigation. The separate
[retained unknown hold policy](phase-5-retained-unknown-pilot-policy.md) permits
new work while keeping that amount reserved and its actual cost unknown. The
verified-billing recovery rules below remain unchanged; their blanket freeze
requirement is superseded only for the new explicitly accepted pilot action.

**Status:** Assessment and internal synthetic-only execution implemented; live authority/evidence unavailable  
**Date:** 2026-09-09  
**Scope:** Stranded cited-generation reservations in the canonical pilot ledger.
No public administration route, paid provider, automatic hold release or shared
budget reset is enabled.

## Two distinct steps

1. **Assess:** an explicitly authorized operator inspects a scoped spending
   snapshot and, optionally, a trusted evidence attestation. The result is a
   recommendation only, bound to the observed state and event sequence.
2. **Execute:** separately authorize the mutation, reload the
   canonical state under the budget lock, reverify evidence and commit an
   attributable, idempotent recovery event together with the accounting change.

An assessment is neither a signed capability nor an approval token. Every
assessment has `Executable=false`, `MayRedispatch=false` and
`MayClearSharedFreeze=false`. No writer accepts it. It is deliberately possible
for the database to advance after assessment; the executor rejects stale
expectations. Never use a read-only permission as
permission to mutate money.

## Authority and organization scope

The current `ICitedRecoveryAuthorization` registration denies everyone, including
the fixed development identity and the original requester. There is no body flag,
role string or configuration toggle that grants recovery access. The application
service checks the authority port before reading the database or resolving proof,
then rechecks it before returning. The registered evidence verifier is unavailable.
Only explicit test substitutes currently allow an assessment with proof.
Execution uses its own `ICitedRecoveryExecutionAuthorization`, also denied by
default. It additionally rejects non-`test-only` reservation providers. Neither
interface has an HTTP endpoint or an enabling configuration switch.

The reader filters the reservation and audit-sequence query by organization and
checks the active operator in the same SQL statement. It returns spending
metadata only: no invoice text, question, answer, prompt, credentials, rate JSON,
other organizations' balances or global budget totals. Missing/foreign IDs and
inactive operators yield the same not-found outcome after the privilege gate.

The operator and original requester are separate identities. An inactive original
requester must not make eventual recovery impossible, but the recovery operator
must be active and explicitly authorized within that organization. This does not
grant all organization admins recovery power or define the eventual RBAC mapping.
Phase 6 must supply authenticated permissions; synthetic role substitutes are
not authentication evidence.

Because the pilot allowance is shared, an organization's operator must never
clear the platform-wide freeze or reset spending. A future platform reconciliation
workflow needs a separate authority and independently verified account-wide
billing, including outstanding calls. Its design is still open.

## Assessment matrix

| Stored state / proof | Recommendation | Constraint |
| --- | --- | --- |
| Reserved, no proof supplied | CancelReserved | Only a recommendation; recheck under lock against dispatch |
| Dispatched or Uncertain, no verified proof | KeepHold | Time elapsed, missing local receipt and silence are not zero-cost evidence |
| Dispatched or Uncertain, verified final USD cost | RecordVerifiedCost | Preserve exact amount, even above the quote; flag overrun |
| Dispatched or Uncertain, verified final zero/no-charge proof | RecordVerifiedCost at zero | Terminal settlement, never reopen dispatch |
| Settled or Cancelled, no proof supplied | NoChange | No balance correction or operation revival |
| Proof supplied for Reserved or a terminal state | Reject | Do not silently reinterpret inconsistent evidence |

The overrun flag describes the proposed verified amount only. The assessment
does not read or resolve an existing shared freeze. Execution must always
preserve an existing freeze, including when a later assessed amount is zero.

## Evidence trust boundary

The input accepts only a reservation ID and an optional bounded opaque evidence
reference (1–100 ASCII letters/digits, colon, underscore or hyphen). It cannot
accept an amount, verification flag, arbitrary URL or raw provider response.
References are looked up by a trusted verifier, not fetched from user URLs.

An attestation must bind evidence identity/reference, organization, reservation,
operation, request hash, provider/model/version, final status, USD cost and
verification/expiry times. The verifier must actually validate an independent,
complete usage source; constructing the attestation record is not verification.
The policy rejects substitutions, partial results, future/pre-reservation proof,
expired attestations, non-USD amounts and amounts outside the ledger's exact
six-decimal precision/range. Confirmed-no-charge proof must have amount zero.

For live integration, the verifier must additionally establish canonical provider
account/project and request identity, complete billable usage, corrections/finality
and evidence provenance. The current ledger does not yet persist all these live
identifiers. An HTTP success, invented test receipt, operator-entered amount,
absence from local logs or failed request alone cannot satisfy this requirement.
No real provider evidence source is implemented or contacted.

Assessment has a ten-second deadline and propagates caller cancellation.
Dependency errors, including downstream workflow exceptions, become fixed safe
errors without original exception content. Awaiting is bounded even if a
dependency ignores cancellation; that does not forcibly stop the dependency.
Future adapters must cooperate with cancellation and remain read-only.

## Implemented execution and remaining production requirements

`ICitedRecoveryExecutor` accepts a stable request ID, reservation ID, expected
state/event sequence, action and optional opaque evidence reference. Organization
and operator come from execution context. It accepts no amount, assessment token,
role flag, invoice content or provider request body.

It checks permission before storage, acquires the singleton budget lock, locks
the active operator row `FOR SHARE` through commit, and rechecks execution
permission. The original requester may be inactive. For a new request it reads
the scoped reservation and latest event sequence, resolves proof under the lock,
rechecks permission again and validates proof using fresh database time. The
verifier must be bounded and read-only; a future adapter should read trusted
local attestations, not perform paid or arbitrary remote calls while holding
this shared lock. The operation has a ten-second deadline.

The reservation, balance/freeze, attributed spending event and recovery audit
commit together. Cancellation changes only Reserved to Cancelled. Verified cost
changes only Dispatched/Uncertain to Settled; zero is terminal and an overrun is
recorded without clamping. No action decreases settled spending or clears a
freeze. Unknown outcome, bad proof and stale expectations leave the hold intact.

Request IDs are unique per organization, not per operator. An exact replay by
another currently authorized active operator returns the original outcome and
original recovery-operator attribution. Changed command fingerprints conflict.
Replay does not re-use proof or re-check its present expiry: it returns historical
accounting, not permission to mutate again. A later corrected proof needs a
separate future correction workflow; this executor never rewrites settled cost.

Evidence IDs and canonical opaque references are each unique across recovery
audits in the shared ledger. The trusted evidence registry must issue globally
canonical references; reference aliases/account provenance remain live-verifier
responsibilities. A reused ID or reference is rejected without disclosing the
other organization or reservation. A digest binds the accepted attestation; raw
provider bodies are not stored in the audit.

Migration `20260909002217_AddCitedRecoveryAudit` adds `cited_recovery_audits`,
scoped foreign keys to the reservation, original/operator actors and spending
event, and command/evidence/transition/sequence constraints. The original
reservation actor is never changed. EF rejects persisted audit modification;
a PostgreSQL trigger rejects row UPDATE/DELETE. This does not prevent a database
owner disabling triggers, truncating tables, restoring old backups or forging
new rows. Production database privileges and independent audit retention remain
deployment gates.

The implementation follows these invariants for synthetic execution. Real
authenticated privileges, real evidence provenance, global freeze resolution and
host/database/backup recovery remain unimplemented:

- Separate assess and execute privileges; authenticate and revalidate an active
  organization-scoped operator. Do not impersonate/reactivate the original actor.
- Use a server-scoped recovery request ID with an immutable command fingerprint.
  Replaying the same mutation returns the original outcome; changed payloads or
  evidence conflict. Evidence must not be consumed for multiple reservations.
- Use the same singleton budget lock as reserve/dispatch/settle. Re-read state,
  latest event sequence, authority and current evidence validity. Compare the
  caller's expected state/sequence; never trust an old assessment's recommendation.
- Commit reservation transition, budget change, existing spend event and a new
  recovery audit record atomically. Keep original-requester attribution intact;
  record operator, organization, recovery request, evidence identity/reference,
  reason code, before/after state, amount and time. No raw invoice/provider bodies.
- Cancellation may win only while Reserved. Verified cost may settle only
  Dispatched/Uncertain, including zero. It must not revive an operation, lower
  historical settled spending, silently clamp overruns, or clear any freeze.
- Test duplicate operators, stale previews, cancellation-versus-dispatch races,
  evidence reuse/expiry/revocation, inactive requesters, audit rollback, and process
  termination before/after the recovery commit. A lost acknowledgement must not
  cause double settlement or a fresh provider call.
- A restored/cloned ledger is not a fresh allowance. Host/database/backup recovery
  needs independent billing reconciliation and a canonical-writer policy before
  dispatch resumes. Those controls are outside application-process crash tests.

No HTTP/UI access should be added until authentication, exact privilege mapping
and evidence-source requirements are satisfied. The next provider-free feature
is durable readable generation/preparation metadata and unpublished result replay,
so stored results can be retrieved without redispatch. Exact provider counting,
authoritative usage and embedding accounting still precede any live pilot.

See the [execution verification](../evidence/phase-5/P5-T08-recovery-execution-verification.md),
[assessment review](../evidence/phase-5/P5-T08-recovery-assessment-verification.md)
and the still-open [roadmap gates](../roadmap.md).
