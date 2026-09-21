# Phase 3 startup verification

**Date:** 2026-08-13  
**Scope:** P3-T01/P3-T02 initial offline evaluation slice  
**Result:** Startup slice verified; Phase 3 remains in progress

## Delivered

- Dataset, prediction, and report contracts with explicit versions.
- Strict field comparison and exact-match, precision, recall, correction,
  failure, latency, and cost calculations.
- Field, layout, scan-type, and exact provider-version breakdowns.
- Dataset/version/sample identity, claim boundary, methodology, coverage, and
  canonical input fingerprints in every report.
- Repository-contained source path and SHA-256 verification.
- A UI-independent CLI and two-sample synthetic smoke bundle.
- Five self-contained JSON Schema Draft 2020-12 contracts with stable URNs and
  verified local references.
- Deterministic before/after comparison with explicit metric direction, segment
  changes, configurable thresholds, and distinct invalid/threshold-failure exit
  codes.

## Commands and results

```powershell
$env:PYTHONPATH='apps/api'
python -m unittest discover -s apps/api/tests -p 'test*evaluation*.py' -v
```

Result: **21/21 passed** across evaluator, comparison, schema, and CLI modules.

```powershell
python tools/phase3_evaluate.py `
  --dataset samples/phase-3/smoke-v0.1.0/dataset.json `
  --predictions samples/phase-3/smoke-v0.1.0/local-predictions.json `
  --output outputs/phase-3/smoke-v0.1.0-report.json
```

Result: report written successfully for dataset version `0.1.0`, sample count
2, with all breakdown axes present. Two consecutive final runs were
byte-identical with report SHA-256
`7e8b010c14aab05136e83dbe1668137537b7853c4495f483ffd68b46d8c9509a`.

```powershell
python tools/phase3_compare.py `
  --baseline outputs/phase-3/smoke-v0.1.0-report.json `
  --candidate outputs/phase-3/smoke-v0.1.0-report.json `
  --thresholds samples/phase-3/smoke-v0.1.0/strict-thresholds.json `
  --output outputs/phase-3/smoke-v0.1.0-self-comparison.json
```

Result: four smoke rules passed. Repeated output was byte-identical with
comparison SHA-256
`e0d2534dbd6629cd5ed7b7195db1f6ad534ec63be53816508296d16ce33227b2`.
Tests also verify that a valid regression writes its comparison and exits `3`.

```powershell
$env:PYTHONPATH='apps/api'
python -m unittest discover -s apps/api/tests
```

Result: **92 discovered; 87 passed and 5 expected host-runtime OCR skips**.

```powershell
npm run build --prefix docs
```

Result: VitePress build completed successfully.

## Claim boundary

The smoke report's perfect field values reproduce two committed synthetic
goldens. They are evaluator-contract evidence only. Timing/cost coverage is
zero, the dataset is marked `claim_eligible: false`, and the result is not
client, production, OCR-accuracy, or real-world performance evidence.

## Remaining gate

The contract still needs review, and Phase 3 still requires a claim-eligible
labeled dataset, review UI cues, observed correction analytics, real frozen
provider comparisons, approved benchmark thresholds, error analysis, full
cross-stack regressions, and final acceptance evidence.
