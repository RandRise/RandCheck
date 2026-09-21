# Source context and excerpt binding verification

Historical checkpoint. The [recovered evaluation](P5-source-recovered-evaluation.md) now records all 30 attempts; its outcome supersedes the incomplete-run status below.

Recorded September 11, 2026, Dubai time. Implementation and offline verification
are complete for the [v4 candidate](../../design/phase-5-source-context-v4.md).
This records the pre-run checks. The subsequently approved
[paid evaluation stopped after four cases](P5-source-context-partial-run.md);
26 remain unrun. Phase 5 remains open.

## Changes

- The private evidence packet identifies the actual document, index, completed
  review, extraction, chunk kind, review action and exact page spans for each hit.
- V4 citations require a literal `supportText` excerpt. The .NET validator checks
  it against the referenced page span or reviewed content. Original machine
  quotes stay separate from corrected review values.
- New requests and responses cannot silently downgrade to v3. The v3 path remains
  byte-compatible for saved-request replay. Public generation remains disabled.
- The prompt asks for every requested document and each comparison operand.
  These instructions and excerpt checks do not establish general entailment or
  completeness; the tests deliberately preserve that distinction.

## Verification

Output directory: `outputs/phase-5/source-context-20260911`.

| Check | Result |
| --- | --- |
| Backend non-PostgreSQL regression | 510 passed: 111 domain, 243 application, 74 infrastructure, 78 API, 4 architecture |
| Targeted PostgreSQL integration | 53 passed, then 2 source-aware low-reasoning tests passed |
| Python cited-question checks | 175 passed |
| Versioned answer-report checks | 6 passed |
| Historical v3 prompt/count/generation hashes | All 61 rebuilt exactly, including the retained failed low preparation; no dispatch |
| Offline v4 packets | All 30 constructed; largest generation body 17,822 bytes, below 65,536 |

The database tests cover the real private .NET/Python bridge, valid and invalid
excerpts, corrections, wrong schema versions, known-cost settlement before
rejection, replay without new calls, scope changes, preflight failures and the
existing accounting paths. OpenAI responses in these tests are fixtures.
The first Python run had one mock-stream construction error in the newly added
transport test; the corrected mock and the complete cited suite passed. Both
logs are preserved. No production workaround or weakened assertion was used.

The prior [full cross-phase regression and seven screenshots](P5-full-regression-20260910.md)
remain the broader UI/OCR/migration evidence. This increment changes no public
web flow or database schema; it does not rerun that entire 703-test baseline or
claim that mocked provider tests establish live answer quality.

## Frozen next evaluation

`samples/phase-5/answer-source-v0.4.0/plan.json`, SHA-256:
`56fda3a2d5317cea14700f00965b4a808dc855e9d1d6934d48b8190cce7c08be`.

Thirty fresh retrieval runs reuse existing saved query embeddings and exactly
match the previous low-reasoning evidence/ranks/scores apart from new run/hit
IDs. This preparation made no embedding or generation calls. The retained
database now has 201 retrieval runs; its 123 spending reservations and all
settled/held amounts are unchanged. Before/after offline fingerprints match;
subsequent changes are confined to the new retrieval runs, sources and hits.

The planned run sends only the same three generated synthetic documents to the
fixed OpenAI counting/Responses endpoints, with `gpt-5-nano-2025-08-07`, low
reasoning, at most 30 generation attempts and USD 0.06 reserved in aggregate.
The shared USD 8 ceiling remains. Official
[nano rates](https://developers.openai.com/api/docs/models/gpt-5-nano) were checked;
the saved rate window ends September 11 at 00:00 UTC and must be rechecked if
execution starts after that instant.

Automatic approval review rejected the launch before process creation, stating
that this specific payload/destination and paid calls lacked explicit approval.
The same action was reconsidered after retrieving earlier user authorization
from **Start phase 3**, and rejected again. The provenance and exact scope are
saved in `authorization-review.md`. No launch log, generation output, reservation
or new charge resulted. The run is stopped pending explicit approval in this
task; no workaround was attempted.

The runner, pinned selection and reporter are ready:
`tools/phase5_run_answer_followup.ps1 -SourceContext -ExecutePaid`, private CLI
`--source-case` / `--verify-source-case` / `--replay-source-case`, and
`tools/phase5_answer_followup_report.py --source`. Every outcome must be retained;
no automatic retry is permitted. Manual source assessment and post-run replay
remain necessary. The prior 19/30 and 23/30 quality scores remain unchanged.
