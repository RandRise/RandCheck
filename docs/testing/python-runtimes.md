# Reproducing the Python regression

The September 15 [runtime baseline](../design/phase-6-runtime-baseline.md) verifies
the service on Python 3.11.16. The local Windows 3.11.9 environment remains a
development environment, not the enterprise interpreter baseline.

The test suite has four disjoint unittest groups plus a pytest-native group.
Run all five; both runners treat a
skipped selected test as failure. A single environment cannot satisfy both the
application's Pillow 13.0.0 and the immutable fixture generator's Pillow 9.4.0.
Separate environments are intentional and must not be counted as missing tests.

From the repository root, create an application environment with Python 3.11:

```powershell
python -m venv .venv
.venv/Scripts/python.exe -m pip install --upgrade pip==26.2.1 setuptools==84.0.0
.venv/Scripts/python.exe -m pip install -e '.[dev,embedding-pilot]'
.venv/Scripts/python.exe tools/ci/prepare-python-fixtures.py
.venv/Scripts/python.exe tools/run_python_regression.py --group application
.venv/Scripts/python.exe tools/run_python_regression.py --group native
.venv/Scripts/python.exe tools/run_pytest_regression.py --manifest outputs/python-pytest-native.json
```

The pytest-native runner collects ordinary/parameterized pytest tests and
deselects unittest classes already covered by the other groups. It records exact
node IDs and rejects empty selections, failed collections and skips. This closes
the older unittest-only discovery gap for supplier invoice and Arabic parser
tests. The committed `samples/supplier-invoice/native-table-v0.1.0/` fixture makes
character-geometry tests independent of ignored historical PDF outputs.

The native group uses pinned PDF dependencies from `pyproject.toml`; it does not
need Tesseract. The application group includes PDF auditing, HTTP clients and
embedding token tests. The development extra declares reportlab, and httpx is a
base dependency. Do not run the legacy fixture generator with the application
Pillow version.

The current application pins Pillow 13.0.0. After updating the checkout, reinstall
the project dependencies in the application environment before starting extraction;
the runtime guard deliberately rejects older versions. Keep the historical fixture
environment separate. [Security update and verification](dependency-maintenance-20261027.md).

Create a separate fixture environment using the committed exact requirements:

```powershell
python -m venv tmp/fixture-tests
tmp/fixture-tests/Scripts/python.exe -m pip install -r tools/phase2-fixtures-requirements.txt
tmp/fixture-tests/Scripts/python.exe tools/run_python_regression.py --group fixture
```

Build the pinned native/OCR image and run only its five runtime checks. Mounting
the repository read-only keeps the tested source current without changing fixtures:

```powershell
docker build -f apps/api/Dockerfile -t uae-document-ai-regression .
docker run --rm --network none --mount "type=bind,source=$((Get-Location).Path),target=/workspace,readonly" --workdir /workspace -e PYTHONDONTWRITEBYTECODE=1 uae-document-ai-regression python tools/run_python_regression.py --group ocr
```

The image pins Python, Tesseract and its English model. Record the resolved image
ID with the results. The September 11 run reused already built image
`sha256:8225cc6015de6cf0895a355c5d47fe8923a6a9f87a2fc65946e1f7fa52ffc987`.
No provider key or network is needed by these tests. They exercise fake provider
transports where applicable, and never enable the paid pilot.

The September 15 patched image passed all five OCR cases without regenerating
goldens. Its ID and package audit are in the [runtime report](../evidence/phase-6/P6-runtime-20260915.md).
To run application tests in a derivative of the same service image:

```powershell
docker build -f tools/runtime-regression.Dockerfile --build-arg RUNTIME_IMAGE=uae-document-ai-regression -t uae-document-ai-tests .
docker run --rm --network none --mount "type=bind,source=$((Get-Location).Path),target=/workspace,readonly" --workdir /workspace -e PYTHONDONTWRITEBYTECODE=1 uae-document-ai-tests python tools/run_python_regression.py --group application
```

The tokenizer tests require the existing verified vocabulary at
`.venv/share/tiktoken/cl100k_base.tiktoken`, available through that read-only mount.
The derivative adds test extras; it is not the production service image.

`--manifest PATH` optionally records every selected test ID. The current partition
was 330 application + seven fixture + five OCR + one native = 343 unique tests in
the September record. That historical count excludes later pytest-native cases;
the October 27 focused run selected and passed 153 such cases.
Group counts may grow with new tests; combine manifests by ID, not by adding reruns.
Dependency ranges for framework/development packages remain ranges in
`pyproject.toml`; archive installed versions when auditing a specific run.
