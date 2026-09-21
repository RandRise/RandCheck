import base64
import hashlib
import json
import os
import unittest
from unittest.mock import patch

from fastapi.testclient import TestClient
from app.main import app
from app.config import get_settings
from app.cited_answer_routes import get_answer_adapter, get_prompt_counter
from cited_prompt_fixture import FixturePromptCounter
from test_cited_answers import CONTEXT, MODEL, CONTENT, EVIDENCE, FakeAdapter


def payload():
    return {"schemaVersion": "1.0", "context": {"organizationId": CONTEXT.organization_id,
            "actorId": CONTEXT.actor_id, "retrievalRunId": CONTEXT.retrieval_run_id},
            "model": {"providerAlias": MODEL.provider_alias, "model": MODEL.model, "version": MODEL.version},
            "question": "What is the total?", "maximumOutputTokens": 4000,
            "evidence": [{"organizationId": CONTEXT.organization_id, "retrievalRunId": CONTEXT.retrieval_run_id,
                          "hitId": EVIDENCE[0].hit_id, "spanIds": list(EVIDENCE[0].span_ids),
                          "content": CONTENT, "contentSha256": EVIDENCE[0].content_sha256}]}


class CitedAnswerRouteTests(unittest.TestCase):
    def setUp(self):
        self.env = patch.dict(os.environ, {"WORKER_SHARED_SECRET": "test-only-cited-token"})
        self.env.start()
        get_settings.cache_clear()
        self.client = TestClient(app)
        self.headers = {"X-Worker-Token": "test-only-cited-token"}
        self.fake = FakeAdapter()

    def tearDown(self):
        app.dependency_overrides.pop(get_answer_adapter, None)
        app.dependency_overrides.pop(get_prompt_counter, None)
        self.client.close()
        self.env.stop()
        get_settings.cache_clear()

    def enable(self):
        app.dependency_overrides[get_answer_adapter] = lambda: self.fake
        app.dependency_overrides[get_prompt_counter] = FixturePromptCounter

    def post(self, data=None):
        body = json.dumps(data or payload()).encode()
        prepared = self.client.post("/internal/cited/answer-prepare", content=body, headers=self.headers)
        if prepared.status_code != 200:
            return prepared
        return self.dispatch(body, prepared.json())

    def dispatch(self, body, preparation):
        return self.client.post("/internal/cited/answer-proposal",
                                json={"requestUtf8": base64.b64encode(body).decode(), "preparation": preparation},
                                headers=self.headers)

    def test_default_is_disabled_even_with_valid_token(self):
        self.assertEqual(503, self.post().status_code)
        result = self.client.post("/internal/cited/answer-proposal", content="{}", headers=self.headers)
        self.assertEqual(503, result.status_code)

    def test_authorization_precedes_body_parsing_and_provider(self):
        self.enable()
        for endpoint in ("answer-proposal", "answer-prepare"):
            result = self.client.post("/internal/cited/" + endpoint, content="SECRET INVALID JSON")
            self.assertEqual(404, result.status_code)
        self.assertEqual(0, self.fake.calls)

    def test_exact_wire_hash_and_utf8_proposal_roundtrip(self):
        self.enable()
        for ensure_ascii in (True, False):
            body = json.dumps(payload(), ensure_ascii=ensure_ascii).encode()
            prepared = self.client.post("/internal/cited/answer-prepare", content=body, headers=self.headers)
            self.assertEqual(200, prepared.status_code)
            result = self.dispatch(body, prepared.json())
            self.assertEqual(200, result.status_code)
            self.assertEqual(hashlib.sha256(body).hexdigest(), result.json()["requestSha256"])
            self.assertEqual("Answered", json.loads(base64.b64decode(result.json()["proposalUtf8"]))["status"])

    def test_foreign_evidence_and_unknown_fields_never_reach_provider(self):
        self.enable()
        for change in (lambda x: x.update(enabled=True),
                       lambda x: x["evidence"][0].update(organizationId=CONTEXT.actor_id),
                       lambda x: x["evidence"][0].update(contentSha256="a" * 64),
                       lambda x: x.update(maximumOutputTokens=True)):
            data = payload()
            change(data)
            self.assertEqual(422, self.post(data).status_code)
        self.assertEqual(0, self.fake.calls)

    def test_duplicate_keys_and_invalid_utf8_are_safe(self):
        self.enable()
        for body in (b'{"question":"SECRET","question":"x"}', b'\xff'):
            result = self.client.post("/internal/cited/answer-prepare", content=body, headers=self.headers)
            self.assertEqual(422, result.status_code)
            self.assertNotIn("SECRET", result.text)
        self.assertEqual(0, self.fake.calls)

    def test_body_limit(self):
        self.enable()
        result = self.client.post("/internal/cited/answer-prepare", content=b"x" * 65537, headers=self.headers)
        self.assertEqual(413, result.status_code)
        self.assertEqual(0, self.fake.calls)

    def test_provider_failure_is_safe_and_never_retried(self):
        self.fake = FakeAdapter(error=RuntimeError("SECRET INVOICE"))
        self.enable()
        result = self.post()
        self.assertEqual(502, result.status_code)
        self.assertNotIn("SECRET", result.text)
        self.assertEqual(1, self.fake.calls)

    def test_mismatched_provider_reply_is_rejected(self):
        from dataclasses import replace
        self.fake = FakeAdapter(change=lambda x: replace(x, request_sha256="a" * 64))
        self.enable()
        self.assertEqual(502, self.post().status_code)

    def test_preparation_does_not_generate_and_contains_no_source_text(self):
        self.enable()
        result = self.client.post("/internal/cited/answer-prepare", json=payload(), headers=self.headers)
        self.assertEqual(200, result.status_code)
        self.assertEqual(0, self.fake.calls)
        self.assertEqual("cited-answer-prompt-v3", result.json()["promptVersion"])
        self.assertGreater(result.json()["inputTokens"], len(CONTENT.encode()))
        self.assertNotIn("Invoice total", result.text)

    def test_tampered_measurement_and_prompt_hash_never_generate(self):
        self.enable()
        body = json.dumps(payload()).encode()
        prepared = self.client.post("/internal/cited/answer-prepare", content=body, headers=self.headers).json()
        for key, value in (("inputTokens", 1), ("inputTokens", True), ("promptSha256", "a" * 64),
                           ("promptVersion", "old"), ("counterVersion", "unknown")):
            with self.subTest(key=key, value=value):
                changed = dict(prepared)
                changed[key] = value
                self.assertEqual(422, self.dispatch(body, changed).status_code)
        self.assertEqual(0, self.fake.calls)

    def test_prompt_change_between_preparation_and_dispatch_is_rejected(self):
        self.enable()
        body = json.dumps(payload()).encode()
        prepared = self.client.post("/internal/cited/answer-prepare", content=body, headers=self.headers).json()
        with patch("app.cited_prompt.INSTRUCTIONS", "Changed prompt policy"):
            self.assertEqual(422, self.dispatch(body, prepared).status_code)
        self.assertEqual(0, self.fake.calls)

    def test_unmeasurable_prompt_never_generates(self):
        self.enable()
        class BadCounter(FixturePromptCounter):
            async def count(self, request):
                raise RuntimeError("SECRET INVOICE")
        app.dependency_overrides[get_prompt_counter] = BadCounter
        result = self.post()
        self.assertEqual(422, result.status_code)
        self.assertNotIn("SECRET", result.text)
        self.assertEqual(0, self.fake.calls)

    def test_missing_receipt_and_dispatch_byte_limit(self):
        self.enable()
        for body, status in ((b"{}", 422), (b"x" * 98305, 413)):
            result = self.client.post("/internal/cited/answer-proposal", content=body, headers=self.headers)
            self.assertEqual(status, result.status_code)
        self.assertEqual(0, self.fake.calls)
