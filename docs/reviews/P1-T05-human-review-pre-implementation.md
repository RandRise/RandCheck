# P1-T05 human-review contract review

**Date:** 2026-08-11  
**Task:** P1-T05 — Human-review contract and pre-implementation review  
**Status:** Complete; contract implemented and Integration Guardian review passed with no blockers

## Approved contract

### Endpoints

- `GET /api/documents/{documentId}/review` returns the current review workspace.
- `POST /api/documents/{documentId}/reviews` creates revision 1 or returns the existing active session.
- `POST /api/documents/{documentId}/reviews/{reviewId}/actions` records one append-only field decision.
- `POST /api/documents/{documentId}/reviews/{reviewId}/complete` completes a review only after every machine field has a decision.

### Field decisions

- `Confirmed` retains the immutable machine value.
- `Corrected` references a machine field and supplies a reviewed value, value type, and reason.
- `Added` supplies a new field identity, reviewed value, value type, and reason without a machine-field reference.
- `Cleared` removes a machine or active reviewer-added field from the effective result and requires a reason.
- The workspace returns machine, latest decision, and effective reviewed values separately.

### Concurrency and failures

- Review mutations require a strong `If-Match` ETag.
- A successful mutation rotates the ETag/concurrency token.
- Missing, malformed, and stale preconditions produce explicit problem responses.
- Not-found, validation, invalid-state, incomplete-review, and concurrent-review cases use safe stable error codes.
- Machine output is never overwritten by a review action.

### Scope boundary

- Phase 1 uses the fixed development organization/system actor.
- Authentication, memberships, roles, and tenant-aware authorization remain Phase 6 work.
- Reconciliation, RAG, Arabic OCR, and autonomous approval remain outside Phase 1.

## Review gate outcome

- [x] Request/response contracts and state transitions agreed.
- [x] Effective reviewed-value and stale-edit behavior agreed.
- [x] Integration Guardian reviewed aggregate invariants, persistence, DTO compatibility, API behavior, and UI integration.
- [x] Quality Engineer implemented domain, application, persistence, API, infrastructure, and frontend coverage.
- [x] No additional material product decision was required from Rand.
- [x] P1-T06 and P1-T07 implementation completed.
- [x] Integration Guardian final verdict: **APPROVAL-READY; no blockers**.

## Implemented findings

- Review revision and active-session rules remain database and domain enforced.
- Completion rejects undecided machine fields without changing session state.
- Effective values are derived from the latest append-only decision.
- The frontend keeps machine and reviewed values visible as separate concepts.
- Request helpers attach `If-Match` and retain rotated response ETags.
- `20260811073607_AddReviewActionValueTypeConstraint` constrains persisted corrected value types.

See the [P1-T08 acceptance evidence](../evidence/phase-1/P1-T08-acceptance-verification.md) for final tests, builds, live workflow results, and limitations.

