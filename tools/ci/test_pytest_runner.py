"""Exercise the CI runner with actual pytest collection, not a mocked item list."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("native_runner", Path(__file__).parents[1] / "run_pytest_regression.py")
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class PytestRunnerTests(unittest.TestCase):
    def run_fixture(self, source):
        # Stay under the repository's pytest configuration; an external system
        # temp directory can trigger configuration discovery across the drive.
        temporary_root = Path(__file__).resolve().parents[2] / "tmp"
        temporary_root.mkdir(exist_ok=True)
        with tempfile.TemporaryDirectory(prefix="pytest-runner-", dir=temporary_root) as temporary:
            root = Path(temporary)
            # Unique names avoid Python module caching between in-process runs.
            test = root / ("test_" + root.name.replace("-", "_") + ".py")
            test.write_text(source, encoding="utf-8")
            manifest = root / "manifest.json"
            result = runner.run_tests(test, manifest)
            return result, json.loads(manifest.read_text("utf-8"))

    def test_native_parameters_run_without_repeating_unittest_cases(self):
        result, report = self.run_fixture("""
import unittest
import pytest
class Existing(unittest.TestCase):
    def test_elsewhere(self):
        raise AssertionError('Already covered by the separate unittest runner')
@pytest.mark.parametrize('value', [1, 2])
def test_native(value):
    assert value > 0
""")
        self.assertEqual(0, result)
        self.assertEqual(2, len(report["tests"]))
        self.assertTrue(all("test_native[" in name for name in report["tests"]))
        self.assertEqual([], report["skipped"])

    def test_skipped_native_case_fails_the_group(self):
        result, report = self.run_fixture("import pytest\ndef test_skip():\n    pytest.skip('Missing prerequisite')\n")
        self.assertEqual(1, result)
        self.assertEqual(1, len(report["skipped"]))

    def test_failed_native_case_fails_the_group(self):
        result, report = self.run_fixture("def test_failure():\n    assert False\n")
        self.assertEqual(1, result)
        self.assertNotEqual(0, report["pytestExitCode"])

    def test_no_native_cases_cannot_claim_success(self):
        result, report = self.run_fixture("import unittest\nclass Existing(unittest.TestCase):\n    def test_elsewhere(self):\n        pass\n")
        self.assertEqual(1, result)
        self.assertEqual([], report["tests"])
