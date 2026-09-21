"""Explicit opt-in transport, not a registered route/adapter or dispatch permit.

Only a coordinator that committed a shared-ledger dispatch may call send().
No environment key loading, retries, redirects, proxies or model fallback.
"""
import asyncio
from dataclasses import dataclass, field
import re

import httpx

from .cited_embedding_tokens import LocalEmbeddingTokenizer, MeasuredEmbeddingRequest

# Leave space for base64 raw bytes and audit metadata in the 512 KiB journal.
# The coordinator must bound its complete serialized envelope before dispatch.
MAX_EMBEDDING_HTTP_BYTES = 256 * 1024


class EmbeddingTransportError(RuntimeError):
    def __init__(self, code, http_status=None, provider_request_id=None):
        self.code = code
        self.http_status = http_status
        self.provider_request_id = provider_request_id
        super().__init__("The embedding transport did not provide an accepted response.")


@dataclass(frozen=True)
class EmbeddingHttpResponse:
    body_utf8: bytes = field(repr=False)
    provider_request_id: str | None


def _request_id(value):
    return value if type(value) is str and re.fullmatch(r"req_[A-Za-z0-9_-]{1,196}", value) else None


class OpenAiEmbeddingTransport:
    def __init__(self, api_key: str, tokenizer: LocalEmbeddingTokenizer, *, enabled=False, test_transport=None):
        if enabled is not True:
            raise EmbeddingTransportError("embedding_transport_disabled")
        if (type(api_key) is not str or not 20 <= len(api_key) <= 512
                or not re.fullmatch(r"sk-[A-Za-z0-9_-]+", api_key)):
            raise EmbeddingTransportError("embedding_key_unavailable")
        if type(tokenizer) is not LocalEmbeddingTokenizer:
            raise EmbeddingTransportError("embedding_tokenizer_unavailable")
        self._key = api_key
        self._tokenizer = tokenizer
        self._test_transport = test_transport

    def __repr__(self):
        return "OpenAiEmbeddingTransport(credentials omitted)"

    async def send(self, request: MeasuredEmbeddingRequest) -> EmbeddingHttpResponse:
        try:
            self._tokenizer.verify(request)
        except Exception:
            raise EmbeddingTransportError("invalid_embedding_request") from None
        status = None
        request_id = None
        try:
            async with asyncio.timeout(30):
                transport = (self._test_transport if self._test_transport is not None
                             else httpx.AsyncHTTPTransport(retries=0, verify=True, trust_env=False))
                async with httpx.AsyncClient(transport=transport, follow_redirects=False, trust_env=False,
                                            timeout=httpx.Timeout(30, connect=5)) as client:
                    async with client.stream("POST", "https://api.openai.com/v1/embeddings",
                                             content=request.body_utf8,
                                             headers={"Authorization": "Bearer " + self._key,
                                                      "Content-Type": "application/json", "Accept": "application/json",
                                                      "Accept-Encoding": "identity"}) as response:
                        status = response.status_code
                        request_id = _request_id(response.headers.get("x-request-id"))
                        if status != 200:
                            raise EmbeddingTransportError("embedding_http_rejected", status, request_id)
                        if (response.headers.get("content-type", "").split(";", 1)[0].strip().lower() != "application/json"
                                or response.headers.get("content-encoding", "identity").lower() != "identity"):
                            raise EmbeddingTransportError("embedding_response_rejected", status, request_id)
                        length = response.headers.get("content-length")
                        if length is not None and (not length.isascii() or not length.isdecimal()
                                                   or int(length) > MAX_EMBEDDING_HTTP_BYTES):
                            raise EmbeddingTransportError("embedding_response_rejected", status, request_id)
                        body = bytearray()
                        async for chunk in response.aiter_raw():
                            if len(body) + len(chunk) > MAX_EMBEDDING_HTTP_BYTES:
                                raise EmbeddingTransportError("embedding_response_rejected", status, request_id)
                            body.extend(chunk)
                        if not body:
                            raise EmbeddingTransportError("embedding_response_rejected", status, request_id)
                        return EmbeddingHttpResponse(bytes(body), request_id)
        except asyncio.CancelledError:
            raise  # Not evidence of no charge: committed accounting must keep its hold.
        except EmbeddingTransportError:
            raise
        except Exception:
            raise EmbeddingTransportError("embedding_transport_uncertain", status, request_id) from None
