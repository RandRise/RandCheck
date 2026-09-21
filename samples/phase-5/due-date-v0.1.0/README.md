# Explicit due-date extraction fixture

Synthetic existing native invoice, extracted with local-1.3.0 on September 11,
2026. manifest.json pins the original PDF and normalized extraction JSON by SHA256.
Only provider_duration_ms is normalized to zero.

The fixture is consumed by the PostgreSQL due-date review/citation integration
tests. Scripted confirmation, correction and clearing occur only in the disposable
document_intelligence_test database. No retained paid/typed evaluation is changed.

Date: 2026-08-31. This is extracted from an explicit label, not calculated from Net 30.

The accompanying native PDF is preserved byte-for-byte for clean-source CI. Its
hash matches this manifest and the original Phase 3 v0.2.0 dataset, which declares
the fictional document synthetic and CC0-1.0. The generator/source are
tools/phase3_uae_invoices_v2.py and tools/phase3_uae_invoice_source_v0.2.0.json.
CI validates the hash before materializing the legacy path; no retained output
is overwritten and no expected extraction or manifest was changed.
