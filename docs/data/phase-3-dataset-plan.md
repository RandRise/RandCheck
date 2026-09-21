# Phase 3 evaluation dataset plan

**Status:** Approved foundation on 2026-08-13  
**Task:** P3-T01

Operational details are fixed in the
[source intake contract](./phase-3-source-intake.md) and
[annotation guide](./phase-3-annotation-guide.md).

## Goal

Build a legally usable, versioned English-invoice dataset that exposes weak
fields and document segments without presenting synthetic contract behavior as
client performance.

## Dataset classes

| Class | Purpose | Claim eligible |
|---|---|---|
| Contract smoke | Validate schemas, metrics, reports, and commands | No |
| Development | Tune extraction and normalization without touching the final test split | No |
| Benchmark validation | Select thresholds and compare candidate versions | Only when explicitly approved |
| Benchmark test | Frozen final evaluation used once per approved release candidate | Only when explicitly approved |

The repository starts Phase 3 with `phase3-synthetic-contract-smoke` version
`0.1.0`. Its two reused Phase 2 assets are synthetic and explicitly excluded
from performance claims.

## Source and rights gate

Before a non-synthetic sample enters a dataset version, record:

- provenance and the right to store, label, process, and publish aggregate
  results;
- license or written permission reference;
- SHA-256, byte size, media type, and acquisition date;
- whether personal, financial, or confidential information exists;
- the approved redaction and retention treatment;
- restrictions on committing the source or labels to Git.

No client or employer document is assumed reusable. If rights or privacy are
unclear, exclude the document. Restricted sources must live in approved
storage; their manifest may use an opaque asset identifier while retaining a
separately controlled provenance record.

## Coverage taxonomy

The English benchmark must declare coverage before its labels are inspected:

- native PDF, scanned PDF, PNG, and JPEG;
- clean, rotated, degraded, and mixed native/raster pages;
- one-page and multi-page layouts;
- tabular and non-tabular invoice layouts;
- common UAE invoice fields, including invoice number/date, supplier TRN,
  currency, subtotal, VAT, total, and payment terms;
- present, legitimately absent, repeated, and visually ambiguous fields;
- provider failure cases that are valid documents.

Arabic and mixed-language samples must not be blended into Phase 3 results.
They receive their own Phase 7 dataset and report.

No benchmark sample-count target is asserted yet. The pilot must first show
which strata and fields exist, after which the planned count and minimum per
segment will be approved before final-test labels are used.

## Labeling workflow

1. Freeze the extraction field schema and annotation guide version.
2. Assign stable sample IDs independently of filenames or business identity.
3. Label every in-scope field, including explicit absence through an exhaustive
   field inventory rather than omitted annotation work.
4. Have a second reviewer verify labels without seeing provider predictions.
5. Adjudicate disagreements and record reviewer IDs, timestamps, reasons, and
   the final label revision.
6. Run validation for duplicate identities, types, formats, totals, source
   hashes, and required segment metadata.
7. Freeze the dataset version and its canonical manifest hash.

Provider predictions must never be used as ground truth. Corrections found
after freezing create a new dataset patch version and a published change log;
old reports retain the old version.

## Split and leakage rules

- Keep development, validation, and test splits at source/layout-family level
  so near-duplicates cannot cross splits.
- Detect exact file duplicates by SHA-256 and document transformed or rendered
  derivatives as one family.
- Do not tune prompts, patterns, normalization, or thresholds on the final test
  split.
- Freeze the prediction set before metric calculation.
- Report every attempted sample, including provider failures and missing runs.

## Versioning

Use semantic versions for the logical dataset:

- major: field ontology, population, rights, or split policy changes;
- minor: approved new samples that preserve the policy;
- patch: corrected labels or metadata with no new samples.

Every report names the dataset version and sample count. Dataset and prediction
manifests also receive canonical SHA-256 fingerprints in the report.

## Claim gate

A dataset may set `claim_eligible: true` only after rights, privacy, coverage,
annotation, adjudication, leakage, and version-freeze checks are evidenced. A
report may support a performance statement only when it also discloses:

- dataset version and counts overall and by relevant segment;
- exact provider/model/version and prediction-set fingerprint;
- methodology and normalization version;
- field-level and failure results;
- latency/cost coverage;
- known population and sampling limitations.

The current smoke set fails this gate by design.
