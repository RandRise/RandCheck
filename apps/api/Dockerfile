FROM python:3.11.15-slim-bookworm@sha256:d29f48a31a8b408ed19272ca1e7b10ebae13b240a27e862d3d4217c528e2e0c3
WORKDIR /app
RUN apt-get update \
    && apt-get install --no-install-recommends -y \
        tesseract-ocr=5.3.0-2 \
        tesseract-ocr-eng=1:4.1.0-2 \
        tesseract-ocr-osd=1:4.1.0-2 \
    && rm -rf /var/lib/apt/lists/*
COPY pyproject.toml ./
COPY apps/api ./apps/api
RUN pip install --no-cache-dir .
CMD ["uvicorn", "app.main:app", "--app-dir", "apps/api", "--host", "0.0.0.0", "--port", "8001"]
