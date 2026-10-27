"""Run each declared Python test group in its required dependency environment.

No test is silently skipped: a skipped selected case makes this command fail.
Run all four unittest groups and tools/run_pytest_regression.py for the full
regression; see docs/testing/python-runtimes.md.
"""
import argparse
import json
import os
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(ROOT / 'apps/api'), str(ROOT / 'apps/api/tests')]
NATIVE = 'test_due_date_extraction.DueDateExtractionTests.test_existing_synthetic_invoice_yields_due_date_with_exact_page_evidence'
OCR = 'test_local_extraction.LocalProviderRuntimeFixtureTests'
FIXTURE = 'test_phase2_fixture_cli'


def flatten(suite):
    for item in suite:
        if isinstance(item, unittest.TestSuite):
            yield from flatten(item)
        else:
            yield item


def group_for(test_id):
    if test_id.startswith(FIXTURE + '.'):
        return 'fixture'
    if test_id.startswith(OCR + '.'):
        return 'ocr'
    if test_id == NATIVE:
        return 'native'
    return 'application'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--group', required=True, choices=('application', 'fixture', 'ocr', 'native'))
    parser.add_argument('--manifest', type=Path, help='Write the selected test IDs for unique-coverage auditing.')
    args = parser.parse_args()
    if args.group == 'ocr':
        os.environ['RUN_LOCAL_PROVIDER_INTEGRATION'] = '1'
    if args.group == 'native':
        os.environ['RUN_DUE_DATE_NATIVE'] = '1'
    loader = unittest.TestLoader()
    if args.group == 'application':
        discovered = loader.discover(str(ROOT / 'apps/api/tests'))
    else:
        discovered = loader.loadTestsFromName({'fixture': FIXTURE, 'ocr': OCR, 'native': NATIVE}[args.group])
    selected = [test for test in flatten(discovered) if group_for(test.id()) == args.group]
    if loader.errors or not selected:
        for error in loader.errors:
            print(error, file=sys.stderr)
        return 2
    if args.manifest:
        args.manifest.write_text(json.dumps({'group': args.group, 'tests': [t.id() for t in selected]}, indent=2) + '\n')
    result = unittest.TextTestRunner(verbosity=2).run(unittest.TestSuite(selected))
    return 0 if result.wasSuccessful() and not result.skipped else 1


if __name__ == '__main__':
    sys.exit(main())
