# Phase 5 application integration

September 11, 2026. This extends the existing reviewed-answer and evidence-search
contracts. Optional model generation remains disabled. No route added here can
dispatch a paid model or embedding request.

## Reconciliation case selection

The reconciliation workspace links to `/questions?case={id}`. The question page
prefills the case ID; **Use case sources** calls
`POST /api/cited-cases/{id}/sources`. The server requires an active actor in the
case's organization and resolves its linked completed reviews, extraction IDs and
document hashes. It indexes those exact revisions through the existing indexing
handler, then checks the case version and links again. A concurrent case change
returns a conflict rather than silently mixing revisions.

The response contains the case name/version and one to three distinct pinned
sources. Loading a case replaces the current selection only after success.
Questions persist those exact index/review identities. Editing the selection
clears the case label. Later case edits cannot rewrite an earlier answer. The
case version is selection provenance; saved answer authority is its frozen
review/index corpus, not a live query against a mutable case.

## Evidence retrieval choices

Keyword search remains the default, with the existing versioned lexical policy.
Vector and hybrid choices call `POST /api/cited-questions/semantic` with question,
request key, selected index IDs, mode and top-K. The hybrid UI uses
`pgvector-english-rrf60-c20-nonredundant-v1`; vector uses exact cosine search.

The server finds an already settled query embedding for the same active actor,
organization, exact question and exact corpus. Lookup is bounded to the latest
200 settled embedding operations for that actor. An existing request instead
reuses its recorded operation. Canonical storage revalidates source eligibility,
artifact hashes, provider accounting, model/dimension, corpus coverage and query
binding before ranking and atomic persistence. No client vector or operation ID
is accepted. Changing same-key inputs or retrieval policy conflicts.

When no reusable query exists, HTTP 422 explains that semantic search is
unavailable and offers keyword search or reviewed fields. It neither silently
falls back nor buys an embedding. This is prepared-query semantic search for the
development corpus, not unrestricted live embedding ingestion. Empty results
use the existing persisted no-hit refusal; saved history and source navigation
reuse the public authorized routes. The receipt retains semantic policy and
embedding identities separately from any answer policy.

All new routes inherit the cited API's no-store, body/stream limits, deadlines
and safe error boundary. The UI uses a new request identity when modes change.

## Source screening

`cited-source-screen-v1` screens new indexes using filename, ordered canonical
pages and effective reviewed values. It combines fragments before matching,
normalizes Unicode compatibility forms and HTML/numeric escapes, and detects
the declared instruction, false system/developer label, script, forged-citation
and tool/exfiltration patterns. Suspicious indexes are persisted as Quarantined
and cannot enter an eligible question corpus. Canonical text is retained for
review; it is not silently stripped or rewritten.

The generation coordinator checks its ordered evidence again before prompt
preparation or any reservation/dispatch. Existing records are not bulk rewritten.
The deterministic reviewed-field path never interprets page text as instructions;
its operands still come from validated completed review decisions.

This conservative screen can require review of benign quoted instructions. It is
not a complete classifier for every encoding, language or attack. Structural
scope/citation checks, inert rendering, no tool exposure and disabled public
model generation remain independent controls. Existing paid outputs and unknown
spend holds remain immutable.

See [development acceptance](../evidence/phase-5/P5-development-acceptance-20260911.md).
