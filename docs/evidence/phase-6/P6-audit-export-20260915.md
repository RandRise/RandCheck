# Private audit export verification snapshot

September 15, 2026. [Export contract and retention runbook](../../design/phase-6-audit-export-retention.md).

## Verified result

**225 checks pass: 219 infrastructure and six PostgreSQL.** The infrastructure suite
includes 30 archive-specific cases. The full backend solution builds with zero
warnings and errors. No new package version, schema, HTTP route or browser component
was introduced.

The private CLI exports one organization/time window through a read-only PostgreSQL
snapshot, then publishes a verified packet and a receipt. The offline command checks
restored copies against the independently retained manifest hash. Audit records
remain append-only; the local retention policy supplies no purge or expiry command.

| Scenario | Verified behavior |
| --- | --- |
| Dedicated read-only database login | Exports selected organization; no document read or audit write grants required |
| Concurrent late commit with an older event timestamp | Excluded from the existing snapshot; included in a subsequent export |
| Timestamp ties and window edges | Stable UUID ordering, inclusive start and exclusive end |
| Row limit exceeded | Fails without publishing truncated output |
| Missing SELECT or RLS filtering | Fails without silently publishing an incomplete archive |
| Changed content, metadata, IDs, ordering or file set | Offline verification refuses invalid packets |
| Interrupted write | No published partial packet; unrelated files remain intact |
| Copy restored to a second private directory | Verifies offline with the original receipt |
| Audit UPDATE/DELETE/TRUNCATE and populated downgrade | Existing protections still refuse destructive changes |

## Captured packet

Artifacts are in `outputs/phase-6/audit-export-20260915`:

- `synthetic-snapshot/receipt.json` identifies a real exported packet containing two
  audit events from a fictional organization: organization creation and its initial
  membership grant. Another fixture organization is excluded.
- The corresponding `synthetic-snapshot/audit-<export-id>/` contains the actual
  `events.ndjson` and `manifest.json` produced by the CLI.
- `offline-verification-receipt.json` records successful verification with the final
  rebuilt CLI. No database configuration is needed for this operation.
- `verification.json` and TRX files record the executed checks. The final 219-case
  infrastructure run supersedes the earlier 218-case run after adding a path/input
  boundary case and bounding directory enumeration.

This is an archive/command snapshot, not a browser screenshot. Before/after source
fingerprints match for all 34 retained tables. PostgreSQL tests use only the disposable
test database; export itself does not mutate its source audit table.

## Limits and next work

The trusted receipt is required: SHA-256 is not a signature or proof against an
attacker replacing both the packet and the receipt. This is a snapshot, not a safe
incremental timestamp checkpoint. Archives require private storage and independent
receipt retention; the tool does not install ACLs or encryption.

Private database-operator exports use database authority and receipts, not application
actor events. Global/unresolved events and a centrally recorded trail of private
operator reads are outside this export scope. Final audit coverage acceptance remains
open, followed by the supported local recovery and overall Phase 6 acceptance work.
Production hosting and automation remain optional for the GitHub portfolio target.
