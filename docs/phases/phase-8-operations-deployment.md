# Phase 8 — Repository delivery and local operations

**Status:** In progress — local demo and CI increments

**Outcome:** A reader can understand, run and verify the project locally from GitHub.

Scope revised September 15, 2026 under the
[portfolio delivery decision](../design/github-portfolio-delivery.md). No domain,
cloud account, paid provider or real Entra tenant is required for this milestone.

The local increment is authorized September 16, 2026. Follow the
[disposable demo guide](../local-demo.md). Phase 6 is complete for its documented
local scope; Phase 7 Arabic/bilingual support remains pending. Local CI checks now
pass; hosted execution and publication review remain separate work. No commit,
push or deployment is authorized.

## Work

- Verify a clean-checkout local setup with pinned prerequisites and disposable
  synthetic data, including startup, shutdown and a safe demo reset.
- Bind local services to loopback and clearly identify local demonstration identity.
- Add CI for tests, builds, linting, migration checks and dependency security scans.
- Verify demo container builds and record component versions and dependencies.
- Write a concise README, architecture overview, setup and contribution guides.
- Capture screenshots and a walkthrough of review, reconciliation, cited answers
  and the security workflows, with limitations visible in the documentation.
- Provide useful diagnostic logs and correlation IDs without sensitive payloads;
  document usage/cost accounting and supported local recovery procedures.
- Run bounded local failure/recovery checks and report measured behavior honestly.
- Review the publication inventory, sample provenance, license and secret exclusions.

## Acceptance criteria

- [x] The declared CI checks pass locally without private credentials or paid calls.
- [ ] GitHub-hosted execution and artifact upload are verified.
- [ ] A fresh checkout can run the documented local demo without an external account.
- [x] A synthetic document can be followed through upload, processing, review and
  persistence, with failures diagnosable from safe logs.
- [x] The supported local recovery procedure is tested on disposable data.
- [ ] README, architecture, screenshots and walkthrough match verified behavior.
- [ ] Files selected for publication contain no private client information or
  credentials; sample rights and the project license are documented.
- [ ] The final report distinguishes local evidence from unverified deployment claims.

## Portfolio evidence

The [September 21 reconciliation walkthrough](../evidence/phase-8/P8-reconciliation-demo-20260921.md)
shows the already completed Phase 4 feature in the disposable demo: three pinned
reviews, nine expected findings and one retained decision. It adds a focused
walkthrough and screenshots without changing reconciliation rules or repeating
the full acceptance suite. Cited-answer and broader security walkthroughs remain
separate portfolio work.

The [September 16 local demo report](../evidence/phase-8/P8-local-demo-20260916.md)
verifies an isolated copy of current uncommitted source, with native English
extraction, review, restart persistence and guarded reset. This is not yet a
clean-HEAD checkout or hosted CI/publication acceptance. Broader workflow screenshots
and repository-wide delivery checks remain pending.

- CI results and clean-checkout reproduction report
- Architecture and security diagrams
- Local demonstration screenshots and walkthrough
- Failure/recovery evidence and documented limitations

## Optional future deployment

Cloud infrastructure, public hosting, domains, Entra registration, TLS ingress,
managed storage/secrets, production alerting and backup automation can be added
later. Any activation must pass the existing enterprise readiness checks and
target-environment verification. Repository completion does not establish that
production systems were deployed or tested. Repository publication itself remains
a separate action.

## CI increment

The [September 19 priority follow-up](../evidence/phase-8/P8-priority-followup-20260917.md)
records two passing demo/restart/reset trials and 1,785 passing tests on updated
source. Its combined CI run was interrupted during dependency audits; no new
all-green CI claim is made. The historical database exit cause remains unproven.

See the [local CI verification report](../evidence/phase-8/P8-ci-local-20260916.md) for
the new workflow, isolated container checks and actual results. Hosted GitHub
execution and publication acceptance remain separate.
