import unittest
from dataclasses import asdict
from pathlib import Path
import sys
import tempfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from app.extraction import MockInvoiceExtractor
from app.main import ExtractResponse


class ExtractionContractTests(unittest.TestCase):
    def test_schema_2_response_round_trips_nullable_metrics_and_utf8_offsets(self):
        with tempfile.TemporaryDirectory() as directory:
            sample = Path(directory) / "invoice.txt"
            sample.write_text("Invoice Number: INV-é-ض\r\nTotal: 10.00\r\n", encoding="utf-8")

            result = MockInvoiceExtractor().extract(sample, "text/plain")
            response = ExtractResponse.model_validate(asdict(result))
            payload = response.model_dump()

        self.assertEqual(payload["schema_version"], "2.0")
        self.assertEqual(payload["outcome"], "Ready")
        self.assertIsNone(payload["metrics"]["estimated_cost"])
        self.assertNotIn("\r", payload["pages"][0]["canonical_text"])
        invoice_number = next(field for field in payload["fields"] if field["key"] == "invoice_number")
        evidence = invoice_number["evidence"][0]
        encoded = payload["pages"][0]["canonical_text"].encode("utf-8")
        selected = encoded[evidence["start_utf8"]:evidence["end_utf8"]].decode("utf-8")
        self.assertEqual(selected, "INV-é-ض")

    def test_canonicalization_preserves_non_newline_whitespace(self):
        with tempfile.TemporaryDirectory() as directory:
            sample = Path(directory) / "invoice.txt"
            sample.write_text("Supplier: ACME\tTrading\rTotal: 1.00", encoding="utf-8")
            result = MockInvoiceExtractor().extract(sample, "text/plain")

        self.assertIn("ACME\tTrading\nTotal", result.pages[0].canonical_text)


if __name__ == "__main__":
    unittest.main()
