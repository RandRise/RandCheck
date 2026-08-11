# Phase 2 synthetic sample plan

**Status:** Approved plan on 2026-08-11  
**Fixtures created:** None in this planning task

## Rules

- Use only reproducibly generated synthetic files or explicitly licensed samples.
- Never commit real UAE business documents, personal data, credentials, or customer identifiers.
- Keep a source/license manifest and generator version beside the fixtures.
- Store expected behavior as reviewed JSON labels; never alter labels merely to match provider output.
- Treat these as contract and failure-behavior fixtures, not an accuracy benchmark.

## Proposed fixture matrix

| Fixture | Construction | Expected purpose |
|---|---|---|
| `native-two-page-invoice.pdf` | Reproducibly generated synthetic English invoice | Native text, two pages, typed fields, page evidence |
| `scanned-two-page-invoice-300dpi.pdf` | Rasterize the same source at 300 DPI | OCR path and evidence mapping |
| `rotated-low-quality-invoice.pdf` | Synthetic scan with rotation and controlled degradation | Deterministic partial or safe-failure behavior |
| `corrupt.pdf` | Deliberately invalid bytes | Safe corrupt-input failure |
| `encrypted.pdf` | Synthetic password-protected PDF | Safe unsupported/encrypted failure |
| `unsupported.txt` | Benign non-PDF payload | Content validation failure |
| `invoice.png` and `invoice.jpg` | Synthetic invoice raster | Supported direct OCR inputs |
| Sanitized provider responses | No secrets or real document data | Adapter mapping and malformed-result tests |
| Simulated timeout/429/500/cancellation/crash | Test doubles, not external calls | Retry classification, bounds, cancellation, and recovery |

Each document fixture has an `.expected.json` label containing expected outcome, document type, language, fields and value types, page evidence, quote/offsets, normalized polygon/unit, warnings, or stable failure code. The label format follows the [provider contract](../design/phase-2-extraction-provider-contract.md).

## Proposed repository layout

```text
samples/phase-2/
  generated/
  expected/
  provider-responses/
  manifest.json
  README.md
```

The generator uses Noto Sans and fixed inputs so CI and local runs receive equivalent files. Before P2-T03, pin and record its font/license plus `pdfplumber`/`pdfminer`, `pypdfium2`, Tesseract, `pytesseract`, English language pack, and container digest. Runtime downloads are forbidden. Generated binaries may be committed only after size, license, safety, and reproducibility review.

## Verification expectations

- Native and scanned versions preserve the same semantic invoice values while exercising different extraction paths.
- Evidence labels include one-based pages, page rotation, exact quote/offset consistency, and `[0,1]` polygons.
- Low-quality input has an explicit reviewed expectation: partial or failed, never nondeterministically either.
- Corrupt, encrypted, unsupported, timeout, 429, 500, malformed-result, cancellation, and crash cases expose safe outcomes.
- Labels validate against the normalized schema before provider tests run.
- The fixture set also preserves Phase 1 PNG/JPEG OCR inputs and TXT development/mock compatibility; Phase 2 acceptance evidence remains PDF-focused.

## Evaluation boundary

Phase 2 fixtures prove contract behavior, orchestration, provenance, and safe failure handling. Phase 3 will introduce the versioned evaluation dataset, representative coverage, field-level accuracy metrics, thresholds, and formal comparisons. No Phase 2 fixture result may be presented as production accuracy.
