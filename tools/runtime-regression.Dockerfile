# Test-only derivative; the deployed image does not include these extras.
ARG RUNTIME_IMAGE=uae-document-ai-runtime-20260915
FROM ${RUNTIME_IMAGE}
RUN pip install --no-cache-dir '.[dev,embedding-pilot]'
