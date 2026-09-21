# P3-T03 intake-readiness checkpoint

**Date:** 2026-08-13  
**Scope:** Rights/privacy intake, independent annotation, and deterministic readiness audit  
**Result:** Governance tooling verified; claim-eligible source collection remains active

**2026-08-14 addendum:** Six original UAE-specific synthetic development PDFs
were generated and visually verified under
`output/pdf/uae-invoice-development-v0.1.0`. Their dataset, source register,
and annotation ledger pass the Phase 3 structural audit; the expected exit is
`4` because the set is synthetic and explicitly not claim eligible. This adds
usable UAE regression coverage without weakening the real-source gate.

**2026-08-14 v0.2.0 addendum:** The separate FTA field-coverage pack at
`output/pdf/uae-invoice-development-v0.2.0/README.md`
adds nine PDFs and ten rendered pages. It covers standard full, simplified,
zero-rated, reverse-charge, foreign-currency, discounted multipage, clean
scan, and degraded scan conditions. Full line-level calculations, FTA
requirement statuses, and the MoF non-eInvoice boundary are recorded beside
the Phase 3 eight-field manifest. Visual review found and corrected clipped
line descriptions and strengthened the synthetic/legal-warning banner.

Verification confirmed nine source/manifest hashes, no forbidden active PDF
content, complete source/annotation closure, seven native-text PDFs, two
image-only scan PDFs, and the expected readiness exit `4` with ten blockers:
one dataset-level non-claim blocker and nine synthetic-source blockers.

## Delivered

- Source register contract for provenance, permissions, privacy/redaction, and
  storage classification.
- Independent annotation ledger with reviewer separation, UTC sequencing,
  adjudication, label revisions, and canonical final-field hashes.
- Deterministic readiness audit with exact sample closure and source/dataset
  fingerprint agreement.
- Distinct CLI exits: invalid input `2`, valid claim-ready `0`, valid with
  blockers `4`.
- Three additional JSON Schema Draft 2020-12 artifacts and synthetic smoke
  examples.
- A six-document UAE synthetic development pack at
  `output/pdf/uae-invoice-development-v0.1.0/README.md`
  covering native, scanned, degraded, one-page, and two-page UAE tax-invoice
  conditions.
- A nine-document FTA field-coverage development pack at
  `output/pdf/uae-invoice-development-v0.2.0/README.md`
  with conditional tax scenarios, full compliance ground truth, and a clear
  MoF structured-eInvoice boundary.

## Guardrail result

The two-sample smoke set passes source/annotation structural checks but is not
claim-ready. Its audit reports exactly three blockers:

1. the dataset explicitly excludes performance claims;
2. the native sample is synthetic;
3. the scanned sample is synthetic.

The output is stored at
`outputs/phase-3/smoke-v0.1.0-readiness-audit.json`. This demonstrates that
complete annotations and permissive fixture licensing cannot accidentally turn
synthetic documents into real-world accuracy evidence.

## Commands and results

```powershell
$env:PYTHONPATH='apps/api;apps/api/tests'
python -m unittest discover -s apps/api/tests -p 'test*dataset*.py'
```

Result: **12/12 P3-T03 readiness and CLI tests passed**.

```powershell
python tools/phase3_dataset_audit.py `
  --dataset samples/phase-3/smoke-v0.1.0/dataset.json `
  --source-register samples/phase-3/smoke-v0.1.0/source-register.json `
  --annotation-ledger samples/phase-3/smoke-v0.1.0/annotation-ledger.json `
  --output outputs/phase-3/smoke-v0.1.0-readiness-audit.json
```

Result: expected exit `4`, three deterministic blockers, and audit SHA-256
`6408cf4340e1c307427918887a8f51ff964d023e8c0d713b976e6db76eb2a74e`.

```powershell
$env:PYTHONPATH='apps/api;apps/api/tests'
python -m unittest discover -s apps/api/tests
```

Result: **104 discovered; 99 passed and 5 expected host-runtime OCR skips**.

## Remaining P3-T03 work

- Approve the two-population source strategy documented in
  [public invoice-source research](../../data/phase-3-public-source-research.md).
- Obtain the unavailable MIDD scans and clarify their conflicting use terms,
  or obtain and approve the complete DocILE access terms.
- Obtain organization-owned or expressly permitted UAE invoices for the
  primary claim population; external datasets cannot establish UAE accuracy.
- Obtain and verify rights/privacy evidence outside this automated contract.
- Calibrate the annotation guide on a rights-cleared pilot.
- Set coverage and sample-count targets before inspecting final labels.
- Independently label, verify, adjudicate, and freeze the benchmark version.
- Run the audit with zero blockers and publish the source/label audit evidence.

No claim-eligible document has been collected or claimed at this checkpoint.
