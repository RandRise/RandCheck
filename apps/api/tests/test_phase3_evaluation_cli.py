import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


REPO_ROOT = Path(__file__).resolve().parents[3]
TOOL = REPO_ROOT / "tools" / "phase3_evaluate.py"
COMPARE_TOOL = REPO_ROOT / "tools" / "phase3_compare.py"
SMOKE_ROOT = REPO_ROOT / "samples" / "phase-3" / "smoke-v0.1.0"


def run_cli(*arguments: str) -> subprocess.CompletedProcess[str]:
    environment = os.environ.copy()
    environment.update({"PYTHONHASHSEED": "0", "TZ": "UTC"})
    return subprocess.run(
        [sys.executable, str(TOOL), *arguments],
        cwd=REPO_ROOT,
        env=environment,
        capture_output=True,
        text=True,
        timeout=30,
        check=False,
    )


def run_compare_cli(*arguments: str) -> subprocess.CompletedProcess[str]:
    environment = os.environ.copy()
    environment.update({"PYTHONHASHSEED": "0", "TZ": "UTC"})
    return subprocess.run(
        [sys.executable, str(COMPARE_TOOL), *arguments],
        cwd=REPO_ROOT,
        env=environment,
        capture_output=True,
        text=True,
        timeout=30,
        check=False,
    )


class Phase3EvaluationCliTests(unittest.TestCase):
    def test_committed_smoke_report_is_byte_identical_across_runs(self):
        with tempfile.TemporaryDirectory() as directory:
            first = Path(directory) / "first.json"
            second = Path(directory) / "second.json"
            arguments = (
                "--dataset",
                str(SMOKE_ROOT / "dataset.json"),
                "--predictions",
                str(SMOKE_ROOT / "local-predictions.json"),
            )

            first_result = run_cli(*arguments, "--output", str(first))
            second_result = run_cli(*arguments, "--output", str(second))

            self.assertEqual(0, first_result.returncode, first_result.stderr)
            self.assertEqual(0, second_result.returncode, second_result.stderr)
            self.assertEqual(first.read_bytes(), second.read_bytes())
            report = json.loads(first.read_text(encoding="utf-8"))
            self.assertEqual("0.1.0", report["dataset"]["version"])
            self.assertEqual(2, report["dataset"]["sample_count"])
            self.assertFalse(report["dataset"]["claim_eligible"])
            self.assertEqual(
                {"field", "layout", "scan_type", "provider_version"},
                set(report["breakdowns"]),
            )

    def test_invalid_dataset_version_returns_safe_nonzero_result(self):
        with tempfile.TemporaryDirectory() as directory:
            invalid = Path(directory) / "invalid.json"
            invalid.write_text(
                json.dumps({"dataset_schema_version": "9.0"}), encoding="utf-8"
            )

            result = run_cli(
                "--dataset",
                str(invalid),
                "--predictions",
                str(SMOKE_ROOT / "local-predictions.json"),
            )

            self.assertEqual(2, result.returncode)
            self.assertIn("Evaluation input rejected", result.stderr)
            self.assertNotIn("Traceback", result.stderr)

    def test_self_comparison_is_deterministic_and_passes_strict_smoke_thresholds(self):
        report = REPO_ROOT / "outputs" / "phase-3" / "smoke-v0.1.0-report.json"
        thresholds = SMOKE_ROOT / "strict-thresholds.json"
        with tempfile.TemporaryDirectory() as directory:
            first = Path(directory) / "first.json"
            second = Path(directory) / "second.json"
            arguments = (
                "--baseline",
                str(report),
                "--candidate",
                str(report),
                "--thresholds",
                str(thresholds),
            )

            first_result = run_compare_cli(*arguments, "--output", str(first))
            second_result = run_compare_cli(*arguments, "--output", str(second))

            self.assertEqual(0, first_result.returncode, first_result.stderr)
            self.assertEqual(0, second_result.returncode, second_result.stderr)
            self.assertEqual(first.read_bytes(), second.read_bytes())
            comparison = json.loads(first.read_text(encoding="utf-8"))
            self.assertTrue(comparison["thresholds"]["passed"])
            self.assertEqual(4, comparison["thresholds"]["evaluated_rule_count"])

    def test_comparison_cli_returns_three_when_a_threshold_fails(self):
        report_path = REPO_ROOT / "outputs" / "phase-3" / "smoke-v0.1.0-report.json"
        thresholds = SMOKE_ROOT / "strict-thresholds.json"
        with tempfile.TemporaryDirectory() as directory:
            candidate = Path(directory) / "candidate.json"
            output = Path(directory) / "comparison.json"
            payload = json.loads(report_path.read_text(encoding="utf-8"))
            payload["prediction_set_id"] = "synthetic-regression-test"
            payload["metrics"]["recall"] = 0.5
            candidate.write_text(json.dumps(payload), encoding="utf-8")

            result = run_compare_cli(
                "--baseline",
                str(report_path),
                "--candidate",
                str(candidate),
                "--thresholds",
                str(thresholds),
                "--output",
                str(output),
            )

            self.assertEqual(3, result.returncode, result.stderr)
            comparison = json.loads(output.read_text(encoding="utf-8"))
            self.assertFalse(comparison["thresholds"]["passed"])
            failed = [
                item
                for item in comparison["thresholds"]["results"]
                if not item["passed"]
            ]
            self.assertEqual(["overall-recall"], [item["rule_id"] for item in failed])


if __name__ == "__main__":
    unittest.main()
