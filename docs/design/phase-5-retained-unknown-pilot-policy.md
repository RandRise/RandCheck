# Small unknown embedding holds in the development pilot

**Approved change:** 2026-09-10. The user declined further usage investigation
after the assistant proposed retaining the uncertain amount against the existing
allowance while permitting new work. This policy supersedes the blanket freeze
requirement for this explicitly accepted, small development-pilot risk. It is not
verified billing recovery or a claim that the provider charged zero.

## State and admission

`ICitedPilotUnknownHold.RetainAsync` is an internal operator action with an
explicit enabled argument and no HTTP/UI route. It checks the current active
original actor and organization, and locks the same singleton budget as all
reserve/dispatch/settle operations. Only an `Uncertain` OpenAI
`text-embedding-3-small` embedding reservation with its preparation and unknown
transport outcome is eligible. Each retained amount is at most USD 0.000160;
all retained amounts together are capped at USD 0.001.

The operation rejects other unresolved dispatches, known overruns and flagged
settled responses. Acceptance atomically changes the state to `HeldUnknown`,
keeps `actual_usd` null and `reserved_usd` unchanged, records an attributed
`pilot-unknown-hold-v1:user-accepted` spending event and removes this pilot freeze.
The event's reference records the acceptance policy, not a billing receipt.
`completed_at_utc` marks the end of active dispatch handling; it does not imply
the provider bill was reconciled. Replays preserve the original event and never
clear a subsequent freeze.

Admission v2 subtracts **settled + retained unknown + active reservations** from
the unchanged USD 8 allowance. Retained unknowns do not occupy the one active
dispatch slot. The original operation cannot be dispatched, cancelled or settled
by the existing interfaces. A PostgreSQL trigger prevents later edits/deletion
of retained rows. Downgrade refuses to discard retained holds. Verified billing
corrections would require a separate future audited adjustment workflow.

This accepts a small residual risk: the reserved amount is a local conservative
estimate, not independently confirmed provider cost. It does not promise an
absolute provider-bill ceiling in the absence of usage evidence. The historical
unknown amount remains visible and is never counted as known settled spending.

## Frozen evaluation continuation

Original operation `ccaecf4e-d611-5042-aa64-84d054d42346` retains USD 0.000001.
The two missing chunks are submitted in a separate explicitly planned operation,
`5ab2113f-a773-52a5-8509-e5447e140ecd`; possible cost from both attempts remains
accounted for. This continuation also includes the original 30 not-yet-dispatched
query operations. The 15 successful batches and original artifacts are reused.

`samples/phase-5/retrieval-evaluation-v0.1.0/continuation.json` has SHA-256
`b6d217cf1b595764bb63415d0b29279b0a08ecb805841cded82df6d607a21184` and binds the
unchanged parent plan. The runner has separate retain, verify, continue and
offline replay commands. The launcher writes a new exclusive evidence file;
it never overwrites the failed attempt and still performs no automatic HTTP retry.
The fixed question set, lexical baseline and vector/hybrid scoring policies stay
unchanged. No public generation or production recovery capability is enabled.
