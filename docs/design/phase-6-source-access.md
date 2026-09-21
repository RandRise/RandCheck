# Authorized original-file access

Implemented September 12, 2026. Enterprise readers can download the original
source from the document workspace. This extends the
[storage admission contract](phase-6-storage-safety.md) without a schema migration
or changes to retained Phase 5 data. Development identity cannot use these routes.

## Access protocol

| Operation | Contract |
| --- | --- |
| `POST /api/documents/{documentId}/source-access` | Current Viewer, Reviewer or Operator access; returns a relative source URL, opaque access token and expiry five minutes after issue |
| `GET /api/documents/{documentId}/source` | Requires normal API authentication and exactly one `X-Document-Access` header; verifies the grant, current membership and the file before returning bytes |

The URL is a stable resource location. Its temporary authority is carried in a
separate header and expires after five minutes. It cannot authorize anonymous
access or act as a transferable bearer URL. The browser obtains a fresh grant on
each click and uses the existing authenticated transport for both requests.
Credentials stay out of URL queries/fragments, navigation history, local storage,
session storage, audit fields and UI errors. Query parameters are not credentials.

ASP.NET Data Protection encrypts and authenticates each grant under a dedicated
versioned purpose. The payload binds organization, actor, membership version,
document, stored SHA-256, size, issue time and expiry. This uses the standard
[Data Protection consumer API](https://github.com/dotnet/AspNetCore.Docs/blob/main/aspnetcore/security/data-protection/consumer-apis/overview.md).
Invalid/tampered grants, expiry, changed identity or changed content binding fail
before file access. A membership change invalidates existing grants, including
revocation followed by reactivation. Grants may be reused by the same unchanged
member within their validity window; single-use consumption is not implemented.

Every request revalidates the JWT and resolves current database membership.
Both routes scope document reads by the resolved organization. A same-organization
peer needs their own grant; another organization's document remains unavailable.
Missing/foreign documents and invalid grants return 404. Existing authentication
and role denials remain 401/403. Administrative membership permission alone does
not grant source-file access.

## File and response boundary

Source access requires Enforced storage and, outside Development/Testing, HTTPS.
Production HTTP and Locked storage return 503 without issuing credentials or
reading source bytes. A real deployed TLS/proxy configuration remains to be verified.

The reader shares the worker's organization/document path validation and reparse
checks. It bounds sources to 10 MiB, checks exact file length and SHA-256, then
scans a read-only memory snapshot. The response uses those same bytes without
reopening a filesystem path. Missing files, identity/path substitutions, size/hash
changes and scan failure prevent release. Existing source files remain intact.
Real ClamAV deployment, signature freshness and private filesystem controls remain
the explicit open items described in the storage contract.

After scanning, the API rechecks grant expiry and membership version. Revocation
or expiry during scanning prevents release. The final check defines admission;
revocation cannot retract bytes after transmission starts.

Responses are attachment-only `application/octet-stream`, with a generated document
filename and an allowlisted extension. Arbitrary original filenames and executable
extensions are not returned in Content-Disposition. `no-store`, `nosniff` and
`no-referrer` apply. Range processing and conditional/ETag shortcuts are disabled.
The configured browser origins may read Content-Disposition. The browser validates
the returned grant URL against the selected document, creates a temporary blob URL,
initiates the attachment download and revokes that URL. Changing the selection or
unmounting the session aborts pending work. Errors display fixed safe messages.

## Audit semantics

| Action | Meaning |
| --- | --- |
| `document.source-access-issued` | A five-minute grant was issued to the audited Human actor for the document |
| `document.source-download-authorized` | The source passed integrity/scanning and final access checks; authorization was durably recorded before releasing bytes |
| `document.source-access-denied` | A permitted request was rejected by grant, membership, file or scanner checks; reason is a fixed safe code |

These use the existing append-only audit sink. Issuance and download authorization
fail closed when audit insertion fails. Failed issuance also passes through the
existing independent request-failure filter. Authentication/role denials use the
existing access-denied audit. No original content, filenames, paths or credentials
are recorded. The successful download event records hash and bytes, and denotes
authorization, not proof that a browser saved the entire attachment. Disconnected
or cancelled in-flight work does not imply a completed download audit record.

## Deployment and remaining work

[Explicit source-key protection](phase-6-source-key-protection.md) now provides
certificate-encrypted shared keys, application isolation and rotation. Enterprise
source access requires that configuration. Losing keys invalidates outstanding grants;
operator ACLs, actual shared mounts, certificate delivery, deployed proxy configuration
and live Entra still require verification.

Later increments added request/capacity limits, retention/holds, deletion/recovery and
restricted runtime grants. Their deployment limitations remain documented in the
[Phase 6 ledger](../tasks/phase-6.md).
