"""Explicit standalone loopback embedding pilot. Never imported by the default API."""
import os
from pathlib import Path

from fastapi import FastAPI, HTTPException, Request
from .cited_answer_routes import _authorize, _body
from .cited_embedding_protocol import EmbeddingProtocolError
from .cited_embedding_tokens import LocalEmbeddingTokenizer
from .cited_embedding_transport import OpenAiEmbeddingTransport
from .cited_embedding_workflow import prepare, propose


def create_app():
    if os.getenv("CITED_EMBEDDING_PILOT") != "small-v1" or not os.getenv("WORKER_SHARED_SECRET"):
        raise RuntimeError("Embedding pilot is not explicitly enabled.")
    tokenizer = LocalEmbeddingTokenizer(Path(os.getenv("EMBEDDING_VOCABULARY", "")))
    transport = OpenAiEmbeddingTransport(os.getenv("OPENAI_API_KEY", ""), tokenizer, enabled=True)
    return build_app(tokenizer, transport)


def build_app(tokenizer, transport):
    app = FastAPI(docs_url=None, redoc_url=None, openapi_url=None)

    @app.post("/internal/cited/embedding-prepare")
    async def prepare_route(request: Request):
        _authorize(request)
        try:
            return prepare(await _body(request, 49152), tokenizer)[0]
        except EmbeddingProtocolError:
            raise HTTPException(422, "Embedding preparation rejected") from None
        except TimeoutError:
            raise HTTPException(408, "Embedding preparation timed out") from None

    @app.post("/internal/cited/embedding-proposal")
    async def propose_route(request: Request):
        _authorize(request)
        try:
            return await propose(await _body(request, 98304), tokenizer, transport)
        except EmbeddingProtocolError:
            raise HTTPException(422, "Embedding dispatch rejected") from None
        except TimeoutError:
            raise HTTPException(408, "Embedding dispatch timed out") from None

    return app
