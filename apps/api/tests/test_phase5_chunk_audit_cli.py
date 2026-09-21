import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[3]


class Phase5ChunkAuditCliTests(unittest.TestCase):
    def test_committed_pack_coverage_and_report_replay(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "chunk-audit.json"
            reports = []
            for _ in range(2):
                result = subprocess.run([sys.executable, str(ROOT / "tools/phase5_chunk_audit.py"),
                                         "--output", str(output)], capture_output=True, text=True,
                                        encoding="utf-8", timeout=30)
                self.assertEqual(0, result.returncode, result.stderr)
                report = json.loads(output.read_text("utf-8"))
                self.assertEqual(json.loads(result.stdout), report)
                reports.append(report)
            self.assertEqual(reports[0], reports[1])
            self.assertEqual(46, reports[0]["labels_contained_in_one_chunk"])
            self.assertEqual(6, reports[0]["total_chunks"])
            self.assertTrue(reports[0]["passed"])
            self.assertFalse(reports[0]["claim_eligible"])

    def test_too_small_profile_reports_misses_without_lowering_gate(self):
        result = subprocess.run([sys.executable, str(ROOT / "tools/phase5_chunk_audit.py"),
                                 "--max-bytes", "32", "--overlap-bytes", "0"],
                                capture_output=True, text=True, encoding="utf-8", timeout=30)
        report = json.loads(result.stdout)
        self.assertEqual(1, result.returncode)
        self.assertFalse(report["passed"])
        self.assertGreater(len(report["misses"]), 0)
        self.assertEqual(46, report["evidence_labels"])
