# Low-reasoning comparison and scope restriction

September 10: low reasoning passed **23/30 end to end**, compared with 19/30
for minimal reasoning. Both use prompt v3, the same nano snapshot, questions,
reference policy, source evidence, retrieval ranks and scores. New run/hit IDs
distinguish attempts. One sample per case cannot establish a statistical or
causal improvement; neither profile passes the Phase 5 release gates.

| Measure | Minimal | Low |
|---|---:|---:|
| End-to-end passes | 19/30 | 23/30 |
| Structurally accepted | 30/30 | 29/30 |
| Claims supported by their own citation set | 24/28 | 21/25 |
| Additional generation token-rate estimate | USD 0.004464 | USD 0.008392 |
| Median reservation-to-settlement time | 4.55 s | 7.16 s |
| p95 reservation-to-settlement time | 6.83 s | 11.03 s |

Timing includes the count/generation/accounting interval, not the full browser
workflow. Minimal used 54,960 input and 4,255 output tokens; low used 54,952 input
and 14,073 output tokens, including 9,024 reasoning tokens. Counts are from the
saved provider outcomes; the failed local preflight is separately retained above.

The low failures are q002 (quotation-number citation for a total), q009
(duplicate citation pairs rejected), q011 (irrelevant subtotal citation added
to otherwise supported total evidence), q013 (missing quotation citation for
an all-three-documents claim), q024 (invoice omitted), q028 (legal conclusion
instead of refusal) and q030 (authorized quotation amount used to answer a
foreign-invoice request). q030 did not retrieve foreign data; the foreign mirror
is absent. This is a refusal/source-attribution failure, not evidence of a leak.

The manual report explicitly distinguishes a supported claim with an irrelevant
extra citation from one whose own citation set cannot support it. Both are
answer-quality failures. Original reports, manual assessments and raw outcomes
remain unchanged; no rejected output is repaired into a successful answer.

## Recorded transport failure and continuation

The original low attempt stopped on q001 because transport validation rebuilt
the request with minimal effort before HTTP. The fixed transport binds the
declared effort through validation and both request bodies. The failed source,
receipt, outcome and [retained-cost policy](../../design/phase-5-local-preflight-hold-v1.md)
are preserved. Its entire USD 0.002 remains HeldUnknown, actual USD null.
An earlier attempt to create replacement retrieval while frozen was denied
without writing a run; its output is also retained.

The continuation manifest `samples/phase-5/answer-low-fixed-v0.3.1/plan.json`
has SHA256 `9695a66bdc7fc3394525d10731537f8d33c326f9a1cc3b1b00d9479cf9a44632`.
Only q001 uses a replacement run; the other 29 original cases had not dispatched.
All 30 continuation operations settled. Current shared state: USD 0.014888
settled plus USD 0.002001 retained unknown, USD 0.016889 accounted against the
unchanged USD 8 ceiling; no active reservations or reconciliation freeze.
123 reservations, 246 artifacts, 371 spending events, 69 vectors, 171 retrieval
runs and 15 migrations remain in the retained database.

All 30 outcomes replayed exactly without provider access, including the same
q009 rejection. Forty accepted citation references resolved through the real
workspace handler to their authorized source/page/quote. Every table fingerprint
matched across replay. Correct navigation does not imply relevant citations.

## Follow-up scope restriction

After freezing the comparison, `cited-scope-restriction-v1` now requires an empty
InsufficientEvidence/unsupported_scope response for detected English requests
about legal/input-tax determinations, another organization/tenant, or secrets.
The coordinator applies it to new proposals and saved replays. It rejects an
invalid answer; it does not rewrite the model output into a refusal.

Actual saved q028 and q030 answers are now rejected on offline replay; the valid
q029 refusal remains accepted. Every database fingerprint is unchanged. These
checks do not turn historical failures into passes or establish general semantic
support, multilingual detection, production authorization or tenant isolation.
No public generation or UI answer publication is enabled.

Verification includes 234 application tests after the scope change; the other
267 non-PostgreSQL backend tests passed earlier in the same increment. Selected
PostgreSQL checks passed: eight semantic retrieval, 24 generation, nine retained
hold. Python: 168 cited tests, including the OpenAI transport suite; 13 report
tests. Runner and documentation builds and whitespace checks pass. The final
remaining work is source-aware answer/citation binding and complete comparisons,
then another versioned quality run, public integration and broader security/UI
acceptance. No Phase 5 completion claim is made.
