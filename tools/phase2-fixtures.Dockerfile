FROM python:3.11.15-slim-bookworm@sha256:d29f48a31a8b408ed19272ca1e7b10ebae13b240a27e862d3d4217c528e2e0c3

ENV LANG=C.UTF-8 \
    LC_ALL=C.UTF-8 \
    PYTHONDONTWRITEBYTECODE=1 \
    PYTHONHASHSEED=0 \
    TZ=UTC

WORKDIR /workspace

COPY tools/phase2-fixtures-requirements.txt /opt/phase2-fixtures-requirements.txt
RUN pip install --disable-pip-version-check --no-cache-dir \
    --requirement /opt/phase2-fixtures-requirements.txt

COPY tools/phase2_fixtures.py /opt/phase2_fixtures.py
COPY tools/phase2_fixture_source.json /opt/phase2_fixture_source.json
COPY apps/api/app/fixture_contract.py /opt/app/fixture_contract.py

ENTRYPOINT ["python", "/opt/phase2_fixtures.py"]
