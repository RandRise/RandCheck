import os
import unittest
from unittest.mock import patch
from fastapi.testclient import TestClient
from app.main import app
from app.config import get_settings
from app.cited_retrieval import lexical_plan
from app.cited_chunking import ChunkingError, content_hash
from test_cited_chunking import snapshot


class CitedRouteTests(unittest.TestCase):
    def setUp(self):
        self.env = patch.dict(os.environ, {"WORKER_SHARED_SECRET": "test-only-cited-token"})
        self.env.start()
        get_settings.cache_clear()
        self.client = TestClient(app)
        self.headers = {"X-Worker-Token": "test-only-cited-token"}

    def tearDown(self):
        self.client.close()
        self.env.stop()
        get_settings.cache_clear()

    def test_private_routes_require_token_before_parsing(self):
        for path in ("chunks", "lexical-plan"):
            result = self.client.post("/internal/cited/" + path, content="invalid JSON")
            self.assertEqual(404, result.status_code)

    def test_chunk_route_preserves_wire_identity(self):
        result = self.client.post("/internal/cited/chunks", json=snapshot(), headers=self.headers)
        self.assertEqual(200, result.status_code)
        self.assertEqual(snapshot()["indexId"], result.headers["X-Request-ID"])
        self.assertEqual(snapshot()["pages"][0]["canonicalText"], result.json()["chunks"][0]["content"])

    def test_invalid_source_error_does_not_echo_invoice_text(self):
        source = snapshot()
        source["reviewStatus"] = "SECRET-UNREVIEWED-INVOICE"
        result = self.client.post("/internal/cited/chunks", json=source, headers=self.headers)
        self.assertEqual(422, result.status_code)
        self.assertNotIn("SECRET", result.text)

    def test_lexical_plan_preserves_identifiers_as_literals_and_is_versioned(self):
        question = 'Invoice SYN-STD-260801 total 100.00; DROP TABLE documents'
        result = self.client.post("/internal/cited/lexical-plan",
                                  json={"question": question, "topK": 5}, headers=self.headers)
        self.assertEqual(200, result.status_code)
        self.assertIn('"syn-std-260801"', result.json()["searchText"])
        self.assertEqual(content_hash(question), result.json()["questionSha256"])
        self.assertEqual("lexical-or-english-v1", result.json()["policyVersion"])
        self.assertNotIn(";", result.json()["searchText"])

    def test_lexical_plan_is_deterministic_and_handles_no_searchable_terms(self):
        self.assertEqual(lexical_plan("total total", 5), lexical_plan("total total", 5))
        self.assertEqual('"total"', lexical_plan("total total", 5)["searchText"])
        self.assertEqual("", lexical_plan("???", 5)["searchText"])
        with self.assertRaises(ChunkingError):
            lexical_plan("a" * 101, 5)

    def test_lexical_bounds_and_unknown_fields_are_rejected(self):
        for payload in ({"question": "x", "topK": 21}, {"question": "x", "topK": True},
                        {"question": "x", "topK": 5, "organizationId": "foreign"}):
            response = self.client.post("/internal/cited/lexical-plan", json=payload, headers=self.headers)
            self.assertEqual(422, response.status_code)

    def test_transport_byte_limit_applies_without_content_length_trust(self):
        result = self.client.post("/internal/cited/lexical-plan", content=b"x" * 32769, headers=self.headers)
        self.assertEqual(413, result.status_code)
