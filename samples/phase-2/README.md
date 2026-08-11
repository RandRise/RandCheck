# Phase 2 synthetic fixtures

This set contains only fictional English invoice data generated from
`tools/phase2_fixture_source.json`. It is for deterministic contract and failure-path
tests, not for extraction-accuracy claims.

## Verify

```powershell
python tools/phase2_fixtures.py verify --root samples/phase-2
```

## Canonical regeneration

Build the digest-pinned generator image, then mount the repository at `/workspace`:

```powershell
docker build -f tools/phase2-fixtures.Dockerfile -t uae-di-phase2-fixtures .
docker run --rm -v "${PWD}:/workspace" uae-di-phase2-fixtures `
  generate --output /workspace/samples/phase-2
```

The generator uses fixed locale/timezone settings, exact Python dependencies, fixed PDF
metadata, fixed image encoder settings, and the hash-verified Bitstream Vera font shipped
by ReportLab. Runtime downloads are not performed.

The password for `generated/encrypted.pdf` is `synthetic-fixture-password`; it is public
test data, not a credential. `generated/unsupported.txt` is expected to be rejected by the
Phase 2 `local` provider even though Phase 1's `mock` provider supports text files.

Native PDFs embed Bitstream Vera. Its required redistribution notice is registered at
`licenses/bitstream-vera-license.txt`; those PDFs carry both CC0 synthetic content and the
Bitstream Vera font license.

Exact native-text labels include canonical pages and evidence. OCR-backed labels assert
only provider-independent semantic values until P2-T04 pins the OCR/model mapping.
`provider-responses/malformed-result.json` is intentionally invalid and must be rejected.
