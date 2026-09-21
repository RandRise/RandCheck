# Count-stage network recovery for the frozen source-v4 pilot

September 11, 2026 Dubai time. Rand instructed continuation after explicitly
authorizing 30 synthetic cases for up to USD 0.06. The first four attempts remain
immutable. The continuation includes only original cases q005–q030 (26 attempts,
at most USD 0.052 in reservations). The prior four reservation ceilings sum to
USD 0.008, so the entire original batch remains bounded by USD 0.06.

Three unauthenticated GET requests to the fixed OpenAI models endpoint completed
with TLS verification and the expected HTTP 401. No key, evidence, or generation
was sent. These establish current reachability, not the cause or permanent
resolution of the historical intermittent failures.

The private `RetainSourceCountFailureAsync` operation accepts only an Uncertain
generation reservation with v4 prompt/low reasoning, the pinned nano model,
at most USD 0.002 reserved, and maximum output 4,000. Preparation and outcome
hashes must agree. The outcome must explicitly record a network transport error,
unknown accounting, no count receipt or measurement, and no generation response,
HTTP status or provider request identity. The Python workflow awaits a valid
count before calling generation; failure at this point never reaches generation.
Nevertheless the entire reservation remains HeldUnknown with actual cost null.
There is no inference that a request was free, no retry, and no deletion.

Separate count-stage holds are capped at USD 0.054: q004 plus the 26 remaining
cases, each at USD 0.002. Other generation holds remain capped at USD 0.004.
The combined generation limit is USD 0.058 and all-workload limit USD 0.059;
the shared USD 8 admission checks still apply. The broader count allowance
does not authorize retaining another generation-stage failure. Other unresolved
dispatches, known overruns and flagged settled responses still prevent recovery.

Only the private synthetic runner exposes this action and selects the original
case from the SHA-pinned source-v4 manifest. The launcher checks frozen prior
outputs and the exact remaining case set. Eligible count failures are saved,
fully retained and skipped; any other failure stops the run. Saved cases are
never attempted again. A count failure counts as a failed evaluation case, not
as an answer-quality success. This policy is not enabled in the public API.

## Connection diagnosis and second continuation

The first continuation stopped at q005 after a successful count (1,894 tokens)
and another network failure before an accepted generation response. Its original
record remains unchanged. Further unauthenticated empty POST probes reproduced
the intermittent failure: TLS certificate verification reported a self-signed
certificate in the chain. This is stronger diagnostic evidence than the initial
three successful GETs, but does not prove the precise cause of either historical
generation failure, whose records contain only a broad network category.

The transport now supplies `ssl.create_default_context()` to HTTPX so it uses
the operating system's trusted certificates. Hostname checking and CERT_REQUIRED
remain enabled. No certificate was installed, no verification was disabled,
no key/evidence was sent in probes, and retries remain zero. All ten subsequent
unauthenticated empty POST probes returned the expected 401.

The exact q005 operation `7351e37b-3261-48aa-8294-71c3c521710f` has a separate
one-incident recovery allowance. It still requires the bound v4/low preparation,
valid count receipt, no generation response and unknown accounting. It retains
USD 0.002 in full; the generation/combined limits for this operation alone are
USD 0.008/0.009 including prior holds. Other generation failures keep the original
limit. Subsequent count-stage accounting recognizes this specific additional hold
separately; it does not relax other generation eligibility.

The `tls-continuation.json` manifest binds all five original outputs and includes
only q006–q030. Its maximum new reservations are USD 0.050 plus USD 0.010 for the
first five reservation ceilings, preserving the original USD 0.06 batch cap.
There is no retry or new model/prompt version. Fifty-five Python OpenAI tests
passed, including verification of CERT_REQUIRED, hostname checks and zero retries.
The count/connection hold tests passed. An initial cap-test setup attempted to
modify a HeldUnknown row and was correctly blocked by the database trigger;
the corrected test creates 27 real separate holds, preserving that protection.
