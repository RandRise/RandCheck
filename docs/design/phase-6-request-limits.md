# Request and in-flight resource limits

September 14, 2026. Public API admission now has process/IP and authenticated
organization/actor budgets. Limits apply to `/api` requests, including unknown paths,
before database grant verification. Authorization still runs before actor admission,
so revoked membership remains denied even if its earlier request budget is exhausted.
See the [verification report](../evidence/phase-6/P6-request-limits-20260914.md).

## Default limits per API process

| Budget | Requests per 60-second window | Concurrent requests |
| --- | ---: | ---: |
| All API traffic | 1200 | 64 |
| Transport peer IP | 300 | 16 |
| Organization | 600 | 16 |
| Actor within organization | 180 | 6 |
| Organization work | 60 | 4 |
| Actor work | 20 | 2 |

Work includes every method except GET/HEAD, plus original-file GET downloads. This
groups uploads, query/index creation, processing changes, reviews, reconciliation and
administration under tighter budgets. Ordinary document/session/history reads remain
available when only the work budget is exhausted. All request budgets still apply.
Every authorized attempt counts, including invalid payloads and failed operations;
only concurrent capacity is released when a request completes, fails or is cancelled.

Budgets use monotonic time and fixed windows anchored when each partition is created.
Window expiry resets the request count, including while older requests remain active;
it never resets concurrent usage. Multiple budgets at each admission stage are checked
and consumed atomically, with no waiting queue. An ingress-admitted request still counts
there if later authentication, authorization or actor admission fails. Fixed-window
boundary bursts are possible. This distinction between request rate and concurrent
work follows the [ASP.NET rate-limiting guidance](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-8.0).

The custom budget registry caps tracked partitions at 8192. Only expired partitions
without active requests may be reclaimed. When it cannot safely allocate a partition,
admission returns 429 instead of resetting another caller's budget. There is no timer
or queue per identity. Keys use the actual transport IP and server-bound organization/
actor UUIDs, never token claims, document IDs, request keys, raw URLs or forwarded headers.
IPv4-mapped IPv6 addresses normalize to IPv4; unknown peers share one bucket.

## Ordering, bodies and responses

Ingress admission precedes DB grant verification, authentication and request-body
buffering. After authorization binds the current identity, actor/organization admission
precedes cited-body buffering, normal model binding, recovery checks and workflow work.
The local disabled profile retains its earlier cited-response ordering and behavior.

Admitted enterprise requests have a 64 KiB actual-body cap, except the upload route,
which permits 10 MiB plus 64 KiB of multipart framing. The existing per-file limit
continues to apply. These total caps cover unknown-length/chunked bodies and multiple
multipart sections; no second full-body buffer is introduced. Route metadata identifies
uploads, including a trailing-slash request. The existing cited boundary retains its
own 64 KiB limit and safe error contract. Request-body reads have a 15-second deadline;
this is not a deadline for the entire workflow. Client cancellation is propagated.
Size overflow returns 413; a body-read timeout returns 408.

A throttle response is a generic 429 problem with `request_limit_exceeded`, `no-store`,
`nosniff` and integer `Retry-After`, exposed to allowed browser origins through CORS.
Window exhaustion reports remaining window seconds. Concurrency exhaustion suggests
one second, and partition-capacity exhaustion suggests one window; neither promises
capacity will be available then. The response contains no identity, source, SQL or
credential detail. `/health` and CORS preflight remain liveness/protocol responses.

`DocumentIntelligence.RequestLimits` publishes the `request_limit_rejections` counter
with only `stage=ingress|authorized` tags. Throttling does not insert a database audit
row per rejected request, which would amplify a flood. Successful and denied business
actions retain their existing audit behavior. Durable metrics export is deployment work.

## Configuration and deployment

The `RequestLimits` section maps directly to `RequestLimitOptions`. Limits default on
outside Development/Testing; attempts to disable them there fail startup. Local
profiles default off and can opt in with `RequestLimits:Enabled=true`. Invalid values
fail startup rather than silently removing a bound. Window seconds must be 1–3600,
partition count 16–65536, request counts 1–100000, and concurrency 1–1024.

Configure using the property names in the table: `GlobalRequests/GlobalConcurrent`,
`IpRequests/IpConcurrent`, `OrganizationRequests/OrganizationConcurrent`,
`ActorRequests/ActorConcurrent`, `OrganizationWorkRequests/OrganizationWorkConcurrent`,
and `ActorWorkRequests/ActorWorkConcurrent`; plus `WindowSeconds` and `MaxPartitions`.
Configuration is a startup snapshot; changing it requires restarting the process.

These counters are per process and reset on restart. Multiple replicas multiply
capacity. They do not provide a distributed quota, durable queue backlog cap, stored-byte
quota, or network DDoS protection. Behind a proxy, the proxy's transport IP shares the IP
budget; forwarded headers are deliberately not enabled without a trusted-proxy policy.
Deployment needs edge limits, measured budgets and metrics export. Existing operation
deadlines, worker concurrency and provider controls remain separate.

Next implementation work is durable queue/storage capacity admission. Live identity,
TLS/scanner/storage verification, backup expiry and audited restore repair remain open.
