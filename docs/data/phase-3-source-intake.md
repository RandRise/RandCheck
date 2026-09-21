# Phase 3 source intake and claim-readiness contract

**Status:** Approved foundation; web and Reddit source research complete, acquisition not started  
**Version:** 1.0  
**Task:** P3-T03

The current public-source decision and clearance questions are recorded in the
[public invoice-source research](./phase-3-public-source-research.md). No
candidate identified there is claim-ready yet.

This contract prevents a technically valid evaluation set from becoming a
legally or ethically invalid performance claim. It is an engineering gate, not
legal advice or a substitute for an authorized rights/privacy review.

## Required source record

Every dataset sample has exactly one source-register record containing:

- stable sample and opaque asset identifiers;
- SHA-256, byte size, media type, and acquisition timestamp;
- provenance type: organization-owned, written permission, public licensed, or
  synthetic;
- a reference to the license, permission, or ownership evidence;
- separate permission flags for storage, labeling, processing, and publication
  of aggregate results;
- personal/confidential-data flags, redaction status, and privacy-review
  reference;
- storage classification, controlled-location reference, and whether the bytes
  are committed to Git.

The source register and dataset manifest must have exact sample closure. Their
sample SHA-256 values must agree.

## Claim-readiness rules

A source blocks a claim-eligible dataset when any of these conditions applies:

- the dataset or source is synthetic;
- any required use is not explicitly permitted;
- privacy review or required redaction is pending or prohibited;
- personal or confidential data exists without completed redaction;
- a restricted asset is committed to Git;
- source or annotation coverage is incomplete;
- final labels do not match their canonical ledger hashes.

Passing the automated audit means the recorded contract is internally
consistent. It does not prove that a permission reference is authentic or
legally sufficient; that evidence still needs an authorized human review.

## Storage rules

- Public, redistributable sources may be committed only when their license and
  privacy review explicitly permit it.
- Internal or restricted sources use opaque asset IDs and controlled storage;
  document bytes, secrets, personal data, and confidential values stay out of
  Git.
- Dataset manifests may reference controlled assets without exposing filenames,
  parties, account numbers, or business identifiers.
- Redacted derivatives receive their own SHA-256 and provenance link; they do
  not silently replace the acquired source record.

## Intake workflow

1. Record the candidate source without adding it to a benchmark.
2. Verify ownership/license/permission for each required use.
3. Complete privacy classification and any required redaction.
4. Assign its storage classification and controlled location.
5. Calculate the final evaluation-asset SHA-256 and byte size.
6. Add dataset segments without inspecting provider predictions.
7. Complete independent annotation and adjudication under the annotation guide.
8. Run the readiness audit and resolve every blocker.
9. Freeze the dataset version and canonical input fingerprints.

## Audit command

```powershell
python tools/phase3_dataset_audit.py `
  --dataset path/to/dataset.json `
  --source-register path/to/source-register.json `
  --annotation-ledger path/to/annotation-ledger.json `
  --output path/to/readiness-audit.json
```

Exit code `0` means the recorded inputs are structurally claim-ready, `4` means
the inputs are valid but blockers remain, and `2` means an input contract is
invalid. The audit is deterministic and includes canonical fingerprints for
all three inputs.

## Current checkpoint

The synthetic smoke set includes a complete example register and annotation
ledger. Its audit intentionally exits `4`, reporting the dataset-level claim
exclusion and both synthetic sources. This is a positive test of the guardrail,
not a deficiency to bypass.
