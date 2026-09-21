# Runtime and dependency security baseline

Reviewed September 15, 2026. Package audits and runtime versions are separate checks:
a clean dependency report does not establish that the process host, operating system,
container image or native tools are patched.

## Backend and browser baseline

| Component | Selected baseline |
| --- | --- |
| .NET SDK | 8.0.425, pinned by root `global.json`; later patches in the same feature band may roll forward |
| .NET and ASP.NET runtime | Minimum 8.0.31 in generated runtime configuration |
| EF Core / MVC testing | 8.0.31 |
| PostgreSQL provider | Npgsql EF 8.0.11; driver 8.0.6 |
| SQLite native bundle | SQLitePCLRaw 3.0.5, replacing the vulnerable legacy native package |
| Node | 24.21.0 LTS, pinned in `.nvmrc`, bootstrap metadata and browser Docker stages |
| Browser test runner | Vitest 4.1.11 with Node 24 types |
| Documentation | Patched nanoid in the existing lockfile |
| Python service image | Python 3.11.16 slim-bookworm with a verified OCI digest; packaging tools patched before application installation |
| Python development/build tools | pytest at least 9.0.3, ReportLab 3.6.13, setuptools at least 83; audited environment uses pip 26.2.1 and setuptools 84.0.0 |

.NET 8 remains in maintenance support through November 10, 2026. Plan its supported
major-version migration before that date; this patch increment retains the existing
target framework. See [Microsoft's support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
and [Node release policy](https://nodejs.org/en/about/previous-releases).

NuGet restore now explicitly audits direct and transitive dependencies, with existing
warnings-as-errors retained. Audit feed failures must not be presented as a clean
result. Offline test commands may disable repeated audit only after a separate fresh
audit has been captured. See [NuGet audit behavior](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages).
The xUnit update retains existing equivalent assertion semantics; only the style rule
requiring `Assert.Single`'s predicate overload is suppressed. Vulnerability warnings
and other compiler/analyzer warnings are not suppressed.

SQLitePCLRaw 3 keeps the bundle interface; the resolved dependency graph supplies
native `SQLite` 3.53.4. This removes the legacy native package rather than suppressing
its advisory. See the [maintainer's package documentation](https://github.com/ericsink/SQLitePCL.raw).
Both SQLite regression and PostgreSQL integration must pass for this change.

## Reproduce on Windows x64

From the repository root:

```powershell
./tools/install-project-runtimes.ps1
. ./tools/use-project-runtimes.ps1
dotnet --info
node --version
dotnet restore apps/backend/UaeDocumentIntelligence.sln -p:NuGetAuditMode=all
dotnet list apps/backend/UaeDocumentIntelligence.sln package --vulnerable --include-transitive --format json
```

The bootstrap downloads the reviewed official archives and verifies committed SHA-512
or SHA-256 digests before extraction. Runtime files stay in ignored `.dotnet` and
`.node` directories. Existing mismatched installations are refused, not overwritten.
Dot-sourcing selects them for the current shell and child processes; it does not
replace system installations or persist a machine PATH change. Update the reviewed
metadata and hashes when selecting a newer release.

Use `npm ci` in `apps/web` and `docs`, then run audits with development dependencies
included. The browser container now uses `npm ci` and the same Node release. Container
tags still require rebuilds and image/OS scanning; changing a Dockerfile does not
update an existing deployment.

Python application and historical fixture environments remain separate. The
[Python regression instructions](../testing/python-runtimes.md) describe why one
environment cannot satisfy both sets of native dependencies. Do not use old fixture
generation tooling to process untrusted inputs or as the enterprise service runtime.

The local Windows interpreter remains 3.11.9. Upstream Python 3.11 security releases
are now source-only; see the [3.11.16 release](https://www.python.org/downloads/release/python-31116/).
The patched Linux image is the verified service baseline. Windows interpreter
migration and historical fixture-tool updates are separate work, not covered by
the clean Python package-audit result.

## Acceptance limits

The [verification report](../evidence/phase-6/P6-runtime-20260915.md) records actual
versions, fresh advisory results and executed test groups. Audits cover known package
advisories as observed that day; they do not prove absence of vulnerabilities. Source
code, OS/native tools, Docker images, deployed secret delivery and operational access
controls need their own checks. No preflight live-evidence requirement is discharged
solely by a local dependency update.
