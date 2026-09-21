FROM pgvector/pgvector:pg16@sha256:a36250871de0833b8757561c72f2477ef1ddd1101afa4e617fb552e0de514c6b AS postgres
FROM python:3.11.16-slim-bookworm@sha256:528257d48c1da0dcecc2e725d1ae34498d60c965f1241e39cd6a85a8859bdf84 AS python
FROM mcr.microsoft.com/dotnet/sdk:8.0.425@sha256:5ef85cc12cb25be6ec319a7392d1e9efd53c3bc8abb971c53d8058a473f09053
RUN apt-get update && apt-get install --no-install-recommends -y libpq5 libreadline8 liblz4-1 libzstd1 && rm -rf /var/lib/apt/lists/*
COPY --from=python /usr/local/ /usr/local/
RUN ldconfig
COPY --from=postgres /usr/lib/postgresql/16/bin/ /usr/local/bin/
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false PHASE5_PYTHON=python3 DOTNET_PROCESSOR_COUNT=2
WORKDIR /workspace
COPY global.json ./
COPY apps/backend apps/backend
COPY pyproject.toml ./
COPY apps/api apps/api
RUN pip install --no-cache-dir pip==26.2.1 setuptools==84.0.0 && pip install --no-cache-dir '.[embedding-pilot]'
COPY samples samples
COPY docs docs
COPY tools tools
RUN python tools/phase5_prepare_tokenizer.py --download
RUN dotnet build apps/backend/UaeDocumentIntelligence.sln -c Release -m:1 -p:UseSharedCompilation=false -p:NuGetAudit=false
CMD ["sh", "tools/ci/backend.sh"]
