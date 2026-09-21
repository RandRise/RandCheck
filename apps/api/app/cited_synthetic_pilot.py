"""Standalone loopback pilot app, not imported by main. Explicit launch only."""
import os

from fastapi import FastAPI, HTTPException, Request
from .cited_answer_routes import _authorize, _body
from .cited_answers import AnswerBoundaryError
from .cited_openai_protocol import OpenAiProtocolError
from .cited_openai_transport import OpenAiResponsesTransport
from .cited_openai_workflow import prepare, propose


def create_app():
    profile = os.getenv("CITED_SYNTHETIC_PILOT")
    if profile not in ("nano-v1", "nano-low-v1") or not os.getenv("WORKER_SHARED_SECRET"):
        raise RuntimeError("Synthetic pilot is not explicitly enabled.")
    effort = "low" if profile == "nano-low-v1" else "minimal"
    transport = OpenAiResponsesTransport(os.getenv("OPENAI_API_KEY", ""), enabled=True)
    app = FastAPI(docs_url=None, redoc_url=None, openapi_url=None)

    @app.post("/internal/cited/answer-prepare")
    async def prepare_route(request: Request):
        _authorize(request)
        try:
            return prepare(await _body(request, 65536), effort)[0]
        except (ValueError, TypeError, KeyError, UnicodeError, RecursionError, AnswerBoundaryError, OpenAiProtocolError):
            raise HTTPException(422, "Pilot preparation rejected") from None
        except TimeoutError:
            raise HTTPException(408, "Pilot request timed out") from None

    @app.post("/internal/cited/answer-proposal")
    async def proposal_route(request: Request):
        _authorize(request)
        try:
            return await propose(await _body(request, 98304), transport, effort)
        except (ValueError, TypeError, KeyError, UnicodeError, RecursionError, AnswerBoundaryError, OpenAiProtocolError):
            raise HTTPException(422, "Pilot dispatch rejected") from None
        except TimeoutError:
            raise HTTPException(408, "Pilot request timed out") from None

    return app
