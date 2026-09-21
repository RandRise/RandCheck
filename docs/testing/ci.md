# Continuous integration checks

The Phase 8 CI increment uses the same PowerShell entry point in a GitHub-hosted
Ubuntu job and in an isolated local Windows source copy. GitHub execution is not
yet verified: this work does not commit, push, dispatch or publish anything.
Phase 7 Arabic support remains pending.

For a local credential review of the newly built browser, pass
`-ExportBrowserReview` with `-Group web` or `-Group all`. The Locked build's static
assets and rendered app output are copied to that run's ignored `browser-review/`
directory. Point `tools/phase6_secret_review.py --bundle` there. Export is off by
default and this directory is excluded from the workflow's artifact allowlist.

## Declared checks

| Group | Checks | Network during checks |
| --- | --- | --- |
| `workflow` | SHA-256-pinned actionlint 1.7.12 validates the workflow; five demo database-health classification checks | Public linter download |
| `backend` | Release build; six .NET test projects, including PostgreSQL migrations, snapshot drift, security and recovery | Private disposable PostgreSQL only |
| `web` | ESLint with zero warnings; Vitest and Node tests; production Locked build; VitePress build | Internal Docker network, no external route |
| `python` | Ruff correctness rules and compile checks; application, native and OCR test groups; fixture-preservation and credential-scanner regression tests | Internal Docker network, no external route |
| `fixture` | Seven legacy fixture-generator tests in their separate pinned environment | Internal Docker network, no external route |
| `audits` | Fresh NuGet audit (all transitive dependencies/severities); npm audits for browser/docs; pip-audit of installed default service dependencies | Public package/advisory services |

Image builds download public packages and the hash-verified tokenizer vocabulary.
Tests have no provider key, account, paid call or production identity. Unit tests
use fake service transports. The scanner regression creates and commits only
temporary synthetic repositories inside its disposable container; it does not
commit the source checkout. NuGet warnings fail the build. Audit failures or
unavailable feeds fail the job instead of being treated as a clean scan.

The Python partition is deliberate: the immutable generator uses Python 3.11.15,
Pillow 9.4.0 and reportlab 3.6.12, separately from the Python 3.11.16 service.
The default-service dependency audit excludes this legacy generator and test-only
extras. It is not an OS/image vulnerability scan or a repository secret/PII review.
Do not change fixture dependencies or goldens merely to make this check green.

## Local reproduction

Use PowerShell 7, Git and Docker Desktop with Linux containers. Initial downloads
need internet access; reserve several GB of disk space. Builds run sequentially on
the local host to reduce memory pressure. No host .NET or Node command is needed
for this container path. Any separate host command still requires dot-sourcing
`tools/use-project-runtimes.ps1`.

From the working repository root:

```powershell
$ci = ./tools/prepare-ci-source.ps1
Set-Location $ci
./tools/ci/check.ps1 -Group all
```

Or run one declared group, for example `./tools/ci/check.ps1 -Group backend`.
The snapshot includes current uncommitted source, test fixtures and documentation;
it excludes retained data, private environment files, dependencies, build outputs
and Git history. `source-inventory.json` records the SHA-256 of every copied file.
Local runs refuse the original workspace or a changed snapshot. Prepare a fresh
copy after changing source. This is not proof that the current HEAD can reproduce
the project; the extensive uncommitted implementation must be delivered later.

Each invocation owns a random `uaedi-ci-<id>` Compose project. The database has no
published ports, uses a private internal network and keeps data in container tmpfs.
The backend joins that database network namespace and connects over loopback;
the existing TLS and crash-probe guards remain intact.
The persistence fixture can recreate only `document_intelligence_test` in that
service. It does not use the host PostgreSQL instance or accept a connection from
the operator's environment. Runtime credentials are explicit disposable fixtures.
Only the newly created result directory is mounted from the host.

`finally` removes that invocation's containers, networks and volumes on success
or failure. Images, the source snapshot and `outputs/ci/<id>` remain. The script
does not prune Docker or delete host source/data. Abruptly terminating PowerShell
or Docker itself can prevent `finally`; identify the exact project from its result
directory and inspect its labels before manual cleanup. Never run a global prune.

## Workflow and evidence boundaries

`.github/workflows/ci.yml` declares push, pull-request and manual triggers, six
matrix jobs, a timeout and concurrency cancellation. Actions are pinned to full
commit SHAs; the token has only `contents: read`, checkout does not persist it,
and no repository secrets, deployment environment or `pull_request_target` event
is used. These choices follow [GitHub's secure-use guidance](https://docs.github.com/en/actions/reference/security/secure-use).

Only bounded summaries, counts, test-ID manifests and public dependency-audit
results are selected for seven-day artifacts. Raw TRX, logs, databases, backup
files and runtime credentials are not selected. Actual job scheduling, artifact
upload and branch protection remain unverified until an authorized GitHub run.

The .NET result gate rejects missing projects, zero selected tests and skipped or
failed cases. The existing Python runner rejects skipped or empty selections.
Vitest fails an empty selection. Test counts must come from fresh output, not
older build directories. The backend also tests the model against its migration
snapshot and exercises migration apply/rollback/reapply on disposable data.
Crash-probe children initialize EF and their guarded test connection before invoking
the operation. Their parent allows 60 seconds for process setup and the existing
45-second generation bound. Recovery's ten-second deadline and all crash-state,
audit/accounting and no-redispatch assertions remain unchanged. These are durability
tests; they do not establish cold-process latency on a memory-constrained host.
Browser checks are jsdom component tests and Node request tests, not a live-browser
journey. The separate local-demo screenshots provide that earlier manual evidence.

The [local verification report](../evidence/phase-8/P8-ci-local-20260916.md) records
the checks actually run, failures encountered and the remaining limits.

## Synthetic Python setup

The application checks first materialize five hash-pinned files from
`samples/phase-4/procurement-v0.1.0` and `samples/phase-5/due-date-v0.1.0`
at the legacy paths required by the existing tests. The original question-set
and due-date manifest hashes are verified before use.
The Phase 3 smoke report is freshly calculated from committed synthetic predictions.
No ignored host output directory is imported. The helper preserves matching files
and refuses changed outputs or redirected destinations.
