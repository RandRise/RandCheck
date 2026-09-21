# Phase 6 browser identity and organization access

Implemented September 11, 2026. This increment adds the browser to the existing
[API identity contract](phase-6-identity-contract.md) and
[membership/audit contract](phase-6-membership-audit-contract.md). Live Entra
registration and end-to-end tenant verification remain open.

## Identity and request boundary

The root layout passes public configuration to a client provider. Production
defaults to `Locked`; `Development` is accepted only by the Next development
server. Local development keeps the earlier document workflows and cannot enter
enterprise administration. Configure the values in
`apps/web/.env.enterprise.example` **before building**. Public environment values
are bundled into the browser; never put a client secret there.

MSAL Browser 5.21.0 performs authorization-code/PKCE sign-in and silent acquisition
against the configured Entra tenant. MSAL owns its session-storage cache. The app
does not implement a token cache, put tokens in URLs, or log authentication errors
containing provider details. Redirect and logout destinations are fixed to the
current web origin. The `/auth/callback` page runs the bundled MSAL v5 redirect
bridge, excluded from the authentication provider, with no-store and no-referrer
headers. Do not apply COOP headers to that bridge page at a reverse proxy.
The main provider initializes once under React strict mode.

All document, review, reconciliation, question, source, membership and audit calls
use the shared transport. It obtains an access token immediately before the call,
accepts only the configured HTTPS API origin (loopback HTTP for development),
refuses non-API paths and credential-bearing URLs, omits cookies, prohibits
redirect following, and uses no-store. It preserves request bodies, request keys,
ETags and If-Match. Failed mutations are never automatically replayed. Review
history now downloads through an authenticated request instead of an anonymous
browser link.

`GET /api/session` is the 32nd classified API route. It returns organization and
actor names/IDs, tenant ID, database data role and the independent administrator
flag. It admits active members even with role None, allowing the first private
administrator to manage membership without document access. Token roles cannot
grant permissions. Revoked or unprovisioned identities are denied. This is a read
endpoint and requires no migration or retained data change.

The provider mounts workspace pages only after membership resolves. It checks
again on window focus and every 60 seconds while visible; API authorization still
checks membership on every request. HTTP 401/403 clears the rendered workspace
and aborts outstanding authenticated requests. Role or organization changes reset
page state. Logout immediately clears the current tab's workspace and aborts its
requests before redirecting to Microsoft logout. This is browser logout, not
server-side token revocation or revocation of sessions on every device.

## Organization screens

`/organization` lists up to 500 memberships, with document role, independent
administration and active/revoked status. Granting access takes a display name
and Entra object ID in the configured tenant. It does not send an invitation or
create an Entra account. Edits send the selected membership version. A stale
version requires reload; the UI never blindly retries it. Revocation presents
the affected person and effect before submission. The existing API protects the
last administrator. Revoked rows and their previous work remain retained.

`/organization/audit` is administrator-only. It shows the newest 100 events,
bounded older/newer paging, and expandable actor, target, request, outcome,
reason and before/after fields. React renders those values as text. Paging uses
the existing offset API, so new concurrent events can move page boundaries;
Refresh returns to the latest page. There are no audit edit/delete controls.
This is the supported security/workflow event trail, not a complete HTTP access
log or a record of every sign-in at Microsoft.

Viewer document controls cannot upload, reprocess or review. The reconciliation
editor requires Reviewer/Operator. The source-preparation endpoints still require
Reviewer/Operator; Viewers can reopen authorized saved results in the browser.
Preparing a source as a Viewer is not silently elevated. Operator/local-development
flows retain the Phase 5 behavior and spending restrictions.

## Deployment verification still required

Register a single-tenant SPA and API, expose the delegated
`DocumentIntelligence.Access` scope, and register the exact web
`/auth/callback` URI. The API's tenant, audience and allowed SPA client IDs must
match. Set `Cors:Origins` to the exact web origin. Bootstrap a **new** organization
through the private CLI and migrated disposable/staging database; do not remap
the retained Phase 5 System actor. Verify actual sign-in, consent, redirect,
silent renewal, logout and conditional-access behavior with the real tenant.

The browser screenshots use an explicitly labeled, fictional in-memory HTTP
fixture and injected test identity adapter under `tools/phase6-browser-fixture`.
That harness is outside the Next application and is not a production auth mode.
Its UI verification complements signed-token PostgreSQL API tests; it does not
prove a live Entra/browser/database integration.

Next.js and its ESLint configuration were patched from 16.3.0 to 16.3.5 after the
audit identified critical Next advisories. Compatible js-yaml/nanoid updates were
also applied. Production npm audit reports zero advisories. Two moderate Vitest
development advisories remain; resolving them requires a major test-tool upgrade.
Node 20.18 has an existing ESLint dependency engine warning. The separate .NET
runtime, worker identity, storage, scanning, retention and broader hardening work
remains in Phase 6.

## References and evidence

- [Microsoft MSAL v5 migration and redirect bridge](https://github.com/AzureAD/microsoft-authentication-library-for-js/blob/dev/lib/msal-browser/docs/v4-migration.md)
- [Microsoft browser cache guidance](https://learn.microsoft.com/en-us/entra/msal/javascript/browser/caching)
- [Browser increment verification](../evidence/phase-6/P6-browser-access-20260911.md)
