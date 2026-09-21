# Phase 5 embedding incident triage

**Date:** 2026-09-10. **Status:** read-only local packet complete; provider access
and live recovery remain blocked. No settlement, release, reset or paid retry.

## Incident packet

`tools/phase5_embedding_recovery_packet.py` reads only the frozen evaluation's
operations from the retained canonical PostgreSQL database. It verifies the plan
hash, uses one repeatable-read read-only transaction, projects spending metadata
and rolls back. It does not read `.env`, call a provider or invoke the recovery
executor. A changed incident state requires reassessment; existing output is
never overwritten. This is a local operator diagnostic, not a public API or
production authorization mechanism.

Saved packet:
`outputs/phase-5/retrieval-evaluation-20260910/recovery-incident.json`.
It contains UTC event timestamps, local operation/reservation IDs, known provider
request IDs, artifact hashes, accounting states and explicit missing identities.
Raw document text, question text, vectors, request/response bodies and credentials
are excluded. It has `billingVerified=false`, `executable=false`,
`mayRedispatch=false` and `mayClearSharedFreeze=false`.

- UTC interval: **2026-09-10 13:42:00 inclusive to 13:44:00 exclusive**.
- Dubai interval: **2026-09-10 17:42:00 to 17:44:00**.
- Known local successful requests: **15**, with **1,684 confirmed input tokens**.
- Unknown operation: `ccaecf4e-d611-5042-aa64-84d054d42346`.
- Its preparation measured **16 tokens**; final provider usage is unknown.
- No historical client request ID was sent for the unknown call, and no provider
  request ID was received. Provider organization/project/API-key IDs were not
  persisted. The local organization UUID must not be mistaken for a provider ID.

Five tests passed, covering unexpected raw metadata, invalid provider IDs, stale
incident states, duplicate rows, unknown usage, timezone conversion and changed
plans. Every database table fingerprint matches the prior checkpoint after this
inspection. Current totals remain 67 vectors, USD 0.002001 settled and one
USD 0.000001 hold with the shared budget frozen.

## Provider access and evidence limit

The in-app browser reached OpenAI's sign-in page. The user reported that they do
not have the account password. No account session, usage export or billing proof
was obtained; no credential was requested or inspected. The sign-in tab remains
available for the account owner or an existing supported sign-in method.

An authorized account member can select the correct project, date and embedding
model in Usage, then inspect/export the interval above. Determine which API key
the project used and whether other traffic occurred during the window. All local
counts in this packet describe this evaluation only.

The official [embeddings Usage API](https://developers.openai.com/api/reference/python/resources/admin/subresources/organization/subresources/usage/methods/embeddings)
returns bucketed token/request counts with project/key/model dimensions and
pagination; it uses an admin credential. Its documented response does not expose
individual request IDs or billing finality. Therefore matching aggregate counts
can assist diagnosis but cannot alone satisfy the application's request-bound
verified-billing recovery contract. A dashboard-rounded zero is not proof of no
charge.

For future dispatch, OpenAI documents
[X-Client-Request-Id for timeout investigation](https://developers.openai.com/api/reference/overview#debugging-requests).
A versioned persisted transport identity should record that correlation ID before
dispatch together with the configured provider account/project. This has not been
added retroactively to the historical operation or enabled for new paid calls.

The actual next dependency is provider access and independent request/billing
evidence. A live verifier and separately authorized global reconciliation remain
unimplemented. The [recovery contract](../../design/phase-5-recovery-contract.md)
continues to require retaining the unresolved hold. The earlier
[evaluation checkpoint](P5-retrieval-evaluation-checkpoint.md) remains unchanged.
