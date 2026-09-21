import json
from pathlib import Path
import unittest

from app.evaluation import (
    DATASET_SCHEMA_VERSION,
    PREDICTION_SCHEMA_VERSION,
    REPORT_SCHEMA_VERSION,
    evaluate,
    validate_dataset,
    validate_prediction_set,
)
from app.evaluation_comparison import (
    COMPARISON_SCHEMA_VERSION,
    THRESHOLD_SCHEMA_VERSION,
    compare_reports,
    validate_report,
    validate_thresholds,
)
from app.dataset_readiness import (
    ANNOTATION_LEDGER_SCHEMA_VERSION,
    AUDIT_SCHEMA_VERSION,
    SOURCE_REGISTER_SCHEMA_VERSION,
    audit_dataset_readiness,
)


REPO_ROOT = Path(__file__).resolve().parents[3]
PHASE3_ROOT = REPO_ROOT / "samples" / "phase-3"
SCHEMA_ROOT = PHASE3_ROOT / "schemas"
SMOKE_ROOT = PHASE3_ROOT / "smoke-v0.1.0"


class Phase3EvaluationSchemaTests(unittest.TestCase):
    def test_all_schemas_use_draft_2020_12_unique_urns_and_valid_local_refs(self):
        schemas = {
            path.name: json.loads(path.read_text(encoding="utf-8"))
            for path in sorted(SCHEMA_ROOT.glob("*.schema.json"))
        }
        self.assertEqual(
            {
                "annotation-ledger-1.0.schema.json",
                "comparison-1.0.schema.json",
                "dataset-1.0.schema.json",
                "prediction-set-1.0.schema.json",
                "readiness-audit-1.0.schema.json",
                "report-1.0.schema.json",
                "source-register-1.0.schema.json",
                "threshold-set-1.0.schema.json",
            },
            set(schemas),
        )
        identifiers = {schema["$id"] for schema in schemas.values()}
        self.assertEqual(len(schemas), len(identifiers))
        for name, schema in schemas.items():
            with self.subTest(schema=name):
                self.assertEqual(
                    "https://json-schema.org/draft/2020-12/schema",
                    schema["$schema"],
                )
                self.assertTrue(schema["$id"].startswith("urn:uae-document-intelligence:"))
                self._assert_local_refs_resolve(schema, schema)

    def test_schema_version_constants_match_executable_contracts(self):
        expected = {
            "dataset-1.0.schema.json": (
                "dataset_schema_version",
                DATASET_SCHEMA_VERSION,
            ),
            "prediction-set-1.0.schema.json": (
                "prediction_schema_version",
                PREDICTION_SCHEMA_VERSION,
            ),
            "report-1.0.schema.json": (
                "report_schema_version",
                REPORT_SCHEMA_VERSION,
            ),
            "threshold-set-1.0.schema.json": (
                "threshold_schema_version",
                THRESHOLD_SCHEMA_VERSION,
            ),
            "comparison-1.0.schema.json": (
                "comparison_schema_version",
                COMPARISON_SCHEMA_VERSION,
            ),
            "source-register-1.0.schema.json": (
                "source_register_schema_version",
                SOURCE_REGISTER_SCHEMA_VERSION,
            ),
            "annotation-ledger-1.0.schema.json": (
                "annotation_ledger_schema_version",
                ANNOTATION_LEDGER_SCHEMA_VERSION,
            ),
            "readiness-audit-1.0.schema.json": (
                "audit_schema_version",
                AUDIT_SCHEMA_VERSION,
            ),
        }
        for filename, (property_name, version) in expected.items():
            with self.subTest(schema=filename):
                schema = json.loads(
                    (SCHEMA_ROOT / filename).read_text(encoding="utf-8")
                )
                self.assertEqual(
                    version, schema["properties"][property_name]["const"]
                )

    def test_committed_smoke_artifacts_pass_the_executable_schema_contracts(self):
        dataset_payload = json.loads(
            (SMOKE_ROOT / "dataset.json").read_text(encoding="utf-8")
        )
        prediction_payload = json.loads(
            (SMOKE_ROOT / "local-predictions.json").read_text(encoding="utf-8")
        )
        threshold_payload = json.loads(
            (SMOKE_ROOT / "strict-thresholds.json").read_text(encoding="utf-8")
        )
        dataset = validate_dataset(dataset_payload)
        validate_prediction_set(prediction_payload, dataset)
        validate_thresholds(threshold_payload)
        report = evaluate(dataset_payload, prediction_payload)
        validate_report(report, "smoke report")
        comparison = compare_reports(report, report, threshold_payload)

        self.assertEqual(
            COMPARISON_SCHEMA_VERSION, comparison["comparison_schema_version"]
        )
        self.assertTrue(comparison["thresholds"]["passed"])

        source_register = json.loads(
            (SMOKE_ROOT / "source-register.json").read_text(encoding="utf-8")
        )
        annotation_ledger = json.loads(
            (SMOKE_ROOT / "annotation-ledger.json").read_text(encoding="utf-8")
        )
        audit = audit_dataset_readiness(
            dataset_payload, source_register, annotation_ledger
        )
        self.assertEqual(AUDIT_SCHEMA_VERSION, audit["audit_schema_version"])
        self.assertFalse(audit["claim_ready"])

    def _assert_local_refs_resolve(self, value: object, root: dict) -> None:
        if isinstance(value, dict):
            reference = value.get("$ref")
            if reference is not None:
                self.assertIsInstance(reference, str)
                self.assertTrue(reference.startswith("#/"), reference)
                target: object = root
                for encoded_component in reference[2:].split("/"):
                    component = encoded_component.replace("~1", "/").replace("~0", "~")
                    self.assertIsInstance(target, dict)
                    self.assertIn(component, target)
                    target = target[component]
            for child in value.values():
                self._assert_local_refs_resolve(child, root)
        elif isinstance(value, list):
            for child in value:
                self._assert_local_refs_resolve(child, root)


if __name__ == "__main__":
    unittest.main()
