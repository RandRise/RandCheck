# Offline recovery verification

September 14, 2026. The [offline recovery contract](../../design/phase-6-orphan-recovery.md)
documents reviewed cleanup of unregistered promoted source files and schema 2 restore
snapshots that include capacity policies.

442 distinct checks pass: 200 API, 189 Infrastructure and 53 PostgreSQL. The database
total includes 16 new recovery cases. The initial 16-case run also passed; the final
PostgreSQL run supersedes it and is counted once. No tests were skipped. Documentation
and whitespace checks pass. Stable TRX test IDs distinguish two Infrastructure cases
with identical truncated display names.

## Artifacts

Raw evidence is in `outputs/phase-6/orphan-recovery-20260914`.

| Artifact | Scope |
| --- | --- |
| `recovery-initial.trx` | Initial recovery checks, including capacity snapshot state and removal-audit failure boundaries |
| `persistence-final.trx` | Final private CLI, bounded inventory, journal refusal, new recovery checks and existing deletion/quarantine/capacity regressions |
| `api.trx` | Complete API regression suite |
| `infrastructure.trx` | Complete Infrastructure regression suite |
| `verification.json`, `unique-tests.txt` | Final outcomes deduplicated by stable TRX test ID |
| `retained-before.txt`, `retained-after.txt` | Read-only fingerprints of 34 retained tables |
| `docs-build.txt` | Documentation build output |

PostgreSQL runners execute serially against disposable `document_intelligence_test`
on loopback port 25434. Files removed in tests are fictional fixtures under isolated
temporary roots. Private command processes are hidden. No live cleanup, deployment,
new schema migration, browser UI change, commit or paid provider call was performed.
NuGet auditing was disabled for offline test commands; this is not a dependency audit.

## Boundaries exercised

- Inventory preserves registered, young, exclusively held and unknown-organization
  sources and unrecognized files. Organization holds preserve otherwise eligible files.
  It refuses to proceed without explicit offline acknowledgment or with unresolved
  deletion intents, and reports partial coverage at the 100-source batch limit.
- Execution removes only files in the reviewed plan. A file created afterward remains.
  It rechecks new registration and hold state, and refuses changed source content even
  when its timestamp is restored. Misdirected roots/databases, escaping keys, expired
  plans and absent offline acknowledgment leave sources untouched.
- Independent System audit events bracket removal. Authorization-audit failure leaves
  the source intact; completion-audit failure leaves an unresolved authorization.
  Replay reports an absent file without manufacturing a completion event. Audit fields
  contain IDs, bytes and hashes, with no source body or absolute path.
- The actual private CLI exports controls, requires offline acknowledgment, rejects
  plan files under source storage, creates reviewable inventory without removing files,
  refuses overwrite, executes the saved plan, and refuses a wrong database or changed
  capacity snapshot. Console output excludes fixture paths and document content.
- Schema 2 snapshots detect newly present capacity policies, changed limits with an
  unchanged version and changed versions with restored limits. Legacy schema 1 is
  refused. Current snapshots verify alongside retained hold and deletion checks.

## Operational limits

Offline acknowledgment is an operator precondition, not a process-detection mechanism.
All writers must remain stopped through inventory and execution. Database locks do not
fence promoted uploads awaiting admission. Local filesystem checks cannot protect the
close-to-unlink gap against a privileged writer; private ACLs and deployment validation
remain required. Reparse checks are implemented, but this increment does not establish
cross-platform or network-filesystem safety.

No automatic repair or audit-completion inference is supplied after a partial failure.
Unknown layouts and files above the source-size bound require investigation. Discovery
is bounded to 1000 entries and 100 sources without a durable cursor. A current control
snapshot must be independently retained; rolling back all copies together can evade
the comparison. Deployment readiness is next; Phase 6 acceptance remains open.
