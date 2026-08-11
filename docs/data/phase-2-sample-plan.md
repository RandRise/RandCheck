# Phase 2 synthetic sample plan

**Status:** P2-T03 complete on 2026-08-11
**Review:** Integration Guardian **APPROVED** with no blockers

## Rules

- Use only reproducibly generated synthetic files. Third-party business-document samples are outside P2-T03.
- Never commit real UAE business documents, personal data, credentials, or customer identifiers.
- Keep a source/license register beside the fixtures with generator inputs/version, tool/font sources, applicable licenses, and artifact hashes.
- Store expected behavior as versioned JSON labels validated against extraction schema 2.0; never alter labels merely to match provider output.
- Treat these as contract and failure-behavior fixtures, not an accuracy benchmark.

## Implemented fixture matrix

| Fixture | Construction | Expected purpose |
|---|---|---|
| `native-two-page-invoice.pdf` | Reproducibly generated synthetic English invoice | Exact canonical page text, typed fields, UTF-8 evidence, and geometry truth |
| `scanned-two-page-invoice-300dpi.pdf` | Rasterize the same source at 300 DPI | Provider-independent semantic assertions for the future OCR path |
| `rotated-low-quality-invoice.pdf` | Synthetic scan with rotation and controlled degradation | Reviewed `Partial` semantic outcome |
| `corrupt.pdf` | Deliberately invalid bytes | Safe corrupt-input failure |
| `encrypted.pdf` | Synthetic password-protected PDF | Safe unsupported/encrypted failure |
| `unsupported.txt` | Benign synthetic text payload | Unsupported by the future `local` provider; retained only for Phase 1 mock compatibility |
| `invoice.png` and `invoice.jpg` | Synthetic invoice raster | Provider-independent semantic assertions for direct OCR inputs |
| Sanitized provider responses | No secrets or real document data | Valid normalized result, valid error envelope, and explicit malformed-result rejection |
| Simulated timeout/429/500/cancellation/crash | Test doubles, not external calls | Retry classification, bounds, cancellation, and recovery |

## Truth-model split

1. **Exact native truth:** the native PDF label freezes canonical page text, typed values, UTF-8 quote/offset evidence, and normalized geometry because the generator owns that representation. Each polygon is derived from the same labeled line, Bitstream Vera font metrics, drawing origin, leading, and page dimensions used to draw the PDF; byte offsets use that same located text occurrence.
2. **Provider-independent scan truth:** scanned PDF, PNG, JPG, and low-quality labels assert reviewed semantic values/outcomes only. They do not freeze OCR text, confidence, offsets, or geometry before P2-T04 pins the OCR/model mapping.
3. **Provider contract goldens:** sanitized fixtures include a valid normalized result, a valid safe error envelope, and an explicitly malformed payload that must be rejected.

Each document fixture has a versioned `.expected.json` label containing schema version `2.0` and the fields appropriate to its truth model. Labels pass schema and semantic validation before tests consume them. The format follows the [provider contract](../design/phase-2-extraction-provider-contract.md).

## Repository layout

```text
samples/phase-2/
  generated/
  expected/
  provider-responses/
  source-license-register.json
  README.md
```

The canonical generator is digest-pinned Python 3.11.15 on `linux/amd64`, with Pillow 9.4.0, ReportLab 3.6.12, and the ReportLab-distributed Bitstream Vera Sans 1.10 font verified by SHA-256. It fixes locale, timezone, PDF metadata, and image encoding, and performs no runtime downloads.

The required Bitstream Vera redistribution notice is itself a registered artifact. PDFs that embed the font carry composite `CC0-1.0 AND Bitstream-Vera` licensing; rasterized outputs contain synthetic pixels and remain CC0-1.0.

P2-T04—not P2-T03—must pin and register `pdfplumber`/`pdfminer`, `pypdfium2`, Tesseract/`pytesseract`, the English `eng` data, its container digest, and the exact OCR/model mapping before local-provider implementation or acceptance.

## Safety, budget, and register

- All business data is fictional and released as repository test data under CC0-1.0.
- Every readable page carries the visible watermark `SYNTHETIC TEST DOCUMENT - NOT A REAL TAX INVOICE`, and the supplier TRN is fifteen zeroes.
- The register records source/tool/dependency licenses, versions, artifact sizes, media types, purposes, and SHA-256 hashes.
- Validation enforces exact file closure: every non-register file must be registered, and extras, missing files, symlinks, unsafe paths, missing licenses, hash/size tampering, files over 10 MiB, and a set over 25 MiB are rejected.
- Direct verification passes for 22 registered artifacts totaling 827,913 bytes. The self-excluded register makes 23 files in the fixture tree.
- Fixture value types use the exact .NET enum vocabulary, and normalized provider metrics follow the .NET nullability, finite-number, bounded-currency, and price-basis rules.

## Verification expectations

- Native and scanned versions preserve the same semantic invoice values while exercising different future extraction paths.
- Exact evidence is frozen only for native truth; scan/OCR text, confidence, offsets, and geometry remain provider-independent until P2-T04.
- Low-quality input has the explicit reviewed outcome `Partial`.
- Corrupt, encrypted, unsupported, timeout, 429, 500, malformed-result, cancellation, and crash cases expose safe outcomes.
- Labels validate against the normalized schema before provider tests run.
- The fixture set also preserves Phase 1 PNG/JPEG OCR inputs and TXT development/mock compatibility; Phase 2 acceptance evidence remains PDF-focused.

## Evaluation boundary

Phase 2 fixtures verify deterministic contract behavior, orchestration, provenance, and safe failure handling for the covered synthetic cases. They are not a representative evaluation dataset and do not establish accuracy. Phase 3 remains the owner of evaluation-dataset design, representative coverage, field-level accuracy metrics, thresholds, provider comparisons, and any defensible accuracy statement.

## P2-T03 completion gate

- [x] Every committed file was regenerated from recorded synthetic inputs with the digest-pinned canonical container.
- [x] The source/license register covers every generated artifact, font, generator dependency, and tool.
- [x] A clean pinned Linux container matched the committed Windows-generated set byte-for-byte across all 23 files.
- [x] Label validation fails safely for unknown versions, malformed expectations, unsafe register content, and the explicit reject payload.
- [x] Automated tests and Integration Guardian review are linked.

Final verification is 26/26 targeted fixture-contract and CLI tests, 29/29 full Python discovery, direct verification of 22 artifacts totaling 827,913 bytes, and a 23/23 canonical file comparison. Integration Guardian reported **APPROVED** with no blockers. See the [P2-T03 evidence](../evidence/phase-2/P2-T03-synthetic-fixture-verification.md).
