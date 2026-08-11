# P2-T03 synthetic fixture verification

**Date:** 2026-08-11

**Status:** Complete on 2026-08-11
**Review:** Integration Guardian **APPROVED** with no blockers

## Implemented boundary

- Synthetic-only English invoice source data and reproducible document/error fixtures.
- Digest-pinned canonical generator: Python 3.11.15 slim Bookworm on `linux/amd64`, Pillow 9.4.0, ReportLab 3.6.12, and hash-verified Bitstream Vera Sans 1.10.
- Versioned schema 2.0 labels and provider-response wrappers validated before use.
- Source/license register with hashes, sizes, media types, purposes, generator/tool inputs, and licenses.
- Registered Bitstream Vera redistribution notice and composite `CC0-1.0 AND Bitstream-Vera` licensing for PDFs embedding the font.
- A visible synthetic-document watermark on every readable page and a deliberately invalid all-zero supplier TRN.
- Exact native truth with pages and evidence; provider-independent scan semantics without frozen OCR confidence/text/geometry; valid normalized/error goldens plus an explicit malformed-result rejection fixture. Native polygons derive from the same labeled line, font metrics, drawing origin, leading, and page dimensions used for PDF drawing, while offsets use the same located occurrence.
- Reviewed `Partial` outcome for the rotated low-quality PDF. TXT is unsupported by the future local provider and retained only for Phase 1 mock compatibility.
- Exact file closure and safety validation reject extras, missing files, symlinks, unsafe paths, absent licenses, tampering, and budget violations.
- Contract validation keeps fixture value types and provider metrics aligned with the .NET enum/nullability/bounds contract.

P2-T04 owns native/OCR runtime and model pins (`pdfplumber`/`pdfminer`, `pypdfium2`, Tesseract/`pytesseract`, English pack), not this fixture task.

## Current verification

| Check | Result |
|---|---|
| Direct fixture verification | 22 registered artifacts, 827,913 bytes; passed |
| Fixture tree | 23 files including the self-excluded source/license register |
| Canonical-container comparison | 23/23 files; pinned Linux output matched the committed Windows-generated set byte-for-byte |
| Targeted fixture contract + CLI | 26/26 passed |
| Full Python discovery | 29/29 passed |
| Ruff 0.15.22 container | `ruff check apps/api tools` passed |
| 10 MiB per-file / 25 MiB set budget | Passed |
| Register hashes/licenses/path/symlink safety | Passed locally |
| Schema 2.0 labels and explicit reject payload | Passed locally |
| Integration Guardian | **APPROVED**, no blockers |

The artifact, comparison, and Python test totals above are final.

## Broader regression context

The broader .NET invocation passed Domain 72, Application 23, PostgreSQL Persistence 17, Architecture 4, and Infrastructure 6 before the overall command reached its 120-second timeout while moving to the API project. The API suite then passed 15/15 in an isolated rerun. Across the split invocations, 137/137 tests passed and no failure was observed. This is reported as a split verification, not as one uninterrupted full-solution command.

## Honest scope

These fixtures verify reproducibility, contract validation, safe failure inputs, and reviewed semantics for synthetic cases. They are not an accuracy dataset, do not benchmark OCR, and do not establish production performance or client usage. Phase 3 remains the owner of formal evaluation and accuracy reporting.

## Post-completion fixture correction

P2-T04 identified that the PNG, JPEG, and rotated/low-quality inputs should show the complete one-page synthetic invoice. They now do so, with rotated generation using `expand=True`. The corrected register reports 22 artifacts and 999,112 bytes across a 23-file tree; the pinned canonical container matched all 23/23 files exactly. P2-T04 runtime verification is recorded in the [P2-T04 draft evidence](./P2-T04-local-provider-verification.md). The table above remains the historical P2-T03 approval checkpoint.
