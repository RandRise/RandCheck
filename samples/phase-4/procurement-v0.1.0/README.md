# Synthetic procurement fixtures v0.1.0

These three fictional PDFs and their truth set are the existing Phase 4 development
pack, preserved byte-for-byte for clean-source regression runs. They are synthetic
development inputs, not client records or real tax documents.

License: **CC0-1.0**, as declared by
`tools/phase4_procurement_source_v0.1.0.json` and the existing Phase 5 question set.
The generator is `tools/phase4_procurement_documents.py`; its original source and
artifact hashes are recorded in `generation-report.json`.

The four file hashes were verified against
`samples/phase-5/cited-questions-v0.1.0/question-set.json` before copying them from
the original generated pack. No PDF, truth label, expected hash or retained output
was modified. The invoice includes the earlier synthetic Phase 3 invoice page.

`tools/ci/prepare-python-fixtures.py` validates those same hashes and materializes
the legacy paths inside the disposable test workspace. It creates the Phase 3
smoke report from committed sample predictions, preserves matching existing files
and refuses changed outputs or redirected destination paths.
