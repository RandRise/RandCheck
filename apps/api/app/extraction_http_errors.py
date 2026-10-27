"""Safe HTTP errors for extraction, without validation inputs or source contents."""
import re

from fastapi import Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse

from .extraction import ExtractionError


EXTRACTION_ERROR_STATUSES = {
    "unsupported_media_type": 415,
    "provider_timeout": 504,
    "ocr_unavailable": 503,
    "provider_unavailable": 503,
    "provider_network_error": 503,
    "provider_throttled": 429,
}


def _safe_request_id(request: Request) -> str:
    """Echo only the contract's bounded header characters, never arbitrary input."""
    supplied = request.headers.get("x-request-id", "")
    return supplied if re.fullmatch(r"[A-Za-z0-9._:-]{1,100}", supplied) else "local-request"


def extraction_error_handler(request: Request, error: ExtractionError) -> JSONResponse:
    request_id = _safe_request_id(request)
    return JSONResponse(
        status_code=EXTRACTION_ERROR_STATUSES.get(error.code, 422),
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


def request_validation_error_handler(
    request: Request, error: RequestValidationError
) -> JSONResponse:
    # FastAPI's default validation response can include the rejected document input.
    del error
    request_id = _safe_request_id(request)
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
