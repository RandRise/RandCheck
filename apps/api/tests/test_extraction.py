import unittest
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from app.extraction import MockInvoiceExtractor


class ExtractionTests(unittest.TestCase):
    def test_extracts_bilingual_synthetic_invoice(self):
        sample = Path(__file__).resolve().parents[3] / "samples" / "uae-invoice.txt"
        result = MockInvoiceExtractor().extract(sample, "text/plain")
        values = {field.key: field.value for field in result.fields}
        self.assertEqual(result.schema_version, "2.0")
        self.assertEqual(result.outcome, "Ready")
        self.assertEqual(result.provider.alias, "mock")
        self.assertEqual(result.metrics.page_count, 1)
        self.assertEqual(len(result.pages), 1)
        self.assertEqual(result.language, "ar-en")
        self.assertEqual(result.document_type, "invoice")
        self.assertEqual(values["invoice_number"], "INV-AE-2026-001")
        self.assertEqual(values["total"], "1050.00")
        for field in result.fields:
            evidence = field.evidence[0]
            page_bytes = result.pages[evidence.page_number - 1].canonical_text.encode("utf-8")
            self.assertEqual(
                page_bytes[evidence.start_utf8:evidence.end_utf8].decode("utf-8"),
                evidence.quote,
            )
        self.assertEqual(values["supplier_ar"], "ديزرت كلاود تكنولوجي ذ.م.م")


if __name__ == "__main__":
    unittest.main()
