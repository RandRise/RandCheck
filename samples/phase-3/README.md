# Phase 3 evaluation data

`smoke-v0.1.0` is a two-sample synthetic contract smoke set. It reuses the
licensed Phase 2 native and scanned invoice assets to verify the Phase 3
dataset, prediction, metric, breakdown, and report contracts.

The `schemas` directory contains self-contained JSON Schema Draft 2020-12
contracts for datasets, prediction sets, reports, thresholds, comparisons,
source registers, annotation ledgers, and readiness audits. Runtime validation
remains dependency-free and adds semantic checks that JSON Schema cannot
express, including unique field identities and dataset/source agreement.

It is deliberately marked `claim_eligible: false`. Its results are not client,
production, OCR-accuracy, or real-world performance evidence. A later dataset
version must meet the Phase 3 dataset plan before any performance claim can be
published.

The [UAE FTA field-coverage development pack v0.2.0](../../output/pdf/uae-invoice-development-v0.2.0/README.md)
adds nine UAE-specific PDFs, conditional tax scenarios, complete line-level
calculations, and an FTA requirement matrix. The earlier
[v0.1.0 pack](../../output/pdf/uae-invoice-development-v0.1.0/README.md) remains
the initial eight-field baseline. Both are explicitly non-claim and do not
replace the future rights-cleared real UAE benchmark.

Run the smoke evaluation from the repository root:

```powershell
python tools/phase3_evaluate.py `
  --dataset samples/phase-3/smoke-v0.1.0/dataset.json `
  --predictions samples/phase-3/smoke-v0.1.0/local-predictions.json `
  --output outputs/phase-3/smoke-v0.1.0-report.json
```

The prediction values are copied from the committed Phase 2 local-provider
goldens. Timing is intentionally null because golden fixture values are not
latency measurements.

Generate live predictions for the UAE development pack inside the pinned Phase
2 runtime, then evaluate them:

```powershell
python tools/phase3_predict.py `
  --dataset output/pdf/uae-invoice-development-v0.2.0/dataset.json `
  --prediction-set-id phase2-local-uae-development-v0.2.0 `
  --provider local `
  --output outputs/phase-3/uae-development-v0.2.0/predictions.json

python tools/phase3_evaluate.py `
  --dataset output/pdf/uae-invoice-development-v0.2.0/dataset.json `
  --predictions outputs/phase-3/uae-development-v0.2.0/predictions.json `
  --output outputs/phase-3/uae-development-v0.2.0/report.json
```

The host must provide the exact pinned dependencies and Tesseract models. The
repository Docker AI image is the reference runtime. Results from this pack
remain synthetic development evidence regardless of the score.

Run the deterministic comparison smoke check:

```powershell
python tools/phase3_compare.py `
  --baseline outputs/phase-3/smoke-v0.1.0-report.json `
  --candidate outputs/phase-3/smoke-v0.1.0-report.json `
  --thresholds samples/phase-3/smoke-v0.1.0/strict-thresholds.json `
  --output outputs/phase-3/smoke-v0.1.0-self-comparison.json
```

This self-comparison validates the comparison and threshold contracts. It is
not a provider improvement claim or an approved release threshold for future
datasets.

Audit source and annotation readiness:

```powershell
python tools/phase3_dataset_audit.py `
  --dataset samples/phase-3/smoke-v0.1.0/dataset.json `
  --source-register samples/phase-3/smoke-v0.1.0/source-register.json `
  --annotation-ledger samples/phase-3/smoke-v0.1.0/annotation-ledger.json `
  --output outputs/phase-3/smoke-v0.1.0-readiness-audit.json
```

The expected exit is `4`: the inputs are structurally valid, but the dataset
and both sources are explicitly synthetic and therefore not claim-ready.
