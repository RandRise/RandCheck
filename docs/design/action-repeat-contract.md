# Repeat actions and deliberate revisions

R4-T05 is split into separately verified comparison (T05a) and document/processing/
question (T05b) tasks. This prevents one broad change from bypassing the per-task gate.

## Comparison contract — R4-T05a

| Action | Retry rule | Deliberate new action |
| --- | --- | --- |
| Create case | UI sends an intent key; same actor/organization/key and normalized original name/category returns the existing case, even after later metadata edits | A successful creation clears the UI intent; another creation gets a new key, even for the same title |
| Edit case metadata | Strong ETag; exact unchanged metadata is a no-op | Changed name/category rotates ETag; closed cases reject changes |
| Pin reviewed source | Same completed document/review/extraction/hash is a no-op after normal validation | Replace pinned review opens a separate choice; cancel writes nothing; replacement affects only future comparisons |
| Run comparison | Pending lock and strong ETag; a retry with the consumed ETag cannot append another run | New comparison explicitly opens default tolerances; Run comparison creates a new immutable snapshot, including when inputs intentionally repeat |
| Decide finding | Strong ETag; exact latest action and trimmed reason is a no-op | Revise decision opens controls; changing action/reason appends history; cancel writes nothing |
| Recover uncertain/stale write | Reload reads saved state and preserves drafts; no automatic replay with a refreshed ETag | User checks saved sources/results/decisions before deliberately submitting again |

Create accepts optional `requestKey` up to 100 characters without surrounding spaces.
Legacy callers without a key retain legacy creation semantics and are not protected
against uncertain retries. The actual UI always supplies a key. The database enforces
uniqueness by organization, creating actor and key; immutable payload hashes distinguish
changed inputs. Each retry still passes current authorization and audit admission.
Keys/hashes are not exposed in case views. Insert, reference allocation and business audit
are atomic; replay creates no second business event (request-attempt audit may repeat).

The UI retains an unacknowledged key in memory and, when available, tab session storage
scoped by organization/account. Retyping the same normalized name/category after reload
recovers that intent. Acknowledged success clears only its own key, so a late response
cannot erase a newer intent. Storage is optional; when blocked only mounted-page retry
recovery is retained. This does not deduplicate separate deliberate creations or two
independent tabs with different intent keys.

Reviewer and Operator can perform comparison writes. Viewer reads; administrator-only
has no document access. The independent administrator flag never adds workflow rights.
Closed cases remain read-only; drafts surviving a remote close are exposed for copying.

## Document/processing/question contract — R4-T05b

| Action | Retry rule | Deliberate new action |
| --- | --- | --- |
| Upload | UI supplies `X-Operation-Key`; same organization/actor/key and exact filename, MIME type, declared length and byte hash returns the original document | Acknowledged upload clears its own intent; another upload gets a fresh key |
| Reprocess | Same organization/actor/key plus document/provider/version/schema returns the original job/run, including after terminal processing; response exposes actual original status and `replayed` | Acknowledged request clears its own intent; deliberately processing again gets a fresh key |
| Record field decision | Current strong ETag is required even for a repeat; exact latest action, value type/text and normalized reason is a no-op | Revise decision deliberately opens editing; a changed valid decision appends history |
| Cancel processing | Synchronous submission lock; on uncertain response inspect latest processing before deciding whether to retry | Terminal processing cannot be restarted by cancellation |
| Ask question | Existing actor-scoped request key and source/payload fingerprint survive uncertain retries; synchronous lock prevents overlapping submissions | Changed question/source/mode creates a new request intent |

Operation keys accept 1–100 ASCII letters, digits, `.`, `_`, `:`, or `-`. Empty,
padded, multi-valued or malformed keys fail with `invalid_operation_key` (400).
Reusing a key with changed inputs fails with `operation_payload_changed` (409).
Deleted or ineligible original sources and missing original jobs fail with
`operation_source_unavailable` (409); a retry never recreates a removed source.

Migration 22 adds immutable `document_processing_requests` receipts. They contain
only organization/actor/operation scope, a payload hash and document/run/job IDs;
they survive source deletion and have no expiry in this local contract. Current
authorization, request admission and recovery checks still precede every replay.
Legacy API callers omitting the header retain their previous upload/reprocess
semantics and are not protected against uncertain terminal retries.

Capacity preflight and final insertion check replay under the existing organization
admission lock. Receipt, source metadata, queue job and business audit commit in one
transaction. An upload losing the race compensates only its own generated file.
When commit acknowledgement or compensation lookup is uncertain, an owned source
is preserved and an unverified file is left for existing orphan recovery.

The UI retains unacknowledged intents in memory and, when available, tab session
storage scoped by organization/account. With browser SHA-256 available, the upload
fingerprint also binds file bytes, so a reload retry requires choosing the exact
same file again. Without it, only the same mounted File object can reuse the
upload intent; filename and size alone never restore an upload key. Storage is
optional; blocked storage limits recovery to the mounted page. Independent tabs
with independent intent keys remain deliberate separate operations.

### Historical gaps before T05b

This inventory predates the separately reviewed and tested T05b changes above.
It preserves the original reasons for the work; the current behavior is the contract
table above and the [verification record](../testing/simplification-20261002.md).

- Start review already resumes the same active extraction review; uniqueness is
  enforced in the database. A completed extraction cannot silently be reviewed again.
- Review actions use ETags and completion is terminal. The UI still needs explicit
  revision controls and identical-decision suppression for saved fields.
- Reprocessing locks the document and rejects conflicting active jobs. Its tracing
  identifier is not durable intent identity: an uncertain retry after completion can
  start another extraction. Upload also needs a separately designed retry contract.
- Upload/reprocess/cancel handlers need synchronous submission guards as well as their
  rendered disabled states. These are follow-up work, not claimed fixed by T05a.
- Question requests already retain a payload/source fingerprint and actor-scoped key;
  the backend serializes same-key requests and reuses saved results. Preserve this
  behavior and verify rapid input/retry controls during T05b.
- Administrative mutations retain their existing version/last-administrator/hold
  rules. Their complete journey verification remains in the R5 security tasks.
