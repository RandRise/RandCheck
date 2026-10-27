# Python API organization

October 27, 2026. This finishes the earlier requested first Python cleanup slice
under the delegated local-completion work. It changes file organization, not the
invoice workflow or extraction policy.

## Change and boundaries

- `apps/api/app/extraction_models.py` owns the existing request/response models,
  field aliases, limits and UTC-deadline validation. Their definitions were moved
  unchanged from `main.py`.
- `extraction_http_errors.py` owns the safe error envelopes, HTTP status mapping
  and bounded request-ID handling. Both handlers use one request-ID helper. A
  duplicate identical timeout mapping was removed without changing its value.
- `main.py` composes the application, middleware, routers and handlers. Existing
  extraction routes, storage checks, authentication, deadlines and provider calls
  retain their behavior and patch points. Existing model imports used by tests
  remain available through `main.py`.

Extraction, providers, cited services, configuration and their models already
have separate modules. Further splitting should follow a concrete change or
maintenance need; this batch does not claim every Python file has been rewritten.
No tests, retained inputs or history were deleted.

## Evidence

Baseline: 1,400 file hashes under
`outputs/local-completion-20261027/python-organization/`. The before/after OpenAPI
documents match exactly. All 102 direct comparisons of old and new error responses
match in status, headers and body, covering known/unknown error codes, retry flags,
valid/invalid request IDs and validation errors. The original function bodies are
loaded only in the verification process; they are not shipped as another API.

Focused worker, contract, service-security and cited-route tests passed: 43 tests,
zero skips, with 28 additional subtest assertions. Critical Ruff checks pass.
The full Python CI group passed against the pre-cleanup source. The same 43 focused
cases pass in Linux with three readonly source overlays and an identical OpenAPI
comparison against that image. These are 43 unique cases on two runtimes, not 86
unique cases. Its first harness attempt had an import-alias error before testing;
the corrected harness passes and the earlier log remains. Independent review
found no material issue. All 1,400 baseline files remain.

A later Pillow security update is a separate dependency batch. This cleanup
clearance does not establish that update's runtime compatibility.

The UI and data are unchanged. When revisiting a real upload, check that processing
still reaches Check details and a failed document still shows a safe error. The
earlier S6 browser journey is dated integration evidence, not a rerun of this batch.
