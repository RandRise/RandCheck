# Phase 6 — Enterprise security

**Status:** Proposed  
**Outcome:** Access, storage, review, and deletion follow documented enterprise controls.

## Work

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

## Acceptance criteria

- [ ] Anonymous users cannot access documents.
- [ ] A user from one organization cannot discover another organization's data.
- [ ] Sensitive actions create audit events.
- [ ] Deleted/expired documents follow the documented retention behavior.
- [ ] Secrets are absent from source, logs, and client bundles.
- [ ] Security limitations are explicit; no certification is implied.

## Portfolio evidence

- Threat model
- Authorization matrix
- Tenant-isolation tests
- Audit-event demonstration
