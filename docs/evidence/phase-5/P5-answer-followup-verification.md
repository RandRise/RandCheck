# Prompt v3 answer evaluation

The September 10 thirty-case evaluation completed with **19/30 end-to-end passes**.
All 30 proposals passed structure and citation identity checks. Manual review by
Codex against actual cited passages found 24/28 claims fully supported by their
own citations. This is not an independent human assessment or an automated
semantic gate. Public generation remains disabled.

Failures retained in the report:

- q013 falsely refuses the three-document supplier-TRN comparison.
- q014 and q017 omit invoice facts required by the frozen full comparison.
- q015 omits the purchase-order citation for an arithmetic operand.
- q019 incorrectly calls the tax-inclusive line total “before VAT.”
- q022 returns the separately labeled TIN rather than the reference supplier TRN.
- q024 has an unsupported comparison citation, repetition and incomplete coverage.
- q027 returns an absence claim as Answered instead of InsufficientEvidence; it
  does not invent a numeric exchange rate.
- q028–q030 refuse without claims but return the wrong reference reason.

q025 now refuses the payment-date question correctly. All 24 answerable cases
have full retrieval-label coverage. These facts do not override the remaining
answer failures. The original failed 4/10 baseline and its labels remain intact.

Thirty calls cost an additional USD 0.004464 by provider token-rate estimates.
Shared settled spending is USD 0.006496 plus USD 0.000001 retained unknown under
the unchanged USD 8 ceiling. All 30 operations settled. All 30 offline replays
matched outputs; 33 citation references resolved through the authorized workspace
page-navigation handler with matching source, page and exact quote. Every table
fingerprint remained unchanged across replay. There are 92 reservations and
184 artifacts at this checkpoint. No paid call was repeated.

Artifacts: `outputs/phase-5/answer-followup-20260910/`, including the frozen
digest-bound manual assessment, report, generation outputs, ledger and replay.
Four new report tests verify output binding, citations, incomplete assessments,
refusal reasons and the distinction between structural and semantic acceptance.

Next is the [controlled low-reasoning comparison](../../design/phase-5-answer-low-v3.md).
This changes effort on the same model, preserving prompt and reference labels.
