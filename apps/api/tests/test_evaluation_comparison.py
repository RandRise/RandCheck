import copy
import unittest

from app.evaluation import EvaluationValidationError, evaluate
from app.evaluation_comparison import compare_reports, validate_thresholds
from test_evaluation import dataset, predictions


def thresholds() -> dict:
    return {
        "threshold_schema_version": "1.0",
        "threshold_set_id": "release-gate-v1",
        "rules": [
            {
                "rule_id": "overall-recall",
                "scope": "overall",
                "metric": "recall",
                "minimum_candidate": 0.5,
                "maximum_regression": 0.0,
            },
            {
                "rule_id": "scan-failure-rate",
                "scope": "scan_type.scanned_pdf",
                "metric": "failure_rate",
                "maximum_candidate": 0.0,
                "maximum_regression": 0.0,
            },
        ],
    }


class EvaluationComparisonTests(unittest.TestCase):
    def test_comparison_reports_metric_direction_and_breakdown_deltas(self):
        baseline = evaluate(dataset(), predictions())
        improved_predictions = predictions()
        improved_predictions["prediction_set_id"] = "improved"
        improved_predictions["runs"][1]["fields"] = [
            {
                "key": "invoice_number",
                "occurrence": 0,
                "value_type": "String",
                "value": "INV-2",
                "normalized_value": "INV-2",
            },
            {
                "key": "total",
                "occurrence": 0,
                "value_type": "String",
                "value": "200.00",
                "normalized_value": "200.00",
            },
        ]
        candidate = evaluate(dataset(), improved_predictions)

        comparison = compare_reports(baseline, candidate)

        self.assertEqual(0.5, comparison["metrics"]["recall"]["delta"])
        self.assertEqual(0.5, comparison["metrics"]["recall"]["favorable_delta"])
        self.assertEqual(
            -0.6, comparison["metrics"]["correction_rate"]["delta"]
        )
        self.assertEqual(
            0.6, comparison["metrics"]["correction_rate"]["favorable_delta"]
        )
        self.assertIn("total", comparison["breakdowns"]["field"]["shared"])
        self.assertEqual(
            ["unexpected"], comparison["breakdowns"]["field"]["baseline_only"]
        )
        self.assertIsNone(comparison["thresholds"])

    def test_thresholds_pass_when_candidate_meets_floor_and_does_not_regress(self):
        report = evaluate(dataset(), predictions())

        comparison = compare_reports(report, report, thresholds())

        self.assertTrue(comparison["thresholds"]["passed"])
        self.assertEqual(2, comparison["thresholds"]["evaluated_rule_count"])
        self.assertTrue(
            all(result["passed"] for result in comparison["thresholds"]["results"])
        )

    def test_thresholds_fail_for_higher_and_lower_is_better_regressions(self):
        baseline_predictions = predictions()
        baseline_predictions["runs"][1]["fields"] = [
            {
                "key": "invoice_number",
                "occurrence": 0,
                "value_type": "String",
                "value": "INV-2",
                "normalized_value": "INV-2",
            },
            {
                "key": "total",
                "occurrence": 0,
                "value_type": "String",
                "value": "200.00",
                "normalized_value": "200.00",
            },
        ]
        baseline = evaluate(dataset(), baseline_predictions)
        candidate_predictions = copy.deepcopy(baseline_predictions)
        candidate_predictions["prediction_set_id"] = "regressed"
        candidate_predictions["runs"][1]["outcome"] = "Failed"
        candidate_predictions["runs"][1]["fields"] = []
        candidate = evaluate(dataset(), candidate_predictions)

        comparison = compare_reports(baseline, candidate, thresholds())

        self.assertFalse(comparison["thresholds"]["passed"])
        results = {
            result["rule_id"]: result
            for result in comparison["thresholds"]["results"]
        }
        self.assertFalse(results["overall-recall"]["passed"])
        self.assertFalse(results["scan-failure-rate"]["passed"])
        self.assertGreater(
            results["scan-failure-rate"]["comparison"]["delta"], 0
        )
        self.assertLess(
            results["scan-failure-rate"]["comparison"]["favorable_delta"], 0
        )

    def test_rejects_different_dataset_fingerprint_even_when_version_matches(self):
        baseline = evaluate(dataset(), predictions())
        changed_dataset = dataset()
        changed_dataset["samples"][0]["source"]["sha256"] = "f" * 64
        candidate = evaluate(changed_dataset, predictions())

        with self.assertRaisesRegex(EvaluationValidationError, "fingerprint"):
            compare_reports(baseline, candidate)

    def test_malformed_report_is_rejected_without_key_errors(self):
        report = evaluate(dataset(), predictions())
        malformed = copy.deepcopy(report)
        malformed.pop("prediction_set_id")

        with self.assertRaisesRegex(EvaluationValidationError, "prediction_set_id"):
            compare_reports(report, malformed)

    def test_threshold_contract_rejects_unknown_metrics_and_invalid_rules(self):
        unknown = thresholds()
        unknown["rules"][0]["metric"] = "cost"
        with self.assertRaisesRegex(EvaluationValidationError, "not supported"):
            validate_thresholds(unknown)

        empty = thresholds()
        empty["rules"][0].pop("minimum_candidate")
        empty["rules"][0].pop("maximum_regression")
        with self.assertRaisesRegex(EvaluationValidationError, "at least one"):
            validate_thresholds(empty)

        invalid_scope = thresholds()
        invalid_scope["rules"][0]["scope"] = "provider_version.local"
        with self.assertRaisesRegex(EvaluationValidationError, "scope"):
            validate_thresholds(invalid_scope)


if __name__ == "__main__":
    unittest.main()
