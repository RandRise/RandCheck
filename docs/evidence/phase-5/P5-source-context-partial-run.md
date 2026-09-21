# Source-context v4: paid run stopped after four cases

Historical checkpoint. The [recovered evaluation](P5-source-recovered-evaluation.md) now records all 30 attempts; its outcome supersedes the incomplete-run status below.

September 11, 2026, Dubai time. The user explicitly authorized sending the
synthetic evidence to OpenAI and running 30 tests for up to USD 0.06. Automatic
approval accepted the launch. Login and Usage-page access are not blockers.

The original frozen `answer-source-v0.4.0` manifest and its 30 retrieval IDs
remain unchanged. Four cases were attempted, two received settled responses,
one answer was accepted, and one proposal failed validation. Two transport
attempts have unknown accounting. The remaining 26 cases were not run. This is
an incomplete evaluation; neither a 30-case score nor Phase 5 completion follows.

| Case | Result | Accounting |
| --- | --- | --- |
| q001 | Count succeeded at 2,265 tokens; generation response absent | USD 0.002 HeldUnknown |
| q002 | Correct total value, but full-page support excerpt retained a trailing newline and failed the trimmed-text rule | USD 0.000403 settled |
| q003 | Quotation VAT 420.00, supported by the exact quotation page excerpt | USD 0.000268 settled |
| q004 | Network failure during counting; generation was not reached | USD 0.002 Uncertain |

Confirmed run cost is USD 0.000671. The two full unknown reservations are
USD 0.004, giving an accounted upper bound of USD 0.004671 within USD 0.06.
No zero-charge assumption or retry was made. The shared ledger now has
USD 0.015559 settled, USD 0.004001 HeldUnknown (including historical holds),
and USD 0.002 Uncertain. Its reconciliation guard remains active.

## Recovery and diagnostics

The first failure occurred about three seconds after dispatch, not at the
30-second generation deadline. Its original artifact lacks a failure category;
its cause cannot be inferred from the later failure.

A narrowly tested `pilot-source-generation-hold-v1:authorized-budget` policy
retained q001's entire reservation, with actual cost null. It requires the
bound v4/low preparation, matching generation hash, valid saved count receipt,
no generation response, no other unresolved dispatch, and global budget checks.
The combined generation hold cap is USD 0.004 (including the older USD 0.002
preflight hold), and the all-workload cap is USD 0.005. Held operations cannot
be redispatched, canceled, or settled as zero. The continuation manifest
excluded q001 and allowed only the other 29 original IDs, at most USD 0.058
in new reservations.

Safe allowlisted diagnostics were added without exception messages or secrets:
deadline, HTTP timeout, network, protocol, or other. No retries, endpoint changes,
timeout changes, model changes, or automatic accounting release were added.
q004 records `openai_transport_uncertain` / `network` with no count receipt.
It does not qualify for the source-generation hold policy. The launcher stopped
and cleaned up its private Python service. Do not rerun either launcher against
saved attempts or broaden the hold policy merely to make the suite continue.

## Verification and next work

- 54 Python OpenAI protocol/transport/workflow tests passed, including safe
  error categories, secret suppression, no retries, and unknown accounting.
- 243 application tests and 15 PostgreSQL hold tests passed. The latter cover
  receipt binding, cap enforcement, global budget admission, and immutable holds.
- Six report tests passed; SyntheticPilot built with zero warnings/errors.
- Documentation build and whitespace checks passed. The build retained its
  existing large-chunk advisory. Post-run fingerprints differ only in the
  generation artifacts, pilot budget, spend events, and spend reservations;
  all other retained tables match the pre-run snapshot. The private port has
  no listener and the paid runner has exited.
- Earlier full regression and v4 packet/bridge checks remain documented in
  their original evidence reports. They were not replaced by this partial run.

Next resolve the network/count-stage interruption with a bounded recovery plan
that preserves q004 and the USD 0.06 total allowance, then freeze only the 26
untouched cases before further dispatch. Do not rewrite the prompt or repair
q002's output during this frozen evaluation. Public generated answers remain
disabled; literal citation membership does not establish semantic support.

Local artifacts are under `outputs/phase-5/answer-source-20260911`: `partial-run.json`,
`ledger-continuation-stopped.json`, both stopped outcomes, `SNAPSHOTS.md`,
the four generation files, test logs/TRX, and the post-run database fingerprints.
The snapshot index shows the actual q003 answer/excerpt and q002 rejection,
and links the seven existing integration screenshots.
