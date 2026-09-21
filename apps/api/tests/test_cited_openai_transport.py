import asyncio
from dataclasses import replace
import json
import traceback
import unittest
from unittest.mock import patch

import httpx

from app.cited_answers import prepare_answer_request
from app.cited_openai_protocol import MODEL, prepare_openai_request
from app.cited_openai_transport import OpenAiResponsesTransport, OpenAiTransportError
from test_cited_answers import CONTEXT, EVIDENCE


FAKE_KEY = "sk-test-only-not-a-real-key-123456789"


class Chunks(httpx.AsyncByteStream):
    def __init__(self, parts):
        self.parts = parts
        self.closed = False

    async def __aiter__(self):
        for part in self.parts:
            yield part

    async def aclose(self):
        self.closed = True


class OpenAiTransportTests(unittest.IsolatedAsyncioTestCase):
    async def test_low_reasoning_reaches_transport_with_exact_body_and_no_retry(self):
        low = prepare_openai_request(self.request.source, "low")
        await self.transport().send_counting(low)
        await self.transport().send_generation(low)
        self.assertEqual(2, len(self.calls))
        self.assertEqual(low.counting_utf8, self.calls[0].content)
        self.assertEqual(low.generation_utf8, self.calls[1].content)
        self.assertEqual("low", json.loads(self.calls[1].content)["reasoning"]["effort"])
        with self.assertRaises(OpenAiTransportError):
            await self.transport().send_counting(replace(low, reasoning_effort="minimal"))
        self.assertEqual(2, len(self.calls))

    def setUp(self):
        self.request = prepare_openai_request(prepare_answer_request(CONTEXT, MODEL, "total?", EVIDENCE))
        self.calls = []

    def transport(self, response=None, error=None):
        async def handler(request):
            self.calls.append(request)
            if error is not None:
                raise error
            return response if response is not None else httpx.Response(200,
                headers={"content-type": "application/json", "x-request-id": "req_test_123"},
                stream=Chunks([b'{"object":"response.input_tokens","input_tokens":10}']))
        return OpenAiResponsesTransport(FAKE_KEY, enabled=True, test_transport=httpx.MockTransport(handler))

    async def test_real_transport_uses_system_trust_without_disabling_tls_verification(self):
        import ssl
        fake = self.transport()._test_transport
        with patch("app.cited_openai_transport.httpx.AsyncHTTPTransport", return_value=fake) as factory:
            await OpenAiResponsesTransport(FAKE_KEY, enabled=True).send_counting(self.request)
        config = factory.call_args.kwargs
        self.assertIsInstance(config["verify"], ssl.SSLContext)
        self.assertEqual(ssl.CERT_REQUIRED, config["verify"].verify_mode)
        self.assertTrue(config["verify"].check_hostname)
        self.assertEqual(0, config["retries"])
        self.assertFalse(config["trust_env"])

    async def test_disabled_constructor_cannot_access_network_or_ambient_key(self):
        with patch("os.getenv", side_effect=AssertionError()), patch("socket.socket", side_effect=AssertionError()):
            for enabled in (False, None, 1, "true"):
                with self.assertRaises(OpenAiTransportError) as error:
                    OpenAiResponsesTransport(FAKE_KEY, enabled=enabled)
                self.assertEqual("openai_transport_disabled", error.exception.code)

    async def test_bad_keys_are_sanitized_before_transport_construction(self):
        for key in (None, "", "sk-short", FAKE_KEY + "\n", "Bearer " + FAKE_KEY, "sk-" + "x" * 600):
            with self.assertRaises(OpenAiTransportError) as error:
                OpenAiResponsesTransport(key, enabled=True)
            self.assertNotIn(FAKE_KEY, str(error.exception))

    async def test_fixed_count_url_and_exact_frozen_body(self):
        transport = self.transport()
        reply = await transport.send_counting(self.request)
        self.assertEqual(10, json.loads(reply.body_utf8)["input_tokens"])
        self.assertEqual("req_test_123", reply.provider_request_id)
        self.assertEqual(1, len(self.calls))
        self.assertEqual("https://api.openai.com/v1/responses/input_tokens", str(self.calls[0].url))
        self.assertEqual(self.request.counting_utf8, self.calls[0].content)
        self.assertEqual("Bearer " + FAKE_KEY, self.calls[0].headers["authorization"])
        self.assertNotIn(FAKE_KEY, repr(transport))
        self.assertNotIn("input_tokens", repr(reply))

    async def test_generation_uses_exact_frozen_body_and_no_tools(self):
        await self.transport().send_generation(self.request)
        self.assertEqual("https://api.openai.com/v1/responses", str(self.calls[0].url))
        self.assertEqual(self.request.generation_utf8, self.calls[0].content)
        self.assertEqual([], json.loads(self.calls[0].content)["tools"])

    async def test_changed_request_is_rejected_before_a_send(self):
        with self.assertRaises(OpenAiTransportError):
            await self.transport().send_generation(replace(self.request, generation_utf8=b"{}"))
        self.assertEqual([], self.calls)

    async def test_error_statuses_and_redirects_are_never_retried_or_followed(self):
        for status in (301, 302, 307, 400, 401, 403, 404, 429, 500, 503):
            before = len(self.calls)
            with self.subTest(status=status), self.assertRaises(OpenAiTransportError) as error:
                await self.transport(httpx.Response(status, headers={"location": "https://example.invalid/steal",
                    "x-request-id": "req_safe"}, stream=Chunks([FAKE_KEY.encode()]))).send_generation(self.request)
            self.assertEqual(before + 1, len(self.calls))
            self.assertEqual(status, error.exception.http_status)
            self.assertEqual("req_safe", error.exception.provider_request_id)
            self.assertNotIn(FAKE_KEY, "".join(traceback.format_exception(error.exception)))

    async def test_network_failure_does_not_retry_or_expose_exception(self):
        with self.assertRaises(OpenAiTransportError) as error:
            await self.transport(error=httpx.ReadTimeout("PRIVATE " + FAKE_KEY)).send_generation(self.request)
        self.assertEqual("openai_transport_uncertain", error.exception.code)
        self.assertEqual("http_timeout", error.exception.failure_kind)
        self.assertEqual(1, len(self.calls))
        self.assertNotIn(FAKE_KEY, "".join(traceback.format_exception(error.exception)))

    async def test_safe_failure_categories_preserve_no_retry_and_hide_private_details(self):
        for exception, category in ((TimeoutError(FAKE_KEY), "deadline"),
                (httpx.ConnectError(FAKE_KEY), "network"),
                (httpx.RemoteProtocolError(FAKE_KEY), "protocol"),
                (RuntimeError(FAKE_KEY), "other")):
            before = len(self.calls)
            with self.subTest(category=category), self.assertRaises(OpenAiTransportError) as error:
                await self.transport(error=exception).send_generation(self.request)
            self.assertEqual(category, error.exception.failure_kind)
            self.assertEqual(before + 1, len(self.calls))
            self.assertNotIn(FAKE_KEY, "".join(traceback.format_exception(error.exception)))

    async def test_declared_and_streamed_response_limits_are_enforced(self):
        for headers, parts in (({"content-length": "1025"}, [b"{}"]), ({}, [b"x" * 1000, b"y" * 25]),
                                ({"content-length": "-1"}, [b"{}"]), ({}, [])):
            chunks = Chunks(parts)
            with self.assertRaises(OpenAiTransportError):
                await self.transport(httpx.Response(200, headers={"content-type": "application/json", **headers},
                                                    stream=chunks)).send_counting(self.request)
            self.assertTrue(chunks.closed)

    async def test_non_json_or_compressed_content_is_not_accepted(self):
        for headers in ({"content-type": "text/html"}, {"content-type": "application/json", "content-encoding": "gzip"}):
            with self.assertRaises(OpenAiTransportError):
                await self.transport(httpx.Response(200, headers=headers, stream=Chunks([b"{}"]))).send_generation(self.request)

    async def test_total_deadline_cancels_slow_stream_without_retry(self):
        class Slow(httpx.AsyncByteStream):
            async def __aiter__(self):
                yield b"{"
                await asyncio.Event().wait()
        timeout = asyncio.timeout(.01)
        with patch("app.cited_openai_transport.asyncio.timeout", return_value=timeout), self.assertRaises(OpenAiTransportError):
            await self.transport(httpx.Response(200, headers={"content-type": "application/json"},
                                                stream=Slow())).send_generation(self.request)
        self.assertEqual(1, len(self.calls))

    async def test_caller_cancellation_propagates_without_retry(self):
        with self.assertRaises(asyncio.CancelledError):
            await self.transport(error=asyncio.CancelledError()).send_generation(self.request)
        self.assertEqual(1, len(self.calls))

    async def test_untrusted_request_id_is_not_exposed(self):
        reply = await self.transport(httpx.Response(200, headers={"content-type": "application/json",
            "x-request-id": "PRIVATE-UNRECOGNIZED"}, stream=Chunks([b"{}"]))).send_generation(self.request)
        self.assertIsNone(reply.provider_request_id)
