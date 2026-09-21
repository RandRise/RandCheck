# Phase 5 cited-question evaluation set v0.1.0

This development-only set derives 30 questions from the synthetic Phase 4
quotation, purchase-order, and invoice pack. It covers 24 answerable questions
and 6 expected refusals, including paraphrases, cross-document calculations,
unsupported legal conclusions, prompt-based corpus escape, and a foreign-
organization source fixture.

The set is synthetic and `claim_eligible=false`. It cannot support real-world
retrieval, UAE invoice, compliance, or answer-quality claims.

## Provenance

- Source pack: `uae-procurement-reconciliation-synthetic` v0.1.0
- Canonicalization: `local-procurement-v1` / `local-1.2.0`
- Source licenses: CC0-1.0
- Labels: authored from the Phase 4 truth set without using generated answers
- Foreign-organization fixture: the same synthetic invoice is intentionally
  registered under a second organization identity to test isolation

Every answer claim names exact page text. The audit verifies source file hashes,
canonical page hashes, unique quote occurrence, question-category closure,
allowed-corpus evidence, and refusal shape.

## Reproduce and audit

```powershell
python tools/phase5_questions.py
python tools/phase5_question_audit.py
```

The audit requires the project PDF dependencies because it recreates canonical
page text from the committed source PDFs.
