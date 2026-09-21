# Private audit export and retention

September 15, 2026. `EnterpriseAudit` captures a bounded security-audit snapshot for
one organization and verifies archived copies offline. It is an operator tool for
the local GitHub portfolio; it needs no domain, cloud account or external provider.

## Authority and source protection

Export authenticates using a privately supplied PostgreSQL connection. The existing
database role must be authorized to read the organization's audit history. This is
database-operator authority, not a browser membership grant: no HTTP endpoint or
global audit viewer is added. The manifest records the actual database and
`current_user`; callers cannot provide an application actor to impersonate.

A dedicated read-only login is sufficient. An operator can grant an existing role:

```sql
GRANT USAGE ON SCHEMA public TO audit_reader;
GRANT SELECT (id) ON public.organizations TO audit_reader;
GRANT SELECT ON public.security_audit_events TO audit_reader;
```

The tool does not create credentials, repair permissions, migrate, seed, start
services or register providers. Every export uses a read-only Repeatable Read
transaction, including when an operator supplies a more privileged connection.
Database-name confirmation is mandatory, retained `phase5_` databases are refused,
and parameter/error-detail logging settings are rejected.

Organization selection limits this packet; it is not a new database isolation
mechanism. A role with table-wide SELECT can read other organizations outside this
tool. The snapshot refuses row-security filtering rather than silently treating
filtered results as complete; it does not grant an RLS bypass.
Events with no resolved organization, including anonymous global denials, are outside
this organization-scoped export. There is no wildcard or global export mode.

The export performs no audit-table insert. Its independently retained receipt and
manifest are the operator's export evidence, not an application actor event. Private
database reads outside the tool and failed export attempts are not a complete,
centrally persisted attempt trail. Those limits remain in the final coverage review.

## Commands

From the repository root, select the project runtime and build:

```powershell
. ./tools/use-project-runtimes.ps1
dotnet build apps/backend/tools/EnterpriseAudit/EnterpriseAudit.csproj
```

Supply `PHASE6_AUDIT_CONNECTION` through a private environment. Keep the plan and
archive directories outside web-served storage and public repository contents. The
output directory must already exist and be controlled by the operator; the tool
does not configure Windows ACLs, encryption or shared-folder permissions.

A private plan uses exact camel-case fields and explicit UTC bounds, for example:

```json
{
  "expectedDatabase": "document_intelligence_demo",
  "organizationId": "11111111-1111-1111-1111-111111111111",
  "fromUtc": "2026-09-01T00:00:00Z",
  "beforeUtc": "2026-09-15T00:00:00Z",
  "outputDirectory": "C:\\Private\\AuditArchives",
  "maxRows": 10000
}
```

Replace the example database, organization, window and directory with the reviewed
local target. Bounds are inclusive `fromUtc`, exclusive `beforeUtc`, with at most
microsecond precision. Future intervals and non-UTC offsets are rejected.

```powershell
dotnet apps/backend/tools/EnterpriseAudit/bin/Debug/net8.0/EnterpriseAudit.dll export C:\Private\audit-plan.json
dotnet apps/backend/tools/EnterpriseAudit/bin/Debug/net8.0/EnterpriseAudit.dll verify C:\Private\AuditArchives\audit-EXPORT_ID TRUSTED_MANIFEST_SHA256
```

Export returns a JSON receipt with the export ID, event count, byte count, data hash
and manifest hash. Save that receipt independently before accepting the archive.
`verify` requires the original trusted manifest hash and does not read a connection
environment variable or contact a database. Exit codes are 0 for success, 1 for a
failed operation and 2 for invalid command usage. Failures print a fixed message,
not connection strings, file contents, identity payloads or exception details.

## Packet and consistency contract

Each `audit-<export-id>` directory contains exactly two files:

- `events.ndjson`: the explicit audit-field projection in database timestamp/UUID
  order, UTF-8 with one JSON record per LF-terminated line.
- `manifest.json`: schema version 1, export ID, database role, organization/window,
  snapshot capture time, requested row limit, count, byte size and SHA-256 data hash.

The projection retains audit IDs, actor kind/IDs, action, target, outcome, reason,
request correlation, timestamp and existing before/after audit states. It joins no
document, membership, actor-name or identity tables and adds no document text, token,
source grant or connection secret. Archives still contain sensitive access metadata.

The default limit is 10,000 rows; callers may select 1–50,000. Packets are capped at
64 MiB of data, records at 8 KiB, and plans/manifests at 16 KiB. The command has a
two-minute cancellation budget and 30-second database command timeout. Row, byte,
parse, permission or cancellation failure does not publish a shortened archive.
Choose a smaller reviewed window if the bounds are exceeded.

Output uses new files in a private `.partial` directory. The writer flushes both
files, verifies hashes and record invariants, then renames the directory without
overwriting an existing archive. Linked paths are refused. This assumes a trusted
filesystem directory without concurrent hostile replacement; it is not protection
against a compromised OS or a guarantee against filesystem/power-loss failure.

Verification checks the trusted manifest hash, schema and required fields, bounded
file contents, data checksum, counts, unique event IDs, ordering, organization and
time bounds. Unknown/duplicate JSON fields, missing records, extra files, truncated
lines and unpublished directory names are rejected. The checksum detects changes
relative to the independently trusted receipt. It does not provide a digital
signature, prove database authenticity or detect an attacker replacing both the
archive and the trusted receipt.

The export is complete only for rows visible in that database snapshot and selected
window. An event inserted earlier but committed after snapshot capture is absent,
even when its recorded timestamp falls inside the window. There is deliberately no
incremental cursor or claim that the last timestamp is a safe checkpoint. Re-export
reviewed overlapping windows and compare event IDs, or coordinate a quiescent capture,
when completeness across multiple captures matters.

## Local retention and restore runbook

The supported project policy is indefinite retention of source audit records and
accepted archive/receipt pairs. Document expiry, legal-hold release and document
deletion do not purge security audit history. No export option authorizes source
deletion, and no audit purge, archive-expiry scheduler or trigger bypass is supplied.
This is a project policy, not a legal retention or compliance claim.

1. Review the organization/window and use a private output directory. Capture the
   receipt independently, then verify the resulting packet using its manifest hash.
2. Keep the original packet and trusted receipt. Back up the folder under its export
   ID and retain the receipt through a separate trusted channel/location. Apply the
   same access restrictions to all copies; copying does not make a public-safe demo.
3. Restore the two files into a directory with the same `audit-<export-id>` name.
   Run offline verification against the independently retained hash before accepting
   the restored copy. No database import or audit-history replacement is performed.
4. Treat `.partial` output, missing receipts and failed verification as unconfirmed.
   Keep such material private for review and re-export if needed. A process may stop
   after publication but before returning its receipt; file existence alone is not
   a successful export acknowledgement.
5. Monitor storage and define a separately reviewed retention change before enabling
   any finite expiry or destruction. Do not disable audit protections to reclaim space.

The repository ignores the conventional `audit-archives/` directory, but operators
remain responsible for other chosen destinations. Only synthetic verification
packets may be considered for portfolio publication after review. Off-host immutable
storage, scheduled verification and production expiry automation are optional future
operations; the local verification and restore workflow is implemented now.
