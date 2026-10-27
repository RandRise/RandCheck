#!/bin/sh
set -eu
if [ "${1:-test}" = audit ]; then
  python -m pip_audit --disable-pip --no-deps -r /opt/production-requirements.txt --format json -o /results/audit-python.json
  exit 0
fi
ruff check --select E9,F63,F7,F82 apps/api/app tools
python -m compileall -q apps/api/app tools
python -m unittest discover -s tools/ci -p test_prepare_python_fixtures.py -v
python -m unittest discover -s tools/ci -p test_pytest_runner.py -v
python tools/ci/prepare-python-fixtures.py
for group in application native ocr; do
  python tools/run_python_regression.py --group "$group" --manifest "/results/python-$group.json"
done
python tools/run_pytest_regression.py --manifest /results/python-pytest-native.json --junit /results/python-pytest-native.xml
# Synthetic temporary Git repositories only; never commits the checkout.
PYTHONPATH=tools python -m unittest discover -s tools -p test_phase6_secret_review.py -v
PYTHONPATH=tools python -m unittest discover -s tools -p test_publication_inventory.py -v
python -m pip freeze > /results/python-packages.txt
