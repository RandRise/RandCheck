# Phase 6 identity foundation verification

September 11, 2026. Phase 6 has begun; this report covers the API identity foundation,
not Phase 6 completion or production deployment. See the [identity contract](../../design/phase-6-identity-contract.md)
and [remaining task ledger](../../tasks/phase-6.md).

## Delivered increment

- Locked access by default outside the explicit local development profile.
- Entra v2 bearer validation with issuer, audience, lifetime, RSA signature,
  tenant, delegated scope and authorized-client checks.
- Active Human actor resolution using immutable Entra tenant/object identity;
  no implicit enrollment or caller-selected organization. Ambiguity denies access.
- Viewer/Reviewer/Operator permissions, an inventory of all 27 business routes,
  fallback/default authorization and scoped execution context without a fallback.
- API no-store/nosniff responses, public minimal health, and preserved Phase 5
  behavior in the explicitly selected local test profile.

No database migration, retained-data change, provider call, paid operation or
general-model enablement is part of this increment. Existing Phase 5 preview
processes were left running with their prior builds and configurations.

## Verification

Machine-readable results and original logs are in
`outputs/phase-6/identity-20260911/verification.json` and its adjacent files.
The first non-PostgreSQL regression passes 659 checks: Domain 111, Application 335,
Infrastructure 74, Architecture 4 and API 135. The API total includes 48 new
security cases and all 87 existing contract/logging checks. Final API package and
PostgreSQL results are recorded in the machine-readable report. Final acceptance
for this increment is **908 backend checks passed**, zero failures/skips: 659
non-PostgreSQL and all 249 PostgreSQL checks. The final API run passes all 135
checks with JWT bearer 8.0.31. The PostgreSQL run took 29 minutes 8 seconds and
removed its disposable fixture database on completion. All 34 retained-table
fingerprints match both before/after and the Phase 5 closure, including every
spending row and unknown hold. No new provider calls or cost.

A fresh real Kestrel process in Production, with no Security override and an
uninitialized in-memory database, returns health 200 and documents 401 with
no-store/nosniff. Workers and seeding were disabled. The process was stopped in
the harness's cleanup; `locked-host.json` records its HTTP results. This verifies
the production default outside the in-process test server without accessing
retained data or invoking a provider. Documentation build passes.

The security suite uses offline OIDC metadata with real RSA-signed tokens and the
production JWT validator. It covers unsigned/wrong-signature, issuer/audience,
expired/future tokens; tenant/subject/version/client/scope/role checks; anonymous
access across all 27 routes; forged headers/query/token context; active membership
rechecks; role matrix and mutation denial before body binding; unclassified-route
denial; local-only profile and invalid configuration startup refusal. Five
relational SQLite actor-store cases cover active, inactive, System, missing and
ambiguous membership. The bearer HTTP tests substitute the downstream document
repository: they prove identity forwarding/admission, not a complete enterprise
PostgreSQL tenant-isolation matrix. That remains an explicit next acceptance layer.

Initial attempts are retained: configuration callbacks applied after the early
startup binding, a temporary diagnostic event used an uninitialized Events object,
and the minimal SQLite actor fixture omitted its schema's `is_active` default.
Tests now use early host settings and the matching relational default. Production
authorization checks were not relaxed to make tests pass. The initial newer-package
restore failed in restricted networking; the approved network-enabled restore
succeeded. Final code pins JWT bearer 8.0.31. The first non-PG run used cached
8.0.10 for the new package; its API checks are superseded by the final 8.0.31 run.

## Reproduction

From the repository root, restore dependencies and run:

```powershell
dotnet test apps/backend/UaeDocumentIntelligence.sln --no-restore -p:UseAppHost=false --filter 'FullyQualifiedName!~UaeDocumentIntelligence.Persistence.Tests' --logger 'trx;LogFilePrefix=nonpg' --results-directory outputs/phase-6/reproduction -o tmp/phase6-reproduction
```

For PostgreSQL, use only the existing disposable `document_intelligence_test`
fixture, one runner at a time. Set `TEST_POSTGRES_CONNECTION` to the development
server's admin database and `PHASE5_PYTHON` to the application `.venv` Python,
then run the Persistence.Tests project separately. Do not point any test reset
at a retained database. Read-only all-table preservation checks use
`tools/phase6_retained_fingerprints.sql`; compare with the closure's 34 table rows.

## Limits and next work

Browser sign-in/logout and real Entra verification are pending. Audited actor/role
administration, complete enterprise relational/service/storage isolation, append-only
security audit, malware quarantine, protected downloads, retention/deletion,
rate limits and runtime/dependency hardening remain open. Local .NET runtime
8.0.10 is preserved for development; it is not the intended deployment baseline.
Token role revocation follows token replacement/expiry; no immediate session
revocation or cancellation of already admitted work is claimed. No certification
or production-readiness claim follows from this increment.
