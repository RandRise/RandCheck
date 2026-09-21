FROM python:3.11.16-slim-bookworm@sha256:528257d48c1da0dcecc2e725d1ae34498d60c965f1241e39cd6a85a8859bdf84
WORKDIR /app
RUN apt-get update && apt-get install --no-install-recommends -y tesseract-ocr=5.3.0-2 tesseract-ocr-eng=1:4.1.0-2 tesseract-ocr-osd=1:4.1.0-2 git && rm -rf /var/lib/apt/lists/*
COPY pyproject.toml ./
COPY apps/api ./apps/api
RUN pip install --no-cache-dir pip==26.2.1 setuptools==84.0.0 && pip install --no-cache-dir . \
    && pip freeze | sed '/^uae-document-intelligence-ai/d' > /opt/production-requirements.txt
RUN pip install --no-cache-dir '.[dev,embedding-pilot]' pip-audit==2.10.1
COPY tools tools
COPY samples samples
COPY docs docs
RUN python tools/phase5_prepare_tokenizer.py --download
ENV PYTHONDONTWRITEBYTECODE=1 OMP_THREAD_LIMIT=1
CMD ["sh", "tools/ci/python.sh"]
