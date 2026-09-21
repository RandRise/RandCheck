# P5-T08 — Read-only spending recovery assessment

**Date:** 2026-09-09  
**Scope:** Default-denied recovery assessment, organization-filtered spending
snapshot reader and trusted-evidence policy. No recovery execution, public route,
paid call, provider SDK, new dependency or schema migration.

## Outcome

Recovery now has a separate read-only application boundary. A caller must pass
an explicit authority port before any spending metadata or evidence lookup.
The production registration denies every caller; the evidence verifier is also
unavailable. Test substitutes exercise authorized-path behavior without pretending that
Phase 6 authentication or a provider billing verifier exists.

The assessment keeps the active operator separate from the original requester,
who may be inactive. It recommends keeping an unknown hold, cancelling only an
undispatched reservation, or recording independently verified final cost. Zero
cost is a terminal settlement recommendation, never permission to retry. An
overrun is retained exactly and flagged; no assessment can clear the shared
freeze, reset allowance, dispatch, publish an answer or mutate the ledger.

The observed state/event sequence accompanies every recommendation. The existing
ledger rejects cancellation if dispatch wins after a Reserved assessment. This
checks the current ledger transition guard, not a newly implemented atomic
recovery executor. See the [contract](../../design/phase-5-recovery-contract.md)
for the concrete execution and authority requirements that remain.

## Verification

- Initial focused unit run: 39 passed. Two additional cancellation/deadline tests
  were then added. All **403 non-persistence .NET tests passed**: 111 Domain,
  143 Application (41 new recovery cases), 67 Infrastructure, 4 Architecture,
  78 API. Zero failures/skips.
- All **five new PostgreSQL cases passed**: deny-default registration; separate
  active operator/inactive requester and foreign filtering; no ledger or freeze
  changes from a verified-cost preview; unknown outcome/default-verifier denial;
  and stale Reserved preview after dispatch. The cost-preview case also checks
  the exact existing application schema, constraints and indexes.
- Combined PostgreSQL regression: **52 passed**, zero failures/skips: five
  recovery, 12 ledger, 15 answer-coordinator, 13 prompt-preflight and seven
  process-crash cases. The private .NET/Python fake-answer round trip is included.
  With the non-persistence suite, **455 unique .NET cases passed**.
- Final Debug solution build passed with zero warnings/errors. EF reports no
  pending model changes. Documentation build and `git diff --check` passed;
  existing bundle-size and line-ending normalization notices are non-blocking.

Unit tests cover every stored state, final and zero cost, overrun preservation,
19 invalid-evidence variants, malformed snapshots, missing proof, opaque reference
validation, changed organization/reference, privilege revocation, cancellation
and an uncooperative-verifier deadline. Downstream exceptions—including ones
already typed as workflow exceptions—cannot escape with private details.

Reports: `tmp/phase5-test-results/phase5-recovery-assessment-*.trx`. Focused reruns
do not add unique cases. Python code, public API routes and frontend are unchanged;
no fresh Python-unit/full-OCR/browser or full PostgreSQL acceptance is claimed.

## What this does not prove

- Recovery is not operational yet. There is no executor, recovery audit table,
  privileged HTTP/UI surface, authenticated permission adapter or real evidence
  verifier. The default-denied component does not grant real admin access.
- The evidence record is an attestation from a trusted port, not a signature or
  proof by itself. Tests construct synthetic evidence. Provider account/request
  identity, authoritative billing, evidence storage/reuse and live finality remain
  unverified. No operator-entered amount is accepted by the application request.
- A preview can become stale. Its non-executable marker is a contract, not a
  cryptographic capability. Future mutation must use a separate permission,
  re-read under lock, and persist recovery/audit/idempotency state atomically.
- Original-requester inactivity is covered for assessment only. Attributed
  recovery execution, freeze resolution and recovery after a restored/cloned
  database still need implementation and tests. An org admin must not reset a
  platform-wide allowance.
- Prior crash/restart evidence keeps PostgreSQL alive. It is not host/power-loss,
  database/WAL, backup or real-provider lost-response proof. Live generation,
  embedding accounting, semantic support and the wider Phase 5 gates stay open.

## Reproduction

From the repository root with the existing isolated PostgreSQL on loopback 25434:

```powershell
dotnet test apps/backend/UaeDocumentIntelligence.sln --no-restore --filter 'FullyQualifiedName!~UaeDocumentIntelligence.Persistence.Tests' --logger 'trx;LogFilePrefix=phase5-recovery-assessment-nonpg' --results-directory tmp/phase5-test-results
$env:TEST_POSTGRES_CONNECTION='Host=127.0.0.1;Port=25434;Database=postgres;Username=document_intelligence;Password=local_development_only'
$env:PHASE5_PYTHON=(Resolve-Path .venv/Scripts/python.exe).Path
dotnet test apps/backend/tests/UaeDocumentIntelligence.Persistence.Tests --no-restore --filter 'FullyQualifiedName~Recovery_|FullyQualifiedName~Prompt_preflight|FullyQualifiedName~Generation_|FullyQualifiedName~Ledger_' --logger 'trx;LogFileName=phase5-recovery-assessment-db-final.trx' --results-directory tmp/phase5-test-results
```

The fixture exclusively recreates/resets/drops `document_intelligence_test`;
never run database suites concurrently or substitute an application database.
The retained `phase5_e2e_20260908` acceptance database is outside these tests.
No key, funded API account, Docker change or persistent application host is needed.

Final read-only SQL checks confirmed PostgreSQL on `127.0.0.1:25434`, the fixture's
disposable database absent, the retained acceptance database present and no other
database clients remaining. Test helpers exited. The reproducible disposable
fixture data was removed; retained synthetic invoice evidence and historical logs
were preserved. PostgreSQL remains running for continued work.

## Next

The follow-up [execution review](P5-T08-recovery-execution-verification.md)
records the internal transactional executor and operator audit/idempotency
increment. Authority/evidence adapters remain denied/unavailable. Public access
and paid dispatch stay disabled. Full live and operational
readiness remain unchecked in the [roadmap](../../roadmap.md).
