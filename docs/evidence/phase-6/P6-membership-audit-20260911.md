# Phase 6 membership and audit verification

September 11, 2026. This report covers the approved backend membership/audit increment,
following the [identity foundation](P6-identity-foundation-20260911.md). The current
[identity contract](../../design/phase-6-identity-contract.md) and
[membership/audit contract](../../design/phase-6-membership-audit-contract.md) supersede
the foundation's temporary token-role and actor-subject-only authorization design.
Phase 6 as a whole remains in progress.

## Delivered behavior

- Immutable Entra tenant/object identity, organization membership and existing actor
  relationship; one active organization per identity, no retained actor reassignment.
- Database Viewer/Reviewer/Operator roles with a separate membership/audit administrator
  grant, no authority from token role claims, and next-request revocation.
- Private organization/first-admin bootstrap CLI; organization-scoped grant/change/revoke
  API with version checks and serialized protection of the last active administrator.
- Safe security events in business transactions; independent denial/failure events;
  append-only PostgreSQL trigger, downgrade preservation and deployment runtime grants.
- 31 classified business routes. Development identity cannot use enterprise administration.

New migration `20260911162600_AddEnterpriseMembershipAndAudit` was exercised only
against `document_intelligence_test`. The original Phase 5 database remains at its
15 migrations. No live tenant, browser sign-in, retained migration or deployment
was performed. Existing Phase 5 previews continue using their prior builds.

## Verification

Evidence is in `outputs/phase-6/membership-audit-20260911/`.

| Check | Latest result |
| --- | --- |
| Domain / Application / Infrastructure / Architecture regression | 111 / 335 / 74 / 4 passed |
| API regression | 139 passed, including 52 security cases and 87 preserved API checks |
| Focused PostgreSQL security scenarios | All 9 pass; final rerun of the 2 strengthened access scenarios also passes |
| Full PostgreSQL regression | 258 passed, zero failures/skips; 32 minutes 51 seconds |
| CLI compilation | Passed, zero warnings/errors |
| Retained database preservation | All 34 original table fingerprints match before/after |
| Documentation build | Passed |

**921 unique backend checks have passing latest results**, zero failures/skips:
663 non-PostgreSQL and 258 PostgreSQL. Reruns are not counted as additional tests.
`verification.json` records the original TRX files and the latest outcomes. The
test database and temporary role are removed after the final run; `cleanup.json`
records the check. The migration matches the current EF model with no pending changes.
`audit-example.json` is a real organization-scoped response from the fictional
fixture, with successful membership changes, denied access and failed operations.

The PostgreSQL scenarios use signed tokens, actual membership lookup, actual repositories,
and two fictional organizations. They exercise scoped membership/audit access; role
separation and escalation denial; revocation with the same JWT; version conflicts;
organization transfer with historical actor preservation; concurrent administrator
removal; forced audit-write failure rolling back identity/actor/membership; and upload
rollback with file compensation. They also cover SQL/EF audit immutability, guarded
downgrade, the executable bootstrap and its retained-database refusal, and repeatable
runtime grants with forbidden DML checks. Upload, review corrections/completion,
indexing, saved questions and reconciliation produce safe actor-attributed events.

Cross-organization HTTP checks cover documents, review/history, processing status,
metrics, reconciliation detail, sources, saved retrieval/question history and citation
evidence. A second actor in the same organization can read the shared document but
cannot read another actor's saved retrieval/question or open its citation evidence.
Source lists positively include the owner's completed review and exclude it from
the foreign organization. These extend the existing Phase 5 repository tests without changing their
behavior or expected results. Test providers are local; no external paid request occurs.

Retained row counts include 464 spend events, 153 reservations, 306 generation artifacts,
69 stored chunk embeddings and the single original System actor. Whole-row fingerprints
match, not just counts. All unknown holds, question/source history and Phase 5 evaluation
distinctions remain retained. The grants test creates one unique NOLOGIN role with grants
only in the disposable database and removes it in cleanup.

Initial failed attempts remain alongside final evidence: unavailable NuGet vulnerability
feed during migration generation; a missing private Python fixture credential; incorrect
fixture response field names and missing span/value-type inputs; and a compile attempt
using an internal data-protection repository type. Fixtures were corrected to the existing
contracts. Production validation was not relaxed. Offline cached restore used
`NuGetAudit=false`; this build is not a completed vulnerability audit. The docs build's
sandbox Node path error is recorded separately from its approved build retry.

## Reproduction

From the repository root, restore cached/approved dependencies, then:

```powershell
dotnet test apps/backend/UaeDocumentIntelligence.sln --no-restore -p:UseAppHost=false --filter 'FullyQualifiedName!~UaeDocumentIntelligence.Persistence.Tests' -o tmp/phase6-reproduction --logger 'trx;LogFilePrefix=nonpg' --results-directory outputs/phase-6/reproduction
```

Set `TEST_POSTGRES_CONNECTION` privately to the local server's admin database and
`PHASE5_PYTHON` to the repository `.venv/Scripts/python.exe`. The fixture always selects
the exact disposable `document_intelligence_test` database. Run one PostgreSQL runner:

```powershell
dotnet test apps/backend/tests/UaeDocumentIntelligence.Persistence.Tests --no-restore -p:UseAppHost=false -o tmp/phase6-pg-reproduction --logger 'trx;LogFileName=pg.trx' --results-directory outputs/phase-6/reproduction
```

Its project builds the referenced private CLI. The CLI process test needs that executable
alongside the direct-run assembly, or in its normal build location. A focused run adds
`--filter 'FullyQualifiedName~Phase6_'`. Read-only preservation uses
`tools/phase6_retained_fingerprints.sql`; do not run test resets against retained data.

## Remaining scope

Browser login/logout and administration, live Entra registration verification, worker
identity, the remaining mutation/semantic isolation matrix, protected file access,
malware quarantine, retention/restore and rate limits remain Phase 6 work. Runtime
and dependency hardening are still required; the local .NET runtime is 8.0.10.
Audit is limited to documented sensitive state changes and denials/failed admitted
mutations; it is not complete access logging or owner-proof tamper evidence. Production
runtime grants, transport controls and operational policies are not deployed here.
