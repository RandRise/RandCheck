# Evaluation image only; reuses the previously verified local service runtime.
ARG RUNTIME_IMAGE=uae-document-ai-runtime-20260915:latest
FROM ${RUNTIME_IMAGE}
RUN apt-get update && apt-get install --no-install-recommends -y \
    tesseract-ocr-ara=1:4.1.0-2 fonts-dejavu-core=2.37-6 \
    && rm -rf /var/lib/apt/lists/*
RUN sha256sum /usr/share/tesseract-ocr/5/tessdata/ara.traineddata
