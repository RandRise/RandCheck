# GitHub portfolio delivery

September 15, 2026. Rand chose an independent GitHub project as the delivery target.
A domain, cloud subscription, public server and real Entra tenant are not required
for portfolio completion. This decision supersedes earlier mandatory deployment
milestones; it does not change the application's security controls.

## What finished means

- A reader can understand the problem, architecture and supported workflows from
  the README, with screenshots and a short walkthrough using synthetic documents.
- A clean checkout can run a documented local demo with no paid provider or external
  identity account. Required runtimes, initial dependency downloads, setup, seed,
  startup, shutdown and disposable demo reset are explicit and verified.
- Local demo services bind to loopback. Any demonstration identity is clearly
  identified as local-only and cannot activate enterprise authentication profiles.
- Organization membership, roles, revocation, tenant isolation, audit history,
  protected source access and retention have reproducible tests and evidence.
  Simulated identity tests are distinguished from real Entra integration.
- CI runs the declared checks without private credentials or paid calls. The test
  guide distinguishes fast checks, PostgreSQL integration and OCR/container checks.
- The repository has an architecture overview, limitations, contribution/setup
  guidance, license decision and a reviewed publication file inventory. Credentials,
  private documents, local databases and generated working artifacts are excluded.
  Demo samples have clear provenance and redistribution permission.
- Acceptance records state exactly what passed. Optional model results, language
  coverage and production readiness retain their actual limits.

These are delivery criteria, not a claim that the clean-checkout demo or CI already
passes. Publishing or pushing the repository is a separate action.

## Remaining sequence

1. Audit operations are accepted under the [local coverage map](../security/phase-6-audit-coverage.md):
   operational attempts, private export and indefinite audit retention.
2. Local retention/recovery is defined by the [backup/recovery contract](phase-6-local-recovery.md),
   including indefinite backup retention, registered-file checks and repair limits.
   The [final local acceptance](../security/phase-6-local-acceptance.md) maps security claims
   and records the source/history/log/client credential review.
3. Verify the local demo from a clean checkout with disposable synthetic data and
   no external accounts. Capture the user journey and security scenarios.
4. Complete repository delivery: README, screenshots, architecture, CI, publication
   hygiene and a reproducible acceptance report. This is the revised Phase 8 scope;
   Arabic support remains separately scoped in Phase 7.

## Optional production track

Real Entra registration, domains/DNS, TLS ingress, cloud infrastructure, managed
secrets/storage, deployed scanner and key permissions, multi-replica behavior,
production backup automation, monitoring and target-image/OS scanning belong to a
future deployment. They do not block the GitHub portfolio milestone.

The [enterprise preflight](phase-6-deployment-readiness.md) remains strict. A local
demo is not deployment verification: `DeploymentVerified` stays false until the
actual target has the required evidence. Locked/Entra profiles, protected keys,
restricted database grants and service authentication must not be weakened to
accommodate the demo. Dependency maintenance remains part of repository quality.
