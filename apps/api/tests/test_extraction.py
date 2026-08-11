import unittest
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from app.extraction import MockInvoiceExtractor


class ExtractionTests(unittest.TestCase):
    def test_extracts_bilingual_synthetic_invoice(self):
        sample = Path(__file__).resolve().parents[3] / "samples" / "uae-invoice.txt"
        result = MockInvoiceExtractor().extract(sample, "text/plain")
        values = {field.name: field.value for field in result.fields}
        self.assertEqual(result.language, "ar-en")
        self.assertEqual(result.document_type, "invoice")
        self.assertEqual(values["invoice_number"], "INV-AE-2026-001")
        self.assertEqual(values["total"], "1050.00")
        self.assertEqual(values["supplier_ar"], "ديزرت كلاود تكنولوجي ذ.م.م")


if __name__ == "__main__":
    unittest.main()
