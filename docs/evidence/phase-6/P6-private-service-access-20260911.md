# Private-service access verification — September 11, 2026

Rand authorized continuing Phase 6. This increment implements the
[private-service access contract](../../design/phase-6-private-service-access.md).
It does not complete Phase 6 or establish production readiness.

## Verified result

| Check | Result |
| --- | --- |
| Python application regression | 330 passed, zero skips; includes 14 new service-security tests |
| .NET Infrastructure regression | 111 passed, zero skips; includes 37 new security/transport cases |
| .NET API regression | 143 passed, zero skips; includes four new real-host service-mode cases |
| Documentation | VitePress production build passes; existing bundle-size warning |
| Retained database | All 34 table count/content fingerprints match before and after; original migration count 15 |
| Spending / model activity | No paid provider calls, ledger changes, model enablement or retained migration |

The 584 tests above are unique within this increment; repeated runs are not added.
The Python fixture/native/OCR groups and PostgreSQL suite were not rerun: their
prior evidence remains separate. No domain, repository, schema, storage or web
code changed. Browser evidence remains under `outputs/phase-6/browser-20260911`;
this server increment has no new visual UI.

Python checks exercise real FastAPI routing with HTTPS ASGI scopes, exact service
permissions, overlapping key rotation, removal/reload revocation, expiry without
reload, malformed configuration, duplicate headers/JSON keys, unknown paths,
plain-HTTP rejection, legacy route suppression and safe settings representations.
Separate probes assert no body read or provider dependency construction before
admission. Access-event capture proves verified attribution, generated request
IDs, safe outcome fields and exclusion of tokens/content/untrusted identifiers.

.NET checks exercise fixed credential selection, TLS origin constraints, refusal
before network dispatch, actual extraction-client success with a named key,
missing/malformed credentials and the real API host's production/default mode.
Registered handler chains are inspected for disabled redirects, proxies, cookies
and framework HTTP loggers. Existing cited logging tests and payload/error
contracts pass. Enterprise mode cannot enable the loopback embedding pilot.

## Reproduction and evidence

From the repository root:

```powershell
.venv/Scripts/python.exe tools/run_python_regression.py --group application --manifest outputs/phase-6/service-access-20260911/python-application-manifest.json
dotnet test apps/backend/tests/UaeDocumentIntelligence.Infrastructure.Tests --output tmp/phase6-service-infrastructure -p:NuGetAudit=false --logger 'trx;LogFileName=service-infrastructure.trx' --results-directory outputs/phase-6/service-access-20260911
dotnet test apps/backend/tests/UaeDocumentIntelligence.Api.Tests --output tmp/phase6-service-api -p:NuGetAudit=false --logger 'trx;LogFileName=service-api.trx' --results-directory outputs/phase-6/service-access-20260911
```

Evidence directory: `outputs/phase-6/service-access-20260911`. It contains the
Python manifest/log, both TRX reports, before/after retained fingerprints and
machine-readable verification. Local .NET tests used the installed runtime and
disabled NuGet audit metadata fetching; this is not a clean dependency-audit
claim. The Python runner reports the existing httpx/TestClient deprecation warning.
The initial documentation build hit a Windows sandbox path-resolution EPERM;
the approved local build passed outside the sandbox. Both logs are retained.

The original `phase5_e2e_20260908` was queried read-only using
`tools/phase6_retained_fingerprints.sql`. No disposable database was needed or
created in this increment. No current preview or retained application was restarted.

## Limits

TLS enforcement is tested at client configuration and ASGI admission boundaries,
not against a deployed certificate/proxy chain. No real Entra, cloud secret store,
managed service identity or operational rotation across replicas was used.
Safe Python access events require INFO-level protected log delivery; they are not
append-only database audit. Worker database authority, durable service audit,
remaining tenant mutation tests, scanning, retention and rate limits remain open.
