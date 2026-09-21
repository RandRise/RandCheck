# Phase 6 task ledger

Rand authorized beginning Phase 6 on September 11, 2026 after Phase 5 development
acceptance. Phase 6 is complete for the supported local portfolio contract as of
September 16, 2026. Read the [acceptance matrix](../security/phase-6-local-acceptance.md)
and [verification](../evidence/phase-6/P6-local-acceptance-20260916.md).

**September 15 delivery scope:** the [GitHub portfolio plan](../design/github-portfolio-delivery.md)
supersedes mandatory live deployment acceptance. Deployment gaps listed below remain
honest prerequisites for future production use, not blockers for local portfolio
completion. T05 audit operations and T07 recovery are accepted for their explicit
local contracts. T09's final acceptance and scoped credential review pass.
Real Entra, deployed scanner/TLS/key
permissions, edge infrastructure and production backup automation are optional.
Clean-checkout demonstration, screenshots and CI are tracked in the revised Phase 8.

| ID | Task | Status | Dependencies / evidence |
| --- | --- | --- | --- |
| P6-T01 | Identity contract, authorization matrix and initial threat/data-flow model | Complete for the local contract | [Contract](../design/phase-6-identity-contract.md), [membership/audit](../design/phase-6-membership-audit-contract.md), [threat model](../security/phase-6-threat-model.md) |
| P6-T02 | Entra OIDC/API authentication and browser sign-in | Complete locally: API and browser boundary; real Entra registration/verification pending | T01; [verification](../evidence/phase-6/P6-identity-foundation-20260911.md) |
| P6-T03 | Organization membership, roles, provisioning and revocation | Backend complete: immutable identity, DB role/admin grant, private bootstrap, versioned administration and next-request revocation | [Contract](../design/phase-6-membership-audit-contract.md); one active organization, no retained actor remapping; browser administration implemented; [browser contract](../design/phase-6-browser-access.md) |
| P6-T04 | Tenant/caller isolation across API, workers, storage, search, answers and history | Complete locally: signed-token read/mutation checks with restricted API login, including review/case/run/finding substitution and prepared semantic actor/organization binding; live deployment acceptance remains | [Data-access inventory](../security/phase-6-data-access-inventory.md); [API database contract](../design/phase-6-api-database-access.md); [worker contract](../design/phase-6-worker-security.md) |
| P6-T05 | Append-only sensitive-action audit with safe fields and atomic writes | Complete for the supported local contract: 25 distinct admission operations, atomic Human/System changes including isolated semantic retrieval, scoped viewer, private export and indefinite archive retention/restore | [Coverage and exclusions](../security/phase-6-audit-coverage.md); [verification](../evidence/phase-6/P6-audit-coverage-20260915.md); [export/retention](../design/phase-6-audit-export-retention.md); private database reads use operator authority and receipts, not application actor events; missing historic events are not backfilled |
| P6-T06 | Protected storage, expiring authorized access and malware quarantine before processing | Complete locally: staged admission, scan audit, worker rescan and authenticated five-minute original-file grants implemented; explicit leased quarantine cleanup implemented; certificate-protected shared keys implemented; real daemon/key/TLS deployment and private storage permissions remain | [Storage contract](../design/phase-6-storage-safety.md); [source access](../design/phase-6-source-access.md); safe rejection/scan outage/retry behavior |
| P6-T07 | Retention, deletion, legal holds, backup and restore policy/implementation | Complete for the supported local contract: existing versioned holds/retention, explicit eligible deletion and orphan cleanup; indefinite backup retention, read-only registered-source verification and actual same-capture restore drill. Exact-byte offline repair is documented; automatic historical reconciliation/expiry are unsupported | [Local backup/recovery](../design/phase-6-local-recovery.md); [verification](../evidence/phase-6/P6-local-recovery-20260915.md); [retention](../design/phase-6-retention-controls.md), [deletion](../design/phase-6-document-deletion.md); current authority and private operator review remain required |
| P6-T08 | Rate limiting, secret handling, service identity and supported dependency/runtime baseline | Complete locally: private-service credentials, restricted worker/API grants, request/body/concurrency limits, durable organization capacity and scoped credential review | [Capacity contract](../design/phase-6-organization-capacity.md), [capacity evidence](../evidence/phase-6/P6-capacity-20260914.md); [request limits](../design/phase-6-request-limits.md); [service contract](../design/phase-6-private-service-access.md); [API database contract](../design/phase-6-api-database-access.md); offline untracked-file recovery and capacity snapshot comparison implemented; trusted snapshot freshness, distributed/edge limits, live measurement, TLS/secret delivery and API gate activation remain; [runtime baseline](../design/phase-6-runtime-baseline.md) now verifies .NET 8.0.31, Node 24.21.0 and the Python 3.11.16 service image with clean package audits; Windows Python migration, historical fixture tooling and deployed OS/native/image checks remain |
| P6-T09 | Full security and earlier-phase regression; acceptance evidence | Complete for the local contract: 597 fresh checks, production browser build with private synthetic markers, scoped source/history/log/client review, current threat model and claim-to-evidence acceptance; strict preflight retains future production gates | [Final verification](../evidence/phase-6/P6-local-acceptance-20260916.md); [readiness evidence](../evidence/phase-6/P6-readiness-20260914.md); [portfolio scope](../design/github-portfolio-delivery.md); local audit and recovery evidence now complete their bounded contracts |

No paid evaluation, model enablement, commit, subagent, automation or new task was
requested. Keep development accuracy results and the paid 17/30 result distinct.
The original `phase5_e2e_20260908` is read-only. PostgreSQL fixtures may recreate
only `document_intelligence_test`, with one runner at a time. Any future schema
work starts against disposable data; retained migration requires preservation
checks and a concrete documented need.
