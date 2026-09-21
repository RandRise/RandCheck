import asyncio
from dataclasses import replace
import json
import traceback
import unittest
from unittest.mock import patch

import httpx

from app.cited_embedding_protocol import prepare_embedding_request
from app.cited_embedding_tokens import LocalEmbeddingTokenizer
from app.cited_embedding_transport import (
    OpenAiEmbeddingTransport, EmbeddingTransportError, MAX_EMBEDDING_HTTP_BYTES,
)
from test_cited_embedding_protocol import INPUTS, SCOPE
from test_cited_embedding_tokens import VOCABULARY
from test_cited_openai_transport import Chunks, FAKE_KEY


class EmbeddingTransportTests(unittest.IsolatedAsyncioTestCase):
    @classmethod
    def setUpClass(cls):
        cls.tokenizer = LocalEmbeddingTokenizer(VOCABULARY)
        cls.measured = cls.tokenizer.measure(prepare_embedding_request(SCOPE, INPUTS))

    def setUp(self):
        self.calls = []

    def transport(self, response=None, error=None):
        async def handler(request):
            self.calls.append(request)
            if error is not None:
                raise error
            return response if response is not None else httpx.Response(200,
                headers={"content-type": "application/json", "x-request-id": "req_fixture"}, stream=Chunks([b"{}"])
            )
        return OpenAiEmbeddingTransport(FAKE_KEY, self.tokenizer, enabled=True,
                                       test_transport=httpx.MockTransport(handler))

    async def test_disabled_and_invalid_configuration_cannot_send_or_read_ambient_key(self):
        with patch("os.getenv", side_effect=AssertionError()), patch("socket.socket", side_effect=AssertionError()):
            for enabled in (False, None, 1, "true"):
                with self.assertRaises(EmbeddingTransportError) as error:
                    OpenAiEmbeddingTransport(FAKE_KEY, self.tokenizer, enabled=enabled)
                self.assertEqual("embedding_transport_disabled", error.exception.code)
            for key in (None, "", "sk-short", FAKE_KEY + "\n", "Bearer " + FAKE_KEY, "sk-" + "x" * 600):
                with self.assertRaises(EmbeddingTransportError):
                    OpenAiEmbeddingTransport(key, self.tokenizer, enabled=True)
            with self.assertRaises(EmbeddingTransportError):
                OpenAiEmbeddingTransport(FAKE_KEY, object(), enabled=True)

    async def test_fixed_endpoint_exact_token_wire_and_no_proxy_or_redirects(self):
        constructor = httpx.AsyncClient
        with patch("app.cited_embedding_transport.httpx.AsyncClient", wraps=constructor) as client:
            transport = self.transport()
            reply = await transport.send(self.measured)
        self.assertFalse(client.call_args.kwargs["follow_redirects"])
        self.assertFalse(client.call_args.kwargs["trust_env"])
        self.assertEqual(1, len(self.calls))
        request = self.calls[0]
        self.assertEqual("https://api.openai.com/v1/embeddings", str(request.url))
        self.assertEqual("POST", request.method)
        self.assertEqual(self.measured.body_utf8, request.content)
        self.assertEqual(self.measured.input_tokens, sum(map(len, json.loads(request.content)["input"])))
        self.assertEqual("Bearer " + FAKE_KEY, request.headers["authorization"])
        self.assertEqual("identity", request.headers["accept-encoding"])
        self.assertEqual("req_fixture", reply.provider_request_id)
        self.assertNotIn(FAKE_KEY, repr(transport))
        self.assertNotIn("{}", repr(reply))

    async def test_changed_receipt_is_rejected_before_http(self):
        for request in (None, replace(self.measured, body_utf8=b"{}"),
                        replace(self.measured, token_counts=(1, 1)),
                        replace(self.measured, source=replace(self.measured.source, binding_sha256="0" * 64))):
            with self.assertRaises(EmbeddingTransportError):
                await self.transport().send(request)
        self.assertEqual([], self.calls)

    async def test_http_failures_are_not_retried_followed_or_echoed(self):
        for status in (301, 302, 307, 400, 401, 403, 404, 429, 500, 503):
            before = len(self.calls)
            with self.assertRaises(EmbeddingTransportError) as error:
                await self.transport(httpx.Response(status, headers={"location": "https://example.invalid/steal",
                    "x-request-id": "req_safe"}, stream=Chunks([FAKE_KEY.encode()]))).send(self.measured)
            self.assertEqual(before + 1, len(self.calls))
            self.assertEqual(status, error.exception.http_status)
            self.assertEqual("req_safe", error.exception.provider_request_id)
            self.assertNotIn(FAKE_KEY, "".join(traceback.format_exception(error.exception)))

    async def test_actual_and_declared_size_limits_leave_journal_room_and_close_streams(self):
        self.assertLess(4 * ((MAX_EMBEDDING_HTTP_BYTES + 2) // 3) + 8192, 524288)
        for headers, parts in (({"content-length": str(MAX_EMBEDDING_HTTP_BYTES + 1)}, [b"{}"]),
                               ({}, [b"x" * MAX_EMBEDDING_HTTP_BYTES, b"x"]),
                               ({"content-length": "-1"}, [b"{}"]), ({}, [])):
            stream = Chunks(parts)
            with self.assertRaises(EmbeddingTransportError):
                await self.transport(httpx.Response(200, headers={"content-type": "application/json", **headers},
                                                    stream=stream)).send(self.measured)
            self.assertTrue(stream.closed)

    async def test_non_json_compressed_and_untrusted_request_ids_are_rejected_or_sanitized(self):
        for headers in ({"content-type": "text/html"}, {"content-type": "application/json", "content-encoding": "gzip"}):
            with self.assertRaises(EmbeddingTransportError):
                await self.transport(httpx.Response(200, headers=headers, stream=Chunks([b"{}"]))).send(self.measured)
        reply = await self.transport(httpx.Response(200, headers={"content-type": "application/json",
            "x-request-id": "PRIVATE"}, stream=Chunks([b"{}"]))).send(self.measured)
        self.assertIsNone(reply.provider_request_id)

    async def test_network_failure_is_sanitized_and_not_retried(self):
        with self.assertRaises(EmbeddingTransportError) as error:
            await self.transport(error=httpx.ReadTimeout(FAKE_KEY)).send(self.measured)
        self.assertEqual("embedding_transport_uncertain", error.exception.code)
        self.assertNotIn(FAKE_KEY, "".join(traceback.format_exception(error.exception)))
        self.assertEqual(1, len(self.calls))

    async def test_total_timeout_and_caller_cancellation_do_not_retry(self):
        class Slow(httpx.AsyncByteStream):
            async def __aiter__(self):
                yield b"{"
                await asyncio.Event().wait()
        with patch("app.cited_embedding_transport.asyncio.timeout", return_value=asyncio.timeout(.01)), \
             self.assertRaises(EmbeddingTransportError):
            await self.transport(httpx.Response(200, headers={"content-type": "application/json"}, stream=Slow())).send(self.measured)
        self.assertEqual(1, len(self.calls))
        with self.assertRaises(asyncio.CancelledError):
            await self.transport(error=asyncio.CancelledError()).send(self.measured)
        self.assertEqual(2, len(self.calls))
