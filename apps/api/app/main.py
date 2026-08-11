from dataclasses import asdict
from pathlib import Path
import re

from fastapi import FastAPI, HTTPException, Request
from fastapi.responses import JSONResponse
from pydantic import BaseModel, Field

from .config import get_settings
from .extraction import ExtractionError, get_extractor

app = FastAPI(title="UAE Document Intelligence Private AI Service", version="0.1.0")


@app.exception_handler(ExtractionError)
def extraction_error_handler(request: Request, error: ExtractionError) -> JSONResponse:
    supplied_request_id = request.headers.get("x-request-id", "")
    request_id = (
        supplied_request_id
        if re.fullmatch(r"[A-Za-z0-9._:-]{1,100}", supplied_request_id)
        else "local-request"
    )
    status = {
        "unsupported_media_type": 415,
        "provider_timeout": 504,
        "ocr_unavailable": 503,
    }.get(error.code, 422)
    return JSONResponse(
        status_code=status,
        content={
            "schema_version": "2.0",
            "error": {
                "code": error.code,
                "correlation_id": request_id,
                "retryable": error.retryable,
                "summary": error.summary,
            },
        },
    )


class ExtractRequest(BaseModel):
    file_path: str = Field(alias="filePath", description="Path in the volume shared with the API")
    content_type: str = Field(alias="contentType")


class PageResponse(BaseModel):
    page_number: int
    canonical_text: str
    rotation_degrees: int


class EvidenceResponse(BaseModel):
    page_number: int
    quote: str | None
    start_utf8: int | None
    end_utf8: int | None
    polygon: list[list[float]] | None
    coordinate_unit: str | None


class FieldResponse(BaseModel):
    key: str
    occurrence: int
    value_type: str
    value: str
    normalized_value: str | None
    confidence: float | None
    evidence: list[EvidenceResponse]


class ProviderResponse(BaseModel):
    alias: str
    adapter_version: str
    model: str
    model_version: str


class MetricsResponse(BaseModel):
    provider_duration_ms: int
    page_count: int
    estimated_cost: float | None
    cost_currency: str | None
    price_basis: str | None


class WarningResponse(BaseModel):
    code: str
    severity: str
    safe_summary: str
    page_number: int | None
    field_key: str | None
    occurrence: int | None


class ExtractResponse(BaseModel):
    schema_version: str
    outcome: str
    document_type: str
    language: str
    provider: ProviderResponse
    metrics: MetricsResponse
    pages: list[PageResponse]
    fields: list[FieldResponse]
    warnings: list[WarningResponse]


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok", "service": "document-intelligence-ai"}


@app.post("/extract", response_model=ExtractResponse)
def extract(request: ExtractRequest) -> ExtractResponse:
    provider_name = get_settings().extraction_provider
    if provider_name == "local":
        raise HTTPException(
            status_code=503,
            detail="Local provider activation requires the trusted Phase 2 worker request.",
        )
    path = Path(request.file_path).resolve()
    if not path.is_file():
        raise HTTPException(status_code=404, detail="Shared document not found")
    result = get_extractor(provider_name).extract(path, request.content_type)
    return ExtractResponse.model_validate(asdict(result))
