FROM python:3.11-slim
WORKDIR /app
COPY pyproject.toml ./
COPY apps/api ./apps/api
RUN pip install --no-cache-dir .
CMD ["uvicorn", "app.main:app", "--app-dir", "apps/api", "--host", "0.0.0.0", "--port", "8001"]
