# Enterprise deployment readiness

**Optional production track (September 15, 2026):** these checks apply to future
deployment. They do not gate the [GitHub portfolio milestone](github-portfolio-delivery.md).
Local demo success must not be reported as `DeploymentVerified`.

September 14, 2026. `EnterpriseReadiness` provides a private configuration inspection
and optional read-only resource verification. It never starts the application, launches
workers, modifies configuration, repairs grants, migrates data or enables a deployment.
It returns fixed check IDs/messages without configuration values or exception details.

## Commands and input

```text
EnterpriseReadiness inspect <private-effective-config.json>
EnterpriseReadiness verify-resources <private-effective-config.json>
```

The JSON input has these exact top-level properties:

| Property | Meaning |
| --- | --- |
| `Environment` | Explicit `Production` or `Staging` |
| `Backend` | Nested effective .NET configuration, including the intended database connections and operation credentials |
| `Browser` | String map containing `AUTH_MODE`, the three `NEXT_PUBLIC_ENTRA_*` values, `NEXT_PUBLIC_API_URL`, and `WEB_ORIGIN` |
| `ExpectedDatabase` | Exact intended database name; retained `phase5_` names are refused |
| `RecoveryControlFile` | Optional absolute path; required when document deletion is enabled |

`WEB_ORIGIN` is preflight metadata for checking CORS and HTTPS. It is not a new browser
runtime setting. The remaining browser names map to the existing build/runtime inputs.
Run on the intended backend operating system so absolute path rules match deployment.

Supply the actual effective configuration privately after deployment overrides have
been resolved. The tool does not load ambient environment variables, appsettings overlays,
secret stores or web build artifacts. A valid supplied file cannot prove that deployed
processes use it. Do not commit a file containing real credentials. Input is capped at
1 MiB; duplicate properties, ambiguous casing, excessive nesting and unknown top-level
fields are refused. Unknown backend sections remain allowed because normal application
configuration includes unrelated feature settings.

Exit codes are `1` for blockers, `2` for checks passing but live verification still
required, and `3` for invalid input or an interrupted verification. There is deliberately
no deployment-success exit code. `ConfigurationValid` concerns static checks;
`DeploymentVerified` remains false. Resource failures can block an otherwise valid
configuration. Consumers must inspect `Status` and `Checks`, not configuration validity alone.

## What is checked

Configuration checks cover API Entra settings; browser tenant/client/audience/scope
alignment; HTTPS origins; explicit CORS and AllowedHosts; matching restricted database
profiles; separate worker identity; enforced source/quarantine paths and size limits;
the supported loopback ClamAV profile; HTTPS ServiceKeys clients; bounded request
limits; disabled development seeding/legacy maintenance; and journal/control path
separation when deletion is enabled. The existing Entra and request-limit validators
are reused. Service credential checks validate client shape only, not receiver acceptance
or token entropy. Remote database profiles require VerifyFull TLS and safe diagnostics.

`inspect` reads only the supplied input. `verify-resources` additionally:

- Connects to the exact configured API and worker databases using their own credentials
  and verifies the existing shared catalog allowlists. Startup session options force
  read-only transactions, with public search path, no pooling and five-second connection/
  command timeouts. The CLI has a 60-second overall deadline.
- After API grant verification passes, checks that at least one organization exists and
  every organization has a capacity policy and active System actor for the configured worker.
- Checks that source/quarantine directories exist with no reparse-point ancestors.
  This does not establish ACL access for another process identity or encryption.
- If deletion is enabled, compares the existing independent journal and schema 2 controls
  with the database using the recovery guard. It does not create files or repair a mismatch.

Verification inserts no audit event because it performs no business mutation. Full-row
fingerprints in disposable tests verify that direct and CLI resource checks leave all
database rows unchanged. Source bytes also remain unchanged.

## Checks still requiring live evidence

The report always retains explicit `VerificationRequired` items for deployed Entra/
TLS/browser behavior; actual database activation across instances; storage ACLs,
encryption and shared mounts; clean/malware/scanner-outage behavior; Python service
verifier scopes/expiry/rotation; protected Data Protection keys and cross-instance grants;
independent backup/restore freshness; patched runtimes, secrets, edge limits and telemetry.
Observed local resources do not discharge these broader deployment requirements.

The API now consumes [explicit protected source-key configuration](phase-6-source-key-protection.md).
Static inspection checks its shape; resource verification loads wrapping certificates
and decrypts existing descriptors without creating a key. Actual target-environment
permissions, mounted storage and replica behavior still require live evidence.

## Repository baseline

The checked-in base backend configuration, evaluated as a Production candidate with
the browser's production defaults and no deployment overrides, had 10 configuration
blockers, four static passes and seven live-verification requirements. It is a local
configuration, not an inspected staging deployment. See the
[verification report](../evidence/phase-6/P6-readiness-20260914.md) for the captured result.
No real tenant, TLS endpoint, private service credentials or deployment key-ring evidence
was supplied in this increment. A fictional fully configured candidate passes static
checks but still returns `VerificationRequired`, never deployment acceptance.

The September 15 key-protection increment adds a `source-keys.profile` blocker to
that repository baseline (11 blockers, four passes, seven live requirements). This
describes the repository candidate; it does not discover deployed settings.
