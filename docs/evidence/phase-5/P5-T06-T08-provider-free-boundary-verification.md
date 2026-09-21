# P5-T06/T08 — Provider-free answer and budget boundaries

**Date:** 2026-09-08  
**Result:** First preparation slice implemented; no paid transport or public
generated-answer workflow. Phase 5 and all live-readiness gates remain open.

## What changed

The [roadmap](../../roadmap.md) now explicitly
tracks spending, credentials, model lifecycle, answer quality, roles/isolation,
logs/storage/backups, real-data evaluation and optional paid-model entitlements.
The user authorized continuing provider-free Phase 5 work with these gates kept
visible. This does not approve the proposed server-log deployment policy.

### Budget preflight — ASP.NET application layer

`CitedGenerationBudgetPolicy` is a pure calculation, not a ledger or dispatcher.
It defaults to disabled and, when explicitly exercised, validates:

- Server-supplied budget identity, USD currency, nonnegative spending and a
  maximum pilot admission ceiling of USD 8, within the user's USD 10 budget.
- Provider/model/rate versions and an inclusive-start/exclusive-end validity
  window. Future configuration must bound that window by model retirement.
- At most 8,000 input and 4,000 output tokens; the allowance must cover billable
  reasoning as well as visible output.
- No in-flight call, unresolved outstanding reservation or reconciliation flag.
- A maximum-rate cost rounded upward to one microdollar; an exact ceiling is
  allowed, but any excess is rejected. Invalid/overflowing input fails closed.

The quote retains budget/model/rate identities and token limits. No actual
provider price is configured; test rates are fictional inputs to arithmetic.
This code neither records spending nor authorizes dispatch by itself.

### Answer envelope — private Python component

`app/cited_answers.py` adds immutable context, evidence, model, request and
untrusted-result types, a protocol and an explicitly disabled default adapter.
Only tests supply a fake adapter. There is no new FastAPI route or SDK import.

- Request preparation checks canonical nonempty IDs, consistent organization
  and retrieval-run IDs, unique hits/spans, UTF-8 content hashes, up to 20 hits,
  a 4,000-UTF-16-unit question and a 64 KiB aggregate serialized data limit.
- A deterministic SHA-256 binding includes organization, actor, retrieval run,
  model/version, output allowance and exact question/evidence data. This binds
  a result to a request; it is not a signature or authorization credential.
- Returned envelopes must match model and request binding, contain bounded
  UTF-8 bytes and integer usage within the declared limits. Payloads are omitted
  from normal dataclass representations.
- The adapter is called at most once per invocation, with a 30-second cooperative
  deadline and no retries. Cancellation propagates; other errors become fixed
  safe errors. A shortened real deadline test verifies cancellation of a waiter.
- Provider refusal/truncation is not relabelled as an evidence-based refusal.
  `semantic_support_established` is always false. An envelope containing invalid
  JSON deliberately passes this transport boundary: the existing ASP.NET
  structural validator, and a later semantic gate, must reject it before use.

The two components are not yet connected through HTTP or a spending ledger.
Existing public APIs remain evidence-only. No reconciliation rules, schema,
migration, frontend, authentication or deployed logging settings changed.

## Verification

| Check | Result |
| --- | --- |
| New .NET budget cases | 14 passed |
| .NET application suite, including existing citation/no-hit tests | 102 passed |
| .NET domain / infrastructure / architecture / API | 111 / 52 / 4 / 78 passed |
| Non-persistence .NET total | 347 passed; zero failures/skips |
| New Python answer-boundary tests | 12 passed, with additional subcases |
| Python answer/chunking/embedding/question-evaluation modules | 36 passed in the existing native-PDF runtime |
| Python cited route module | 7 passed in the existing project `.venv` |
| Python cited total across the two documented runtimes | 43 distinct tests passed; not a single-environment full-suite run |

Raw .NET reports are under `tmp/phase5-test-results/phase5-answer-boundary-*.trx`.
The initial focused report is `phase5-budget-preflight.trx`. Python results are
recorded above with reproducible commands below; no fabricated raw report is
presented. Full PostgreSQL, OCR/Python discovery, frontend/browser acceptance and
migration checks were not rerun in this component-only increment. The prior
385-test full .NET/database result remains historical, not a new combined total.

### Python environment finding

An attempted combined cited run in `tmp/phase5-native-runtime-20260908` passed
36 tests but failed to import the route module because its test client requires
missing `httpx2`. A combined run in `.venv` executed 43 tests: 40 passed, with
one error and two assertion failures caused by missing `pdfplumber`. The final
split runs cover all 43 cited tests using the appropriate existing environments.
The route environment also emits an `httpx` deprecation warning. No packages,
versions or environments were changed; a reproducible unified runtime remains
an explicit pre-release follow-up, not a claimed fix.

## What this does not prove

- **No enforced spending ceiling yet.** A future PostgreSQL transaction must
  reload state, calculate and persist the reservation atomically before dispatch.
  Concurrent callers, duplicate jobs, process crashes and unknown charges still
  require durable-ledger integration and tests. Repeated pure quotes are not
  replay protection; separate fake-adapter invocations may each call the fake.
- **No pre-dispatch token measurement yet.** Byte limits are not token limits.
  Full prompt/schema/evidence token accounting and streaming transport bounds
  must be enforced before a live call. Post-response usage checks are too late
  to prevent a charge; failures must retain the reservation for reconciliation.
- **No authentication guarantee.** Python checks consistency of server-supplied
  IDs, not who a user is. ASP.NET must authorize the frozen evidence, validate
  current eligibility and citations, and recheck access before publication.
- **No grounded generated answers.** JSON-encoded evidence is not an injection
  defense. Prompt construction, conflict handling, exact calculations, semantic
  support, source revocation and adversarial/UI acceptance remain open.
- **No paid-model quality, privacy or billing proof.** No live credentials,
  provider calls, premium entitlements or raw-log deployment controls were added.

## Reproduction and next increment

From the repository root, run the application test project with `dotnet test`;
run the other four non-persistence test projects for the 347-case regression.
Use distinct TRX filenames per project as recorded above.

```powershell
$env:PYTHONPATH='apps/api;apps/api/tests'
& 'tmp/phase5-native-runtime-20260908/Scripts/python.exe' -m unittest -v test_cited_answers test_cited_chunking test_cited_embeddings test_cited_question_evaluation
```

```powershell
$env:PYTHONPATH='apps/api'
& '.venv/Scripts/python.exe' -m unittest discover -s apps/api/tests -p test_cited_routes.py -v
```

Next: implement the durable pilot-spending ledger and transactional reservation
workflow, then connect the preparation components through the private service
boundary with fake-provider integration tests. Keep live dispatch and public
generated answers disabled until the roadmap gates pass.
