import base64
import os
import unittest
from unittest.mock import patch

from fastapi.testclient import TestClient
from app.config import get_settings
from app.cited_embedding_pilot import build_app, create_app
from app.cited_embedding_tokens import LocalEmbeddingTokenizer
from app.cited_embedding_transport import EmbeddingHttpResponse
from test_cited_embedding_tokens import VOCABULARY
from test_cited_embedding_workflow import source, encode
from test_cited_embedding_protocol import response


class EmbeddingPilotTests(unittest.TestCase):
    def setUp(self):
        get_settings.cache_clear()
        self.env = patch.dict(os.environ, {"WORKER_SHARED_SECRET": "test-only-token"})
        self.env.start()
        self.tokenizer = LocalEmbeddingTokenizer(VOCABULARY)
        self.calls = 0
        parent = self

        class FakeTransport:
            async def send(self, measured):
                parent.calls += 1
                value = response()
                value["usage"] = {"prompt_tokens": measured.input_tokens,
                                  "total_tokens": measured.input_tokens}
                return EmbeddingHttpResponse(encode(value), "req_fixture")

        self.client = TestClient(build_app(self.tokenizer, FakeTransport()))

    def tearDown(self):
        self.client.close()
        self.env.stop()
        get_settings.cache_clear()

    def test_no_explicit_enablement_fails_before_key_or_provider(self):
        with patch.dict(os.environ, {"CITED_EMBEDDING_PILOT": ""}):
            with self.assertRaises(RuntimeError):
                create_app()

    def test_authentication_precedes_private_preparation(self):
        self.assertEqual(404, self.client.post("/internal/cited/embedding-prepare", content=encode(source())).status_code)
        self.assertEqual(0, self.calls)

    def test_preparation_is_local_and_oversized_or_invalid_input_is_rejected(self):
        headers = {"X-Worker-Token": "test-only-token", "content-type": "application/json"}
        response = self.client.post("/internal/cited/embedding-prepare", headers=headers, content=encode(source()))
        self.assertEqual(200, response.status_code)
        self.assertGreater(response.json()["inputTokens"], 0)
        self.assertEqual(0, self.calls)
        self.assertEqual(413, self.client.post("/internal/cited/embedding-prepare", headers=headers, content=b"x"*49153).status_code)
        self.assertEqual(422, self.client.post("/internal/cited/embedding-proposal", headers=headers, content=b"{}").status_code)
        self.assertEqual(0, self.calls)

    def test_default_app_does_not_expose_embedding_pilot_routes(self):
        from app.main import app
        self.assertFalse(any("embedding-prepare" in getattr(route, "path", "") or
                             "embedding-proposal" in getattr(route, "path", "") for route in app.routes))

    def test_authenticated_preparation_and_proposal_roundtrip(self):
        headers = {"X-Worker-Token": "test-only-token", "content-type": "application/json"}
        body = encode(source())
        receipt = self.client.post("/internal/cited/embedding-prepare", headers=headers, content=body).json()
        dispatch = encode({"requestUtf8": base64.b64encode(body).decode(), "preparation": receipt})
        self.assertEqual(404, self.client.post("/internal/cited/embedding-proposal", content=dispatch).status_code)
        self.assertEqual(0, self.calls)
        result = self.client.post("/internal/cited/embedding-proposal", headers=headers, content=dispatch)
        self.assertEqual(200, result.status_code)
        self.assertEqual("vectors", result.json()["outcome"])
        self.assertEqual(receipt["inputTokens"], result.json()["inputTokens"])
        self.assertFalse(result.json()["publicationAllowed"])
        self.assertEqual(1, self.calls)
