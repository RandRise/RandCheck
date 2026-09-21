import copy
import hashlib
from pathlib import Path
import tempfile
import unittest

from app.evaluation import (
    EvaluationValidationError,
    evaluate,
    validate_dataset,
    verify_dataset_sources,
)


def field(key: str, value: str, occurrence: int = 0) -> dict:
    return {
        "key": key,
        "occurrence": occurrence,
        "value_type": "String",
        "value": value,
        "normalized_value": value,
    }


def dataset() -> dict:
    return {
        "dataset_schema_version": "1.0",
        "dataset_id": "invoice-eval",
        "dataset_version": "1.0.0",
        "purpose": "contract_smoke",
        "claim_eligible": False,
        "claim_exclusion_reason": "Synthetic test data is not representative performance evidence.",
        "normalization": {"name": "strict-normalized-value", "version": "1.0"},
        "samples": [
            {
                "sample_id": "native",
                "source": {
                    "path": "native.pdf",
                    "sha256": "0" * 64,
                    "license": "CC0-1.0",
                    "synthetic": True,
                },
                "segments": {
                    "layout": "two-page",
                    "scan_type": "native_pdf",
                    "language": "en",
                },
                "ground_truth": {
                    "fields": [field("invoice_number", "INV-1"), field("total", "100.00")]
                },
            },
            {
                "sample_id": "scan",
                "source": {
                    "path": "scan.pdf",
                    "sha256": "1" * 64,
                    "license": "CC0-1.0",
                    "synthetic": True,
                },
                "segments": {
                    "layout": "two-page",
                    "scan_type": "scanned_pdf",
                    "language": "en",
                },
                "ground_truth": {
                    "fields": [field("invoice_number", "INV-2"), field("total", "200.00")]
                },
            },
        ],
    }


def run(sample_id: str, fields: list[dict], *, duration: int, cost: float | None) -> dict:
    return {
        "sample_id": sample_id,
        "outcome": "Ready",
        "provider": {
            "alias": "local",
            "adapter_version": "1.0",
            "model": "test-model",
            "model_version": "1",
        },
        "metrics": {
            "provider_duration_ms": duration,
            "estimated_cost": cost,
            "cost_currency": "AED" if cost is not None else None,
        },
        "fields": fields,
    }


def predictions() -> dict:
    return {
        "prediction_schema_version": "1.0",
        "prediction_set_id": "local-test",
        "dataset_id": "invoice-eval",
        "dataset_version": "1.0.0",
        "runs": [
            run(
                "native",
                [field("invoice_number", "INV-1"), field("total", "100.00")],
                duration=10,
                cost=0.1,
            ),
            run(
                "scan",
                [field("invoice_number", "WRONG"), field("unexpected", "x")],
                duration=30,
                cost=None,
            ),
        ],
    }


class EvaluationTests(unittest.TestCase):
    def test_metrics_count_correct_incorrect_missing_and_unexpected_fields(self):
        report = evaluate(dataset(), predictions())
        metrics = report["metrics"]

        self.assertEqual(4, metrics["expected_field_count"])
        self.assertEqual(4, metrics["predicted_field_count"])
        self.assertEqual(2, metrics["correct_field_count"])
        self.assertEqual(1, metrics["incorrect_field_count"])
        self.assertEqual(1, metrics["missing_field_count"])
        self.assertEqual(1, metrics["unexpected_field_count"])
        self.assertEqual(0.5, metrics["precision"])
        self.assertEqual(0.5, metrics["recall"])
        self.assertEqual(0.6, metrics["correction_rate"])
        self.assertEqual(0.5, metrics["exact_match"]["document_rate"])
        self.assertEqual(0.666667, metrics["exact_match"]["field_value_rate"])
        self.assertEqual(
            {"mean": 20.0, "measured_sample_count": 2, "p50": 10, "p95": 30},
            metrics["latency_ms"],
        )
        self.assertEqual(0.5, metrics["cost"]["coverage_rate"])
        self.assertEqual({"AED": 0.1}, metrics["cost"]["totals_by_currency"])

    def test_report_includes_dataset_identity_claim_boundary_and_all_breakdowns(self):
        report = evaluate(dataset(), predictions())

        self.assertEqual(
            {
                "id": "invoice-eval",
                "version": "1.0.0",
                "sample_count": 2,
                "purpose": "contract_smoke",
                "claim_eligible": False,
                "claim_exclusion_reason": (
                    "Synthetic test data is not representative performance evidence."
                ),
            },
            report["dataset"],
        )
        self.assertEqual(
            {"invoice_number", "total", "unexpected"},
            set(report["breakdowns"]["field"]),
        )
        self.assertEqual({"two-page"}, set(report["breakdowns"]["layout"]))
        self.assertEqual(
            {"native_pdf", "scanned_pdf"},
            set(report["breakdowns"]["scan_type"]),
        )
        self.assertEqual(
            {"local/1.0/test-model/1"},
            set(report["breakdowns"]["provider_version"]),
        )
        self.assertEqual(
            64, len(report["input_fingerprints"]["dataset_canonical_sha256"])
        )
        self.assertEqual(
            64, len(report["input_fingerprints"]["prediction_set_canonical_sha256"])
        )

    def test_missing_prediction_is_a_failure_and_all_truth_fields_are_missing(self):
        payload = predictions()
        payload["runs"] = payload["runs"][:1]

        report = evaluate(dataset(), payload)
        metrics = report["metrics"]

        self.assertEqual(0.5, metrics["failure_rate"])
        self.assertEqual(2, metrics["missing_field_count"])
        self.assertIn("missing_prediction", report["breakdowns"]["provider_version"])

    def test_normalized_value_is_preferred_and_type_is_part_of_exact_match(self):
        payload = predictions()
        payload["runs"] = [copy.deepcopy(payload["runs"][0])]
        payload["runs"][0]["fields"][0].update(
            {"value": "display form", "normalized_value": "INV-1"}
        )
        payload["runs"][0]["fields"][1]["value_type"] = "Decimal"
        source = dataset()
        source["samples"] = source["samples"][:1]

        report = evaluate(source, payload)

        self.assertEqual(1, report["metrics"]["correct_field_count"])
        self.assertEqual(1, report["metrics"]["incorrect_field_count"])

    def test_rejects_duplicate_field_identity_and_dataset_version_mismatch(self):
        duplicate = dataset()
        duplicate["samples"][0]["ground_truth"]["fields"].append(
            field("invoice_number", "INV-1")
        )
        with self.assertRaisesRegex(EvaluationValidationError, "duplicate field identity"):
            evaluate(duplicate, predictions())

        mismatch = predictions()
        mismatch["dataset_version"] = "2.0.0"
        with self.assertRaisesRegex(EvaluationValidationError, "dataset_version"):
            evaluate(dataset(), mismatch)

    def test_non_claim_dataset_requires_an_explicit_exclusion_reason(self):
        payload = dataset()
        payload.pop("claim_exclusion_reason")

        with self.assertRaisesRegex(EvaluationValidationError, "claim_exclusion_reason"):
            evaluate(payload, predictions())

    def test_claim_eligible_dataset_cannot_include_synthetic_sources(self):
        payload = dataset()
        payload["claim_eligible"] = True
        payload.pop("claim_exclusion_reason")

        with self.assertRaisesRegex(EvaluationValidationError, "claim-eligible"):
            evaluate(payload, predictions())

    def test_source_verification_rejects_hash_mismatch_and_root_escape(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            manifest_directory = root / "datasets" / "v1"
            manifest_directory.mkdir(parents=True)
            source_path = root / "invoice.pdf"
            source_path.write_bytes(b"synthetic invoice")
            payload = dataset()
            payload["samples"] = payload["samples"][:1]
            payload["samples"][0]["source"].update(
                {
                    "path": "../../invoice.pdf",
                    "sha256": hashlib.sha256(source_path.read_bytes()).hexdigest(),
                }
            )
            validated = validate_dataset(payload)
            verify_dataset_sources(validated, manifest_directory, root)

            payload["samples"][0]["source"]["sha256"] = "0" * 64
            with self.assertRaisesRegex(EvaluationValidationError, "does not match"):
                verify_dataset_sources(payload, manifest_directory, root)

            payload["samples"][0]["source"].update(
                {"path": "../../../outside.pdf", "sha256": "0" * 64}
            )
            with self.assertRaisesRegex(EvaluationValidationError, "escapes"):
                verify_dataset_sources(payload, manifest_directory, root)


if __name__ == "__main__":
    unittest.main()
