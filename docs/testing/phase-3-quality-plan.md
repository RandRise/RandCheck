# Phase 3 quality plan

**Status:** Approved foundation on 2026-08-13  
**Execution status:** Complete; development-only boundary approved by Rand on 2026-08-18

All P0 evaluator tests run offline. Live providers and paid credentials cannot
gate Phase 3 acceptance.

## Test matrix

| Priority | Layer | Required checks |
|---|---|---|
| P0 | Dataset contract | Version/identity, duplicate samples/fields, source path containment, SHA-256, license/synthetic metadata, claim exclusion, claim-eligible synthetic rejection |
| P0 | Prediction contract | Dataset match, duplicate/unknown/missing runs, provider versions, field identity, outcome, nullable duration/cost, non-finite and negative values |
| P0 | Metrics | Exact type/value, normalized-value precedence, precision, recall, correction, failures, missing/unexpected fields, null denominators |
| P0 | Breakdowns | Field, layout, scan type, and exact provider version retain counts and expose weak segments |
| P0 | Reproducibility | Same logical inputs generate the same canonical fingerprints and byte-identical report |
| P0 | Schemas | Draft 2020-12 schema inventory, unique stable IDs, local reference integrity, and version alignment with executable validators |
| P0 | Comparison | Exact dataset fingerprint match, metric direction, shared/added/removed segments, deterministic deltas, and malformed-report rejection |
| P0 | Thresholds | Floors, ceilings, maximum regression, null handling, safe invalid-input exit, and distinct threshold-failure exit |
| P0 | Claims | Dataset ID/version/sample count and claim boundary appear in every report; synthetic smoke is explicitly ineligible |
| P1 | Review workflow | Missing/low-confidence cues; machine/effective value separation; immutable action provenance; history reconstruction |
| P1 | Comparative evaluation | Frozen prediction sets, before/after deltas, regression identification, approved thresholds, and error analysis |

## Current verified slice

- Twenty-one focused Phase 3 tests cover metric arithmetic, breakdown axes,
  missing predictions, normalization/type comparison, duplicate identity and
  version rejection, claim boundaries, source containment/hash checks, formal
  schema alignment, comparison direction, threshold pass/fail behavior, CLI
  exit contracts, determinism, and safe malformed-input rejection.
- Twelve P3-T03 tests cover exact source/annotation closure, rights and privacy
  blockers, restricted storage, final-label hashes, independent reviewers,
  reviewer sequencing, adjudicated revisions, deterministic audit output, and
  safe `0`/`2`/`4` CLI exit behavior.
- The UI-independent CLI successfully writes the committed two-sample smoke
  report.
- The comparison CLI writes deterministic overall/segment deltas, evaluates
  four strict smoke rules, and distinguishes invalid input (`2`) from a valid
  threshold failure (`3`).
- Latency and cost are null with zero coverage in the smoke prediction set,
  rather than copying deterministic golden-fixture placeholders into a
  performance measurement.

The generated UAE v0.2.0 pack now also verifies live prediction generation,
native/OCR provider execution, before/after comparison, development thresholds,
review cues, observed action analytics, and reconstructable history. These are
development results only and do not approve real-world performance claims.

## Release gate

- [x] Dataset and prediction contracts are approved and schema-negative tests pass.
- [x] The generated English UAE development dataset passes source, annotation, adjudication, and leakage checks while retaining its synthetic blockers.
- [x] Reports are reproducible from documented commands and retain all failed samples.
- [x] Field/layout/scan/provider breakdowns and error analysis identify weak areas.
- [x] Frozen development thresholds are evaluated against a separately retained baseline.
- [x] Review UI exposes missing/low-confidence cues and immutable correction provenance.
- [x] Observed review correction analytics are labeled separately from truth-derived evaluator correction rate.
- [x] Before/after comparison identifies improvements and would surface regressions.
- [x] Full Python, .NET, web, and documentation regressions pass.
- [x] Acceptance evidence contains no synthetic or client-performance overclaim.
- [x] Rand approves Phase 3 completion for the D-015 development-only boundary.
