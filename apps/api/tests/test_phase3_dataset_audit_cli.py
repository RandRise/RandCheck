import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


REPO_ROOT = Path(__file__).resolve().parents[3]
TOOL = REPO_ROOT / "tools" / "phase3_dataset_audit.py"
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


class Phase3DatasetAuditCliTests(unittest.TestCase):
    def test_smoke_audit_is_deterministic_and_returns_not_ready_exit(self):
        with tempfile.TemporaryDirectory() as directory:
            first = Path(directory) / "first.json"
            second = Path(directory) / "second.json"
            arguments = (
                "--dataset",
                str(SMOKE_ROOT / "dataset.json"),
                "--source-register",
                str(SMOKE_ROOT / "source-register.json"),
                "--annotation-ledger",
                str(SMOKE_ROOT / "annotation-ledger.json"),
            )

            first_result = run_cli(*arguments, "--output", str(first))
            second_result = run_cli(*arguments, "--output", str(second))

            self.assertEqual(4, first_result.returncode, first_result.stderr)
            self.assertEqual(4, second_result.returncode, second_result.stderr)
            self.assertEqual(first.read_bytes(), second.read_bytes())
            audit = json.loads(first.read_text(encoding="utf-8"))
            self.assertFalse(audit["claim_ready"])
            self.assertEqual(3, audit["blocker_count"])

    def test_invalid_audit_input_returns_safe_exit_two(self):
        with tempfile.TemporaryDirectory() as directory:
            invalid = Path(directory) / "invalid.json"
            invalid.write_text("{}", encoding="utf-8")

            result = run_cli(
                "--dataset",
                str(invalid),
                "--source-register",
                str(SMOKE_ROOT / "source-register.json"),
                "--annotation-ledger",
                str(SMOKE_ROOT / "annotation-ledger.json"),
            )

            self.assertEqual(2, result.returncode)
            self.assertIn("Dataset audit input rejected", result.stderr)
            self.assertNotIn("Traceback", result.stderr)


if __name__ == "__main__":
    unittest.main()
