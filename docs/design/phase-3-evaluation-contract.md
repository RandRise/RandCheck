# Phase 3 evaluation contract

**Status:** Approved by Rand on 2026-08-13  
**Version:** 1.0  
**Task:** P3-T01

This contract defines how a labeled dataset and a frozen prediction set become
a reproducible extraction-quality report. It does not define a provider, alter
machine output, or turn synthetic fixtures into performance evidence.

## Ownership and boundaries

- Python owns offline dataset validation, metric calculation, breakdowns, and
  deterministic JSON reports.
- ASP.NET remains the exclusive owner of transactional documents, immutable
  extraction runs, and append-only review history.
- The Next.js workspace may display review cues and provenance, but the
  evaluator must run without the product UI.
- English invoices are the Phase 3 scope. Arabic and mixed-language accuracy
  remain Phase 7 work.
- The current two-sample smoke bundle is contract evidence only. It is not a
  benchmark and cannot support a client, production, or real-world accuracy
  claim.

## Versioned inputs

### Dataset 1.0

Every dataset manifest contains:

- `dataset_id`, `dataset_version`, `purpose`, and a non-empty sample list;
- `claim_eligible`, plus a required exclusion reason when false;
- a named and versioned normalization policy;
- a stable `sample_id`, source path, SHA-256, license, and synthetic flag for
  each sample;
- layout, scan-type, and language segments;
- an exhaustive reviewed field list for the evaluation schema.

Field identity is `(key, occurrence)`. Duplicate identities are invalid. A
claim-eligible dataset cannot contain a source marked synthetic. The CLI also
checks that each relative source stays inside the repository and matches its
recorded SHA-256.

### Prediction set 1.0

A frozen prediction set identifies its dataset ID/version and contains at most
one run for each sample. Every run records its outcome, exact provider adapter,
model and versions, fields, nullable provider duration, and nullable estimated
cost/currency. A missing run is retained as an evaluation failure rather than
silently dropping the sample.

Unknown samples, duplicate runs, mismatched dataset versions, duplicate field
identities, invalid durations, and invalid costs are rejected.

### Report 1.0

Every report includes:

- evaluator version;
- dataset ID, version, purpose, sample count, and claim boundary;
- prediction-set ID;
- canonical SHA-256 fingerprints of both JSON inputs;
- aggregate metrics and field/layout/scan/provider-version breakdowns;
- normalization and metric methodology.

No wall-clock generation timestamp is included, so identical logical JSON
inputs produce byte-for-byte identical reports.

### Machine-readable schemas

Self-contained JSON Schema Draft 2020-12 artifacts define dataset, prediction,
report, threshold, and comparison formats under `samples/phase-3/schemas`.
They use stable URN identifiers and local references. The dependency-free
runtime validators additionally enforce relationships that JSON Schema cannot
express reliably, including unique field identities, source SHA-256 checks,
and exact dataset agreement between inputs.

## Value comparison policy

The first normalization policy is `strict-normalized-value` version `1.0`:

1. Compare `normalized_value` when present; otherwise compare `value`.
2. Normalize the selected text to Unicode NFC.
3. Require `value_type` and selected text to match exactly.
4. Do not trim, case-fold, remove punctuation, apply numeric tolerances, or
   perform provider-specific transformations inside evaluation.

Changing these rules requires a new normalization version. Labels must never
be changed merely to make a provider score better.

## Metric definitions

Let `TP` be exact predictions, `I` be aligned identities with an incorrect type
or value, `M` be missing ground-truth identities, and `U` be unexpected
prediction identities.

| Metric | Definition |
|---|---|
| Field value exact match | `TP / (TP + I)`; value accuracy where the identity was predicted |
| Document exact match | Ready documents with no `I`, `M`, or `U`, divided by all samples |
| Precision | `TP / (TP + I + U)` |
| Recall | `TP / (TP + I + M)` |
| Correction rate | `(I + M + U) / (TP + I + M + U)` |
| Failure rate | Missing or non-Ready runs divided by all samples |
| Latency | Mean, p50, and nearest-rank p95 over non-null provider duration |
| Cost | Sum by currency plus measurement-coverage rate; currencies are never converted |

The report's correction rate estimates review actions required against ground
truth: correct an incorrect value, add a missing value, or clear an unexpected
value. Observed reviewer correction rate from real append-only review actions
is a separate product-workflow measure and must be labeled separately.

Undefined ratios are JSON `null`, not zero. Latency and cost always include a
measured-sample count and coverage rate so missing telemetry cannot masquerade
as free or instantaneous processing.

## Breakdown policy

The same calculations run for:

- each field key;
- each declared layout;
- each scan type;
- each exact provider adapter/model/version combination.

Overall averages may be reported only beside field-level results. Segment
counts remain visible because a perfect result on one sample is not equivalent
to a well-supported result.

## Before/after comparison and thresholds

Only reports with the same dataset ID, version, sample count, canonical dataset
SHA-256, and normalization policy may be compared. The comparator records
canonical hashes for both reports and identifies the baseline/candidate
prediction sets and provider versions.

Comparable metrics are emitted overall and for shared field, layout, and scan
groups. Groups present on only one side are listed rather than silently
dropped. Provider-version groups are identified but not paired because changing
the provider/model version is normally the subject of the comparison.

Metric direction is explicit:

- exact match, precision, and recall are higher-is-better;
- correction rate, failure rate, and latency are lower-is-better.

Every delta is `candidate - baseline`. `favorable_delta` reverses lower-is-
better metrics so a positive value always means improvement.

A versioned threshold rule selects `overall` or one field/layout/scan group and
may define a minimum candidate value, maximum candidate value, and/or maximum
unfavorable regression. Null candidate metrics fail an applicable rule. A
threshold failure still writes the complete comparison and exits with code `3`;
invalid input exits with code `2`. Thresholds become release gates only after
approval against a claim-eligible dataset. The strict smoke thresholds validate
the mechanism and are not production thresholds.

## Current implementation

Run from the repository root:

```powershell
python tools/phase3_evaluate.py `
  --dataset samples/phase-3/smoke-v0.1.0/dataset.json `
  --predictions samples/phase-3/smoke-v0.1.0/local-predictions.json `
  --output outputs/phase-3/smoke-v0.1.0-report.json
```

Compare two reports from the same frozen dataset:

```powershell
python tools/phase3_compare.py `
  --baseline outputs/phase-3/baseline.json `
  --candidate outputs/phase-3/candidate.json `
  --thresholds path/to/approved-thresholds.json `
  --output outputs/phase-3/comparison.json
```

The implementation validates and evaluates frozen predictions, generates them
from the Phase 2 local provider, and compares versioned reports. The generated
UAE development pack exercises this path together with review cues, action
analytics, history export, and development thresholds. Statistical uncertainty
and any claim-eligible real-document benchmark remain future validation work.
