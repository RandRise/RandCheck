# Retained local preflight failure

The first low-reasoning attempt stopped before its counting HTTP request: the
transport rebuilt the request with its default minimal effort and rejected the
different low-effort hash. Protocol tests covered low effort, but the transport
test had not; a full transport test now covers both exact request bodies.

The failed source is preserved at
`outputs/phase-5/answer-low-20260910/transport-at-failure.py`, SHA256
`7eb49c8bfa7f3eb56476be6133b7de7ddf20fe0685dc49cf989cbd3db68d17ea`.
Operation `3ae994cd-4973-4c28-9619-5e886cb51e29` has a bound low-effort receipt,
no count response, no provider IDs or responses and an unknown accounting outcome.
The code path explains the local failure; it is not a provider billing receipt.

To continue the already authorized synthetic work proportionately, retain its
entire USD 0.002 reservation as HeldUnknown with actual USD null. Do not invent a
zero settlement or reopen the failed operation. This follows the user's direction
to proceed within the existing budget without investigating tiny uncertain costs;
it does not represent a new explicit user approval of this individual event.

The separate internal `RetainLowPreflightFailureAsync` action requires explicit
operator enablement, the active owning actor, OpenAI nano generation at the pinned
snapshot, at most USD 0.002, a bound low-effort preparation, zero reported tokens,
and absent count/provider responses and IDs. It rejects other active uncertainty,
known overruns and flagged settlements. Retained generation amounts are capped
at USD 0.002, embedding amounts remain capped at USD 0.001, combined USD 0.003.
The USD 8 admission ceiling is unchanged and includes every retained amount.
No public endpoint or general billing recovery is added.

An attributed `pilot-local-preflight-hold-v1:authorized-budget` event records this
distinct policy. The original embedding hold and acceptance event remain intact.
The replacement case needs a new frozen continuation manifest and operation ID.
