# Phase 3 — Human review and evaluation

**Status:** Complete; development-only boundary approved by Rand on 2026-08-18
**Outcome:** Development extraction quality is measurable, reproducible, and improved through reviewer corrections.

The [Phase 3 task ledger](../tasks/phase-3.md) records the active work. The
[evaluation contract](../design/phase-3-evaluation-contract.md),
[dataset plan](../data/phase-3-dataset-plan.md), and
[quality plan](../testing/phase-3-quality-plan.md) are approved. The offline
evaluator/comparator and live prediction bridge are verified. Rand selected the
generated UAE FTA field-coverage pack as the Phase 3 development benchmark on
2026-08-18. It remains synthetic and is not real-world performance evidence.

## Work

- Store machine output immutably by extraction run.
- Store reviewer corrections with user, timestamp, and reason.
- Highlight missing and low-confidence fields.
- Build a versioned labeled invoice dataset.
- Add an evaluation command that can run without the product UI.
- Calculate exact match, precision, recall, correction rate, latency, failure rate, and cost.
- Break results down by field, layout, scan type, and provider version.
- Publish evaluation methodology with the results.

## Acceptance criteria

- [x] Machine output is never overwritten by a correction.
- [x] Review history is reconstructable through existing append-only sessions/actions.
- [x] Dataset version and sample count appear in every evaluator report.
- [x] The initial smoke evaluation can be repeated from documented steps.
- [x] Field-level results are reported rather than hidden in one average.
- [x] The synthetic smoke demonstration is explicitly excluded from client performance.
- [x] Phase 2 provider output can be frozen directly into the Phase 3 prediction contract.
- [x] Missing and low-confidence fields are visible without hiding machine values or evidence.
- [x] Observed review-action metrics remain distinct from evaluator correction rate.
- [x] Full append-only review history is exportable and reconstructable.
- [x] Frozen before/after reports pass the predeclared synthetic development thresholds.
- [x] Error analysis identifies the remaining degraded-scan miss.

The development implementation gate is satisfied and Rand approved Phase 3 on
2026-08-18. The generated pack remains `claim_eligible: false`, so the results
cannot support client, production, or real UAE invoice accuracy claims.

## Change record

### 2026-08-18 — Synthetic development completion boundary

- Previous decision: Phase completion required a claim-eligible real English invoice dataset.
- New decision: Use the generated UAE FTA field-coverage pack to complete the development boundary.
- Reason: Rand explicitly selected the generated UAE invoices for the remaining Phase 3 work.
- Effect on scope: Live integration, review workflow, analytics, comparisons, thresholds, and acceptance are included; real-world accuracy claims are excluded.
- Effect on later phases: Phase 4 may use reviewed synthetic document values, while real-document validation remains future work.
- Decision-log reference: D-015.
- Completion approval: Rand confirmed the replaceable synthetic dataset boundary on 2026-08-18.

## Portfolio evidence

- Versioned evaluation report
- Confusion/error analysis
- Before-and-after provider or prompt comparison
- Review UI showing correction provenance
