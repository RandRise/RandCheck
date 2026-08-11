from pathlib import Path

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field

from .config import get_settings
from .extraction import get_extractor

app = FastAPI(title="UAE Document Intelligence Private AI Service", version="0.1.0")


class ExtractRequest(BaseModel):
    file_path: str = Field(alias="filePath", description="Path in the volume shared with the API")
    content_type: str = Field(alias="contentType")


class FieldResponse(BaseModel):
    name: str
    value: str
    confidence: float
    page: int


class ExtractResponse(BaseModel):
    document_type: str
    language: str
    extractor: str
    fields: list[FieldResponse]


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok", "service": "document-intelligence-ai"}


@app.post("/extract", response_model=ExtractResponse)
def extract(request: ExtractRequest) -> ExtractResponse:
    path = Path(request.file_path).resolve()
    if not path.is_file():
        raise HTTPException(status_code=404, detail="Shared document not found")
    result = get_extractor(get_settings().extraction_provider).extract(path, request.content_type)
    return ExtractResponse(
        document_type=result.document_type,
        language=result.language,
        extractor=result.extractor,
        fields=[FieldResponse(**field.__dict__) for field in result.fields],
    )
