# Phase 7 mixed OCR refinement — September 22, 2026

The opt-in `best` / `eng+ara` / `lines-v1` profile fixes both September 21 failures.
The original nine cases now pass 9/9, with identical source images and expected
values. The expanded twelve-case set passes 11/12. Phase 7 remains in progress.

| Scope | Passed | Failed | Failure rate |
| --- | --- | --- | --- |
| Original v0.2.0 cases | 9/9 | 0 | 0% |
| Added v0.3.0 variations | 2/3 | 1 | 33.3% |
| Expanded set | 11/12 | 1 | 8.3% |
| Arabic group, expanded set | 5/5 | 0 | 0% |
| Mixed group, expanded set | 5/6 | 1 | 16.7% |
| English control | 1/1 | 0 | 0% |

These are development examples, not estimates of real-invoice accuracy. A case
passes only if every expected field, absence/warning rule, language and exact
evidence check passes. The final evaluator scores actual pipeline fields and
warnings instead of reparsing canonical text. Receipt:
[final twelve-case run](./P7-lines-final-20260922.json).

## What changed

Whole-page mixed OCR misread `يوماً` as `log,` and scrambled a crowded totals row.
Line-crop diagnostics recovered the totals, and an Arabic-only line pass recovered
the word. The extractor now offers bounded retries for suspicious Arabic lines:
low-confidence Latin tokens, separator-delimited rows, or trailing labels/colons.
Clean lines retain their original output. Numeric runs, leading zeroes and
protected Latin identifiers/words must survive unchanged before a retry is accepted.
An Arabic-only alternative also needs a confidence improvement.

Coordinates from each crop are translated back to the page before canonical text
and citation spans are constructed. Engine token order is preserved. Overlapping
line regions or more than 80 detected lines keep the block output. Retries share
one page deadline; deadline expiry remains a safe retryable extraction error.
No dictionary replacement, label guessing or amount repair was added.

The application now accepts the exact best Arabic model pin through
`OCR_ARABIC_MODEL=best`; `OCR_LAYOUT_PROFILE=lines-v1` requires an explicitly mixed
OCR language. Provider metadata records `local-1.5.0`, model choice and layout
profile. English/fast/block defaults and demo configuration remain unchanged.
The best model must be supplied at the verified runtime model path; the default
fast-model image will reject a mismatched best configuration. Arabic OCR continues
to produce a degraded review warning, even when these checks pass.

## Additional test and renderer finding

v0.3.0 preserves the nine existing cases and adds different IDs, amounts and
payment duration, reordered totals, and genuine English payment wording under
an Arabic label. Expectations were text-checked before OCR:
[12/12 text cases](./P7-text-v03-20260922.json).

The added English phrase exposed a rendering defect: `isolated-v2` isolated each
word separately and visibly produced `days 45 Net`. `isolated-v3` keeps the whole
Latin phrase together. Visual inspection verified `Net 45 days` in the final PNG.
All nine original PNG hashes are unchanged between v2 and v3. The citation
component uses the same phrase-isolation rule, preserving exact text and highlights;
its regression tests include English phrases inside Arabic paragraphs.
This renderer correction is separate from the OCR improvement.

The remaining `mixed-latin-payment` failure persists with the corrected image:
invoice number `INV-019`, TRN `000000000000702`, currency `AED` and total `315.75`
are correct, but the Arabic payment label is damaged, so `payment_terms` is absent.
Do not count the numeric fields' success as a complete document pass. The next
OCR investigation should consider script-aware label/value regions, with original
coordinates and conservative handling of genuine Latin text.

## Failed attempts retained

- [Unconditional line rereading](./P7-lines-first-20260922.json) fixed the original
  mixed failures but regressed currency/identifier recognition elsewhere.
- [Targeted retries](./P7-lines-targeted-20260922.json) passed the original 9/9.
- [First expanded run](./P7-lines-expanded-20260922.json) exposed the new phrase case.
- [Subsequent v2 run](./P7-lines-timeout-20260922.json) recorded one `provider_timeout`
  on `ar-basic`, as well as the phrase failure. The timeout is not an OCR accuracy
  failure and is not erased by the later successful run. No timeout increase was used.
- [Original line diagnostics](./P7-lines-diagnostic-20260922.json) and
  [English phrase diagnostics](./P7-latin-diagnostic-20260922.json) retain raw tokens,
  confidence and image coordinates.

All runs used the pinned September 21 evaluation image, offline, with the same
hash-verified best Arabic weights. Raw PNGs remain in `outputs/phase-7/`.
The final run completed without timeouts, with recorded extraction durations of
about 2.4–15.7 seconds per image on this host. This is not a performance benchmark.

## Verification and remaining scope

59 focused Python tests and 43 subtests pass, with six previously gated integration
skips. All 12 canonical-text cases pass. The citation/display test run passes
21 tests. TypeScript, focused ESLint, isolated Python E4/E7/E9/F lint, the
whitespace check and documentation build pass. The documentation build retains
its existing large-chunk warning.

Focused Python checks cover numeric/identifier preservation, crop coordinates,
exact UTF-8 spans, empty/overlapping-line fallback, configuration and model pins,
and shared retry deadlines. Browser component checks cover unchanged evidence
text, highlights crossing runs and whole English phrases. No new browser screenshot
session or persisted backend journey is claimed for this increment.

Native/multipage and realistic invoice testing, persisted language review/answer
flows, broader RTL history/reconciliation and multilingual semantic retrieval
remain open. No paid call, retained database change, commit or push was made.
Docker was used only for offline synthetic evaluation and stopped afterward.
