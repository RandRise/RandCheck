"""Versioned extraction HTTP contracts, including trusted worker input limits."""
from datetime import datetime, timezone
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field, field_validator


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
