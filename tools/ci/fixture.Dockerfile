# Preserve the immutable fixture generator's separate dependency contract.
FROM python:3.11.15-slim-bookworm@sha256:d29f48a31a8b408ed19272ca1e7b10ebae13b240a27e862d3d4217c528e2e0c3
WORKDIR /workspace
COPY tools/phase2-fixtures-requirements.txt /opt/phase2-fixtures-requirements.txt
RUN pip install --disable-pip-version-check --no-cache-dir -r /opt/phase2-fixtures-requirements.txt
COPY apps/api apps/api
COPY tools tools
COPY samples samples
ENV PYTHONDONTWRITEBYTECODE=1 PYTHONHASHSEED=0 TZ=UTC
CMD ["python", "tools/run_python_regression.py", "--group", "fixture", "--manifest", "/results/python-fixture.json"]
