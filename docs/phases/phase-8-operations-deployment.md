# Phase 8 — Operations and deployment

**Status:** Proposed  
**Outcome:** The platform is deployable, observable, recoverable, and demonstrable without fabricated production claims.

## Work

- Add CI for tests, builds, linting, migrations, and security scans.
- Build production containers and an SBOM.
- Define staging and production-like environments through infrastructure as code.
- Deploy the web, API, AI worker/service, PostgreSQL/pgvector, object storage, messaging, and secrets.
- Add structured logs, metrics, distributed traces, and correlation IDs.
- Track throughput, latency, failures, queue depth, model/provider usage, and cost.
- Create dashboards, alerts, backup/restore procedures, and incident runbooks.
- Run load, failure, and recovery tests.
- Publish a safe portfolio demo using synthetic or licensed data.

## Acceptance criteria

- [ ] Every commit is checked by CI.
- [ ] A documented deployment can be recreated.
- [ ] One document can be traced across web, API, queue, AI processing, and persistence.
- [ ] Alerts cover meaningful failure conditions.
- [ ] Backup restoration is tested.
- [ ] The public demo contains no private client information or fabricated claims.

## Portfolio evidence

- CI/CD pipeline
- Deployment architecture
- Observability dashboard
- Load and recovery report
- Live demonstration and documented limitations
