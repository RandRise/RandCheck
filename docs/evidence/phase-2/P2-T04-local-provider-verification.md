# P2-T04 local-provider verification

**Date:** 2026-08-11
**Status:** Complete on 2026-08-11
**Review:** Integration Guardian **APPROVED** with no blockers

## Implemented boundary under review

- Internal/testable English `local` provider; deterministic `mock` remains available.
- Per-page native PDF extraction through `pdfplumber`/`pdfminer`, otherwise 300-DPI PDFium rendering plus Tesseract `eng`/`osd` OCR.
- PNG/JPEG inputs use the same Tesseract path.
- Safe failures distinguish unreadable content and readable unsupported/non-invoice content without returning extracted data.
- Validated output is `Ready` when no warnings remain or `Partial` when at least one validated field/evidence remains with a degraded warning.
- Page rotation records the clockwise correction needed for upright output. Native PDF provenance comes from `/Rotate`; raster inputs honor EXIF orientation; OCR-backed pages use Tesseract OSD.
- All-native results report `pdfplumber`/`pdfminer`, all-OCR results report `tesseract-eng-osd`, and mixed native/OCR PDFs report `pdfplumber-tesseract-eng-osd` with both version sets.

Docker Compose and the legacy HTTP extraction endpoint remain on `mock`. P2-T06 must implement the trusted storage-key worker request and orchestration boundary before workflow traffic can call `local`; P2-T04 does not expose arbitrary filesystem paths.

## Runtime pins

| Component | Adopted pin |
|---|---|
| Python base | `python:3.11.15-slim-bookworm@sha256:d29f48a31a8b408ed19272ca1e7b10ebae13b240a27e862d3d4217c528e2e0c3`, `linux/amd64` |
| `pdfplumber` | 0.11.10 |
| `pdfminer.six` | 20260107 |
| `pypdfium2` | 5.12.1 |
| Pillow | 12.3.0 |
| `pytesseract` | 0.3.13 |
| Tesseract | 5.3.0-2 |
| Language/orientation data | `eng` 4.1.0-2; `osd` 4.1.0-2 |
| PDFium runtime | 152.0.7947.0 |
| Production image ID | `sha256:4ac9f479d4de61a24d1c266f533237070dbfd32afd5479e2ff15f8d08cf12684` |
| `eng` data SHA-256 | `7d4322bd2a7749724879683fc3912cb542f19906c83bcc1a52132556427170b2` |
| `osd` data SHA-256 | `9cf5d576fcc47564f11265841e5ca839001e7e6f38ff7f7aacf46d15a96b00ff` |

## Runtime sources and licenses

| Runtime component | License | Recorded source |
|---|---|---|
| Python 3.11.15 | PSF License | [Python license](https://docs.python.org/3/license.html); digest-pinned official slim Bookworm image |
| `pdfplumber` 0.11.10 | MIT | [`jsvine/pdfplumber` license](https://github.com/jsvine/pdfplumber/blob/stable/LICENSE) |
| `pdfminer.six` 20260107 | MIT | [`pdfminer/pdfminer.six` license](https://github.com/pdfminer/pdfminer.six/blob/master/LICENSE) |
| `pypdfium2` 5.12.1 | Apache-2.0 OR BSD-3-Clause; bundled PDFium/dependency notices also apply | [Upstream licensing section](https://github.com/pypdfium2-team/pypdfium2#licensing) |
| Pillow 12.3.0 | HPND | [Pillow license](https://github.com/python-pillow/Pillow/blob/main/LICENSE) |
| `pytesseract` 0.3.13 | Apache-2.0 | [`madmaze/pytesseract` license](https://github.com/madmaze/pytesseract/blob/master/LICENSE) |
| Tesseract 5.3.0-2 | Apache-2.0 | [Tesseract license](https://github.com/tesseract-ocr/tesseract/blob/main/LICENSE); [Debian source snapshot](https://snapshot.debian.org/package/tesseract/5.3.0-2/) |
| `eng`/`osd` 4.1.0-2 | Apache-2.0 | [tessdata license](https://github.com/tesseract-ocr/tessdata/blob/main/LICENSE); [Debian language-data source](https://snapshot.debian.org/package/tesseract-lang/1%3A4.1.0-2/) |
| PDFium 152.0.7947.0 | BSD-style plus bundled third-party notices | [PDFium license](https://pdfium.googlesource.com/pdfium/+/refs/heads/main/LICENSE); pypdfium2 wheel build-license notices |

Sources, licenses, installed/runtime versions, hashes, container identity, and returned provider/model identity are recorded and verified.

## Fixture correction under review

PNG, JPEG, and rotated/low-quality fixtures now contain the complete visible one-page synthetic invoice. Rotation uses `expand=True`; the degraded rotated case remains semantically `Partial`. Direct verification passes for 22 artifacts totaling 999,112 bytes, with 23 files including the register. The pinned canonical container matched all 23/23 committed files exactly.

## Duration-neutral provider goldens

`tools/phase2_local_provider_goldens.py` verifies five committed normalized outputs under `apps/api/tests/goldens/local-provider`: native two-page PDF, scanned two-page PDF, PNG, JPEG, and rotated low-quality PDF. Each complete pinned-container result matched its committed golden after removing runtime duration only. This demonstrates deterministic contract output for five synthetic cases; it is not an accuracy or performance measurement.

## Security and boundary hardening

- PDF input is limited to 50 pages before rendering. Render dimensions and decoded/corrected images are limited to 25,000,000 pixels per page; oversized PDF dimensions are rejected before PDFium rendering.
- Image header dimensions are checked before decode, and Pillow decompression-bomb warnings/errors become safe nonretryable `input_too_large` failures.
- Every result passes the schema 2.0 normalized-result limits and invariants before it leaves the local adapter.
- The installed Tesseract version, required `eng`/`osd` availability, and both trained-data SHA-256 hashes are verified. Missing or mismatched model data fails safely as unavailable runtime rather than processing with an unrecorded model.
- If the legacy `/extract` route is configured as `local`, it returns the same path-independent `503` before constructing or reading a path. Existing and missing filenames are indistinguishable, and unsafe request IDs are replaced with a bounded safe correlation ID. Compose remains on `mock` until P2-T06.
- Regression coverage exercises the 50-page limit, pre-render/decode pixel limits, decompression bombs, model hash success/missing/mismatch, normalized-result rejection, all-native/all-OCR/mixed identity, safe correlation IDs, and legacy-route path independence.

## Current verification

| Check | Result |
|---|---|
| Host full Python discovery | 62 total: 57 passed, 5 runtime-gated skipped |
| Pinned production-container runtime | 5/5 passed; every complete normalized result matched its duration-neutral golden |
| Fast P2-T04 suite | 28/28 passed |
| Fixture verification | 22 artifacts, 999,112 bytes; passed |
| Canonical fixture comparison | 23/23 files matched exactly |
| Ruff 0.15.22 | `ruff check --no-cache apps/api tools` passed |
| Docker Compose configuration | Passed |
| Integration Guardian | **APPROVED**, no blockers |

## Excluded claims and tasks

No Azure adapter, queue/worker orchestration, UI processing state, Arabic support, extraction accuracy, production performance, or client outcome is claimed. This evidence completes only the approved P2-T04 internal local-provider boundary.
