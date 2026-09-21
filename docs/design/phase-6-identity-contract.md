# Phase 6 identity and access contract

September 11, 2026. API identity plus membership/audit increment; Phase 6 remains in progress.
The [Phase 5 acceptance](../evidence/phase-5/P5-development-acceptance-20260911.md)
remains the baseline. No retained actor, document, answer, embedding, spend event,
reservation or unknown hold is migrated or rewritten by this increment.

## Authentication boundary

The ASP.NET API has three explicit profiles:

| Profile | Behavior |
| --- | --- |
| `Locked` (base default) | Public health only; business requests receive 401. |
| `Development` | Fixed local identity, allowed only in Development or explicitly configured Testing. Development appsettings selects this profile to preserve the local synthetic workflow. Never expose this profile to untrusted networks. |
| `Entra` | Validated Entra v2 delegated access token plus an active database membership and its permission. |

Entra mode requires `Security:TenantId`, `Security:Audience` (API application GUID),
`Security:RequiredScope` (default `DocumentIntelligence.Access`) and a nonempty
`Security:AllowedClientIds` array. Environment variable equivalents use `__`;
for example `Security__AllowedClientIds__0`. These identifiers are configuration,
not secrets. The API does not need a client secret to validate bearer tokens.
Missing/invalid Entra configuration and a Development profile in Production or
Staging fail startup. The authority is constructed as the tenant-specific public
Entra `https://login.microsoftonline.com/{tenant}/v2.0`; arbitrary authorities,
`common`, sovereign clouds and multitenant Entra registration are not implemented.

The maintained ASP.NET JWT handler obtains signing keys from HTTPS OIDC discovery.
The added package is pinned to [JWT bearer 8.0.31](https://www.nuget.org/packages/Microsoft.AspNetCore.Authentication.JwtBearer/8.0.31).
It requires RS256 signatures, issuer, audience, expiration and valid lifetime
(30-second clock skew). Tokens are not saved. Authentication errors omit token
validation details. Only Authorization-header bearer tokens are accepted; query
tokens, cookies and identity headers cannot authenticate. The API requires exactly
one `ver=2.0`, `tid`, `oid`, `azp` and `scp` claim; the tenant and client must match
configuration and the exact delegated scope must be present. App-only tokens and
ID tokens without the delegated scope are refused.

This follows Microsoft's [JWT bearer validation guidance](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-8.0)
and [claims validation guidance](https://learn.microsoft.com/en-us/entra/identity-platform/claims-validation).
Browser authorization-code/PKCE sign-in, logout and session handling are now
implemented; see the [browser contract](phase-6-browser-access.md). Live Entra
registration verification remains open. Offline signed-token tests are not a live SSO claim.

## Membership and tenant resolution

The immutable Entra tenant and object IDs form a canonical lowercase subject:
`entra:{tid:D}:{oid:D}`. A unique `enterprise_identities(tenant_id, object_id)` row
joins an active `organization_memberships` row to its existing organization and
Human actor. `actors.external_subject` alone grants no access. Both the actor and
membership must be active. A filtered unique index permits one active organization
per enterprise identity in this version. Missing, revoked, ambiguous or System
bindings deny access. There is no automatic signup, email matching, caller-selected
organization or remapping of historical actors. See the
[membership and audit contract](phase-6-membership-audit-contract.md).

After permission and membership checks, a scoped execution context binds the
existing internal organization and actor IDs. It throws before binding and cannot
change identity within a request. Headers, query parameters, body fields, and token
claims named organization/actor ID do not choose the execution context. Existing
repository organization filters and Phase 5 actor/source/history checks still apply.
Membership is queried on each request, without a positive authorization cache.
Deactivation and role changes affect the next request using the same token; an
already admitted operation is not cancelled automatically. Membership mutations
also recheck administrator status inside an organization-locked transaction.
Continuous access evaluation is not implemented.

Initial organization/admin provisioning uses the private `EnterpriseAdmin` CLI.
It creates new actors and transactional audit events. The initial administrator
has membership administration with data role `None`. Organization-scoped HTTP
administration grants/changes/revokes memberships. Neither CLI nor API migrates a
database automatically. Do not bind a user to the retained synthetic System actor.

## Authorization matrix

Roles are database values `None`, `Viewer`, `Reviewer`, and `Operator`. Token
`roles` claims neither grant nor restrict these permissions. Unknown values grant
nothing. Operator does not grant membership administration, deletion or paid
model dispatch. A separate `CanManageMembers` grant enables membership and audit
access without requiring document access.

| Permission | Viewer | Reviewer | Operator | Existing endpoints |
| --- | --- | --- | --- | --- |
| Read | Yes | Yes | Yes | Document list/detail/processing/review/history, metrics, reconciliation detail, cited sources/history/evidence |
| Ask | Yes | Yes | Yes | Lexical, prepared semantic and reviewed-field questions/retrievals; these can save actor-scoped history |
| Review | No | Yes | Yes | Start/action/complete review, reconciliation create/link/evaluate/resolve, index reviewed source and prepare case sources |
| Operate | No | No | Yes | Upload, reprocess and cancel extraction |

An exact route-template/method inventory classifies all 32 current business routes,
including four membership/audit routes. Development mode cannot use those four routes.
The default and fallback authorization policies require authentication, permission
and membership. New/unclassified routes deny access in Entra mode until assigned
a permission. Only `/health` is anonymous. All `/api` responses carry `no-store`
and `nosniff`, including 401/403. Existing CORS preflight remains available without
granting business access. Resource misses continue using existing safe 404 contracts.

## Preservation and remaining boundaries

This increment changes the public ASP.NET request boundary. Internal infrastructure
and standalone synthetic pilot tools retain their existing development execution
context; they must not be exposed as enterprise endpoints. Workers, private Python
service identity, direct storage access, malware quarantine, retention,
rate limits and deployment secrets remain on the [Phase 6 task ledger](../tasks/phase-6.md).
No general model answers, arbitrary live embeddings, new provider calls, paid
budgets, automatic database migrations or retained-data deletion are enabled.
Existing running Phase 5 previews are not restarted as enterprise services.

Production use remains gated on the full Phase 6 acceptance criteria and subsequent
deployment work. No certification, universal isolation proof or production readiness
is implied by this first increment.
The local installed .NET/ASP.NET runtime remains 8.0.10 and earlier dependency
versions are retained. A current supported runtime and full dependency vulnerability
audit/update are required before enterprise deployment; this increment is not that audit.
HTTPS termination, trusted proxy/forwarded-header configuration and HSTS are also
deployment gates. Requiring HTTPS for OIDC metadata does not itself enforce HTTPS
for incoming API requests; no transport-enforcement middleware is added here.
