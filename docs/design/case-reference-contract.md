# Case references and categories

R4-T04 implements the approved case-form direction without imposing a title pattern.

- The UUID remains the internal identity and existing case/document/run links remain
  valid. `reference` is server-owned, for example `REC-2026-000123`.
- Numbering is scoped to organization and UTC creation year. Numbers use at least
  six digits, expanding when needed. Gaps are acceptable; references are never
  renumbered when titles or categories change.
- `name` remains the required, trimmed human-readable title (up to 200 characters),
  including Arabic or mixed-language text. No supplier or date must be encoded in it.
- `category` defaults to `Uncategorized`. Other canonical values are `OfficeSupplies`,
  `Services`, and `Equipment`, displayed as Office supplies, Services and Equipment.
  Category is separate from Draft/Evaluated/Closed workflow status.

## API and permissions

POST `/api/reconciliation-cases` accepts `{name, category?, requestKey?}`. The server
supplies the reference. R4-T05a adds [durable creation retry identity](action-repeat-contract.md);
the UI always supplies a key. Detail and list responses include `reference` and `category`.

PUT `/api/reconciliation-cases/{id}` accepts `{name, category}` with a strong
`If-Match` ETag. Reviewers/operators can edit mutable cases. Viewers can read and
filter them. Administration alone does not grant document access. Closed cases are
read only. Metadata changes leave UUID, reference, pinned reviews, comparisons and
decisions intact; stale writes fail, and exact no-ops retain their version.

GET `/api/reconciliation-cases` accepts bounded `offset` plus optional `q` (reference
or title substring), `category`, `status`, `createdFrom`, and `createdTo`. Date inputs
are inclusive UTC dates in YYYY-MM-DD format. All filters execute on the server
inside the organization scope before paging. The UI applies filters explicitly and
resets paging when they change.

Metadata audit events identify the case and record category/status and whether the
title changed. They do not copy the human-entered title into the audit trail.

## Existing data and concurrency

The schema migration backfills references by organization and UTC creation year,
ordered by creation time then UUID. Existing titles and relationships are retained;
existing categories start Uncategorized. A PostgreSQL counter allocated in the
case-insert transaction provides concurrent reference assignment; a unique index
enforces organization/reference uniqueness. Database enforcement keeps references
immutable. Migration verification must use a disposable database, not the retained
application database. Production rollout still requires applying the normal reviewed
migration before deploying code that reads these fields.

Existing restricted API database accounts also need the normal private EnterpriseAdmin
API-role configuration step after migration to grant updates to `name` and `category`.
The runtime rejects outdated grants. The reference counter remains private; the
application account receives neither counter-table access nor direct allocator execution.
