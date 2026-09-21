# P5-T07 evidence workspace review

**Date:** 2026-09-07
**Status:** Provider-free workspace implemented and verified.
P5-T07 and Phase 5 are not complete.

## What is available

The `/questions` screen lets a reviewer select completed document revisions,
pin their indexes, search only those sources, reopen a saved retrieval, and
open the exact source text behind a result. It separates original page text
from reviewed corrections and displays no-hit refusals without claiming a fact
is absent. The existing document/reconciliation navigation links to it.

The backend adds organization-scoped source discovery and actor/organization-
checked evidence navigation. Opening a page rechecks index eligibility and
verifies its saved UTF-8 quote against canonical extraction text. Reviewed
facts can open review provenance without inventing page evidence.

The UI does not present hits as model answers. **No paid calls were made;
this checkpoint cost USD 0 in provider API usage.** No credentials, billing
configuration, provider selection, migrations, or production settings changed.

See the [workspace contract](../../design/phase-5-workspace-contract.md) for
endpoint behavior, bounds, retry rules, and limitations.

## Verification

- Web: 17 component tests plus 3 review-contract script tests passed. The 10
  new workspace tests cover source pinning, exact-quote highlighting, no-hit
  refusal, hostile text rendering, request-key retry, ignored late responses,
  unavailable-source clearing, review-only provenance, read-only saved-result
  loading, and empty-catalog guidance.
- Web lint passed with no warnings/errors. Next.js production build passed,
  including TypeScript and the new static `/questions` route.
- .NET Debug build passed with zero warnings/errors. Non-persistence regression:
  276 passed (111 domain, 88 application, 42 infrastructure, 4 architecture,
  31 API). The 2 new API cases verify invalid catalog offset bounds and no-store.
- PostgreSQL regression: all 46 passed, including the 3 new evidence-workspace
  cases and the existing live private HTTP and migration rollback/reapplication
  cases. Total .NET regression: **322 passed**, with no skipped tests.
- EF reports no pending model changes. Documentation build and scoped whitespace
  checks passed. Python/OCR source was unchanged; its complete standalone suites
  were not rerun in this checkpoint.
- Browser visual/interaction checks used the real Next.js screen with the
  explicitly labelled synthetic preview helper described below. Source pinning,
  question submission, source opening/focus, exact highlighted text, and no-hit
  refusal were observed. Desktop and 390-pixel narrow layouts were inspected;
  no horizontal overflow was observed in the narrow evidence state. The
  temporary viewport override was reset.

The initial web run found a test setup race: a test selected an option before
the asynchronous source list loaded. The helper now waits for that option;
the complete rerun passed. This was not treated as a production regression.

The new PostgreSQL cases exercise actual application services, scoped stores,
and database records: completed-source selection; exact pinned page text;
cross-organization/actor and unknown/swapped IDs; quarantined index access;
review-only provenance; and corrupt canonical text failing quote verification.
The reviewed-fact case launches the real private Python indexing/planning HTTP
service. The disposable test database remains separate from application data.

TRX files are under `tmp/phase5-test-results/phase5-workspace*.trx`.
The isolated PostgreSQL server and both temporary preview processes were stopped
after verification. No application database was migrated or reset.

## Browser preview is not end-to-end acceptance

`tools/phase5_workspace_preview.mjs` serves **synthetic UI fixtures only** on
127.0.0.1:8085. It is not the application API. The filename and policy label
identify preview data; its question matching is a fixed display-state switch,
not the PostgreSQL retrieval algorithm. It uses the existing synthetic invoice
page from `samples/phase-5/lexical-v1/evaluation-input.json` and makes no model
calls. It stores only one in-memory display result and has no security claim.

To reproduce the visual preview, start the helper from the repository root
with `node tools/phase5_workspace_preview.mjs`. In a separate terminal:

```powershell
cd apps/web
$env:NEXT_PUBLIC_API_URL='http://127.0.0.1:8085'
npm run dev -- --hostname 127.0.0.1 --port 3004
```

Open `http://127.0.0.1:3004/questions`. A question containing `unknown` shows
the preview refusal; other questions show the synthetic page. Stop these
processes and clear that terminal's temporary API override before running the
real application. No `.env` file was changed for verification.

Browser checks prove rendering and interaction with those fixtures, not the
complete browser → ASP.NET → Python → PostgreSQL workflow. Real backend
behavior is checked separately in the persistence suite. Full-stack browser
acceptance remains a Phase 5 gate.

## What this does not prove

1. No generated answer has been evaluated for semantic support, completeness,
   hallucination, or reliable refusal with nonempty irrelevant hits.
2. The source view is extracted text, not an original PDF/image layout or a
   polygon overlay. Successful highlighting does not establish OCR accuracy.
3. Development identity, scoped queries, and safe React text rendering are
   not production authentication, comprehensive RAG security, or a security audit.
4. Eligibility is rechecked on opening; already displayed text is not erased
   on later revocation. Full deletion/cache/retention controls remain open.
5. Synthetic fixture success does not establish real UAE invoice accuracy,
   legal compliance, vector/hybrid superiority, or production readiness.

## Next work

Continue provider-independent threat controls/adversarial tests and full-stack
workflow verification. Paid API setup can wait. Before provider-dependent
comparison, agree the concrete model/privacy/cost choice, securely configure
credentials and spend enforcement, then run bounded evaluations. No new task
or additional dataset is required to continue that independent work.
