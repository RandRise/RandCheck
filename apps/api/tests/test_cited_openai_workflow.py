import asyncio
import base64
import copy
import json
import os
import unittest
from unittest.mock import patch

from fastapi.testclient import TestClient
from app.config import get_settings
from app.cited_openai_protocol import MODEL
from app.cited_openai_transport import OpenAiHttpResponse, OpenAiTransportError
from app.cited_openai_workflow import prepare, propose
from app.cited_synthetic_pilot import create_app
from test_cited_answer_routes import payload
from test_cited_openai_protocol import response, encoded


def source():
    data = payload()
    data["model"] = {"providerAlias": MODEL.provider_alias, "model": MODEL.model, "version": MODEL.version}
    return encoded(data)


class Transport:
    def __init__(self):
        self.calls = []
        self.count = 1000
        self.reply = response()
        self.error = None

    async def send_counting(self, wire):
        self.calls.append("count")
        if self.error == "count":
            raise OpenAiTransportError("openai_http_rejected", 429, "req_count")
        return OpenAiHttpResponse(encoded({"object": "response.input_tokens", "input_tokens": self.count}), "req_count")

    async def send_generation(self, wire):
        self.calls.append("generate")
        if self.error == "generate":
            raise OpenAiTransportError("openai_transport_uncertain", None, "req_generate")
        if self.error == "cancel":
            raise asyncio.CancelledError()
        return OpenAiHttpResponse(encoded(self.reply), "req_generate")


class OpenAiWorkflowTests(unittest.IsolatedAsyncioTestCase):
    async def test_low_profile_receipt_is_explicit_and_wrong_profile_never_dispatches(self):
        body = source()
        receipt, wire = prepare(body, "low")
        self.assertEqual("low", receipt["openAi"]["reasoningEffort"])
        dispatch = encoded({"requestUtf8": base64.b64encode(body).decode(), "preparation":receipt})
        transport = Transport()
        with self.assertRaises(ValueError): await propose(dispatch, transport)
        self.assertEqual([], transport.calls)
        self.assertTrue((await propose(dispatch, transport, "low"))["openAi"]["accountingKnown"])
        self.assertEqual(["count", "generate"], transport.calls)

    def setUp(self):
        self.body = source()
        self.receipt, self.wire = prepare(self.body)
        self.dispatch = {"requestUtf8": base64.b64encode(self.body).decode(), "preparation": self.receipt}
        self.transport = Transport()

    async def run_dispatch(self):
        return await propose(encoded(self.dispatch), self.transport)

    async def test_preparation_is_offline_and_explicitly_reserves_the_ceiling(self):
        self.assertEqual(8000, self.receipt["inputTokens"])
        self.assertEqual("openai-count-after-reservation-v1", self.receipt["counterVersion"])
        self.assertEqual(self.wire.generation_sha256, self.receipt["openAi"]["generationSha256"])
        self.assertEqual([], self.transport.calls)

    async def test_count_then_generate_and_preserve_raw_usage_and_identity(self):
        value = await self.run_dispatch()
        self.assertEqual(["count", "generate"], self.transport.calls)
        self.assertTrue(value["openAi"]["accountingKnown"])
        self.assertEqual(1000, value["openAi"]["measuredInputTokens"])
        self.assertEqual(400, value["openAi"]["cachedInputTokens"])
        self.assertEqual(120, value["openAi"]["reasoningTokens"])
        self.assertEqual(self.transport.reply, json.loads(base64.b64decode(value["openAi"]["responseUtf8"])))

    async def test_modified_receipt_never_counts_or_generates(self):
        for field, replacement in (("inputTokens", 7000), ("inputTokens", True), ("promptSha256", "a" * 64)):
            self.dispatch["preparation"] = copy.deepcopy(self.receipt)
            self.dispatch["preparation"][field] = replacement
            with self.assertRaises(ValueError):
                await self.run_dispatch()
        self.assertEqual([], self.transport.calls)

    async def test_oversized_count_saves_uncertainty_and_never_generates(self):
        self.transport.count = 8001
        value = await self.run_dispatch()
        self.assertEqual(["count"], self.transport.calls)
        self.assertFalse(value["openAi"]["accountingKnown"])
        self.assertIsNotNone(value["openAi"]["countResponseUtf8"])

    async def test_count_and_generation_failures_have_no_retry(self):
        for step in ("count", "generate"):
            self.transport = Transport()
            self.transport.error = step
            value = await self.run_dispatch()
            self.assertFalse(value["openAi"]["accountingKnown"])
            self.assertEqual(["count"] if step == "count" else ["count", "generate"], self.transport.calls)

    async def test_transport_diagnostics_are_allowlisted_and_never_establish_accounting(self):
        for code, kind, expected in (("openai_transport_uncertain", "network", "network"),
                ("private-secret", "private-secret", None)):
            with patch.object(self.transport, "send_generation", side_effect=OpenAiTransportError(code, failure_kind=kind)):
                result = await self.run_dispatch()
            self.assertFalse(result["openAi"]["accountingKnown"])
            self.assertEqual(expected, result["openAi"].get("transportFailureKind"))
            self.assertEqual(code if expected else "unclassified", result["openAi"]["transportErrorCode"])
            self.assertNotIn("private-secret", json.dumps(result))

    async def test_refusal_and_incomplete_preserve_known_cost(self):
        for status in ("incomplete", "failed", "cancelled"):
            self.transport.reply["status"] = status
            value = await self.run_dispatch()
            self.assertEqual("provider_incomplete", value["outcome"])
            self.assertTrue(value["openAi"]["accountingKnown"])
        self.transport.reply = response()
        self.transport.reply["output"][1]["content"] = [{"type": "refusal", "refusal": "No"}]
        self.assertEqual("provider_refusal", (await self.run_dispatch())["outcome"])

    async def test_unknown_usage_preserves_raw_body_without_zero_cost_claim(self):
        del self.transport.reply["usage"]
        value = await self.run_dispatch()
        self.assertFalse(value["openAi"]["accountingKnown"])
        self.assertIsNotNone(value["openAi"]["responseUtf8"])

    async def test_overrun_is_preserved_not_clamped(self):
        self.transport.reply["usage"]["input_tokens"] = 1500
        self.transport.reply["usage"]["total_tokens"] = 1700
        self.assertEqual(1500, (await self.run_dispatch())["inputTokens"])

    async def test_cancellation_propagates_without_retry(self):
        self.transport.error = "cancel"
        with self.assertRaises(asyncio.CancelledError):
            await self.run_dispatch()
        self.assertEqual(["count", "generate"], self.transport.calls)

    async def test_default_app_cannot_be_enabled_by_key_alone(self):
        with patch.dict(os.environ, {"OPENAI_API_KEY": "sk-fake-pilot-key-123456789", "CITED_SYNTHETIC_PILOT": ""}):
            with self.assertRaises(RuntimeError):
                create_app()

    async def test_private_routes_authenticate_and_roundtrip_without_network(self):
        with patch.dict(os.environ, {"CITED_SYNTHETIC_PILOT": "nano-v1", "WORKER_SHARED_SECRET": "pilot-test-token"}), \
                patch("app.cited_synthetic_pilot.OpenAiResponsesTransport", return_value=self.transport):
            get_settings.cache_clear()
            with TestClient(create_app()) as client:
                self.assertEqual(404, client.post("/internal/cited/answer-proposal", content=b"bad").status_code)
                headers = {"X-Worker-Token": "pilot-test-token"}
                receipt = client.post("/internal/cited/answer-prepare", content=self.body, headers=headers)
                self.assertEqual(200, receipt.status_code)
                self.assertEqual([], self.transport.calls)
                result = client.post("/internal/cited/answer-proposal", json=self.dispatch, headers=headers)
                self.assertEqual(200, result.status_code)
                self.assertTrue(result.json()["openAi"]["accountingKnown"])
            get_settings.cache_clear()
