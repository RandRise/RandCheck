# Source-v4 evaluation after connection recovery

September 11, 2026 Dubai time. All 30 original question attempts are now recorded.
The remaining cases were completed under the user's original USD 0.06 approval,
without retries, new question IDs, prompt changes or a different model.
Phase 5 is not complete: this candidate fails the answer-quality gate.

| Result | Count |
| --- | ---: |
| Planned/recorded question attempts | 30/30 |
| Settled model responses | 27 |
| Structurally and policy-accepted proposals | 18 |
| End-to-end passes after source review | 17 |
| Transport failures, fully retained as unknown | 3 |
| Rejected model proposals | 9 |
| Accepted but wrong requested field | 1 |
| Claims supported in the context of their question | 13/14 |
| Accepted citation references checked | 20 |

Known generation cost is **USD 0.012233**, with **USD 0.006** retained for q001,
q004 and q005. The batch's accounted upper bound is **USD 0.018233**, below
USD 0.06. Actual total cost remains unknown. The shared pilot ledger has
USD 0.027121 settled plus USD 0.008001 HeldUnknown, including older incidents.
It has zero pending reservations and its reconciliation flag is clear. Clearing
that flag did not erase any unknown hold or authorize retrying those operations.

## What was fixed

Unauthenticated empty requests reproduced intermittent TLS certificate-chain
verification failures. Supplying Python's default system-trust SSL context to
HTTPX resolved the reproduced failure: ten unauthenticated probes then succeeded,
and all 25 subsequent paid question requests returned responses. Certificate and
hostname verification remain enabled; no certificate installation, verification
bypass, endpoint change or retry was added. The historical failures lack enough
detail to attribute every one conclusively to the reproduced TLS issue.

The [bounded recovery policy](../../design/phase-5-count-network-recovery.md)
retains count-stage network failures and the specifically named q005 incident
without zero-cost claims. All five pre-fix outcomes and the original plan remain
unchanged. Separate continuation manifests bind the untouched case IDs and
previous output hashes. The original partial report remains a historical record.

## Remaining answer failures

- q002 and q015 include trailing whitespace in excerpts; q013 and q015 repeat
  identical claims. The frozen strict validator rejects these outputs.
- q014 inserts ellipses, q017 emits literal backslash-n text, q020 changes number
  formatting, and q024 flattens whitespace. Their excerpts are not exact source
  substrings. q024 also omits the invoice from its requested comparison.
- q022 selects **TIN 0000000000** instead of **supplier TRN 000000000000701**.
  Its excerpt is real, so structural validation accepts it. This is a substantive
  field-selection failure and demonstrates the remaining semantic gap.
- q028 produces a legal/input-tax conclusion, and q030 uses an authorized
  quotation to answer a foreign-organization request. The scope guard rejects
  both. The model did not produce the expected unsupported-scope refusals.

Successful examples include PO training unit price 1250.00, the PO/invoice total
difference 105.00 with both operands cited, the VAT difference 5.00, and correct
refusals for missing payment date, IBAN and exchange rate. The 17/30 result is
not an improvement over the earlier 23/30 low-reasoning run. The evaluations use
the same development set, not independent holdouts; this batch also includes
three transport failures and a stricter excerpt contract. Historical scores
remain unchanged. Source review was performed by Codex, not an independent human.

## Verification

- 55 Python OpenAI tests passed, including verified TLS configuration and no retry.
- 243 application tests passed.
- 23 unique PostgreSQL hold/recovery tests passed across the targeted runs.
  One initial cap-test setup was rejected by the immutable-hold database trigger;
  the corrected test creates separate real holds and passed. Both logs remain.
- Six existing source-report tests passed; the new recovered report validates
  the frozen plan/evidence, per-output review hashes, every accepted citation,
  known versus unknown accounting, all 30 outcomes and the USD 0.06 bound.
- The synthetic pilot builds with zero warnings/errors.

- All 27 settled responses replayed exactly with the provider client disabled;
  the database fingerprints were identical before and after replay.
- Since the frozen plan, only generation artifacts and the three accounting
  tables changed. Documents, reviews, reconciliations, chunks, embeddings and
  retrieval records are unchanged. Documentation build and whitespace checks pass;
  the existing documentation bundle-size advisory remains.

Replay and post-run database verification are saved in the artifact folder.
Public generated answers remain disabled and no new answer/claim publication
was performed by this evaluation.

## Concrete next work before Phase 5 completion

The next implementation should remove model-authored excerpt copying from the
critical answer path: select typed reviewed facts and evidence IDs, resolve
display excerpts on the server, and compute monetary comparisons in code.
TRN and TIN must remain distinct typed fields. Scope and insufficient-evidence
decisions should be enforced before generation. Free-form answers still require
an explicit support/completeness check; ID membership alone is insufficient.

After that change, run a versioned evaluation that preserves these failures,
then finish public answer integration and the remaining model-injection,
cross-organization and release-regression acceptance checks. Do not lower the
quality gate, silently repair this frozen evaluation, or enable public generation
based on the successful connection recovery. The current 30-case authorization
has been consumed; this report does not schedule another paid batch.

Artifacts: `outputs/phase-5/answer-source-20260911/evaluation-recovered.json`,
`manual-assessment-recovered.json`, `ledger-after-recovery.json`,
`RECOVERED-SNAPSHOTS.md`, network/TLS diagnostics, test logs, and offline replays.
