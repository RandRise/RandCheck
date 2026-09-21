# Private-service access contract

Implemented September 11, 2026. This increment protects the ASP.NET-to-Python
boundary. It preserves Phase 5 payloads, admission/replay rules, stored outcomes
and spending. It does not enable answer models or production embeddings.

## Identity and authority

Human requests still use Entra identity joined to active database membership.
A private service credential identifies a trusted calling component and permits
specific Python operations. It grants no human role, organization membership,
database access or permission to choose another organization's evidence.
ASP.NET continues loading authorized evidence and claiming extraction jobs; the
existing queue lease and storage-key checks remain necessary.

| Caller credential | .NET option | Python permissions |
| --- | --- | --- |
| Extraction worker | `AiService:ExtractionCredential` | `extraction` |
| Indexing / retrieval component | `AiService:CitedCredential` | `chunking`, `retrieval` |
| Answer component | `AiService:AnswerCredential` | `answer_prepare`, `answer_propose` |

Each credential has `KeyId` and `Token`. The client chooses it from a fixed
operation-to-credential map, never from request content. Components currently
share an ASP.NET process; this separation limits credential use at the Python
boundary but does not isolate a compromised ASP.NET host from its own secrets.

## Modes and transport

The API host resolves `AiService:AuthenticationMode`: unset means Development in
Development/Testing and Locked elsewhere. Explicit Development outside those
environments and unknown values become Locked. `ServiceKeys` must be explicit.
Standalone Phase 5 tools retain their trusted local development options.

Python uses `APP_ENV` and `SERVICE_AUTH_MODE`. Set `APP_ENV=production` for an
enterprise service. An unset mode then becomes Locked; an explicit Development
mode is also locked. Local development keeps the existing nonempty shared token
in `WORKER_SHARED_SECRET`, with no service-key header. ServiceKeys never falls
back to that token. Mislabeling a public deployment as development remains an
operator configuration risk; local compose is not an enterprise deployment.

ServiceKeys requires HTTPS in both clients and Python's ASGI request scope.
The .NET base address must equal the configured HTTPS origin, with no user info,
path prefix, query or fragment. Normal certificate validation remains enabled.
All four registered private HTTP clients disable redirects, proxy use, cookies
and framework HTTP logging. There is no automatic retry added by this change.

Use a private network and direct TLS to Python, or a separately verified trusted
TLS termination arrangement. For direct Uvicorn TLS, disable proxy headers and
access logging (`--no-proxy-headers --no-access-log`) and supply the deployment's
certificate and key through protected configuration. Do not trust arbitrary
`X-Forwarded-Proto` to establish transport security. Certificate issuance, network
policy, proxy trust and a deployed TLS handshake have not been verified here.

## Server credential configuration and rotation

Generate each token from at least 32 cryptographically random bytes, encoded as
unpadded base64url. The accepted token syntax is 43–128 ASCII base64url characters;
syntax validation cannot establish entropy. These are high-entropy bearer tokens,
not user passwords. Store client tokens in the deployment secret store; never
commit them, put them in browser configuration or pass them through URLs.

`SERVICE_KEYS_JSON` contains an array of 1–16 exact records:

| Field | Contract |
| --- | --- |
| `keyId` | Unique ASCII letters/digits/underscore/hyphen, 1–64 characters |
| `serviceId` | Stable safe component identifier, same syntax; no personal data |
| `tokenSha256` | Lowercase SHA-256 hex digest of the token's ASCII bytes; unique |
| `permissions` | Nonempty list of exact operation names, without duplicates |
| `notBeforeUtc` | Explicit UTC timestamp, inclusive |
| `expiresAtUtc` | Explicit UTC timestamp, exclusive; validity at most 90 days |

Unknown fields, malformed records, duplicate JSON keys, duplicate key IDs/digests,
invalid permissions and oversized configuration deny access. The server stores
verifiers rather than plaintext tokens and compares digests in constant time.
SHA-256 here depends on random bearer-token entropy, not password stretching.

For rotation, first install the new verifier alongside the old verifier under a
new key ID, then reload/restart the receiving process. Update the relevant client
secret and restart that client. Remove the old verifier and reload/restart the
receiver after verifying the new client. Expiry is checked on every request;
configuration changes require reload/restart because settings are cached.
Immediate revocation uses the same verifier-removal procedure. The maximum
validity bounds rotation overlap. Secret-store delivery and operational rotation
across deployed replicas remain deployment work.

## Admission and telemetry

ASGI middleware admits only exact known POST paths with no query string and
exactly one worker-token header plus exactly one service-key ID in ServiceKeys
mode. It runs before routing, body parsing or provider dependency construction.
Denied requests return a uniform 404 with `Cache-Control: no-store`. Enterprise
modes also hide legacy `/extract`, docs and other public routes; only GET
`/health` remains anonymous. Authorized private responses are not cacheable.

The `document_intelligence.service_security` logger emits a generated service
request ID, fixed operation, verified service/key IDs and Succeeded/Failed/Denied.
Unverified callers are unattributed. Events omit headers, tokens, incoming request
IDs, URLs, document text, evidence, personal data and exception payloads. Configure
this logger at INFO and forward it to protected operational logging. Success
means the response completed successfully, not that a downstream business result
was accepted or spending settled.

These operational events are **not** the transactional, append-only organization
audit table. No new service event is fabricated as a Human actor. Durable log
delivery, retention, alerting and linkage to worker/database audit remain open.
Existing human sensitive-action audit and the browser audit viewer are unchanged.

## Preserved boundaries and remaining work

Production Python still has disabled answer/provider dependencies. Embedding
permission names are reserved for the private fixture boundary; production main
does not expose those routes. The .NET embedding client remains an explicit
loopback HTTP development pilot and now rejects Locked/ServiceKeys mode.

This increment does not supply worker database least privilege, service-actor
audit for queue completion, per-organization worker credentials, malware scanning,
rate limiting, storage encryption, retention or replay resistance for a stolen
bearer token. TLS, expiry and revocation are required; compromised trusted callers
can still submit operations within their granted scope. No retained database was
migrated or changed. See the [verification record](../evidence/phase-6/P6-private-service-access-20260911.md).
