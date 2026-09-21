# Phase 6 local acceptance matrix

September 16, 2026. This is the claim-to-evidence map for the independent GitHub
portfolio scope. The [final verification](../evidence/phase-6/P6-local-acceptance-20260916.md)
records fresh results separately from earlier evidence. A local signed-token
fixture replaces Entra discovery with a locally generated signing key; it does not
prove a real Entra registration, consent, token rollover or deployed browser flow.

| Local claim | Evidence | Limit |
| --- | --- | --- |
| Anonymous business access is denied; signatures, issuer, audience, lifetime, scope and client identity are checked | API security suite; [identity evidence](../evidence/phase-6/P6-identity-foundation-20260911.md) | Explicit Development mode remains local-only; live identity/transport verification pending |
| A user has at most one active organization; tenant/object identity is immutable; data role and administrator grant come from the database | Fresh `Phase6_browser_session`, `Phase6_bootstrap_membership`, concurrent-administrator and identity/audit rollback scenarios; [membership contract](../design/phase-6-membership-audit-contract.md) | No self-service organization switching or directory synchronization; private bootstrap is trusted |
| Role changes and revocation affect the next request, including the same still-valid JWT | Fresh session/membership HTTP walkthrough, browser access tests | General in-flight revocation is not instantaneous; sensitive mutations/source access have additional checks |
| Public reads and mutations reject foreign organization IDs; saved questions/citations also require the owning actor | Fresh `Phase6_authenticated_organizations` and `Phase6_api_tenant` HTTP scenarios; [data-access inventory](phase-6-data-access-inventory.md) | Explicit application predicates, not PostgreSQL tenant RLS; compromised allowed service credentials remain trusted across organizations |
| Sensitive mutations have safe atomic business events; authorized mutation/source attempts have distinct admission events | Fresh membership/audit and API tests; [25-operation coverage](phase-6-audit-coverage.md), [semantic rollback evidence](../evidence/phase-6/P6-audit-coverage-20260915.md) | Admission is not success; ordinary reads, global events and private operator receipts have explicit boundaries; no historical backfill |
| Runtime identities cannot rewrite audit history; private exports verify scope, ordering and integrity | Fresh append-only/restricted-grant tests; [export evidence](../evidence/phase-6/P6-audit-export-20260915.md) | DBA/owner tampering remains possible; integrity receipt must be independently trusted; archives retain indefinitely |
| Uploaded and downloaded sources require integrity checks and clean scanning; short-lived grants retain current identity checks | API/infrastructure suite; [storage](../evidence/phase-6/P6-storage-safety-20260912.md), [source access](../evidence/phase-6/P6-source-access-20260912.md), [keys](../evidence/phase-6/P6-source-keys-20260915.md) evidence | Real daemon, ACLs, encryption, shared mounts and certificate delivery require deployment checks |
| Retention/holds, explicit deletion and local recovery obey the documented contract | Fresh restricted-login retention/deletion scenario; [retention](../evidence/phase-6/P6-retention-controls-20260912.md), [deletion](../evidence/phase-6/P6-document-deletion-20260913.md), [actual restore drill](../evidence/phase-6/P6-local-recovery-20260915.md) | No automatic document/backup expiry or stale historical reconciliation; current independent controls and operator authority required |
| Workers and private services have bounded authority and safe telemetry | Infrastructure/Python security suites; [worker](../evidence/phase-6/P6-worker-security-20260912.md), [private service](../evidence/phase-6/P6-private-service-access-20260911.md), [API grants](../evidence/phase-6/P6-api-database-20260914.md) evidence | Cross-organization service identity remains trusted; private TLS, secret delivery and process separation unverified in a deployment |
| Request/body/concurrency limits and durable organization admission quotas are enforced | API/infrastructure suite; [request limits](../evidence/phase-6/P6-request-limits-20260914.md), [capacity](../evidence/phase-6/P6-capacity-20260914.md) evidence | Process counters reset on restart; no distributed edge limit or physical-disk guarantee |
| Repository candidates, reachable historical text, selected logs and built client payloads receive a redacted credential review | Fresh scanner report, individually hash-bound review decisions, synthetic server-only build markers and scanner tests; final verification below | Pattern/exact-value review is bounded, not proof against every encoding or credential type; publication inventory remains Phase 8 |
| Security limitations remain explicit and deployment checks cannot falsely pass | [Threat model](phase-6-threat-model.md), [preflight evidence](../evidence/phase-6/P6-readiness-20260914.md), [runtime evidence](../evidence/phase-6/P6-runtime-20260915.md) | Preflight `DeploymentVerified=false`; September 15 package audit is dated evidence, not a promise of future vulnerability status |

## Reproduce the security walkthrough

Use the [project runtime helper](../design/phase-6-runtime-baseline.md), restored
dependencies and a local PostgreSQL test server. The configured test identity must
be allowed to recreate the exact disposable database `document_intelligence_test`.
Run one database runner at a time. Do not point fixtures at a valuable database.

From the repository root in PowerShell:

```powershell
. ./tools/use-project-runtimes.ps1
# Set TEST_POSTGRES_CONNECTION privately for the disposable local test server.
$filter = 'FullyQualifiedName~Phase6_browser_session|FullyQualifiedName~Phase6_bootstrap_membership|FullyQualifiedName~Phase6_concurrent_administrator|FullyQualifiedName~Phase6_audit_insert_failure|FullyQualifiedName~Phase6_audit_is_append|FullyQualifiedName~Phase6_authenticated_organizations|FullyQualifiedName~Phase6_review_changes|FullyQualifiedName~Phase6_upload_and_queue|FullyQualifiedName~Phase6_runtime_grants|FullyQualifiedName~Phase6_private_bootstrap|FullyQualifiedName~Phase6_api_database|FullyQualifiedName~Phase6_api_tenant|FullyQualifiedName~Database_logs_exclude|FullyQualifiedName~Database_constraint_conflict|FullyQualifiedName~Database_conversion_failure'
dotnet test apps/backend/tests/UaeDocumentIntelligence.Persistence.Tests --no-restore -p:NuGetAudit=false --filter $filter --logger trx
```

The fixture provisions two fictional organizations, grants database roles, attempts
escalation and foreign access, changes/revokes membership with the same JWT, checks
last-administrator concurrency, forces audit failures and verifies rollback. It
also exercises positive owner access under restricted database grants. The test
class is shared across partial files, so filter by method names, not filenames.
The membership scenario writes `phase6-audit-example.json` beneath its test output
directory. The final report links a curated synthetic copy from this run. No paid provider is used.

## Secrets and logging boundary

Server credentials belong in private environment/configuration delivery. Ignore
rules cover `.env` variants and private key containers while keeping example
configuration visible. Only deliberately public identity settings use `NEXT_PUBLIC_`.
MSAL manages session tokens in **sessionStorage**; there is no claim that browser
tokens are never stored. Application diagnostics suppress raw database/service
errors; the Development cleanup loop now also omits exception state.

Raw PostgreSQL logs are a separate operator-controlled surface. Existing synthetic
diagnostic logs can contain source/request markers; application log wrappers do
not sanitize them. The [server-log policy](phase-5-server-log-policy.md) remains a
deployment proposal, not evidence that rotation, ACLs or expiry run locally. No
existing raw log was erased. Do not publish raw operational logs or private fixtures.
The credential scan is not a private-document/PII classifier.

## What remains outside Phase 6 local acceptance

Real Entra, TLS/proxy configuration, private storage/scanner/key deployment, edge
limits, image/OS validation and production recovery operations require their own
target-environment evidence. A domain is not needed for this portfolio. The next
delivery work is the clean-checkout local demo, screenshots, CI and publication
inventory in [Phase 8](../phases/phase-8-operations-deployment.md). Arabic support
remains separately scoped in Phase 7. This acceptance does not publish the project.
