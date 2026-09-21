from pathlib import Path
import unittest

from app.extraction import (
    EvidenceResult,
    ExtractionError,
    ExtractionResult,
    FieldResult,
    MetricsResult,
    PageResult,
    ProviderResult,
)
from app.evaluation import validate_prediction_set
from app.prediction_generation import generate_prediction_set


def dataset() -> dict:
    return {
        "dataset_schema_version": "1.0",
        "dataset_id": "prediction-generation-test",
        "dataset_version": "1.0.0",
        "purpose": "development",
        "claim_eligible": False,
        "claim_exclusion_reason": "Synthetic contract test.",
        "normalization": {"name": "strict-normalized-value", "version": "1.0"},
        "samples": [
            {
                "sample_id": "invoice-a",
                "source": {
                    "path": "invoice-a.pdf",
                    "sha256": "0" * 64,
                    "license": "CC0-1.0",
                    "synthetic": True,
                },
                "segments": {
                    "layout": "one_page",
                    "scan_type": "native_pdf",
                    "language": "en",
                },
                "ground_truth": {
                    "fields": [
                        {
                            "key": "invoice_number",
                            "occurrence": 0,
                            "value_type": "String",
                            "value": "INV-1",
                            "normalized_value": "INV-1",
                        }
                    ]
                },
            }
        ],
    }


class SuccessfulProvider:
    def extract(self, path: Path, content_type: str) -> ExtractionResult:
        assert path.name == "invoice-a.pdf"
        assert content_type == "application/pdf"
        return ExtractionResult(
            schema_version="2.0",
            outcome="Ready",
            document_type="invoice",
            language="en",
            provider=ProviderResult("local", "local-1.0.0", "pdfplumber", "1.0"),
            metrics=MetricsResult(17, 1),
            pages=[PageResult(1, "Invoice Number: INV-1", 0)],
            fields=[
                FieldResult(
                    "invoice_number",
                    0,
                    "String",
                    "INV-1",
                    "INV-1",
                    0.99,
                    [EvidenceResult(1, "INV-1", 16, 21)],
                )
            ],
            warnings=[],
        )


class FailingProvider:
    def extract(self, path: Path, content_type: str) -> ExtractionResult:
        del path, content_type
        raise ExtractionError("document_unreadable", "Safe failure.")


class PredictionGenerationTests(unittest.TestCase):
    def test_phase2_result_maps_directly_to_phase3_prediction_contract(self):
        result = generate_prediction_set(
            dataset(),
            Path("/dataset"),
            SuccessfulProvider(),
            prediction_set_id="local-live-1",
            provider_alias="local",
            adapter_version="local-1.0.0",
        )

        self.assertEqual("prediction-generation-test", result["dataset_id"])
        self.assertEqual(1, len(result["runs"]))
        run = result["runs"][0]
        self.assertEqual("Ready", run["outcome"])
        self.assertEqual(17, run["metrics"]["provider_duration_ms"])
        self.assertEqual("INV-1", run["fields"][0]["normalized_value"])
        self.assertNotIn("confidence", run["fields"][0])
        self.assertNotIn("evidence", run["fields"][0])
        self.assertIs(result, validate_prediction_set(result, dataset()))

    def test_expected_provider_failure_is_retained_as_a_failed_run(self):
        result = generate_prediction_set(
            dataset(),
            Path("/dataset"),
            FailingProvider(),
            prediction_set_id="local-live-failure",
            provider_alias="local",
            adapter_version="local-1.0.0",
        )

        run = result["runs"][0]
        self.assertEqual("Failed:document_unreadable", run["outcome"])
        self.assertEqual([], run["fields"])
        self.assertEqual("not-selected", run["provider"]["model"])
        self.assertIsNone(run["metrics"]["provider_duration_ms"])


if __name__ == "__main__":
    unittest.main()
