# Continuation checkpoint

## Current: GitHub checkpoint save authorized — September 21, 2026

The user reversed the earlier deferral and explicitly requested saving the current
work with a GitHub commit and push. This authorizes the checkpoint save; it does
not mark the unfinished phases complete or authorize public hosting, paid provider
calls or deployment. A new repository should be private by default. At preparation,
this checkout has no configured remote; resolve the intended GitHub destination
before pushing. Keep existing source history and do not force-push.

The save includes implementation, tests, documentation, curated screenshots and
reviewed synthetic samples. Retained databases, uploads, keys, private environment
files, raw logs, generated outputs and dependencies remain excluded and preserved
locally. The current file inventory has 858 candidates and no excluded-artifact
errors; the credential scan has zero unresolved findings/errors across current
text, history, the existing Locked browser build and selected demo logs. These
are bounded backup checks, not public-release or license clearance. Raw receipts
are `outputs/github-save-inventory.json` and `outputs/github-save-secret-review.json`.

Use a checkpoint commit with `[skip ci]` so saving does not launch another full
suite. No runtime services need starting. Phase 7 remains unimplemented; the
completed local demo/reconciliation evidence remains valid, and the unfinished
dependency audits, license decision, public-release review and hosted CI remain
explicitly pending. Record the actual commit/remote result in the task once saved.

## Current direction: finish local product work before GitHub delivery — September 21, 2026

The user deferred source delivery: finish the remaining work first, then discuss
commits and pushes. Do not treat that future sequence as authorization to commit,
push, publish or run hosted CI now. Keep verification focused on changed behavior;
do not repeat completed Phase 4 or demo acceptance suites.

The next unfinished product phase is [Phase 7 Arabic/mixed-language support](phases/phase-7-arabic-support.md).
Start with a versioned synthetic language dataset and evidence-preservation
contract, then implement and evaluate Arabic extraction, RTL review and cross-language
questions. The current local extractor explicitly accepts only `OCR_LANGUAGE=eng`
and has English field labels. Existing English results do not establish Arabic
support. Phase 7 is not complete or implemented by this sequencing decision.

After the language work, close remaining local usability gaps (including reopening
a reconciliation case after refresh), align the documentation with measured results,
and complete the necessary local Phase 8 checks. License/notices and publication
review belong to the final repository-preparation step. Clean-checkout and hosted
verification follow separately authorized source delivery. Real Entra/cloud
deployment and optional paid model experiments remain outside this local scope.

Docker and demo services were stopped after the completed walkthrough. This planning
update starts no services and changes no runtime implementation or retained data.
Preserve the uncommitted implementation; HEAD is outdated. Use
`tools/use-project-runtimes.ps1` for host .NET/Node commands.

## Current: Phase 8 reconciliation demo walkthrough complete — September 21, 2026

The user authorized demonstrating the existing Phase 4 feature, not rebuilding it.
The [walkthrough report](evidence/phase-8/P8-reconciliation-demo-20260921.md),
[receipt](evidence/phase-8/P8-reconciliation-receipt.json) and two screenshots show
three pinned synthetic reviews, nine exact expected findings, matching fields,
source-page evidence and one retained reasoned decision. A fresh API read matches.
Only this focused journey and the helper's ownership guard were checked; do not
repeat Phase 4 acceptance, the full CI suite or the earlier restart tests.

`tools/prepare-local-demo.ps1` now includes the unchanged Phase 4 sample pack and
`tools/verify-demo-reconciliation.ps1`. Follow the separate reconciliation section
in [the demo guide](local-demo.md). Use an empty copy for its Prepare stage, complete
the browser case, then run Verify with the displayed case UUID. Application code,
reconciliation rules, security controls and synthetic sample PDFs are unchanged.

Final source: `outputs/local-demo/1e45fce6127e`, 304 inventory files all matching the
workspace; raw journey, startup, verification and cleanup logs remain there. An
initial helper empty-array guard failure occurred before upload in `145e430debdb`;
its failed evidence and successful reset are preserved. `b82bd06f6bb0` was never
started. All owned disposable resources are gone, ports 3308/8308 are closed, and
Docker Desktop/WSL are stopped. The native retained database was not accessed.

Known UI limit: refreshing the reconciliation page loses the selected case in the
browser; the case remains stored and accessible through its UUID. Case reopening
UI, cited-answer and broader security walkthroughs are separate future work.
Phase 6 local scope is complete; Phase 7 Arabic/bilingual remains pending. Keep
services stopped until needed. No commit, push or publication is authorized.

## Current: Phase 8 essential priority checks complete — September 19, 2026

The user requested only important verification. The [follow-up report](evidence/phase-8/P8-priority-followup-20260917.md)
and [receipt](evidence/phase-8/P8-priority-receipt.json) close this local increment.
Two fresh demo trials passed setup/migrations, upload, local extraction, append-only
review, exact restart persistence, health observation and guarded reset. Their
runtime source inventories still match the workspace. Do not repeat these checks.

The final isolated CI source `outputs/ci-source/8e7fa1227ac2` passed 1,785 tests,
Release/Locked browser/documentation builds, lint and migration checks, including
the final crash-test harness. The combined run was interrupted during dependency
audits and lacks its runner summary; do not call it all-green. On September 19,
Docker was initially stopped. It was started for targeted cleanup only. The owned
CI project `uaedi-ci-8389844dc608` was removed; separate cleanup evidence is
`outputs/p8-final-ci-cleanup.json`. No suite or audit rerun is needed for this increment.

Final safety receipt `outputs/p8-final-state.json` verifies zero owned resources,
closed demo ports and retained public-table fingerprints unchanged at SHA-256
`06c07f7f05eb9d20a7ceb561d43821414427bd79d0453e170a65ae41132c7549`.
The retained cluster was initially stopped, started on loopback for this read-only
comparison, allowed to recover its prior interruption, then cleanly stopped.
No retained-data reset or migration was performed.

Fresh CI browser/source/history/selected-log credential review has zero unresolved
findings/errors after exact decisions. It precedes final report edits and does not
establish publication clearance. Source/images/evidence and earlier failed attempts
remain preserved. The historical demo database exit-code-2 cause is still unproven;
the new startup health gate detects recovered crashes and permits slow initialization.

Phase 6 remains complete for documented local scope; Phase 7 Arabic/bilingual is
pending. License choice, notices/publication review, clean-HEAD reproduction and
hosted CI remain pending. No commit, push, account, publication or deployment was
performed. Use `tools/use-project-runtimes.ps1` for host .NET/Node commands.

## Current: Phase 8 local CI increment complete — September 17, 2026

The [CI guide](testing/ci.md) and [verification report](evidence/phase-8/P8-ci-local-20260916.md)
record 1,780 passing tests: 1,365 backend, 64 browser and 351 Python/tool checks.
Release/Locked browser/docs builds, lint, migration checks and scoped NuGet/npm/pip
audits pass. GitHub execution, artifact upload and clean-HEAD reproduction remain
unverified. Phase 6 is complete for its documented local scope; Phase 7 Arabic
support remains pending. Do not ask again about phase order.

Implementation is in `.github/workflows/ci.yml`, `tools/ci/` and
`tools/prepare-ci-source.ps1`. Use an isolated copy including uncommitted source;
HEAD is outdated. Host .NET/Node commands still require the project runtime helper.
No project commit, push, publication or deployment is authorized or performed.

Backend/browser and audits used `outputs/ci-source/c18d1e04b907`; final Python and
fixture checks used `outputs/ci-source/81f785689247`. The initial all-stage attempt
failed at missing Python fixtures. Preserve its failed receipt; successful targeted
reruns supersede only those stages. Code/package inputs for earlier passing stages
are unchanged. The report documents packaging, loopback and synthetic-input fixes.
No production security code, expected hash, OCR golden or retained output changed.

All CI containers, networks and named volumes are removed. Source copies, images
and ignored logs remain. Final retained public-table fingerprint SHA-256 matches
the baseline: `06c07f7f05eb9d20a7ceb561d43821414427bd79d0453e170a65ae41132c7549`.
The new fixture helper verifies five existing synthetic files, generates the smoke
report and refuses changed outputs/redirected paths. Four preservation tests pass.
Remaining Phase 8 work is broader workflow screenshots, publication/license
inventory and a later authorized source delivery/hosted CI run.

## Current: Phase 8 local demo increment — September 16, 2026

The authorized local demo increment is implemented. Start with the
[runbook](local-demo.md) and [verification report](evidence/phase-8/P8-local-demo-20260916.md).
Phase 6 remains complete for its documented local scope. Phase 7 Arabic/bilingual
support remains pending; do not ask again about this accepted phase order.

`tools/prepare-local-demo.ps1` snapshots the current uncommitted runtime source,
one fictional English PDF and its license records into a uniquely owned copy.
`tools/local-demo.ps1` builds, migrates, starts, stops and safely resets only that
copy's Docker project. API/web publish only loopback 8308/3308; DB/Python stay
private. Existing Locked/Entra, original-source access and production identity
gates are unchanged. Host .NET/Node commands still require
`tools/use-project-runtimes.ps1`.

The 295-file journey copy is `outputs/local-demo/4b297fab4fb0`. Upload/local native
extraction, eight append-only review actions, completion, full review-response
equality and source-byte persistence after container recreation all pass. Reset
refuses changed Compose, a foreign path and unexpected project volumes; actual
reset removes owned volumes and fresh migrations yield an empty document store.
Four rebuilt security profile checks pass. Retained PostgreSQL public-table
fingerprints are unchanged. Screenshots and a curated JSON receipt are in
`docs/evidence/phase-8/`; raw logs/receipts stay in ignored `outputs/local-demo/`.

The final startup copy `outputs/local-demo/5a61b2aebc85` changes only the Compose
health check to require PostgreSQL TCP readiness instead of its initialization
socket. Its separate inventory records that bounded difference. Final fresh startup,
19 migrations, empty-store verification and the documentation build pass. Both demo
projects' containers/volumes are removed and loopback ports 3308/8308 are closed;
source copies, image cache and evidence remain. Initial builds were
slow on the 8 GB host; earlier superseded database process-recovery events have
an unproven cause and are documented, not treated as production acceptance.

This is current-working-source reproduction, not clean-HEAD acceptance: HEAD is
still outdated. Preserve the existing extensive uncommitted implementation and
retained data. No external account, paid provider, cloud deployment, commit, push
or publication was used. Local CI verification is recorded above. Broader workflow screenshots and
publication/license inventory review remain pending.

## Prior handoff: user created the local demo chat manually

The user wants the local demo in a new chat they will create manually. The mistaken
"Local demo setup" fork (01a0aa98-8e4a-7a60-9d4e-3651aee9a0e0) was interrupted and
archived; do not restart it or create another chat automatically. The user will
paste a handoff prompt into their new chat. That prompt authorizes the local demo
increment of Phase 8; do not ask again about phase order. Phase 7's Arabic/bilingual
support has not been started or cancelled. Phase 6 is complete for its documented
local contract; deployment and publication are not authorized.

The interrupted Phase 8 investigation was read-only: it inspected Compose,
Docker availability, setup instructions and application configuration. No demo
code/configuration was changed, no containers were started and no database was
mutated. Findings to retain if Phase 8 is chosen: the existing quick start needs
manual migration, the production web image correctly rejects Development identity,
and legacy Compose publishes ports on all interfaces. A separate disposable,
loopback-only demo was proposed but has not been implemented. Preserve the existing
Locked/Entra safeguards. The workspace still contains extensive uncommitted work;
continue in this directory rather than assuming HEAD contains the implementation.

## Delivery decision: independent GitHub portfolio

September 15, 2026: Rand does not have a domain and wants an independent project
to be proud of on GitHub. Follow the [portfolio delivery plan](design/github-portfolio-delivery.md).
Local reproducibility, security evidence, screenshots, documentation and CI are the
completion target. Cloud hosting and actual Entra/TLS/deployment verification are
optional future work, not portfolio blockers. Preserve strict enterprise profiles
and honest preflight results. Do not publish or push as part of this scope decision.
Phase 6 is complete for its supported local contract as of September 16.
Next is Phase 8's clean-checkout local demo, then screenshots, CI and publication
inventory. Arabic remains Phase 7; production integration remains optional.

## Current: Phase 6 local acceptance complete

September 16, 2026: read the [acceptance matrix](security/phase-6-local-acceptance.md)
and [final verification](evidence/phase-6/P6-local-acceptance-20260916.md). P6-T09 and
Phase 6 are accepted for the supported local portfolio scope. No domain or cloud
account is needed. The strict enterprise preflight still has DeploymentVerified=false.

597 fresh primary checks pass: 260 API, 220 Infrastructure, 35 PostgreSQL security
walkthrough, 64 browser, 14 Python service and four scanner tests. The Python run
also has 13 subtests, excluded from that total. Backend builds with zero warnings/errors;
production browser and documentation builds pass. Earlier runtime/worker/storage/
restore evidence remains separately linked, not added to the fresh total. No fresh
package audit is implied. Initial zero-selected-test and incorrect docs-script
attempts are retained and superseded by passing final runs.

The Development orphan cleanup logger now suppresses raw exception/state details,
with a regression against the background loop. Git ignores private .env variants,
key containers, tmp/, outputs/ and .vite/; example .env files remain visible. This
preserves local artifacts while preventing ordinary staging of temporary databases,
raw logs, runtime downloads and generated receipts. Nothing was deleted.

The offline scanner reviews current Git text candidates, reachable historical text,
explicit .log/.trx roots and fresh static JS/maps/prerendered HTML/RSC. Three synthetic
server-only build markers do not appear in client output. There are zero unresolved
findings/read errors; 26 reviewed fixture/field-name matches use exact file hashes,
paths, rules and lines in docs/security/phase-6-secret-review-decisions.json. Scanner
reports never contain credential values. It is bounded detection, not a universal
secret/PII guarantee. Raw PostgreSQL logs remain a sensitive operator boundary;
MSAL uses sessionStorage and service credentials are not constrained by tenant RLS.

Artifacts are under outputs/phase-6/acceptance-20260916, including verification.json,
secret-review.json, TRX/logs and the 14-event audit snapshot. A curated synthetic copy
is docs/evidence/phase-6/P6-local-acceptance-audit-20260916.json. All 34 retained
fingerprints match; no migration, paid call, deployment, commit or push occurred.

**Next:** verify the independent local demo from an isolated copy of intended source
files, including these uncommitted changes; plain HEAD does not contain the current
implementation. The workspace has extensive preexisting changes. Do not clean/reset
or publish it. Read the portfolio delivery plan and Phase 8 plan, establish an exact
source/sample inventory, then demonstrate loopback startup/shutdown/reset and the
synthetic document journey without external accounts. Curate screenshots and evidence
into docs rather than force-adding ignored output/raw-log directories. CI,
README/setup, sample provenance and license/publication review follow. Do not weaken
Locked/Entra settings to make the demo work.

All .NET/Node shells use tools/use-project-runtimes.ps1. PostgreSQL tests run one at a
time and may recreate only document_intelligence_test, local port 25434. The final
walkthrough filter is in the acceptance matrix; its partial test class requires
method-name filters. Certificate tests/builds may need approved host access. Tests
use --no-restore -p:NuGetAudit=false; runtimes remain the September 15 baseline.

## Prior: local backup and recovery

September 15, 2026: read the [local recovery contract](design/phase-6-local-recovery.md)
and [verification snapshot](evidence/phase-6/P6-local-recovery-20260915.md). P6-T07 is
accepted for this explicit local scope. Backups and inventory records retain an
indefinite policy; no expiry/purge scheduler is supplied. Exact-byte repair is a
private offline procedure, not an automatic historical reconciliation command.

New private `EnterpriseRecovery verify-sources <private-config.json>` requires
offline acknowledgment, an exact non-retained database and the existing journal/
controls. It checks all registered canonical files by hash/size in one read-only
Repeatable Read database snapshot with controls/deletion history. It refuses silent
RLS filtering, relative/reparse paths, missing/corrupt/locked sources, more than
1,000 documents, over 10 MiB per file or 1 GiB total. It prints counts and changes
no files/rows. The command has a two-minute verification budget. Normal source
scanning remains mandatory after integrity verification.

The actual pg_dump/pg_restore drill stops its fixture API hosts, captures only
`document_intelligence_test` plus one synthetic source, restores the same capture
after deliberate fixture damage and matches all 41 table fingerprints plus exact
source bytes. Current journal/controls remain independent. No valuable database
is dumped/restored; retained checks remain read-only. Machine results and the
backend/CLI snapshot are under `outputs/phase-6/local-recovery-20260915`.

501 distinct checks pass: 260 API, 219 Infrastructure and 22 PostgreSQL (11 new
source/drill cases plus 11 lifecycle regressions), with no failures or skips.
The initial 10-case source run is superseded and not added again. Backend solution
build has zero warnings/errors. All 34 retained fingerprints match the preceding
checkpoint. Final documentation build status is recorded in `verification.json`.

Code: `RegisteredSourceRecovery.cs`, the recovery CLI and an adjustment allowing
the control snapshot to share the verifier's transaction without committing it.
No schema, HTTP route, browser or package version changed. The dump drill needs
PostgreSQL tools on PATH or `TEST_POSTGRES_BIN`; all .NET/Node shells use the runtime
helper. Run PostgreSQL fixtures one at a time. Test builds use `--no-restore
-p:NuGetAudit=false`; no fresh package audit is implied.

**Next:** finish P6-T09's local claim-to-evidence matrix and source/log/client secret
review. Keep real Entra/TLS/deployment checks separate and optional for portfolio
delivery. Then verify the independent clean-checkout demo. An old backup is not
authority to reopen: current memberships/revocations, controls, keys and grants need
review; stale historical reconciliation remains unsupported. No commit, push or
deployment was requested.

## Prior: operational audit coverage

September 15, 2026: the [coverage map](security/phase-6-audit-coverage.md) closes
P6-T05 for the explicit local enterprise contract. Read the
[verification snapshot](evidence/phase-6/P6-audit-coverage-20260915.md).
New admission reasons identify 25 fixed operations. Historical method-only reasons
remain valid. A coverage test cross-checks every authorized route for unique safe
names, omission on ordinary reads and no body access.

The review also found that semantic retrieval opened an isolated database context
without the bound audit identity. It now carries that context, refuses mismatched
bindings and commits `question.saved` atomically with retrieval/hits. Signed-token
vector/hybrid/nonredundant tests cover audit outage rollback, retry and replay;
unbound Development behavior remains supported. No historical events are fabricated.

The map distinguishes Human/System business events, independent file authorization,
private operator receipts, global events and incomplete attempts. It does not claim
exhaustive HTTP/host/DBA access logging, client byte receipt or tamper-proof storage.
No schema or browser change was needed. Current artifacts and synthetic HTTP/CLI
snapshots are under `outputs/phase-6/audit-coverage-20260915`.

497 distinct checks pass: 260 API, 219 Infrastructure and 18 PostgreSQL. The final
API run follows the semantic fix; no failures or skips. Backend solution build has
zero warnings/errors. All 34 retained fingerprints match the preceding checkpoint
when psql display spacing is normalized. No migration, paid call, deployment, commit
or push occurred. See the verification JSON for final documentation build status.

**Next:** resolve the supported local recovery/backup-expiry contract (P6-T07),
including registered-content repair boundaries. Then complete the final security
and secret review/claim-to-evidence acceptance (P6-T09), followed by the independent
clean-checkout demonstration. Real Entra/TLS/cloud deployment remains optional.
Use the project runtime helper for every .NET/Node shell and run PostgreSQL fixtures
one at a time against `document_intelligence_test` only.

## Prior: private audit export and retention

September 15, 2026: `EnterpriseAudit export <private-plan.json>` now captures a
bounded organization/time snapshot with a read-only PostgreSQL transaction.
`verify <archive-directory> <trusted-manifest-sha256>` works offline. Read the
[export/retention contract](design/phase-6-audit-export-retention.md) and
[verification snapshot](evidence/phase-6/P6-audit-export-20260915.md).

225 checks pass: 219 infrastructure (30 archive cases) and six PostgreSQL. The full
backend solution builds with zero warnings/errors. A synthetic CLI export, trusted
receipt and final offline verification are captured under
`outputs/phase-6/audit-export-20260915`. No package version, schema, HTTP route or
browser component changed. Source audit records and accepted archive/receipt pairs
remain indefinitely retained; no delete/purge/expiry command was added.

Export requires private database-operator authority, an explicit database name,
organization, UTC microsecond-aligned window and existing private output directory.
It refuses retained phase5 databases, silent RLS filtering and oversized/truncated
output. Limits: 1–50,000 rows (default 10,000), 64 MiB data, 8 KiB record and 16 KiB
plan/manifest. Publication follows verification; checksums require an independently
trusted receipt. Late commits can be absent despite older timestamps: no safe
incremental cursor is claimed. Global/unresolved events are not exported.

**Next:** finish the operational audit coverage inventory and local acceptance,
explicitly distinguishing database-operator receipts from application actor events.
Then resolve the supported local recovery/backup-expiry contract and complete Phase 6
acceptance/secret review before the clean-checkout demo. Production deployment is
optional. All 34 retained fingerprints match; no commit, push, paid call or deployment
occurred. Continue using the project runtime helper for every .NET/Node shell below.

## Prior: audit admission

September 15, 2026: [audit operations](design/phase-6-audit-operations.md) now
persists `request.admitted` in Entra mode after authorization and request budgets,
before body parsing, for mutations and source downloads. Admission success is not
business success. A failed admission insert returns 503 without reading the body or
executing the endpoint. The local Entra profile also places cited-body buffering
after admission; Development keeps its original response ordering.

All 277 distinct checks pass: 259 API and 18 focused PostgreSQL cases, with the
latest successful rerun replacing the initial membership-outage failure. Read the
[verification snapshot](evidence/phase-6/P6-audit-admission-20260915.md). The broad
PostgreSQL run was intentionally stopped after 56 completed passes; it is not a full
suite pass and is excluded from these totals. Membership/upload outage tests now
fail their business audit action after admission, preserving rollback coverage.
Membership Save now maps only genuine PostgreSQL membership-table uniqueness errors
and concurrency failures to 409; audit/storage outages reach the safe 503 boundary.
Deletion replay expects one new admission and no repeated business events.
Artifacts: `outputs/phase-6/audit-admission-20260915`.

Next implementation: bounded private audit export, explicit archive/retention
handling and final operational coverage review. Audit records currently remain
append-only and retained indefinitely; document deletion does not purge them.
No schema or browser component changed in this increment.
The artifact folder contains a synthetic HTTP audit-viewer JSON snapshot. All 34
retained fingerprints match. No deployment, paid call, commit or push occurred.

## Prior: runtime and dependency security baseline

Updated September 15, 2026 after Rand asked to keep going. Read the
[runtime contract](design/phase-6-runtime-baseline.md) and
[verification snapshot](evidence/phase-6/P6-runtime-20260915.md).

**Use the project runtimes for every subsequent shell:**
`. ./tools/use-project-runtimes.ps1` from the repository root. The reviewed Windows
bootstrap is `./tools/install-project-runtimes.ps1`; it installs into ignored
`.dotnet` and `.node` folders using official archives and committed checksums.
System dotnet remains 8.0.403/8.0.10 and cannot satisfy the new root global.json.
System Node remains old too; do not accidentally test with the machine PATH.
The project SDK is 8.0.425, both API frameworks require 8.0.31, and Node is 24.21.0.

EF/MVC and vulnerable transitive packages were updated; SQLitePCLRaw 3.0.5 resolves
native SQLite 3.53.4 (loaded version verified). NuGetAuditMode=all remains enabled;
fresh restore/audit passes. xUnit's new assertion-overload style rule alone is
suppressed to preserve equivalent existing tests. Vitest is 4.1.11, with a jsdom
FileReader test adaptation. Two documented React effect-style exceptions preserve
existing retention loading/reset behavior; no download/business logic was rewritten.

The Python service image is digest-pinned to 3.11.16; five OCR goldens and 330
application checks pass with networking disabled and source mounted read-only.
The latter use the test-only derivative in tools/runtime-regression.Dockerfile.
Production and Windows application package inventories/audits are captured separately.
Windows Python remains 3.11.9; its environment is not the enterprise interpreter
baseline. Historical fixture dependencies remain isolated and were not regenerated.
Docker was started for isolated image verification; no Compose application was started.

1,338 distinct checks pass: 938 backend (44 PostgreSQL), 64 browser, 336 Python.
The Python application repeat in the patched image is not counted twice. The final
migration test replaces its initial stale 16-migration assertion failure (current: 19).
TRX aggregation uses test ID plus display name because Domain theory rows share IDs.
Builds, lint, TypeScript and whitespace checks pass. Artifacts are under
`outputs/phase-6/runtime-20260915`. All 34 retained fingerprints match; check silently.
No new migration, deployment activation, paid provider call or commit occurred.

**Next:** audit operations: complete operational-attempt coverage and define/implement
safe private audit export plus retention handling. Read the membership/audit contract
and task ledger first; preserve append-only application grants. Backup expiry/restore
repair need an explicit supported local contract. Windows interpreter and historical
fixture-tool maintenance remain tracked limitations. Deployed OS/native/image scans,
edge limits and real Entra/TLS/scanner/key/recovery evidence now belong to the optional
production track. Plan .NET's supported major migration before its November 10, 2026
end of support. Phase 6 is not complete. Earlier deployment-gated next steps below
are historical and superseded by the portfolio decision above.

Communication preference: mention retained data/spending only if something changes
or needs attention.
## Prior: protected source-grant keys

Updated September 15, 2026 after Rand asked to proceed. Enterprise original-file
grants now use an isolated certificate-protected Data Protection provider with an
explicit shared directory and application name. Read the
[key contract](design/phase-6-source-key-protection.md) and
[verification snapshot](evidence/phase-6/P6-source-keys-20260915.md).

Production/other nonlocal profiles refuse source grants until Certificate mode is
usable. Local Development/Testing defaults remain available. The API still starts;
source issuance fails safely with 503, and unreadable grants release no file.
Wrapping certificates import into ephemeral storage. Directory/certificate resources
must already exist; no production certificate or directory was provisioned.
Configuration changes and wrapping-certificate rotation require restart.

Preflight now consumes `SourceKeyProtection`, checks its static profile and optionally
loads existing certificates/decrypts descriptors without creating a key or grant.
The repository candidate has 11 blockers, four passes and seven live requirements.
The fictional configured candidate still exits 2 with DeploymentVerified false.
Artifacts and final test outcomes are in `outputs/phase-6/source-keys-20260915`.
283 distinct tests pass: 253 API and 30 PostgreSQL; the final key-boundary run
supersedes matching API cases. Documentation and whitespace checks pass.

No new schema, browser screen or deployed settings changed. Key-ring ACLs, actual
shared mounts, private certificate delivery/backup, replica restart/rotation and
framework revocation caching require deployment evidence. Key encryption does not
encrypt source files or protect against a compromised API identity holding both
ring and private certificate. Keep old certificates while retained keys need them.

**Next:** verify the supported dependency/runtime security baseline, then complete
actual target-environment preflight and live Entra/TLS, scanner, private-service,
storage/key permissions and recovery evidence before activation. The local .NET
runtime observed in this verification was 8.0.10; updating package references alone
does not update the deployed runtime. No actual deployment target or private
credentials were supplied. Phase 6 remains in progress.

Communication preference: discuss retained data/spending only if something changes
or needs attention. All 34 retained fingerprints match; continue checking silently.
Do not repeat old evidence collection or invent deployment configuration.
## Prior: enterprise deployment preflight

Updated September 14, 2026 after Rand asked to proceed. Private EnterpriseReadiness
`inspect` and `verify-resources` commands now distinguish static configuration validity,
read-only resource observations and live deployment evidence. Reports contain fixed
check IDs/messages without secrets, paths or exception diagnostics. No service host,
worker, provider request, grant repair or migration is started by the tool.

Read the [preflight contract](design/phase-6-deployment-readiness.md) and
[verification report](evidence/phase-6/P6-readiness-20260914.md). 235 distinct tests pass:
230 API and five PostgreSQL. The final static-check run supersedes matching earlier
cases and adds canonical-CORS boundaries. Private CLI builds and documentation/
whitespace checks pass. Artifacts are in `outputs/phase-6/readiness-20260914`.
All 34 retained fingerprints match; check silently. No runtime settings, deployment,
new migration, source cleanup, browser UI, commit or paid call changed.

The checked-in base backend configuration evaluated as Production with browser
production defaults has 10 static blockers, four passes and seven live-evidence
requirements. This is a repository candidate, not discovered effective staging
configuration. A fictional valid candidate returns exit 2 (VerificationRequired),
never deployment acceptance. No real target tenant, endpoints or key-ring evidence
was supplied. The report always keeps DeploymentVerified false.

Optional probes use exact API/worker identities and startup read-only PostgreSQL
sessions, then inspect current grants, policy/actor coverage, existing storage roots
and schema 2 recovery controls. Failed API grants prevent tenant-control reads.
Full-row disposable fingerprints and source bytes are identical before/after direct
and CLI probes. File/DB observations do not prove ACLs, encryption, receiver credential
validity, independent snapshot freshness or cross-instance deployment behavior.

**Next:** explicit protected Data Protection key storage and cross-instance source-grant
verification. The host currently only calls default AddDataProtection(); do not invent
unconsumed shared-key settings. Implement validated deployment configuration and test
key persistence/protection and compatible application isolation, then use preflight
and actual target-environment evidence before activation. Live Entra/TLS, scanner,
Python verifier/rotation, storage permissions, recovery/backup expiry, patched runtime,
edge limits and protected telemetry remain outstanding. Phase 6 is not complete.

Communication preference: discuss retained data/spending only if something changes
or needs attention. Earlier fictional deletion snapshots remain in their evidence folder.

## Prior: offline source recovery and capacity restore verification

Updated September 14, 2026 after Rand approved the next step. Private EnterpriseRecovery
`inventory-orphans` and `cleanup-orphans` commands now cover unregistered promoted
source files. Inventory produces a new reviewable plan; execution binds it to the exact
database, journal, upload root and control-file digest, rechecks registrations/holds,
and verifies each source's size, timestamp and SHA-256. Independent System audit
authorization precedes removal; completion audit follows it. Missing-file replay never
manufactures a completion event. Registered documents and quota accounting are unchanged.

Read the [offline recovery contract](design/phase-6-orphan-recovery.md) and
[verification report](evidence/phase-6/P6-orphan-recovery-20260914.md).
442 distinct checks pass: 200 API, 189 Infrastructure and 53 PostgreSQL, including
16 new recovery cases. The final PostgreSQL run replaces the initial recovery run.
Documentation and whitespace checks pass.
Artifacts are in `outputs/phase-6/orphan-recovery-20260914`. All 34 retained
fingerprints match; keep checking silently. No new migration, live cleanup, deployment,
browser UI change, commit or paid call occurred.

The recovery commands require `OfflineMaintenanceConfirmed: true` and all storage
writers stopped throughout inventory/execution. This flag acknowledges an operator
precondition; it does not detect or stop processes. Inventory is limited to 1000
entries and 100 sources. Plans expire after 24 hours; minimum source age is 24 hours.
Unknown layouts/organizations, registered, held, young or inaccessible sources are
preserved. Partial failures require audit/filesystem investigation. Existing leased
quarantine cleanup continues to handle staged uploads.

Schema 2 `export-controls` snapshots now include capacity policy presence, limits,
versions and update times. `verify-restore` refuses legacy schema 1 and mismatched
capacity state. Capture current controls from the authoritative database, not from
an unverified restore. Keep the journal and controls independently retained; rolling
back all copies together can evade detection. No online recovery gate is changed.

**Next:** deployment-readiness preflight and verification. Check the current non-secret
configuration and prerequisites for Entra/TLS, restricted API/worker database identities,
scanner, storage permissions, journal/control retention and protected keys. Identify
missing live prerequisites before enterprise activation. Backup expiry, registered-content
restore repair, physical volume limits/monitoring, large-inventory handling and distributed
request limits also remain. Phase 6 is not complete.

Communication preference: discuss retained data/spending only if something changes
or needs attention. Earlier fictional deletion snapshots remain in their evidence folder.

## Prior: durable organization queue and source capacity

Updated September 14, 2026 after Rand approved the next step. Upload and reprocess
admission now enforce privately provisioned organization limits for active jobs and
registered source bytes. PostgreSQL Read Committed transactions serialize admissions
across hosts with an organization-first `FOR NO KEY UPDATE` lock. Worker completion
and audit remain compatible with that lock. Retry/leased jobs remain charged; terminal
work releases slots. Pending source deletion stays charged until completion commits.

Read the [capacity contract](design/phase-6-organization-capacity.md) and
[verification report](evidence/phase-6/P6-capacity-20260914.md). 785 distinct checks
pass: 200 API, 337 Application, 189 Infrastructure and 59 PostgreSQL. Final capacity
coverage replaces its earlier regression-run cases; totals use stable TRX test IDs.
Documentation and whitespace checks pass. Raw artifacts and aggregate verification
are in `outputs/phase-6/capacity-20260914`. All 34 retained fingerprints match; keep
checking silently. No browser UI change, deployment, commit or paid call occurred.

Migration 19 (`20260914125645_AddOrganizationCapacity`) was applied only to disposable
databases. No default quotas are seeded. Enterprise profiles force RequirePolicy;
local profiles may omit policies, but an existing policy is always enforced. Private
EnterpriseAdmin `capacity-policy`/`capacity-status` commands bind to an exact database;
updates require the expected version and commit with System audit. Deployment must
migrate, provision policies and reapply the restricted API SELECT grant. API policy
writes, removal/remapping and populated downgrade are refused.

**Next:** operational recovery of untracked upload files and independent capacity-policy
restore verification. Registered-byte accounting excludes staged/orphan files, derived
content and backups; it is not a hard disk quota. Existing retention-control exports
do not yet pin capacity-policy freshness after restore. Physical storage monitoring,
backup expiry/audited restore repair, edge/distributed request limits, API gate activation
and live Entra/TLS/scanner/storage/key verification remain. Phase 6 is not complete.

Communication preference: discuss retained data/spending only if something changes
or needs attention. Earlier fictional deletion snapshots remain in their evidence folder.

## Prior: request and in-flight resource limits

Updated September 14, 2026 after Rand asked to continue. Process/IP admission now
precedes database access and body buffering; actor/organization budgets follow current
authorization. Mutations and original-file downloads have tighter work budgets.
Admission has bounded partition state, no waiting queue and concurrent leases released
on completion/failure/cancellation. Actual-body caps and a 15-second read deadline
cover unknown-length/chunked input. Generic 429 responses include Retry-After.

Read the [request-limit contract](design/phase-6-request-limits.md) and
[verification report](evidence/phase-6/P6-request-limits-20260914.md). 221 unique checks
pass: 196 API and 25 PostgreSQL. Eight final body checks repeat within the API total.
The documentation build and whitespace checks pass.
Raw evidence and deduplicated counts are in `outputs/phase-6/request-limits-20260914`.
No UI changes, schema migration, deployment restart, commit or paid call occurred.

Limits default on outside Development/Testing; disabling them elsewhere fails startup.
Defaults per 60 seconds: global 1200/IP 300/organization 600/actor 180 requests, with
concurrency 64/16/16/6. Work limits are organization 60 and actor 20, with concurrency
4 and 2. State is capped at 8192 partitions. These are startup configuration values,
per process; replicas multiply capacity and restart resets counters. Forwarded headers
are not trusted. Metrics use bounded stage tags without per-rejection database writes.

Real restricted-login tests prove actor/organization separation, revocation before
throttling, upload refusal without file/queue/audit side effects, and source download
throttling before scanning/release. Exact-limit and excess chunked bodies pass/fail on
real Kestrel; timeout handling distinguishes stalled reads from workflow cancellation.
The earlier failed unknown-length fixture is retained and replaced by real streamed
HttpContent. All 34 retained fingerprints match; check silently on future work.

**Next:** durable queue and storage capacity admission, with atomic organization
limits and safe recovery after work completes or content is removed. Current rate
limits bound incoming/in-flight requests, not persistent backlog or stored bytes.
Distributed/edge throttling, production measurement and telemetry export remain.
API database gate activation, live Entra/TLS/scanner/storage/key verification, backup
expiry and audited restore repair also remain open. Phase 6 is not complete.

Communication preference: discuss retained data/spending only if something changes
or needs attention. Earlier fictional deletion snapshots remain in their evidence folder.

## Prior: restricted API database access and tenant mutations

Updated September 14, 2026 after Rand approved the next task. The API now has an
explicit table/column allowlist, private EnterpriseAdmin `api-role` and
`verify-api-role` commands, and an opt-in per-request catalog guard before membership
lookup. Grant configuration and its System audit event are atomic. Excess/missing
privileges return a generic 503. The gate remains off locally; enterprise deployment
must provision a dedicated login and enable it on every instance.

Read the [API database contract](design/phase-6-api-database-access.md) and
[verification](evidence/phase-6/P6-api-database-20260914.md). 407 unique checks pass:
169 API, 189 Infrastructure and 49 PostgreSQL. Documentation and private CLI builds
pass. Raw evidence, final totals and deduplicated names are in
`outputs/phase-6/api-database-20260914`. The final PostgreSQL run passed 23 cases,
superseding earlier failures. No new schema migration or browser UI change was needed.

Restricted-login signed-token checks cover successful workflows and foreign
document/job/review/field/case/run/finding substitutions, saved-question actor privacy,
and prepared semantic actor/organization binding. Reconciliation link replacement now
records an unlink event instead of failing the generic audited-deletion guard. Audit
failure rolls back the replacement; previous evaluation snapshots survive. The older
audit downgrade test now names its target migration rather than assuming it is latest.

**Next:** request and resource limits: bound expensive upload, processing and query
work, partition authenticated limits by server-resolved actor/organization, bound
unauthenticated requests, and verify throttling/recovery with safe response behavior.
The API service role still accesses allowed rows across organizations; no RLS or
compromised-service isolation is claimed. Deployment activation, live Entra/TLS,
scanner/storage/key protection, backup expiry and audited restore repair remain open.
Phase 6 is not complete. No commit, paid call or production enablement occurred.

Communication preference: run preservation checks silently and mention retained data
or spending only if something changes or needs attention. All 34 fingerprints matched.
Earlier fictional deletion snapshots remain in their prior evidence directory.

## Prior: stored-document deletion and recovery verification

Updated September 13, 2026 after Rand approved proceeding. Administrators can inspect
and explicitly delete expired unreferenced local documents, with hold/dependency
blockers, confirmation, pending-file retries and separate audit stages. An independent
pinned journal precedes database removal; immutable deletion records survive source
removal and permit replay. Enabled business routes and worker sessions reject restored
deleted content. The private EnterpriseRecovery console exports retention controls and
verifies a restored database/files against supplied authoritative controls and journal.

Read the [deletion/recovery contract](design/phase-6-document-deletion.md) and
[verification](evidence/phase-6/P6-document-deletion-20260913.md). 481 unique checks pass:
169 API, 182 Infrastructure, 66 PostgreSQL and 64 web. Production Next and recovery CLI
builds pass. Five labeled fictional snapshots and raw evidence are in
`outputs/phase-6/document-deletion-20260912`; the directory retains the start date.
Loopback preview port 3020 uses in-memory data; `preview.json` records its process.
Migration `20260912200431_AddDocumentDeletion` was exercised only on disposable data.
All 34 retained fingerprints match. Initial failed runs are preserved and excluded
from final totals. No commit, paid call or production enablement occurred.

**Next:** complete restricted API database grants and the remaining tenant mutation
matrix, then request/resource limits. Deletion remains disabled by default and excludes
reviewed/cited/accounting dependencies. Backup expiry, independently durable journal
storage, snapshot freshness, automatic audited restore reconciliation and deployment
verification remain open. The recovery CLI verifies and refuses mismatches; it does
not repair a restore. Old journal/config rollback is outside the runtime guard's
assurance. Live Entra/TLS, real ClamAV, protected shared keys and storage ACLs/encryption
also remain open. Phase 6 is not complete.

Communication preference: run preservation checks silently and discuss retained data
or spending only if something changes or needs attention. Focus updates on new work.

## Prior: retention controls and quarantine lifecycle

Updated September 12, 2026 after Rand approved proceeding. The browser now exposes
Retention & holds: versioned organization settings, organization/document holds,
release confirmation, document expiry preview and explicit quarantine cleanup.
Defaults retain documents indefinitely and disable cleanup. Stored-document deletion
remains disabled. New Enforced uploads carry organization/document/upload identifiers
and an exclusive lease held through scan and promotion; the old orphan cleaner is
blocked outside Development. Cleanup rechecks holds/membership under an organization
lock and requires durable authorization audit before deletion, with separate completion.

Read the [retention contract](design/phase-6-retention-controls.md) and
[verification](evidence/phase-6/P6-retention-controls-20260912.md). 434 unique checks
pass: 169 API, 172 Infrastructure, 35 PostgreSQL and 58 web. The production Next build
passes. Four labeled fictional snapshots are in `outputs/phase-6/retention-20260912`.
Loopback fixture port 3019 uses in-memory records; read `preview.json` for its process.
Migration `20260912173517_AddRetentionControls` was applied only to disposable test
data. All 34 retained fingerprints match. Earlier raw failed/repeated runs remain
in the evidence folder and are not added to the final total.

**Next:** dependency-aware stored-document deletion and backup/restore behavior,
including hold-aware recovery and preventing deleted content from reappearing after
restore. Current retention only previews document expiry. The quarantine batch is
bounded without a durable cursor; unknown legacy staging and interrupted empty lease
markers remain preserved. Filesystem deletion plus DB audit is not atomic: a durable
authorization without completion is unresolved. Live Entra/TLS, real ClamAV, shared
protected key deployment, storage ACLs/encryption, broader tenant mutations, full API
runtime grants and rate limits remain open. Phase 6 is not complete.

Communication preference: Rand asked why unchanged Phase 5 data/spending was repeated.
Continue preservation checks silently; mention them only if something changes or needs
attention. Keep updates focused on new work, results and next steps.

## Prior: authorized original-file access

Updated September 12, 2026 after Rand asked to keep going. Enterprise readers can
use Download original with an authenticated five-minute grant. Grants bind actor,
organization, membership version, document, hash and size; their credential travels
in a header, never a URL. Redemption rechecks expiry/membership after scanning and
requires durable audit before releasing a verified memory snapshot as an attachment.
Production source access requires HTTPS and Enforced storage. No schema migration.

Read the [source-access contract](design/phase-6-source-access.md) and
[verification](evidence/phase-6/P6-source-access-20260912.md). 399 unique checks pass:
165 API, 161 Infrastructure, 21 PostgreSQL and 52 browser checks. All 34 retained
fingerprints match; original spending and 15 migrations remain unchanged. No paid
call, retained write, model enablement or original-file cleanup occurred.
Evidence and three labeled fictional snapshots are in
`outputs/phase-6/source-access-20260912`. The local fixture uses port 3018; read
`preview.json` for its process. Native blob file-save completion was not established
by in-app automation; HTTP bytes and client initiation are tested separately.

**Next:** quarantine lifecycle and retention rules, with holds and preservation
tests before any cleanup. Live Entra/TLS, real ClamAV, shared protected key-ring
deployment, storage permissions/encryption, broader tenant mutations, API runtime
grants and rate limits remain open. Phase 6 is not complete. Preserve Phase 5 data,
spending and prior evidence; use only disposable databases for new schema tests.

## Prior: storage admission and malware screening

Updated September 12, 2026 after Rand asked to keep going. Enforced uploads now
stage in a separate quarantine directory, enforce byte bounds, hash and scan before
promotion. Clean-scan audit commits with the document/run/job; failed admission
compensates the source. Enforced workers verify stored identity/size/hash and rescan
before extraction. Scanner outages use bounded retries; detection and integrity
failures terminate with safe System audit reasons. Production storage defaults Locked.

Read the [storage contract](design/phase-6-storage-safety.md) and
[verification](evidence/phase-6/P6-storage-safety-20260912.md). 655 unique checks pass:
337 Application, 157 Infrastructure, 153 API and eight PostgreSQL integration checks.
All 34 retained fingerprints match this increment and the previous checkpoint;
464 spend events, 153 reservations and 15 original migrations remain unchanged.
No new migration, paid call, retained write or browser change occurred.
Evidence is in `outputs/phase-6/storage-safety-20260912`.

**Next:** authorized original-file access/expiring links and quarantine lifecycle,
plus private storage permissions/encryption and real scanner deployment validation.
No real ClamAV daemon/signatures or detection quality was verified: tests use injected
outcomes and a real TCP protocol fixture. Live Entra/TLS, remaining tenant mutation
checks, full API runtime grants, retention and rate limits remain open. Phase 6 is
not complete. Preserve prior browser snapshots and all Phase 5 evidence/spending.

## Prior: worker database authority and durable audit

Updated September 12, 2026 after Rand asked to go on. Enforced extraction workers
now use a dedicated restricted database connection and provisioned System actors.
Claims, completions, retries, terminal failures and final lease expiry produce
append-only audit events in the same transaction as state changes. Lease locks
now also bind document/run/attempt, and provider identity comes from the stored run.
The private CLI provisions role grants and new service actors without migrations.

Read the [worker contract](design/phase-6-worker-security.md) and
[verification](evidence/phase-6/P6-worker-security-20260912.md). Worker authority
remains cross-organization and shares the API process; it is not database row-level
isolation. The original Phase 5 database remains read-only, with no retained
migration or paid calls. Provisioning refuses retained `phase5_*` targets.

306 unique checks pass: 35 PostgreSQL security/queue, 123 Infrastructure and 148
API tests. Final guard/provisioning checks also pass and are counted once. All 34
retained fingerprints match, including 464 spend events and 153 reservations;
migration count remains 15. Test database and roles are removed. Documentation
build and whitespace checks pass. Evidence is in `outputs/phase-6/worker-security-20260912`.

**Next:** protected storage and malware quarantine before processing; remaining
tenant mutation checks, live Entra/TLS verification, complete API runtime grants,
retention and rate limits remain open. Phase 6 is not complete.

## Prior: private-service access controls

Updated September 11, 2026 after Rand asked to keep going. Named, scoped, expiring
service credentials now protect the Python boundary before request parsing and
provider setup. .NET selects separate extraction/retrieval/answer credentials,
requires HTTPS in ServiceKeys mode and disables redirects, proxies, cookies and
framework HTTP logs for private clients. API production defaults remain Locked;
Python enterprise mode rejects legacy routes. Local Phase 5 fixtures are retained.

Read the [private-service contract](design/phase-6-private-service-access.md) and
[verification](evidence/phase-6/P6-private-service-access-20260911.md). Service
access telemetry has safe fields; it is not a durable organization audit trail.
584 checks pass: 330 Python application, 111 Infrastructure and 143 API. All 34
retained table fingerprints match, including spending; migration count stays 15.
No paid calls or retained writes occurred. The documentation build passes.

**Next:** worker database least privilege and service-actor audit, remaining
tenant mutation checks, storage/scanning controls and live Entra/deployed TLS
verification. Whole-Phase-6 acceptance remains open. Browser snapshots from the
prior increment remain in `outputs/phase-6/browser-20260911`.

## Prior: browser access and organization UI

Updated September 11, 2026. Rand approved browser sign-in, member management and
audit viewing and requested snapshots. This increment is implemented: MSAL v5
sign-in/logout and isolated redirect bridge; database-backed `/api/session`;
authenticated transport for all prior workflows and history download; organization
member grant/edit/revoke with version-conflict and last-administrator handling;
and a paged, read-only audit viewer. Production defaults to Locked until configured.

Read [browser contract](design/phase-6-browser-access.md) and
[browser verification](evidence/phase-6/P6-browser-access-20260911.md).
44 web checks, 139 API checks and one new signed-token PostgreSQL session check
pass. The production build passes on patched Next.js 16.3.5. Production npm audit
has zero advisories; two moderate development-tool advisories remain. Browser
snapshots and verification are in `outputs/phase-6/browser-20260911`.
All 34 retained table fingerprints match; original migration count remains 15.
No paid calls or retained spending changes. The disposable test database is gone.

**Next:** configure and verify real Entra tenant/SPA/API registration, then continue
worker/private-tool identity and remaining isolation/storage controls in Phase 6.
The UI preview uses explicitly fictional data and a test identity adapter; live
Entra has not been verified. No whole-Phase-6 completion or production-readiness
claim. Prior Phase 5 evidence and backend membership increment remain below.

## Prior API and membership checkpoint

Updated September 11, 2026 after Rand asked to begin Phase 6 enterprise security
and access control. Phase 5 development remains complete; its historical reports,
retained data and spending records are preserved.

**Phase 6 is in progress.** Rand approved the user-to-organization and audit plan.
The backend now has immutable Entra tenant/object identity, database organization
membership, Viewer/Reviewer/Operator data roles and separate membership administration.
Token roles confer no permissions. One active organization per identity is enforced;
revocation applies to the next request with the same token. Private bootstrap creates
a new organization and first administrator with data role None. Scoped/versioned
membership APIs preserve the last administrator under concurrent removal.

Safe security events commit in the business transaction. Authorization denials and
failed admitted mutations use an independent audit context. PostgreSQL blocks audit
UPDATE/DELETE/TRUNCATE; populated security history blocks downgrade. A deployment
script configures a separate runtime role. All 31 business routes are classified;
local development identity cannot use the four enterprise administration routes.

Read the [current identity contract](design/phase-6-identity-contract.md),
[membership/audit contract](design/phase-6-membership-audit-contract.md),
[task ledger](tasks/phase-6.md) and
[increment verification](evidence/phase-6/P6-membership-audit-20260911.md).
Evidence is in `outputs/phase-6/membership-audit-20260911/verification.json` and
adjacent TRX/logs. **All 921 unique backend checks pass**, zero failures/skips:
663 non-PG (111 Domain, 335 Application, 74 Infrastructure, 4 Architecture, 139 API)
and all 258 PostgreSQL checks. Nine focused security scenarios pass; the final two
strengthened same-organization history/source and membership checks pass again.
The full PostgreSQL run took 32 minutes 51 seconds. CLI, migration model and docs
checks pass. `audit-example.json` demonstrates actual safe events from a fictional
organization. The disposable test DB and temporary runtime-test role were removed.
Existing Phase 5 previews were not restarted.

Migration `20260911162600_AddEnterpriseMembershipAndAudit` adds three tables and
has been applied only in the disposable `document_intelligence_test`. The original
`phase5_e2e_20260908` stays at 15 migrations; all 34 whole-table fingerprints match
the before snapshot. No retained actor remapping, data rewrite, provider call, spending
change, commit, new task, automation or subagent. General model answers and arbitrary
live embeddings remain disabled; unknown holds stay retained. Python/web production
code is unchanged; their Phase 5 acceptance remains the prior result.

**Next product work after verification:** browser OIDC sign-in/logout, live Entra
registration verification and organization administration UI. No real tenant is
configured. Worker/private-tool identity, remaining HTTP isolation variants, storage
links, malware scanning, retention/deletion, rate limits and runtime/dependency
hardening remain open. Audit scope and database-owner limitations are explicit in
the contract. Local .NET/ASP.NET runtime remains 8.0.10; this is not production readiness.

## Preserved Phase 5 development closure

Updated September 11, 2026 after Rand asked to proceed with closure.
**Phase 5 development is complete.** The authoritative report is
[development acceptance](evidence/phase-5/P5-development-acceptance-20260911.md).
Case selection pins completed linked reviews; public vector/hybrid uses already
settled exact query/corpus embeddings, with clear 422 when unavailable. New
canonical sources quarantine on the declared hostile patterns; prompt preparation
screens again before reservations. General model answers remain disabled.

860 unique backend checks have passing latest results (611 non-PG, 249 PG),
329 Python and 27 web checks pass. The initial full PG run's historical clock
failures are preserved; affected fixtures use the frozen rate-window midpoint
and were rerun. Production rate admission and stored spending were unchanged.
Frozen HTTP acceptance: 30/30, 54 exact cited-fact references, all saved-history,
replay and changed-input checks. Fresh extraction/review/evidence and Phase 4
reconciliation flows pass. Five browser snapshots saved. All 34 retained tables
unchanged; semantic HTTP used a copied database and added only retrieval rows.
Zero new paid calls or charges. No migration, commit, task or automation created.

Evidence: outputs/phase-5/closure-20260911/verification.json and SNAPSHOTS.md.
Reproduce Python with tools/run_python_regression.py and docs/testing/python-runtimes.md.
The app dependency installation is repaired; application/native share .venv,
fixture generation intentionally uses Pillow 9.4 and OCR uses the pinned image.

Next planned phase is Phase 6 enterprise identity/security. Do not silently enable
the optional model route or arbitrary live query embeddings. Historical private
29/30, varied development 36/36 and paid 17/30 remain distinct immutable results;
unknown spending holds stay retained. Synthetic results do not establish real-world
accuracy or production readiness. Original retained phase5_e2e_20260908 remains
read-only. Disposable PG fixture DB is document_intelligence_test, one runner only.
Local preview API8088/Python8009/web3006 uses phase5_reviewed_public_20260911;
secondary API8090/Python8010 uses phase5_semantic_public_20260911. Read service
JSON files for current PIDs; never reuse old PIDs blindly.

## Preserved pre-closure checkpoint

Updated September 11, 2026 after Rand agreed to the final acceptance audit.
**The audit is complete; it no longer remains the next task.** See
[acceptance audit](evidence/phase-5/P5-acceptance-audit-20260911.md).
Six new PostgreSQL adversarial checks pass: canonical poisoned source/direct
hostile questions, corrupt chunk/page refusal, real active other-actor history
isolation and separate same-key run, superseded zero-hit replay/history denial,
and forged/swapped hit/span rejection. Production code is unchanged in this audit.
SeedTypedFactCorpus now optionally appends/indexes a poisoned page; default fixture
behavior is unchanged. All 14 shared-fixture regression cases also pass (20 unique
PostgreSQL checks total); documentation build passed, all 34 retained tables match.
Results are recorded separately
in outputs/phase-5/acceptance-audit-20260911/verification.json.

Correction to prior status: the approved P5-T01 contract makes model adapters
optional. Do not impose enabling OpenAI/general model answers as an automatic
closure condition. If that route is delivered, its known support/quality and
security failures still require resolution. It remains disabled; no paid rerun.

**Next implementation:** connect reconciliation-case selection to a frozen
reviewed corpus; then complete the declared RAG security matrix and the final
full regression. Also reconcile the delivered retrieval policy with the approved
contract: public evidence defaults to lexical, bounded answers read all reviewed
facts, and evaluated vector/hybrid remains private. Do not silently drop planned
features or merge the historical 29/30, paid 17/30 and revised 36/36 scores.
Enterprise identity remains Phase 6. Phase 5 remains open for these concrete items.

Evidence folder: outputs/phase-5/acceptance-audit-20260911.
Only disposable document_intelligence_test is reset by these PG tests. Retained
phase5_e2e_20260908 must remain unchanged. No new model calls, charges, commits,
tasks, automations or subagents were requested. Prior services/ports and browser
snapshots remain as recorded below; no new UI behavior was introduced this turn.

## Preserved public reviewed-answer checkpoint

Updated September 11, 2026 after Rand asked to keep going.
**The broader development evaluation and public reviewed-answer integration are
implemented and verified.** The `/questions` page offers evidence search (default)
and Answer from reviewed fields. The new POST `/api/cited-questions/reviewed`
atomically stores scoped runs, reviewed-fact hits, answers, claims and citations.
Saved history reads the stored answer. Same-key retries replay; changed inputs
return 409. Active actor/source eligibility is checked on creation, history,
alternate retrieval GET and evidence navigation. No migration or provider call.

The new 36-case varied synthetic fixture initially passed 31/36; version 1.2
passes 36/36 after buyer-TIN/compound-identifier, maximum and plural-item fixes.
It is development feedback, not an independent holdout or old-model improvement.
591 unique non-PG backend checks, eight new PostgreSQL cases and 24 web checks
passed. Final extraction-default alignment to local-1.3.0 was followed by another
74 infrastructure and 83 API checks (repeats, not additional unique cases).
Web lint/build passed. Real services passed 20 earlier evidence-flow checks,
16 new answer scenarios plus a partial-answer supplement (41 cited fact refs),
and three-document reconciliation with all nine expected findings and one saved
resolution. Browser checks and six screenshots cover date, original quote,
history, maximum, refusal and partial answer. All 34 retained tables unchanged;
zero model calls, new reservations or new cost.

See [public integration verification](evidence/phase-5/P5-reviewed-public-verification.md)
and [contract](design/phase-5-reviewed-public-contract.md).
Evidence: outputs/phase-5/reviewed-public-20260911/{verification.json,SNAPSHOTS.md}.
New isolated synthetic acceptance DB: phase5_reviewed_public_20260911; API 8088,
private Python 8009, web 3006. Startup: tmp/start_reviewed_public.ps1. Services
remain running for the local preview. Do not reset retained phase5_e2e_20260908.
Disposable PG tests still use only document_intelligence_test, one runner at a time.
No Python production changes this increment; preceding 329-check/OCR result remains
the due-date checkpoint, not a newly rerun total.

**Next:** final Phase 5 acceptance audit against the existing criteria and remaining
RAG/adversarial cases. General semantic/model-answer acceptance remains unmet;
public model generation stays disabled. Keep enterprise identity in Phase 6.
The new reviewed-field route is bounded, uses all reviewed selected-corpus facts,
and is separately versioned reviewed-facts-corpus-v1 / reviewed-fact-answer-v1.2 /
bounded-english-fact-intents-v1.2. Do not rewrite the frozen historical 29/30 typed
trial or 17/30 paid result, silently backfill old reviews, or automatically spend.
No new paid run, commit, task, automation or subagent was requested.

## Preserved due-date repair checkpoint

Updated September 11, 2026 after Rand approved the due-date fix.
**The missing-field path is fixed and verified on fresh extraction/review.**
The local extractor previously recognized Due date only as a neighboring-field
boundary, not an output field. It now emits due_date as Date for explicit valid
YYYY-MM-DD labels in local-1.3.0. Conflicting or invalid/ambiguous dates suppress
the field with a degraded review warning. ReviewWorkspace includes the missing
due_date row. No date is inferred from Net 30.

The existing native synthetic invoice now extracts due date 2026-08-31 separately
from issue date 2026-08-01. New disposable-database tests confirm it, correct it to
2026-09-15 while preserving original evidence, and clear it with no answer fallback.
550 non-PostgreSQL backend, nine unique PostgreSQL and 329 unique Python checks
passed, including all five pinned OCR cases and the native due-date PDF case.
One initial PostgreSQL case timed out creating the disposable DB; all three
date scenarios passed on the completed rerun. Initial compile/environment logs
remain preserved. Native extraction repeats exactly; all 34 retained tables are
unchanged. Zero provider calls, reservations or new model cost.

See [due-date verification](evidence/phase-5/P5-due-date-verification.md).
Fresh fixture: samples/phase-5/due-date-v0.1.0/{manifest.json,extraction.json}.
Evidence: outputs/phase-5/due-date-20260911/{verification.json,SNAPSHOTS.md}.
Old local-1.2.0 goldens are preserved; tests now use the versioned local-1.3.0
subdirectory, with unchanged prior fixture field/page expectations.
Runtime split: .venv for application (316), tmp/phase2-fixture-runtime-20260910
for fixture generator (7), existing pinned Docker image for OCR (5), and
tmp/phase5-native-runtime-20260908 for new native due-date (1). Do not combine
the fixture generator's Pillow 9.4.0 requirement with the application's 12.3.0.

**Next:** broader varied-document/wording evaluation and public answer/citation/
history integration with authorization, replay and security acceptance. The
private English fact route remains bounded and public generation stays disabled.
Existing completed reviews are immutable: deliberately add/review a missing
date in a new revision or use fresh extraction/review. No silent backfill.
The historical 29/30 typed result and 17/30 paid source-v4 result stay frozen;
do not call this 30/30 or Phase 5 complete. No new paid run, commit, automation,
task or subagent was requested.

## Preserved typed-fact trial checkpoint

Updated September 11, 2026 after Rand approved trying backend typed facts.
The private candidate is implemented and tested: **29/30** unchanged development
references pass, **68** cited references verified, all **30** answers repeat
exactly, all **34** retained tables unchanged, **zero** provider calls/new cost.
The only unmet reference is q009: due_date is absent from completed review.
TRN/TIN are separate strings, amounts use decimal arithmetic and citations are
generated from exact reviewed facts. This is a bounded English route reading
all reviewed facts in the selected corpus, not an improvement score for the old
top-five/model pipeline or an independent holdout.

See [typed-fact verification](evidence/phase-5/P5-reviewed-fact-verification.md)
and [design](design/phase-5-reviewed-fact-answers.md).
Evidence: outputs/phase-5/typed-facts-v020-20260911/{evaluation.json,
repeat-verification.json,verification.json,SNAPSHOTS.md}. All 545 non-PostgreSQL
backend tests, six new real PostgreSQL cases, seven evaluator checks, and pilot
build passed. Shared full-suite TRX filename was overwritten by the test runner;
use backend-tests.log for per-assembly totals. No paid process is running.

Current code: CitedTypedFacts.cs, CitedFactQuestionPlan.cs, CitedFactAnswerEngine.cs,
CitedFactWorkspace.cs, CitedFactSourceStore.cs and the private SyntheticPilot
--typed-source-case branch. No public route, model call, spend admission or answer
write exists on this route. New source policy reviewed-fields-from-selected-corpus-v1;
answer reviewed-fact-answer-v1.1; language bounded-english-fact-intents-v1.1.
Earlier typed v0.1.0 (25/30) and v0.1.1 (28/30) outputs are preserved separately.
The first parser missed ordinary reading verbs; the intermediate breakdown gave
quantity a currency. Both are corrected and regression-tested.

**Next:** handle page-only/missing reviewed evidence deliberately (q009), then
evaluate varied documents/wording and integrate accepted behavior into public
answer/citation/history contracts with authorization, replay and security tests.
Do not fabricate a review decision, change the frozen due-date label, claim
Phase 5 complete, or automatically start another paid run. Public generation
remains disabled. User authorized the offline experiment; no new paid budget,
commit, task, automation or subagent was requested.

## Preserved source-v4 paid checkpoint

Updated September 11, 2026 after all 30 source-v4 question attempts completed.
The TLS certificate-chain problem was reproduced and fixed using system-trusted
certificates with verification enabled. All 25 subsequent requests returned.
Result: 17/30 end-to-end passes; 18 accepted proposals; nine rejections; three
HeldUnknown transport failures. q022 still selects TIN instead of TRN.
Known batch cost USD 0.012233 + USD 0.006 unknown reservations = USD 0.018233
accounted upper bound, within the original USD 0.06 approval. Shared settled
USD 0.027121 + HeldUnknown USD 0.008001. No pending reservations or budget freeze.
No paid process is running; all original outputs and holds remain immutable.
See [current recovered evaluation](evidence/phase-5/P5-source-recovered-evaluation.md).
Phase 5 remains open because answer-quality and public/security acceptance
requirements are not satisfied. This supersedes the historical checkpoints below.
Do not retry any saved case or automatically schedule another paid batch.

## User direction and project context

- Rand asked to proceed, compact context and consult “Start phase 3” if needed.
  That task was read: `019ffad1-6cd5-7f62-9029-281f98836bed`. It shares this repo.
- Phases 1–3 complete; Phase 4 approved September 7. Phase 5 foundation approved
  September 1. No request for a commit, new task, automation or subagent.
- Paid synthetic embeddings and the cheapest-first nano answer pilot are already
  authorized within the shared USD 8 admission ceiling (outer allowance USD 10).
  Do not silently change model/raise the budget. The user subsequently explicitly
  approved the synthetic 30-case v4 run for up to USD 0.06; automatic review accepted
  that launch. Preserve every saved attempt. The connection/guard recovery is now complete; the blocker is answer quality,
  followed by public integration and remaining security acceptance.
- Rand declined investigating tiny unknown charges and lacks the account password.
  Do not revisit OpenAI Usage/account access for the retained holds. No Arabic
  explanation/side-chat work; the old task explicitly cancelled that idea.
- Large existing dirty worktree contains Phases 3–5. Preserve unrelated changes.
  No root AGENTS; read apps/web/AGENTS.md and its local Next guide before web edits.
  No web code changed in this increment. No subagents authorized.

## Current result and immediate next work

- [Nonredundant retrieval](evidence/phase-5/P5-nonredundant-retrieval-verification.md)
  fixes q024: all 46 deduplicated labels are covered, full coverage for 24/24
  answerable cases and macro recall@5 100%. It suppresses only confirmed unchanged
  facts covered by a higher-ranked selected page's exact UTF-8 span. Corrections,
  review-only facts and other-source evidence remain distinct. Policy
  `pgvector-english-rrf60-c20-nonredundant-v1`, top 5 after a bounded 20 fused candidates.
  Original lexical/vector/hybrid results (95.83/95.83/98.61%) remain unchanged.
- [Prompt-v3/minimal evaluation](evidence/phase-5/P5-answer-followup-verification.md):
  19/30 end-to-end, 30/30 structure, 24/28 supported claims. Thirty paid calls,
  USD 0.004464 additional settled; all replayed exactly, 33 citation references
  navigated through the workspace handler, every table fingerprint unchanged.
- [Low-reasoning evaluation](evidence/phase-5/P5-low-reasoning-verification.md):
  23/30 end-to-end, 29/30 structure, 21/25 supported claims. Thirty completed
  calls, USD 0.008392 additional settled. All replayed, including q009's unchanged
  duplicate-citation rejection; 40 citation references navigated; no DB change.
- Low failures: q002 cites quotation number for a total; q009 duplicate citation;
  q011 adds an irrelevant subtotal citation; q013 lacks quotation support for an
  all-three claim; q024 omits invoice; q028 legal determination; q030 uses an
  authorized quotation total for a foreign invoice request. No foreign data was
  retrieved; the foreign mirror is absent, so no live isolation claim is justified.
- After freezing those results, `CitedScopeClaimPolicy` / `cited-scope-restriction-v1`
  now requires empty InsufficientEvidence/unsupported_scope for detected English
  legal/input-tax, foreign-organization and secret requests. Applied to new and
  replayed proposals. Actual q028/q030 are now rejected; q029's correct refusal
  still replays. All fingerprints unchanged. This rejects, never repairs outputs.
- [V4 source context](evidence/phase-5/P5-source-context-verification.md) is now
  implemented behind `CitedGenerationSettings.SourceAwarePilot` (default false).
  Source schema 1.1 exposes document/index/review/extraction identity, filename,
  chunk kind, review action and exact spans. `cited-answer-prompt-v4` requires
  per-citation `supportText`; validator `citation-excerpt-binding-v2` checks exact
  page/reviewed-content membership. Wrong excerpts/downgrades settle known cost
  then reject; replay never redispatches. Original machine quotes remain distinct
  from corrections. Membership is NOT entailment or comparison completeness.
- Next implementation: typed reviewed facts/evidence IDs, server-resolved source
  excerpts and arithmetic, distinct TRN/TIN fields, and pre-generation scope and
  missing-evidence decisions. Then versioned evaluation and public/security
  acceptance. Do not weaken the gate or repair historical outputs. See the current
  evaluation for the observed failures; no further paid run has been scheduled.
- Keep original failed 4/10 baseline and both new reports immutable. These are
  development-set evaluations by Codex manual source review, not independent
  human ratings, holdouts, statistical improvements or real-world accuracy claims.

## Database and accounting

- Retained DB: `phase5_e2e_20260908` at localhost:25434, PostgreSQL 16 + pgvector.
  Local `psql -w -U document_intelligence` works. Never reset this database.
- 15 migrations; 7 documents, 5 indexes, 78 chunks, 69 vectors, 201 retrieval runs,
  123 reservations, 246 artifacts, 371 spend events. No active pending dispatch or
  reconciliation freeze. Four historical provider-free answers; no published claims.
- USD 0.014888 settled + USD 0.002001 HeldUnknown = USD 0.016889 accounted against
  the unchanged USD 8 ceiling. Unknown costs remain null, not falsely settled zero.
- Original embedding hold: operation `ccaecf4e-d611-5042-aa64-84d054d42346`, reservation
  `8ca633af-24ed-400d-ae02-5791b68354ca`, USD 0.000001. User-accepted policy
  [retained unknown holds](design/phase-5-retained-unknown-pilot-policy.md).
- Low-run local transport failure: operation `3ae994cd-4973-4c28-9619-5e886cb51e29`,
  reservation `1c46a20c-025f-45fd-8268-1ce3af187283`, USD 0.002 retained. Old transport
  rebuilt low requests as minimal and rejected before HTTP. Fixed transport now
  uses declared effort; the failed code snapshot and all artifacts are preserved.
  [Separate narrow policy](design/phase-5-local-preflight-hold-v1.md) retains full
  cost under existing authorization without claiming verified zero billing.
  Combined caps: USD 0.002 generation + USD 0.001 embeddings; every amount counts
  against USD 8. Retained rows cannot be redispatched, cancelled, settled or edited.
- Test fixtures create/drop ONLY `document_intelligence_test`, never the retained
  DB. Set TEST_POSTGRES_CONNECTION to the same server, Database=postgres and local
  development credentials. Fixtures remove their disposable DB on completion.

## Frozen inputs and evidence

- NEW `samples/phase-5/answer-source-v0.4.0/plan.json` SHA256
  `56fda3a2d5317cea14700f00965b4a808dc855e9d1d6934d48b8190cce7c08be`:
  30 fresh runs reuse saved query embeddings; hit content/ranks/scores identical
  to low-fixed except IDs. Questions/references unchanged. v4/source schema1.1,
  same nano/low; max30 attempts/.06 reserved within8. Four attempts now saved;
  see the current partial-run checkpoint for their states and costs.
  Output `outputs/phase-5/answer-source-20260911`: retrievals + ledger-before.
  CLI `--source-case`, `--verify-source-case`, `--replay-source-case` and launcher
  `tools/phase5_run_answer_followup.ps1 -SourceContext -ExecutePaid` are wired.
  Reporter `tools/phase5_answer_followup_report.py --source` is tested; still needs
  actual generation outputs, final ledger, digest-bound manual assessments.
- Historical pre-run authorization record (superseded by explicit approval):
  automatic review blocked the command before process creation, then rejected
  reconsideration with prior real-model authorization retrieved from Start phase3.
  The later explicit user approval was accepted and the paid run began.
  Do not route around it. Evidence/turn IDs and both outcomes saved at
  `outputs/phase-5/source-context-20260911/authorization-review.md`.
  Fixed outbound destination api.openai.com count/Responses; only the same three
  generated synthetic documents. Current output now includes four generation
  files, partial-run.json, stopped diagnostics, test logs and SNAPSHOTS.md.

- Dataset: `samples/phase-5/cited-questions-v0.1.0/question-set.json`, 30 questions.
  Reference overlay `evidence-conditioned-refusals-v1`: q025–027 incomplete_evidence,
  q028–030 unsupported_scope with nonempty evidence. Original labels unchanged.
- Retrieval plan `samples/phase-5/retrieval-evaluation-v0.1.0/plan.json` SHA256
  `d4176993fac3bc00d0bdbaf8f1ee9e8bb4f7cda6c7b7ff7d50c3366e723e8406`.
- Minimal plan `samples/phase-5/answer-followup-v0.2.0/plan.json` SHA256
  `f342359d97a8cb176013d4aed01a1833116b0a1fe510a294162586b494b8b752`.
- Original low plan `samples/phase-5/answer-low-v0.3.0/plan.json` SHA256
  `ffca5a39729d0f5a57f72212afa54618824322e0e000bbed9183ff5d41ab7321`.
  Only its q001 dispatched, failing locally. Preserve it.
- Completed low continuation `samples/phase-5/answer-low-fixed-v0.3.1/plan.json` SHA256
  `9695a66bdc7fc3394525d10731537f8d33c326f9a1cc3b1b00d9479cf9a44632`.
  New q001 run, other 29 original low run IDs; actual evidence/ranks match baseline.
- Outputs: `outputs/phase-5/nonredundant-retrieval-20260910/`,
  `answer-followup-20260910/`, `answer-low-20260910/`, `answer-low-fixed-20260910/`.
  Last directory includes scope-policy replay and before/after fingerprints.
- EmbeddingPilot private CLI handles evaluation retrieval/replay; SyntheticPilot
  handles frozen answer cases. `--replay-followup-case` and `--replay-low-fixed-case`
  use OfflineAnswerClient and navigate accepted citations. Historical scope failures
  now reject under the new policy; do not rewrite original replay/result files.
- `tools/phase5_answer_followup_report.py --low-fixed` requires frozen manifest,
  settled ledger, actual output digests and explicit manual assessments. Exclusive
  output creation means it refuses to overwrite a finished report.
- OpenAI snapshot remains `gpt-5-nano-2025-08-07`. Current rate version valid only
  September 10–11 UTC: input/cached/output USD 0.05/0.005/0.40 per million.
  Reverify official rates before later new calls; do not extend automatically.
  Private worker profiles nano-v1/minimal and nano-low-v1/low, no public selector.
  Key stays in .env/private Python worker only; never print it. No retries/fallback.

## Verification and preserved history

- Latest v4: 565 backend passed (510 non-PG +53 targeted PG +2 low/source PG),
  175 cited Python +6 report checks passed. One new mock-stream fixture error in
  first Python run was corrected; final suite passes. Exact .NET/Python roundtrip,
  accounting-before-rejection, schema downgrade, correction and replay covered.
  Output `outputs/phase-5/source-context-20260911`; no model-quality score added.
- Offline audit `tools/phase5_source_context_audit.py` rebuilt all61 historicalv3
  prompt/count/generation hashes exactly and created30 v4 packet snapshots,
  max17,822 generation bytes. Reused historical IDs are preview-only, NOT the
  executable plan. `packets/audit.json` records hashes and limitations.
- Before/after offline retained fingerprint SHA stayed
  `764a7c90a54a982459a2514cb08243d8a0e3aad60d77a27241fcf801462faef4`.
  Subsequent prepared evaluation adds30 retrieval runs/sources/hits only;
  spending, old answer history, documents/chunks/vectors remain unchanged.

- Latest user asked to test the complete implemented flow, earlier-phase impact,
  integrations and save important snapshots. [Full regression evidence](evidence/phase-5/P5-full-regression-20260910.md)
  and `outputs/phase-5/full-regression-20260910/regression-report.json` record:
  703 backend checks (501 non-PG + all 202 PG), 304 unique Python checks across
  application/fixture/pinned OCR environments, and all 20 frontend checks passed.
  All five pinned native/scan/image/degraded/corrupt extraction cases ran in the
  existing Docker image with network disabled and repository source read-only.
  Backend Release, frontend production/lint, model/migration and docs checks pass.
- Real API Phase 4: three reviews, nine exact findings, retained resolution.
  Phase 5: all 20 full-stack assertions; browser independently verified search,
  exact source bytes, review provenance, refusal, saved history and quarantine.
  Seven screenshots and annotated `SNAPSHOTS.md` are in that output directory.
  This accepts implemented evidence workflows, not generated-answer/model quality.
- Harness issue: earlier full PG attempts stalled; one failed process stop briefly
  caused overlapping disposable runners. Those attempts were stopped/preserved.
  Diagnostic run hung at before-settlement crash probe; native cause unproven.
  Test-only crash probes now kill the exact child (no descendants) instead of
  enumerating a process tree. All 11 crash cases passed, then all 191 other PG
  cases passed. No production behavior/schema change; no test assertions weakened.
- Setup dependencies: `.venv` gained declared reportlab 3.6.12. Fixture tests use
  `tmp/phase2-fixture-runtime-20260910` with Pillow 9.4.0; app stays Pillow 12.3.0.
  That runtime and `tmp/dotnet-diagnostics` (dotnet-dump 8.x) are Git-ignored.
- Fresh `retained-database.dump` restored into `phase5_restore_20260910`; all-table
  fingerprints match before/after/final. All seven uploaded documents match their
  archive bytes. SHA-256 manifest plus separate regression DB/upload archives saved.
  Original DB/ledger counts above are unchanged; no paid call ran this increment.
- New test-only DB `phase5_regression_20260910` and restored copy remain; disposable
  `document_intelligence_test` is absent. API 8086, Python 8007, Next 3004 and Docker
  test runtime are stopped. Native PostgreSQL remains running. The in-app test tab
  is temporary; snapshot gallery was queued in the Codex file panel.
- Prior longer checkpoint archived at
  `outputs/phase-5/answer-low-fixed-20260910/continuation-before-final.md`.
  Earlier evidence pages preserve all historical results, authorizations and hashes.
