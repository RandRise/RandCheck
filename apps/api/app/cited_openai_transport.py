"""Unregistered, explicit-opt-in transport. The caller must own committed dispatch.

No environment/key-file loading, retries, redirects, alternate hosts or model fallback.
This module alone is NOT the application pilot enablement or a ledger permission.
"""
import asyncio
from dataclasses import dataclass, field
import re
import ssl

import httpx

from .cited_openai_protocol import OpenAiRequest, prepare_openai_request, MAX_PROVIDER_RESPONSE_BYTES


class OpenAiTransportError(RuntimeError):
    def __init__(self, code, http_status=None, provider_request_id=None, failure_kind=None):
        self.code = code
        self.http_status = http_status
        self.provider_request_id = provider_request_id
        self.failure_kind = failure_kind
        super().__init__("The OpenAI transport did not provide an accepted response.")


@dataclass(frozen=True)
class OpenAiHttpResponse:
    body_utf8: bytes = field(repr=False)
    provider_request_id: str | None


def _request_id(value):
    return value if isinstance(value, str) and re.fullmatch(r"req_[A-Za-z0-9_-]{1,196}", value) else None


class OpenAiResponsesTransport:
    def __init__(self, api_key: str, *, enabled=False, test_transport=None):
        if enabled is not True:
            raise OpenAiTransportError("openai_transport_disabled")
        if (type(api_key) is not str or not 20 <= len(api_key) <= 512
                or not re.fullmatch(r"sk-[A-Za-z0-9_-]+", api_key)):
            raise OpenAiTransportError("openai_key_unavailable")
        self._key = api_key
        self._test_transport = test_transport

    def __repr__(self):
        return "OpenAiResponsesTransport(credentials omitted)"

    async def send_counting(self, request: OpenAiRequest) -> OpenAiHttpResponse:
        return await self._send(request, counting=True)

    async def send_generation(self, request: OpenAiRequest) -> OpenAiHttpResponse:
        # Only a committed, one-attempt coordinator may call this in the future integration.
        return await self._send(request, counting=False)

    async def _send(self, request, *, counting):
        try:
            if type(request) is not OpenAiRequest or prepare_openai_request(request.source, request.reasoning_effort) != request:
                raise ValueError()
        except (ValueError, TypeError, AttributeError):
            raise OpenAiTransportError("invalid_openai_request") from None
        path = "/responses/input_tokens" if counting else "/responses"
        body = request.counting_utf8 if counting else request.generation_utf8
        limit = 1024 if counting else MAX_PROVIDER_RESPONSE_BYTES
        deadline = 15 if counting else 30
        status = None
        request_id = None
        try:
            async with asyncio.timeout(deadline):
                transport = (self._test_transport if self._test_transport is not None
                             else httpx.AsyncHTTPTransport(retries=0, verify=ssl.create_default_context(), trust_env=False))
                async with httpx.AsyncClient(transport=transport, follow_redirects=False, trust_env=False,
                                            timeout=httpx.Timeout(deadline, connect=5)) as client:
                    async with client.stream("POST", "https://api.openai.com/v1" + path,
                                             content=body, headers={"Authorization": "Bearer " + self._key,
                                                 "Content-Type": "application/json", "Accept": "application/json",
                                                 "Accept-Encoding": "identity"}) as response:
                        status = response.status_code
                        request_id = _request_id(response.headers.get("x-request-id"))
                        if status != 200:
                            # Do not read/log provider errors, echoed input, request objects or secrets.
                            raise OpenAiTransportError("openai_http_rejected", status, request_id)
                        if (response.headers.get("content-type", "").split(";", 1)[0].strip().lower() != "application/json"
                                or response.headers.get("content-encoding", "identity").lower() != "identity"):
                            raise OpenAiTransportError("openai_response_rejected", status, request_id)
                        length = response.headers.get("content-length")
                        if length is not None and (not length.isascii() or not length.isdecimal() or int(length) > limit):
                            raise OpenAiTransportError("openai_response_rejected", status, request_id)
                        result = bytearray()
                        async for chunk in response.aiter_raw():
                            if len(result) + len(chunk) > limit:
                                raise OpenAiTransportError("openai_response_rejected", status, request_id)
                            result.extend(chunk)
                        if not result:
                            raise OpenAiTransportError("openai_response_rejected", status, request_id)
                        return OpenAiHttpResponse(bytes(result), request_id)
        except asyncio.CancelledError:
            raise  # Caller must retain its committed hold; cancellation is not proof of no charge.
        except OpenAiTransportError:
            raise
        except TimeoutError:
            raise OpenAiTransportError("openai_transport_uncertain", status, request_id, "deadline") from None
        except httpx.TimeoutException:
            raise OpenAiTransportError("openai_transport_uncertain", status, request_id, "http_timeout") from None
        except httpx.NetworkError:
            raise OpenAiTransportError("openai_transport_uncertain", status, request_id, "network") from None
        except httpx.ProtocolError:
            raise OpenAiTransportError("openai_transport_uncertain", status, request_id, "protocol") from None
        except Exception:
            raise OpenAiTransportError("openai_transport_uncertain", status, request_id, "other") from None
