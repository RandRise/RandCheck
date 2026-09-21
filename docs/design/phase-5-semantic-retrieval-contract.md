# Phase 5 persisted vector and hybrid retrieval

**Scope:** private retrieval from a settled query embedding. This does not expose
paid embedding creation or answer generation through a public endpoint.

## Inputs and provenance

`ICitedSemanticRetrievalStore.RetrieveAsync` takes a saved query-operation ID,
request key, mode (`vector` or `hybrid`), top-K (1–20) and, for hybrid only, a
Python lexical plan with a 20-candidate limit. It accepts no arbitrary vector,
question, organization, actor or corpus override. Those values come from the
independently verified, settled query outcome and current execution identity.

The complete query's preparation/outcome hashes, raw usage, model, dimensions,
cost settlement and actor/source eligibility use the same checks as canonical
embedding storage. A hybrid plan must match that query's hash, lexical policy,
English configuration and candidate limit. It is an internal plan from the
existing Python planner, not an externally exposed user-supplied search program.

Saved policy JSON includes mode/version, top-K, query operation, reservation,
outcome artifact and raw-response hash, question hash, embedding model/version,
dimension/distance, candidate limit, fusion constant and full lexical plan.
`RetrievalSource` freezes the selected index revisions. Existing retrieval/hit
tables already support all three scores, so no new migration is required.

## Ranking policies

- `pgvector-exact-cosine-v1`: exact cosine ranking over every eligible stored
  chunk in the selected corpus, descending similarity then canonical UUID.
- `pgvector-english-rrf60-c20-v1`: retrieve up to 20 lexical candidates and 20
  exact-vector candidates from the same scope. Fuse their union with equal-weight
  reciprocal rank fusion: `1/(60+lexicalRank) + 1/(60+vectorRank)`. A missing leg
  contributes zero. Sort by fused score descending, canonical UUID ascending,
  then take top-K. No minimum-similarity threshold or answerability decision is
  introduced by this development policy.

The lexical leg uses the existing Python `lexical-or-english-v1` plan and
parameterized PostgreSQL full-text search. Candidate scopes apply before limits.
Exact vector search requires complete vector coverage of every selected chunk;
missing vectors reject the run rather than silently reducing the corpus.
Scores are ranking signals, not interchangeable scales or confidence estimates.

The C# fusion follows the earlier Python RRF primitive with explicit frozen
production-side bounds. Unit tests cover agreement, missing legs, tied scores,
canonical ordering, invalid values and limits. PostgreSQL tests verify persisted
component scores and generic retrieval history's selected score: fused, otherwise
vector, otherwise lexical. Existing lexical results keep the same score behavior.

## Atomicity and replay

The source eligibility locks from saved-outcome verification remain held through
candidate selection, evidence reconstruction and commit. Same-key semantic calls
also acquire an organization/actor/key advisory transaction lock. Hash collisions
only serialize requests; full identity and policy comparisons decide replay.
Existing request-key uniqueness remains the database backstop.

A semantic run, sources, hits and the Retrieved state commit together. A failure
rolls back the new graph; no new Running semantic record is stranded. The legacy
lexical handler keeps its existing Running/failure-cleanup behavior. No network
call or spending mutation occurs inside the semantic transaction. The existing
30-second operation, 15-second statement and five-second lock limits apply.

Identical keys replay the original run/hit IDs, scores and duration, after current
actor/source and saved-outcome verification. Different mode, query provenance,
plan, top-K or corpus rejects with `request_key_reused`. Policy comparison is
structural because PostgreSQL jsonb normalizes key order and whitespace.
Retirement/actor deactivation denies replay without erasing the historical run.

## Pilot and limits

`EmbeddingPilot --retrieve-saved-plan` uses the frozen synthetic query and saved
embedding from the earlier paid run. Its lexical plan is produced locally by the
existing Python planner and pinned by SHA-256. It persists one lexical, one vector
and one hybrid run; repeating the command replays all three. The runner injects
offline embedding and frozen lexical clients, so it needs no worker or API key.

This establishes private retrieval integration only. The remaining work is a
separately frozen corpus/query population plan, lexical/vector/hybrid comparison
on the fixed question set, and subsequent public question/generation integration
subject to the quality/security gates. The failed 4/10 answer baseline stays intact.
