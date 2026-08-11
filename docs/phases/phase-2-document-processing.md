# Phase 2 — Real document processing

**Status:** Proposed  
**Outcome:** Realistic native PDFs and scanned invoices produce page-aware structured output.

## Work

- Detect native-text versus scanned documents.
- Extract text and layout from native PDFs.
- Add OCR for scanned pages.
- Preserve page number and bounding-box provenance.
- Introduce an Azure Document Intelligence adapter behind the existing provider boundary.
- Retain a local or low-cost development provider.
- Add background processing, retry, timeout, and idempotency behavior.
- Define unsupported, low-confidence, partial, and failed outcomes.
- Add safe document fixtures representing layout variations.

## Acceptance criteria

- [ ] At least one native PDF and one scanned PDF complete successfully.
- [ ] Provider selection is configuration-driven.
- [ ] Local development works without paid credentials.
- [ ] Long-running extraction does not block the public API request lifecycle.
- [ ] Retry behavior does not duplicate documents or extracted fields.
- [ ] Page and bounding-box provenance are retained.
- [ ] Provider, model/version, latency, and cost hooks are recorded.

## Risks

- OCR quality varies by scan resolution and layout.
- Cloud-provider outputs may not map cleanly to one schema.
- Background processing increases operational complexity.

## Portfolio evidence

- Provider-abstraction design
- Native-versus-scan processing comparison
- Retry and idempotency tests
- Honest failure-case demonstration
