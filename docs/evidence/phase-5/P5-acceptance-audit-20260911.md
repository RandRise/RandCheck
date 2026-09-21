# Phase 5 acceptance audit

September 11, 2026. This audit checks the existing approved contract, phase
criteria, quality plan and threat model against the current implementation.
It adds no production identity requirement and makes no provider calls.

The reviewed-field application workflow is implemented and tested. The earlier
description “final audit remains” is now replaced by the concrete findings below.
The whole Phase 5 backlog is not yet closed.

## Original phase criteria

| Criterion | Audit finding | Evidence |
| --- | --- | --- |
| Clickable document/page citations | Met for public reviewed answers and evidence search; review-only facts are labelled separately | [Public integration](P5-reviewed-public-verification.md), 41 cited-fact references and browser source navigation |
| Distinguish answers from insufficient evidence | Met for the bounded public mode, including partial answers | 17 real answer scenarios, saved status/claims and browser refusal/partial snapshots |
| Organization isolation | Structural checks covered by execution-context filtering, database constraints, ownership and citation tests; production identity is Phase 6 | [Workspace](P5-T07-workspace-verification.md), [semantic retrieval](P5-semantic-retrieval-verification.md), public PostgreSQL tests |
| Answerable and unanswerable evaluation | Met | Frozen original 30 cases, varied 36-case development fixture, API refusal scenarios |
| Separate retrieval and answer metrics | Met | [Nonredundant retrieval](P5-nonredundant-retrieval-verification.md), [paid answer evaluation](P5-source-recovered-evaluation.md), separate reviewed-field results |
| Persisted factual claims cite authorized immutable sources | Met for the public reviewed-field route; model proposals remain unpublished | Atomic run/hit/answer/claim/citation persistence, exact review/page binding and rollback tests |
| Retrieved injection cannot change policy or invoke an action | Covered for the deterministic public route; the complete minimum RAG red-team set remains unverified | New reviewed adversarial tests, existing inert rendering/evidence tests; model-path limitations below |

“Met” identifies the verified route and development scope. It is not a new
general-language accuracy score or a claim that every planned Phase 5 task is done.

## Concrete remaining work

1. **Unify the supported application contract and evaluation.** Evidence search
   defaults to lexical retrieval; reviewed answers use all selected reviewed
   fields. The evaluated nonredundant vector/hybrid policy is still a private
   path. The approved question contract also describes selecting a reconciliation
   case; the current question API accepts explicit indexes. Complete that planned
   integration or record an explicit scope decision. Then freeze one current
   end-to-end acceptance set for the delivered behavior. The historical private
   29/30 and paid 17/30 scores cannot be combined with the new 36/36 fixture.
2. **Finish the declared RAG security controls and minimum red-team matrix.**
   The threat model calls for source scanning/quarantine at ingestion and prompt
   preparation, plus obfuscated/split instructions and false source labels. The
   prompt contains untrusted-evidence instructions and exposes no tools, but
   those controls do not establish the promised scanner or full model-path
   resistance. The new tests establish only the deterministic route's boundary.
3. **Run the final complete regression gate after the remaining changes.**
   The current evidence includes 591 non-PG backend checks, eight public PG
   integration cases, 24 web checks, prior Python/OCR checks and real cross-phase
   flows. This audit adds targeted PG tests; it does not relabel those separate
   checkpoints as one fresh full-suite .NET/Python/web/PG migration/docs run.

The approved contract calls answer-model adapters **optional**. Therefore
enabling OpenAI or improving the paid model score is not, by itself, a mandatory
Phase 5 completion condition. If model answers are included in the delivered
scope, their observed field-selection/support failures and adversarial acceptance
must be resolved before enabling them. They remain disabled now. The prior
wording “general model answers must pass” was too broad for the optional-adapter
contract; this audit makes that distinction explicit without silently removing
the planned retrieval and security work.

Enterprise authentication/RBAC remains Phase 6. Deployment and PostgreSQL server
log access/retention remain explicitly tracked risks in the threat model; this
audit neither claims they are solved nor turns production rollout into a new
requirement for the development demo.

## New targeted verification

**Result:** all six new adversarial cases and all 14 existing public/private
reviewed-fact regression cases passed (20 unique PostgreSQL checks in this audit).
Documentation build and whitespace checks passed. All 34 retained database table
fingerprints match the preceding checkpoint. No production code changed and no
provider calls or new provider costs occurred.

`CitedReviewedAdversarialTests.cs` checks:

- A canonical indexed page containing false system instructions, zero-width
  obfuscation, a forged citation, an exfiltration URL and script text cannot
  replace the reviewed TRN. Direct hostile questions refuse without claims.
- A tampered reviewed chunk or original page cannot produce a new saved answer.
- Another real active actor in the same organization cannot reopen the owner's
  saved answer/hit; the same request key creates that actor's separate run.
- Superseding a zero-hit corpus blocks saved history and same-key replay while
  preserving the audit record.
- Forged hit IDs, a hit from another run and swapped/unknown spans fail closed.

All fixtures run only in disposable `document_intelligence_test`, with the
private service address deliberately unreachable for reviewed-answer tests.
The optional poisoned-page fixture leaves every existing seed unchanged by
default. These are synthetic regression checks, not a model security benchmark.
Exact results and source hashes are recorded in
`outputs/phase-5/acceptance-audit-20260911/verification.json` and the TRX/log files.

Next implementation step: connect reconciliation-case selection to the frozen
reviewed corpus, then complete the declared security matrix and final regression.
No paid rerun, production rollout or silent alteration of old evaluations follows
from this audit.
