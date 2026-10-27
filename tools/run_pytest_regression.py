"""Run pytest-native cases omitted by unittest discovery, without duplicating it.

The existing four unittest groups retain their separate runtime requirements.
This runner collects all modules, deselects unittest.TestCase subclasses, records
the exact selected node IDs and fails on empty selection or skipped cases.
"""
import argparse
import json
from pathlib import Path
import sys
import unittest

import pytest

ROOT = Path(__file__).resolve().parents[1]


class NativeCases:
    def __init__(self):
        self.selected = []
        self.skipped = []

    def pytest_collection_modifyitems(self, config, items):
        deselected = []
        selected = []
        for item in items:
            parent = item.getparent(pytest.Class)
            if parent and issubclass(parent.obj, unittest.TestCase):
                deselected.append(item)
            else:
                selected.append(item)
        items[:] = selected
        config.hook.pytest_deselected(items=deselected)
        self.selected = [item.nodeid for item in selected]

    def pytest_runtest_logreport(self, report):
        if report.skipped:
            self.skipped.append(report.nodeid)

    def pytest_collectreport(self, report):
        if report.skipped:
            self.skipped.append(report.nodeid)


def run_tests(test_path, manifest, junit=None):
    collector = NativeCases()
    command = [str(test_path), "-q"]
    if junit:
        command.append(f"--junitxml={junit}")
    result = pytest.main(command, plugins=[collector])
    manifest.write_text(json.dumps({
        "group": "pytest-native", "tests": collector.selected,
        "skipped": collector.skipped, "pytestExitCode": int(result),
    }, indent=2) + "\n", encoding="utf-8")
    return 0 if result == 0 and collector.selected and not collector.skipped else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--junit", type=Path)
    args = parser.parse_args()
    return run_tests(ROOT / "apps/api/tests", args.manifest, args.junit)


if __name__ == "__main__":
    sys.exit(main())
