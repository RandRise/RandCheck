# Retention controls verification

September 12, 2026. Implements the [retention contract](../../design/phase-6-retention-controls.md).
This is development verification of policy previews, holds and abandoned-upload
cleanup. It is not acceptance of stored-document deletion or all of Phase 6.

## Results

434 unique checks pass, with no failures or skips in their final results:

| Suite | Passed | Evidence |
| --- | ---: | --- |
| API | 169 | `api-final.trx` |
| Infrastructure | 172 | `infra.trx` |
| PostgreSQL | 35 | `retention-final-pg.trx` |
| Web | 58 | `web-tests.log`: 55 Vitest and 3 Node tests |

Files are under `outputs/phase-6/retention-20260912`. Earlier API and 19-case
PostgreSQL runs are retained but are not added again. The final PostgreSQL run
covers 24 retention/lifecycle cases, seven storage admission/worker cases, three
source download cases and the restricted runtime grant script.

The production Next.js build passes, including `/organization/retention`. Tests
exercise safe defaults, version conflicts, hold release confirmation, cleanup
preview/confirmation, data-only member denial and safe transport-error display.
No real Entra sign-in or ClamAV daemon was used in the browser fixture.

## Security and lifecycle evidence

- Signed-token PostgreSQL HTTP requests prove default retention, configured expiry
  preview, hold precedence, release history, stale-version rejection and safe audit.
- Foreign document/hold IDs return 404; the other organization's lists and previews
  remain scoped. All seven routes deny a non-administrator before body binding.
- An organization hold blocks cleanup; persisted documents, other organizations,
  legacy filenames, young uploads, missing leases and active leases are preserved.
- A live uploader's exclusive lease is tested while scanning is blocked. Promotion
  succeeds after release and normal completion removes both staging and lease.
- Preview does not remove files. Explicit cleanup produces durable authorization
  and completion; a second request does not delete or audit the same source again.
- Injected audit failures roll back policy/hold mutations and block pre-delete
  authorization. A post-delete audit outage yields 503, preserves the durable
  authorization, and does not falsely claim completion or restore deleted bytes.
- Concurrent policy updates yield exactly one winner. A PostgreSQL lock test
  observes cleanup waiting, commits an organization hold, and verifies cleanup
  rechecks the hold and preserves the staged file.
- Production HTTP is rejected; HTTPS is admitted with the signed test identity.
- Database triggers reject hold rewrites/reopening, deletes and truncates. Populated
  downgrade is refused; empty downgrade/reapply passes. The runtime-grant script
  runs successfully with the new tables on a temporary restricted role.

Migration `20260912173517_AddRetentionControls` was exercised only on disposable
`document_intelligence_test`. All 34 retained-table fingerprints match before and
after; no retained migration or data write occurred. The disposable database and
temporary runtime-test role are removed by the fixtures.

## Browser snapshots

Four full-page screenshots show the real React page using clearly labeled fictional
identity and in-memory API records on loopback port 3019:

1. `01-retention-controls.png`: policy, document hold and three retention dispositions.
2. `02-organization-hold.png`: all documents held, zero eligible uploads, five preserved.
3. `03-cleanup-result.png`: explicit hold release and simulated removal of three
   abandoned uploads while preserving two; the original document hold remains.
4. `04-retention-audit.png`: policy/hold and cleanup authorization/completion entries.

No real file is deleted by this browser fixture. Actual filesystem deletion and
audit behavior are covered separately by the PostgreSQL tests. The snapshot event
details are illustrative, not an export of actual backend audit records. Browser
console warning/error inspection was empty. The visual check used the existing
desktop viewport; responsive CSS was included but no mobile screenshot was taken.

## Verification notes and limits

The initial infrastructure run found an outdated test assumption of one quarantine
file. The lifecycle now uses an upload and a lease, so the test checks each type;
rejected-scan assertions were also tightened to the exact expected exception.
`infra-initial-stage-count.trx` preserves that failed run. All 172 then passed.

Two simultaneous .NET builds initially collided on a shared intermediate DLL;
they were subsequently built sequentially. The web sandbox could not resolve the
Windows user path (`EPERM`); the authorized local test/build ran outside that sandbox.
The initial EF build attempted an unavailable NuGet advisory lookup; restore/build
with `NuGetAudit=false` succeeded using installed packages. This is not a fresh
dependency vulnerability audit. No implementation assertion was weakened.

The contract documents the filesystem/database completion gap, bounded cleanup
batch without a durable cursor, cooperative local leases, retained unknown legacy
staging, and empty markers that can remain after interruption. Stored-document
deletion, backup expiry/restore suppression, deployment ACLs/encryption, live Entra,
real ClamAV, broader isolation and rate limits remain open.

The VitePress documentation build and whitespace check also pass. The documentation
build reports its existing advisory about bundle chunks larger than 500 kB.
