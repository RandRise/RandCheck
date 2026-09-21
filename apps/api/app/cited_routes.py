import json
from fastapi import APIRouter, HTTPException, Request, Response
from starlette.concurrency import run_in_threadpool
from .config import get_settings
from .service_security import authorize
from .cited_chunking import ChunkingError, chunk_document
from .cited_retrieval import lexical_plan

router = APIRouter()
MAX_BODY = 16 * 1024 * 1024


async def _payload(request: Request, limit: int):
    authorize(request, get_settings())
    body = bytearray()
    async for part in request.stream():
        body.extend(part)
        if len(body) > limit:
            raise HTTPException(413, "Cited request exceeds the byte limit")
    try:
        result = json.loads(body)
        if not isinstance(result, dict):
            raise ValueError()
        return result
    except (ValueError, UnicodeError):
        raise HTTPException(422, "Invalid cited request") from None


@router.post("/internal/cited/chunks")
async def chunks(request: Request, response: Response):
    payload = await _payload(request, MAX_BODY)
    try:
        result = await run_in_threadpool(chunk_document, payload)
        response.headers["X-Request-ID"] = str(result["indexId"])
        return result
    except (ChunkingError, ValueError, TypeError, KeyError, UnicodeError):
        # Do not echo the source text or exception payload.
        raise HTTPException(422, "Invalid chunking request") from None


@router.post("/internal/cited/lexical-plan")
async def plan(request: Request):
    payload = await _payload(request, 32768)
    try:
        if set(payload) != {"question", "topK"}:
            raise ValueError()
        return lexical_plan(payload["question"], payload["topK"])
    except (ChunkingError, ValueError, TypeError, KeyError, UnicodeError):
        raise HTTPException(422, "Invalid lexical request") from None
