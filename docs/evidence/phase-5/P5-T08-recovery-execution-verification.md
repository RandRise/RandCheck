# P5-T08 — Atomic recovery execution and audit

**Date:** 2026-09-09  
**Scope:** Internal recovery executor, one new audit migration and synthetic
transaction/concurrency/process-failure tests. No public route, paid call,
authentication grant, provider SDK or global budget reset.

## Outcome

The recovery boundary can now execute a permitted synthetic cancellation or
verified-cost settlement. The production authorization adapter still denies
everyone, and the evidence verifier is unavailable. The executor also rejects
non-test providers. Successful mutations in this review use explicit test-only
authority and evidence substitutes, not live operator credentials or billing.

An action, its spending event and its recovery audit commit together under the
same singleton budget lock as generation. The active operator is locked through
commit and recorded separately from the original requester, who may be inactive.
Settled spending is never reduced and an existing freeze is never cleared.

Stable organization-scoped recovery request IDs provide replay without another
mutation or charge. Replays preserve the original recovery operator; altered
commands conflict. Stale previews, unavailable/invalid proof and reused evidence
cannot release a hold. Verified zero cost is a terminal settlement, never a
fresh dispatch opportunity. The [contract](../../design/phase-5-recovery-contract.md)
documents the boundary and remaining live requirements.

## Verification

- Initial focused execution run: **18 PostgreSQL cases passed**, zero failures/skips.
  Cases cover default denial, separate operator/inactive requester, stable replay,
  exact zero/normal/overrun costs, freeze preservation, duplicate operators,
  cancellation-versus-dispatch, bad proof, revoked/foreign/inactive operators,
  evidence reuse, audit-failure rollback, immutability, migration reapply, malformed
  commands, cancellation and rejection of non-test providers.
- Four separate-process recovery crash cases passed: cancellation and
  settlement, each before and after the commit. The child pauses at a verified
  checkpoint; its parent terminates that exact process and starts a fresh one
  with the same request ID. Assertions cover state, balance, spend-event and audit
  atomicity, repeated replay, schema integrity and no revived dispatch.
- Full .NET regression: **529 tests passed**, zero failures/skips — 111 Domain,
  143 Application, 67 Infrastructure, 4 Architecture, 78 API and **126 PostgreSQL**.
  The 18 focused cases are included in this total, not counted twice.
- Final Debug solution build passed with zero warnings/errors. EF reported no
  pending model changes. Documentation build and diff checks passed with the
  existing bundle-size and line-ending notices.

Reports use `tmp/phase5-test-results/phase5-recovery-execution-*.trx`. Python
source and public/frontend routes are unchanged. Existing private Python bridge
checks run as part of database tests; no new standalone Python-unit/full-OCR or
browser acceptance is claimed.

## Migration and audit

`20260909002217_AddCitedRecoveryAudit` is migration 11. It adds the recovery audit
table and a scoped unique key on spending events for a composite foreign key.
Each audit stores request/command identity, original and operator actors, linked
spending event, action/reason, before/after state and sequence, verified amount,
evidence ID/reference/digest, policy version and database time. It stores no raw
invoice, question, answer, prompt, credential or provider response.

Unique request/reservation/evidence constraints protect duplicate actions. EF
rejects persisted audit changes; a PostgreSQL trigger rejects row UPDATE/DELETE.
Tests deliberately fail an audit INSERT and verify that the reservation, spending
event and balance all roll back; retrying the unchanged command then succeeds.
Reapplying the migration does not reset an existing audit or invalidate replay.
The full migration round-trip fixture passed as part of the full suite.

The fixture alone recreates/resets/drops `document_intelligence_test`. The
retained synthetic `phase5_e2e_20260908` database is not migrated/reset here and
still needs the new migration before using this new executor in that database.
Final read-only checks confirmed loopback port 25434, the disposable database
removed by its fixture, the retained database present and zero other database
client connections. The isolated PostgreSQL cluster remains running for continued
work; no persistent public application servers were started.
Do not downgrade a populated operational audit ledger to obtain a fresh allowance.

## What this does not prove

- This is not an enabled administration feature. Real assess/execute permissions,
  authentication and evidence-registry/provider verification remain unavailable.
  Fixed development identities and test grants are not Phase 6 RBAC evidence.
- Permission is checked before storage, under lock and before new writes. The
  active operator row is locked through commit, but a future external permission
  service must define its own revocation consistency. No distributed atomic
  revocation claim is made by these synthetic ports.
- The proof verifier must establish canonical provider account/project/request,
  complete billable usage, provenance and finality. The existing ledger does not
  yet persist all those live identifiers. Constructing an attestation is not
  proof, and a digest does not make its source authoritative.
- Exact replay returns historical accounting even if proof later expires or
  changes. Cost corrections, global freeze resolution and clearing restored/
  cloned database ambiguity need separately authorized workflows; none is enabled.
- These process failures leave PostgreSQL alive. They do not inject OS/power loss,
  PostgreSQL/WAL/disk failure, backup rollback, wire-level commit-acknowledgement
  loss or real-provider HTTP lost responses. Unknown commit errors must retain
  the original request ID; the executor never automatically retries.
- Audit row guards are not protection against a privileged database owner,
  trigger disabling, TRUNCATE, forged INSERTs or old backup restoration. Database
  privileges, protected external audit retention and canonical-writer recovery
  remain operational/privacy gates.
- Live counting/usage/cost enforcement, embeddings, saved generated-result replay,
  semantic support, model-injection evaluation, real invoice accuracy and wider
  acceptance remain open. Passing recovery tests does not complete Phase 5.

## Reproduction

From the repository root, using the existing isolated PostgreSQL on loopback 25434:

```powershell
$env:TEST_POSTGRES_CONNECTION='Host=127.0.0.1;Port=25434;Database=postgres;Username=document_intelligence;Password=local_development_only'
$env:PHASE5_PYTHON=(Resolve-Path .venv/Scripts/python.exe).Path
dotnet test apps/backend/UaeDocumentIntelligence.sln --no-restore --logger 'trx;LogFilePrefix=phase5-recovery-execution-full' --results-directory tmp/phase5-test-results
```

Database suites must not run concurrently; they share one disposable fixture.
No API key, funded account, Docker change or persistent public app is needed.

## Next

Add durable readable generation/preparation metadata and unpublished result
replay without redispatch. This is another provider-free prerequisite for public
generated answers, not permission to publish or enable paid calls. Real recovery
authority/proof, provider counting/accounting and operational recovery remain in
the [roadmap](../../roadmap.md).
