# Phase 5 development acceptance

September 11, 2026. The delivered development scope is bounded English answers
from completed reviewed fields, keyword evidence search, prepared-query
vector/hybrid search, and case-based selection of frozen review revisions.
Optional general model answers remain disabled. Production identity and roles
remain Phase 6; Arabic and deployment remain later phases.

**Verification status:** Development acceptance passed. All 860 unique backend
checks have passing latest results: 611 non-PostgreSQL and 249 PostgreSQL. The
initial complete run had 28 historical rate-clock fixture failures; a 35-case
affected-fixture rerun passes after the test-only repair. There are no remaining
failures or skips. Python has 329 unique passes and web has 27. Builds, lint,
model alignment and documentation checks pass. This is not a production-readiness claim.

## Delivered behavior and evidence

| Area | Evidence |
| --- | --- |
| Case integration | Case link, active caller/organization checks, exact completed revision indexing, case-version recheck and frozen question corpus |
| Retrieval integration | Public vector/hybrid reuse of scoped settled query outcomes; exact replay and changed-input conflict; 422 without spending when unavailable |
| Answer behavior | Frozen 30-case real HTTP set passes all 30; 54 cited-fact references resolve to expected review/page evidence |
| Recorded timing | Across those 30 runs, backend reviewed-corpus duration p50 183 ms, p95 1,123 ms, maximum 1,339 ms; local synthetic timing, not client end-to-end latency |
| Due dates | Explicit reviewed due date, no issue-date/payment-term inference; missing coverage produces partial/refusal |
| History and citations | Every acceptance case reopens exactly; same-key replay matches; changed question conflicts; no-store on responses |
| Source screening | 16 unit checks; new poisoned indexes quarantine; canonical malicious prompt evidence refuses before preparation/reservation |
| Cross-phase flow | Fresh native extraction/review/index/evidence workflow; fresh three-document reconciliation with nine expected findings and saved resolution |
| Browser | Case load, three-document comparison, reviewed/original page citation, saved history and unavailable semantic-search state; screenshots retained |
| Python/OCR | 316 application, seven fixture, five pinned OCR and one native due-date check passed, 329 unique total |
| Frontend | 27 unique checks passed; lint and production build passed |
| Schema | No pending model changes; no new migration required |
| Provider cost | Zero new provider calls or charges; retained evidence and previous paid outcomes preserved |

Public semantic acceptance also used a copy of the retained synthetic corpus,
`phase5_semantic_public_20260911`. Vector and nonredundant hybrid HTTP requests
reused real previously settled query embeddings: five and two hits respectively,
with exact replay, saved history and page quotes verified. Only retrieval runs,
sources and hits changed in that copy. The ledger, artifacts and embeddings did
not change. All 34 tables in the original retained database match the prior audit.
This proves the public prepared-query integration; it is not a new model-quality
benchmark or automatic embedding ingestion for arbitrary questions.

The 30-case fixture was frozen before dispatch in
`outputs/phase-5/closure-20260911/frozen-http-cases.json`. It includes earlier
development scenarios and new paraphrases/security cases; it is not an independent
holdout. The separate varied-fact fixture remains 36/36 development checks.
Neither score rewrites the historical private 29/30 or paid source-v4 17/30.
Retrieval metrics remain separate: the earlier same-corpus comparison measured
95.83% lexical recall@5 and 100% nonredundant hybrid recall@5. Reviewed-field
answers consider all selected reviewed facts and are not scored as top-five RAG.

## Declared security matrix for the delivered scope

| Scenario | Check |
| --- | --- |
| Direct malicious question | HTTP cases for direct, hidden-character, split-line, false-system, numerical override and script questions refuse without claims |
| Indirect document instructions | Canonical poisoned source is quarantined at indexing; existing injected page cannot replace reviewed TRN in deterministic answers |
| Obfuscated and split source instructions | Unicode compatibility/zero-width, HTML and Unicode escapes, and fragments joined before screening |
| False system/source labels and forged citations | Source-screen unit tests and database forged/swapped hit/span tests |
| Hostile filenames/HTML | Filename screening and existing inert React rendering tests; only internal opaque-ID citation targets |
| Tenant/caller boundaries | Existing lexical/vector/hybrid constraints and caller checks; case-source denial, active other-actor history isolation and per-actor request identity |
| Cache/history and revocation | Exact same-key replay; changed-key input conflicts; inactive/quarantined/superseded and zero-hit history denial |
| Oversized/streamed input | Existing cited boundary plus new semantic/case route coverage, safe correlated errors and no-store |
| Conflicting/missing evidence | Varied reviewed-fact tests, partial date API case and unsupported/legal/payment refusals |
| Deletion/re-index lifecycle | Canonical references restrict destructive deletion; retirement/quarantine blocks source access and replay without erasing saved audit evidence |
| Provider/tool abuse | No paid dispatch in public routes; prompt-source rejection before preparation/reservation; no answer tools or executable output |

The matrix validates these concrete patterns and structural controls. It does not
claim universal prompt-injection resistance or validate unpublished general model
answers. Model-path enabling would require its own quality/security acceptance.
Server-log deployment access/retention remains a documented operational limitation.

## Reproduction and artifacts

`outputs/phase-5/closure-20260911` contains the frozen input set, all 30 HTTP
results, per-case exact replay/citation checks, full-suite TRX/logs, fresh earlier-
phase reports, source hashes, retained-table fingerprints and `SNAPSHOTS.md`.
Initial file-lock build attempts and the corrected frontend lint attempt are
preserved alongside final successful logs.

The full regression exposed expired historical test-clock fixtures. The tests
now use the midpoint of their frozen approved rate window, including the source
hold fixtures. Production `clock_timestamp()` and rate admission remain intact;
no rate validity or historical ledger record was changed. Initial failures and
the affected-fixture rerun are retained separately; unique totals use each test's
latest result and never count reruns twice.

The first repair attempt exposed an EF options-registration issue: a second
`AddDbContext` call did not replace the typed options used by fresh ledger
contexts. The final fixture explicitly registers those typed options with its
clock interceptor and checks the database clock against the frozen rate window.
The unsuccessful repair log is also preserved.

Python reproduction is now explicit in [the runtime guide](../../testing/python-runtimes.md)
and `tools/run_python_regression.py`. Installing the declared development extras
repairs the main environment's missing native dependency. Its 316 application
and one native check pass together; seven fixture checks run with Pillow 9.4 and
five OCR checks run in the pinned image with no network. Selected skips fail the
runner. Logs, group manifests and installed package versions are retained. These
reruns remain 329 unique tests, not additional accuracy cases.

Only `document_intelligence_test` is reset by PostgreSQL tests. Real service
acceptance uses isolated synthetic `phase5_reviewed_public_20260911`; retained
`phase5_e2e_20260908` is read only for fingerprint comparison. Scripted review
confirmations are fixture setup, not a human accuracy study.

Implementation contract: [application integration](../../design/phase-5-application-integration.md).
