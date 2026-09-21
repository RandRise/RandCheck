# Phase 5 evidence workspace contract

**September 11 application extension:** reconciliation-case selection now pins
its linked completed reviews. Evidence search also offers vector and hybrid
methods using existing settled query embeddings; unavailable queries return a
clear refusal without provider dispatch. See the
[application integration contract](phase-5-application-integration.md) for exact
scope, replay identity, lookup limits and source screening.

**September 11 extension:** the workspace now also offers bounded reviewed-field
answers with saved claims, citations, partial/refusal states and history. See the
[reviewed-answer contract](phase-5-reviewed-public-contract.md). The original
evidence-search mode remains the default; the sections below describe that mode.

**Checkpoint:** P5-T07 provider-free subset, 2026-09-07.

The Next.js `/questions` workspace consumes the existing ASP.NET indexing and
question APIs. It searches explicitly pinned completed review revisions. It
does not generate model answers, call a paid provider, or change reviewed values.

## Source selection and saved questions

`GET /api/cited-sources?offset=0` returns `sources` and nullable `nextOffset`.
Each source contains document ID, filename, completed review ID, extraction run
ID, revision number, and completion timestamp. Both review and document are
organization-scoped. Results are ordered by completion time descending then
review ID, with 50 entries per page. Offset must be 0–10,000. This bounded
development catalog is not an unlimited searchable document picker; concurrent
catalog changes can shift offset pages. The UI deduplicates review IDs.

Adding a source explicitly calls `POST /api/cited-indexes` with that document
and review ID. Only a `Ready` index is added. The workspace permits one revision
per document and at most 100 documents, matching the frozen-corpus rules.
Catalog membership alone does not guarantee index eligibility; indexing still
performs its existing server checks.

Search uses `POST /api/cited-questions` with question, sorted pinned index IDs,
`topK: 5`, and a browser-created request key. The same question/corpus retry
retains that key. Editing the question or changing pinned sources clears the
old results and starts a new request identity. Outstanding requests are aborted
where possible; an epoch check prevents a late response from replacing newer
state. Browser cancellation does not roll back server work or recover a crashed
Running run. The UI timeout is 50 seconds; the server retains its own limits.

`GET /api/cited-questions/{id}` reopens a saved result without submitting a new
question. Question responses now additionally expose `question` and `indexIds`
from the saved run, so the displayed question and frozen-source count do not
come from an unrelated current form. IDs are manually copied/pasted; there is
no persistent browser history store. Existing organization/actor history checks
remain in force.

Nonempty results are visibly labelled **evidence, not a generated answer**.
A no-hit refusal warns that keyword retrieval can miss alternative wording:
it does not assert the fact is absent from the document.

## Authorized evidence navigation

### Cited request body boundary

All four cited API path families have a 64 KiB request-body cap before JSON
binding, including streamed bodies, and a 15-second body-read deadline.
Oversize returns `413 cited_request_too_large`; a stalled body returns
`408 cited_request_read_timeout`. These are separate from handler deadlines.
Existing question/source logical bounds remain unchanged. Invoice uploads keep
their separate 10 MiB file limit. See the [request-boundary checkpoint](../evidence/phase-5/P5-T08-request-boundary-verification.md)
for exact-byte behavior, log-privacy checks, and remaining transport limitations.

### Evidence-open contract

Unexpected downstream errors now return `500 cited_request_failed` without raw
exception details. If a response has already started, the connection is aborted
instead of appending a problem document. Existing specific workflow errors and
caller cancellation retain their behavior. See the [workflow-log checkpoint](../evidence/phase-5/P5-T08-workflow-log-verification.md)
for captured payload/credential privacy checks and metadata limitations.

`GET /api/cited-questions/{runId}/hits/{hitId}/evidence?spanId={spanId}` is an
internal read-only route. The server:

1. Loads the run within the executing organization and verifies its actor.
2. Requires the hit to belong to that run and its index to the frozen corpus.
3. Requires the current scoped index status to be `Ready` and reconstructs the
   matching stored hit chunk with the pinned extraction and review identities.
4. Requires the selected span to belong to that chunk, loads the canonical
   extraction page within the same organization, and verifies the exact UTF-8
   byte slice against the saved quote.
5. Returns server-split `before`, `quote`, and `after` strings, byte bounds,
   page number, and document/index/review/extraction identities.

Pages above 1 MiB of canonical UTF-8 text return 413. Invalid or changed byte
spans return 409. Unknown, swapped, foreign-organization, or foreign-actor
targets return the same 404. Retired/quarantined indexes return 409. Operations
have a 15-second server deadline. Source catalog and evidence responses use
`Cache-Control: no-store`; browser requests also use `cache: no-store`.

For a `ReviewedFact` hit, omitting `spanId` opens the exact scoped review
action, field occurrence, action kind, reason, timestamp, and stored reviewed
fact. It returns no page. If a page span is requested for a reviewed fact, the
page quote is original evidence, not a quotation of the corrected value. A
page-text hit cannot omit its span ID.

The UI highlights the server-verified quote in canonical extracted text and
focuses the source heading. It never calculates character offsets from UTF-8
bytes. It renders source content and filenames as React text, not HTML,
Markdown, executable instructions, or source-provided external URLs. Opening a
new source clears the previous source panel before requesting it.

## Deliberate limits

- This is **not a PDF image or polygon viewer**. Original layout, OCR placement,
  and visual comparison against the uploaded document remain future work.
- Current eligibility is checked when opening evidence. It is not a
  transactional revocation guarantee, and already displayed browser content is
  not remotely erased. Saved retrieval history is still an immutable snapshot.
  P5-T08 additionally gates history GET and same-key POST replay on all frozen
  source indexes being Ready. Ineligible saved content returns 409 rather than
  being disclosed; audit storage is unchanged. All cited endpoint responses,
  including routing/binding failures, now receive `Cache-Control: no-store`.
- Development execution identity is not production authentication/RBAC.
- Reconciliation-case corpus selection, generated/partial answers, semantic
  claim support, prompt-injection defense evaluation, vector/hybrid comparison,
  durable crash recovery, and full Phase 5 acceptance remain outstanding.
- The UI consumes trusted typed application responses; this checkpoint does
  not add a complete runtime schema validator for arbitrary malformed servers.

See [checkpoint evidence](../evidence/phase-5/P5-T07-workspace-verification.md).
