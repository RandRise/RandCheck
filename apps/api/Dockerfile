FROM python:3.11.16-slim-bookworm@sha256:528257d48c1da0dcecc2e725d1ae34498d60c965f1241e39cd6a85a8859bdf84
WORKDIR /app
RUN apt-get update \
    && apt-get install --no-install-recommends -y \
        tesseract-ocr=5.3.0-2 \
        tesseract-ocr-eng=1:4.1.0-2 \
        tesseract-ocr-ara=1:4.1.0-2 \
        tesseract-ocr-osd=1:4.1.0-2 \
    && rm -rf /var/lib/apt/lists/*
COPY pyproject.toml ./
COPY apps/api ./apps/api
RUN pip install --no-cache-dir pip==26.2.1 setuptools==84.0.0 \
    && pip install --no-cache-dir .
CMD ["uvicorn", "app.main:app", "--app-dir", "apps/api", "--host", "0.0.0.0", "--port", "8001"]
