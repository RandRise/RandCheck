from dataclasses import asdict
from datetime import datetime, timezone
import hashlib
from pathlib import Path
import re
import time
from uuid import UUID

from fastapi import FastAPI, HTTPException, Request, Response
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from pydantic import BaseModel, ConfigDict, Field, field_validator

from .config import get_settings
from .service_security import authorize, PrivateServiceBoundary
from .extraction import ExtractionError, get_extractor
from .fixture_contract import ContractValidationError, validate_normalized_result

app = FastAPI(title="UAE Document Intelligence Private AI Service", version="0.1.0")
app.add_middleware(PrivateServiceBoundary, settings=lambda: get_settings())

from .cited_routes import router as cited_router
app.include_router(cited_router)
from .cited_answer_routes import router as cited_answer_router
app.include_router(cited_answer_router)


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
        "provider_timeout": 504,
        "provider_unavailable": 503,
        "provider_network_error": 503,
        "provider_throttled": 429,
    }.get(error.code, 422)
    return JSONResponse(
        status_code=status,
        headers={"X-Request-ID": request_id},
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


@app.exception_handler(RequestValidationError)
def request_validation_error_handler(
    request: Request, error: RequestValidationError
) -> JSONResponse:
    del error
    supplied_request_id = request.headers.get("x-request-id", "")
    request_id = (
        supplied_request_id
        if re.fullmatch(r"[A-Za-z0-9._:-]{1,100}", supplied_request_id)
        else "local-request"
    )
    return JSONResponse(
        status_code=422,
        headers={"X-Request-ID": request_id},
        content={
            "schema_version": "2.0",
            "error": {
                "code": "provider_rejected",
                "correlation_id": request_id,
                "retryable": False,
                "summary": "The extraction request is invalid.",
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


class WorkerExtractRequest(BaseModel):
    model_config = ConfigDict(extra="forbid")

    schema_version: str
    job_id: UUID
    document_id: UUID
    extraction_run_id: UUID
    storage_key: str = Field(min_length=1, max_length=255)
    sha256: str = Field(pattern=r"^[0-9a-f]{64}$")
    bytes: int = Field(gt=0, le=10 * 1024 * 1024)
    declared_content_type: str = Field(min_length=1, max_length=100)
    provider_alias: str = Field(pattern=r"^[a-z][a-z0-9_-]{0,99}$")
    attempt: int = Field(ge=1, le=3)
    deadline_utc: datetime
    correlation_id: str = Field(pattern=r"^[A-Za-z0-9._:-]{1,100}$")

    @field_validator("deadline_utc")
    @classmethod
    def require_utc_deadline(cls, value: datetime) -> datetime:
        if value.tzinfo is None or value.utcoffset() != timezone.utc.utcoffset(value):
            raise ValueError("deadline_utc must be an explicit UTC timestamp")
        return value


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok", "service": "document-intelligence-ai"}


@app.post("/extract", response_model=ExtractResponse)
def extract(request: ExtractRequest) -> ExtractResponse:
    settings = get_settings()
    if not settings.enable_legacy_extract:
        raise HTTPException(status_code=404, detail="Not found")
    provider_name = settings.extraction_provider
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


@app.post("/internal/extractions", response_model=ExtractResponse)
def extract_for_worker(
    payload: WorkerExtractRequest, response: Response, http_request: Request
) -> ExtractResponse:
    response.headers["X-Request-ID"] = payload.correlation_id
    authorize(http_request, get_settings())
    if payload.schema_version != "2.0":
        raise ExtractionError(
            "unsupported_schema_version",
            "The requested extraction schema version is not supported.",
        )
    if datetime.now(timezone.utc) >= payload.deadline_utc:
        raise ExtractionError(
            "provider_timeout",
            "The extraction attempt deadline has expired.",
            retryable=True,
        )
    path = _resolve_worker_storage(payload)
    try:
        extractor = get_extractor(payload.provider_alias)
    except (ValueError, RuntimeError) as error:
        raise ExtractionError(
            "provider_configuration_error",
            "The requested extraction provider is not configured.",
        ) from error
    remaining_seconds = (payload.deadline_utc - datetime.now(timezone.utc)).total_seconds()
    deadline_monotonic = time.monotonic() + max(0, remaining_seconds)
    result = extractor.extract(
        path, payload.declared_content_type, deadline_monotonic=deadline_monotonic
    )
    if datetime.now(timezone.utc) >= payload.deadline_utc:
        raise ExtractionError(
            "provider_timeout",
            "The extraction provider did not finish within this attempt.",
            retryable=True,
        )
    payload = asdict(result)
    try:
        validate_normalized_result(payload)
    except ContractValidationError as error:
        raise ExtractionError(
            "malformed_provider_result",
            "The extraction provider returned an invalid result.",
        ) from error
    return ExtractResponse.model_validate(payload)


def _resolve_worker_storage(request: WorkerExtractRequest) -> Path:
    key = request.storage_key
    if "\\" in key or key.startswith("/") or "//" in key:
        raise _invalid_storage_key()
    parts = key.split("/")
    if any(part in {"", ".", ".."} for part in parts):
        raise _invalid_storage_key()
    expected_prefix = ["organizations", parts[1] if len(parts) > 1 else "", "documents"]
    if len(parts) != 5 or parts[:3] != expected_prefix or not re.fullmatch(
        r"[0-9a-f]{32}", parts[1]
    ) or parts[3] != request.document_id.hex or not re.fullmatch(
        r"source\.[a-z0-9]{1,10}", parts[4]
    ):
        raise _invalid_storage_key()

    try:
        root = Path(get_settings().storage_shared_root).resolve(strict=True)
        if not root.is_dir():
            raise _invalid_storage_key()
    except (FileNotFoundError, OSError, ValueError) as error:
        raise _invalid_storage_key() from error
    candidate = root
    try:
        for part in parts:
            candidate = candidate / part
            if candidate.is_symlink():
                raise _invalid_storage_key()
        resolved = candidate.resolve(strict=True)
        resolved.relative_to(root)
    except (FileNotFoundError, OSError, ValueError) as error:
        raise _invalid_storage_key() from error
    if not resolved.is_file():
        raise _invalid_storage_key()

    try:
        stat = resolved.stat()
        if stat.st_size != request.bytes or stat.st_size > 10 * 1024 * 1024:
            raise _integrity_mismatch()
        digest = hashlib.sha256()
        total = 0
        with resolved.open("rb") as stream:
            while chunk := stream.read(64 * 1024):
                total += len(chunk)
                if total > 10 * 1024 * 1024:
                    raise _integrity_mismatch()
                digest.update(chunk)
        if total != request.bytes or digest.hexdigest() != request.sha256:
            raise _integrity_mismatch()
    except OSError as error:
        raise _invalid_storage_key() from error
    return resolved


def _invalid_storage_key() -> ExtractionError:
    return ExtractionError(
        "invalid_storage_key",
        "The stored document reference is invalid or unavailable.",
    )


def _integrity_mismatch() -> ExtractionError:
    return ExtractionError(
        "content_integrity_mismatch",
        "The stored document failed its integrity check.",
    )
