# Deployment-readiness verification

September 14, 2026. The [preflight contract](../../design/phase-6-deployment-readiness.md)
defines static checks, explicit read-only probes and live evidence still required.

235 distinct tests pass: 230 API and five PostgreSQL. The full API run passed 228;
the final 30-case preflight run replaces its matching cases and adds two canonical-CORS
boundaries. Initial runs also passed. No tests were skipped. The private tool builds
without warnings, and final CLI examples return the expected blocked/verification-required
exit codes. Documentation and whitespace checks pass.

## Repository result

The repository candidate is **Blocked**: 10 static blockers, four static passes and
seven live-verification requirements. It combines checked-in backend base settings
with production browser defaults; it does not claim to discover effective staging
configuration. Blockers cover API/browser identity, HTTPS origins and host/CORS policy,
API/worker database profiles, enforced storage, scanning, and private service transport/
credentials. Existing runtime configuration was not modified or activated.

A fictional configuration with aligned settings returns exit 2, `ConfigurationValid:
true` and `DeploymentVerified: false`. This proves that static success is not reported
as a verified deployment. Placeholder credentials/hosts in that artifact are fictional.

## Evidence

Artifacts are in `outputs/phase-6/readiness-20260914`:

| Artifact | Scope |
| --- | --- |
| `repository-candidate.json`, `repository-report.json` | Reproducible repository baseline and sanitized blockers |
| `fictional-configured-candidate.json`, `fictional-configured-report.json` | Static success still requires live evidence |
| `readiness-api-initial.trx`, `api-final.trx`, `readiness-api-final.trx` | New static-check boundaries, complete API regression and final canonical-CORS checks |
| `readiness-persistence.trx` | Real restricted API/worker catalog checks, policy/actor coverage, restore controls, grant drift, CLI validation and immutability |
| `verification.json`, `unique-tests.txt` | Deduplicated final outcomes |
| `retained-before.txt`, `retained-after.txt` | Read-only fingerprints of 34 retained tables |
| `docs-build.txt` | Documentation build output |

Tests check fixed safe diagnostics for invalid profiles, mismatched browser identity,
wrong database/role, unsafe remote TLS and verbose diagnostics, path/service settings,
disabled enterprise limits and malformed input. Actual private CLI tests reject
duplicate/unknown/oversized input without echoing configuration. Disposable resource
checks verify every table's full-row fingerprint before and after direct and CLI
inspection, plus unchanged source bytes. A capacity/worker-actor gap, stale control
snapshot and excess API grants produce blockers.

PostgreSQL uses the disposable `document_intelligence_test` on loopback 25434 with
one runner. No live deployment was probed, enabled or changed. No new migration,
business mutation, source cleanup, browser UI change, commit or paid provider call
occurred. NuGet auditing was disabled for offline commands, not claimed as current.

## Remaining work

Live identity/TLS, scanner and Python authorization, storage ACLs/encryption, protected
keys, distributed deployment behavior and independently retained recovery evidence
remain unverified. Read-only catalog and file checks cannot establish those properties.
The next implementation step is explicit protected Data Protection key storage and
cross-instance source-grant verification. Full Phase 6 acceptance remains open.
