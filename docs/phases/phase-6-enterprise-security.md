# Phase 6 — Enterprise security

**Status:** Complete for the local portfolio contract — September 16, 2026

**Outcome:** Access, storage, review, and deletion follow documented enterprise controls.

**September 15 scope decision:** completion is assessed for the locally reproducible
[GitHub portfolio](../design/github-portfolio-delivery.md). Real Entra, TLS and other
target-environment checks remain necessary for any future production activation, but
are optional for portfolio delivery. Implementation gaps and local security tests
were assessed in the [final acceptance](../evidence/phase-6/P6-local-acceptance-20260916.md).
This does not certify or activate a production deployment.

First increment: [identity contract](../design/phase-6-identity-contract.md),
[task ledger](../tasks/phase-6.md), [initial threat model](../security/phase-6-threat-model.md)
and [quality plan](../testing/phase-6-quality-plan.md). The API identity foundation
and [membership/audit backend](../design/phase-6-membership-audit-contract.md)
and browser administration are implemented. [Private-service credentials](../design/phase-6-private-service-access.md)
enforce operation scope, expiry and HTTPS with safe access telemetry. Local controls
and residual risks are mapped in the [acceptance matrix](../security/phase-6-local-acceptance.md).
Live Entra/TLS, deployed storage/scanning and production operations remain future work.

[Worker authority and audit](../design/phase-6-worker-security.md) adds a dedicated
restricted connection, System actor provisioning, atomic transition events and
document/run/attempt lease binding. [Restricted API database access](../design/phase-6-api-database-access.md)
adds a shared provisioning/verification allowlist and a per-request deployment gate,
with signed-token tenant mutation checks under a real restricted login. Process
separation, enterprise activation and resistance to compromised service credentials
remain explicit limitations. [Request and in-flight resource limits](../design/phase-6-request-limits.md)
now bound process/IP and authenticated actor/organization traffic, concurrent work and
actual body bytes. [Durable organization capacity](../design/phase-6-organization-capacity.md)
now bounds active jobs and registered source bytes across API instances.
[Offline recovery](../design/phase-6-orphan-recovery.md) adds reviewed cleanup of untracked
promoted source files and independent capacity-policy snapshot comparison. Deployment
readiness now has a [private preflight](../design/phase-6-deployment-readiness.md) with
static checks and optional read-only resource observations. The repository candidate
remains blocked; live evidence, trusted snapshot freshness and
measured deployment limits remain.

The [runtime baseline](../design/phase-6-runtime-baseline.md) now pins the backend SDK,
requires patched .NET/ASP.NET runtime versions, updates reported package vulnerabilities
and selects Node 24 LTS. Local verification does not activate or verify deployed images.

[Storage admission and scanning](../design/phase-6-storage-safety.md) now stages
uploads before promotion, commits clean-scan audit with document/job admission,
and verifies and rescans worker sources before extraction. Real ClamAV deployment,
private storage permissions/encryption remain deployment work; explicit local
quarantine cleanup and offline orphan recovery are implemented.

[Original-file access](../design/phase-6-source-access.md) now uses authenticated
five-minute grants, membership-version binding, integrity/scanning and durable
authorization audit. The enterprise workspace exposes Download original. Shared
[key protection](../design/phase-6-source-key-protection.md) now supports encrypted
shared storage and certificate rotation. Live replica and TLS/browser verification remain open.

## Work

[Audit operations](../design/phase-6-audit-operations.md) now records authorized,
budget-admitted mutation/source attempts before body parsing. Admission is separate
from business success. [Private audit export](../design/phase-6-audit-export-retention.md)
now provides bounded snapshots, independently checked receipts, offline archive
verification and a local retention/restore runbook. The [operational coverage map](../security/phase-6-audit-coverage.md)
accepts the local audit contract, including fixed operation names, the corrected
semantic retrieval transaction, private operator boundaries and historical gaps.

[Retention controls](../design/phase-6-retention-controls.md) now add organization
and document holds, versioned retention settings, document-expiry previews and
explicit audited cleanup of abandoned quarantine uploads. The browser exposes
Retention & holds. [Document deletion and restore verification](../design/phase-6-document-deletion.md) add opt-in removal of expired unreferenced local content, resumable source removal and independently retained deletion intents. The [local backup/recovery contract](../design/phase-6-local-recovery.md) now defines indefinite backup retention, registered-source checks, an actual same-capture restore drill and exact-byte repair boundaries. Automatic expiry and historical reconciliation remain unsupported; deployment verification remains future work.

- Add OIDC authentication, with Entra ID as the production target.
- Model organizations, users, roles, and permissions.
- Enforce tenant boundaries in every data-access path.
- Add append-only audit events.
- Protect stored files and use time-limited access links.
- Add malware scanning before processing.
- Define retention, deletion, backup, and recovery behavior.
- Add rate limiting and secure secret management.
- Produce a threat model and data-flow diagram.
- Test authorization and tenant isolation.

## Local portfolio acceptance criteria

- [x] Anonymous users cannot access documents through the Locked/Entra API profiles; live deployment/browser verification remains pending.
- [x] Tested public API paths reject foreign organization discovery and actor-private history; the explicit [data-access inventory](../security/phase-6-data-access-inventory.md) defines the boundary.
- [x] Sensitive actions create audit events within the [documented local coverage](../security/phase-6-audit-coverage.md); earlier missing events are not reconstructed.
- [x] Deleted/expired documents follow the documented local retention and recovery behavior; expiry does not automatically remove live documents or backup copies.
- [x] No unresolved credential findings remain in the documented source/history,
  selected-log and production-client review; raw database logs remain a private
  operator boundary and no universal secret/PII absence claim is implied.
- [x] Security limitations are explicit; no certification is implied.
- [x] Audit attempt coverage, authorized export and retention policy are demonstrated for the supported local contract; private operator and global-event exclusions remain explicit.
- [x] A reproducible security walkthrough covers organization membership, role changes,
  revocation and denied cross-organization access using disposable synthetic data.
- [x] Final acceptance maps each claim to tests or evidence and labels external
  integration checks that require a future deployment.

## Portfolio evidence

- Threat model
- Authorization matrix
- Tenant-isolation tests
- Audit-event demonstration
