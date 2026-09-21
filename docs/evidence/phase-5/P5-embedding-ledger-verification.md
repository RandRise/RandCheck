# Phase 5 — input-only embedding accounting

**Date:** 2026-09-10 (UAE). Accounting increment verified; live embedding integration remains open.

Rand explicitly authorized paid embeddings. Cost approval is no longer an open
question. This increment connects embedding-shaped reservations to the existing
shared ledger; it does not yet connect the provider or index vectors.

## Implementation

- `CitedSpendWorkload` distinguishes Generation and Embedding. Generation remains
  the default for old rows and callers. Its original serialized command bytes and
  fingerprint are preserved by omitting the default workload property. Embedding
  commands include their workload, preventing cross-workload operation replay.
- `QuoteEmbedding` requires a positive input rate, exactly zero output rate and
  1–8,000 total input tokens. No fake output tokens, second budget, new allowance
  or automatic retry. Generation still requires positive output rate/tokens.
  Input-only quotes round up to the same microdollar accounting scale.
- Both workloads use the same singleton USD 8 ceiling, budget row lock, pending
  slot, expiry checks, actor ownership, journal, settlement and reconciliation
  freezes. Known charges settle even if vectors are unusable; unknown usage keeps
  the hold. The journal is an internal trusted accounting boundary, not a raw
  provider-usage verifier or caller-controlled API.
- Migration `20260909213640_AddEmbeddingSpendWorkload` adds the workload column,
  token-shape constraints and a database trigger preventing workload changes.
  Downgrade refuses when any embedding reservation exists; it never deletes
  accounting or invents output tokens. Test resets recreate only the guarded
  `document_intelligence_test` database. Dedicated tests still check migration
  rollback/reapply and preservation of pre-migration generation fingerprints.
- The generation coordinator rejects embedding records before attempting saved
  answer replay. The read-only ledger export now distinguishes operation IDs from
  generation retrieval IDs and includes workload. Existing baseline exports are
  not rewritten.

OpenAI's model page lists `text-embedding-3-small` at USD 0.02 per million input
tokens. This informed the input-only accounting design; it is not a configured
live admission window or proof of the account's bill. At that reference rate,
an 8,000-token ceiling reserves USD 0.000160. [Official model documentation](https://developers.openai.com/api/docs/models/text-embedding-3-small).

## Verification

- 444 non-PostgreSQL backend tests passed: 111 domain, 184 application,
  67 infrastructure, four architecture and 78 API. Eight new application tests
  cover input-only arithmetic, boundaries, disabled defaults and legacy command
  byte compatibility.
- 32 focused PostgreSQL tests passed (including seven new embedding cases):
  shared-slot concurrency, exact replay, known/unknown charges, overrun freezes,
  scope/expiry, SQL constraints, downgrade refusal and legacy migration replay.
  A further 29 PostgreSQL checks passed: generation, crash/restart, legacy payment
  policy and the eighth new embedding case (answer-workload replay rejection).
  Two cases matched both filters and passed twice. Total: **503 distinct backend
  tests passed**, comprising 444 non-PG and 59 unique PG cases (505 executions).
  This is not a full persistence/OCR/browser acceptance run.
- Separate synthetic pilot runner build passed; EF reports no pending model
  changes. No Python code changed; the previous 144 Python checks were not rerun.
- Documentation build and whitespace check passed. Existing VitePress large-chunk
  warning remains. Test fixture database was removed after the run; no private
  embedding/generation service was started for the actual saved-result replays.
- TRX evidence: `tmp/phase5-test-results/phase5-embedding-ledger_net8.0_*.trx`
  and `phase5-embedding-ledger-persistence.trx`; generation follow-up is
  `phase5-embedding-generation-regression.trx`.

## Retained data and backup

Before migration: 11 reservations, 22 immutable artifacts, USD 0.001951 settled,
zero pending holds and no freeze. Read-only row digests were recorded for a
before/after comparison; the new workload column is excluded from the reservation
comparison. These MD5 digests are accidental-change checks, not security attestations.

Migration 13 was applied to `phase5_e2e_20260908` after the focused PostgreSQL
tests passed. All 11 rows are Generation; zero Embedding rows exist. Both digests,
counts, settled total and no-hold/no-freeze state match before and after migration
and two actual saved-result replays. With private service 28157 stopped and no
provider key loaded, q009 still returns Replayed=true, publication=false; q025
still returns `answer_proposal_not_accepted`. No paid request was made.

- Reservation digest: `c3c13063b21d5085ba36d430f1cac6e1`.
- Artifact digest: `98a463cf751914f1867f6182d137b72c`.
- Fresh backup: `tmp/phase5-e2e-before-embedding-ledger-20260910.dump`, 317,893 bytes.
- SHA-256: `D149055C11C387393B124ED22F36EF13956707A05BDA8CBF8B5397B1DC7BAC85`.
- Archive listing verified; this is not a restore drill. Earlier backups retained.

## Still required before actual embedding comparison

1. Verified model-specific token counting. `tiktoken` is not installed in the
   current `.venv`; byte limits in the pure protocol are not token counts.
2. Bounded private embedding transport/coordinator, canonical source and actor
   eligibility checks, a frozen synthetic request plan and current rate/access
   admission. The current journal outcome limit is 524,288 bytes, while the pure
   embedding parser accepts up to 4 MiB: batch/envelope limits must be reconciled
   before dispatch, not after a charge occurs.
3. Provider-response verification and settlement through this ledger, immutable
   raw-outcome replay and organization/source/model/dimension-bound vector storage.
4. SQL-scoped lexical/vector/hybrid comparison against the independent 30-question
   set, then a separately versioned answer-quality follow-up.

No new paid call, key read, rate-window extension, public generation enablement
or claim of improved retrieval/answer accuracy is part of this increment. The
failed 4/10 generation baseline remains frozen. Real billing recovery, production
authorization/roles, privacy and broader Phase 5 acceptance remain separate gates.
