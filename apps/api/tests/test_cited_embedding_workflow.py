import asyncio
import base64
import json
import unittest

import httpx

from app.cited_embedding_protocol import EmbeddingProtocolError
from app.cited_embedding_tokens import LocalEmbeddingTokenizer
from app.cited_embedding_transport import EmbeddingHttpResponse, OpenAiEmbeddingTransport
from app.cited_embedding_workflow import prepare, propose, MODEL_WIRE, SCHEMA_VERSION
from test_cited_embedding_protocol import INPUTS, SCOPE, response
from test_cited_embedding_tokens import VOCABULARY
from test_cited_openai_transport import Chunks, FAKE_KEY


def source():
    return {"schemaVersion": SCHEMA_VERSION, "model": MODEL_WIRE.copy(),
            "context": {"organizationId": SCOPE.organization_id, "actorId": SCOPE.actor_id,
                        "operationId": SCOPE.operation_id, "purpose": SCOPE.purpose,
                        "indexIds": list(SCOPE.index_ids)},
            "inputs": [{"inputId": item.chunk_id, "text": item.text, "inputSha256": item.input_sha256}
                       for item in INPUTS]}


def encode(value):
    return json.dumps(value, separators=(",", ":"), ensure_ascii=False).encode("utf-8")


class EmbeddingWorkflowTests(unittest.IsolatedAsyncioTestCase):
    @classmethod
    def setUpClass(cls):
        cls.tokenizer = LocalEmbeddingTokenizer(VOCABULARY)

    def setUp(self):
        self.body = encode(source())
        self.receipt, self.measured = prepare(self.body, self.tokenizer)
        self.dispatch = encode({"requestUtf8": base64.b64encode(self.body).decode(), "preparation": self.receipt})
        self.calls = 0

    def transport(self, mode="vectors"):
        async def handler(request):
            self.calls += 1
            self.assertEqual(self.measured.body_utf8, request.content)
            if mode == "cancel":
                raise asyncio.CancelledError()
            if mode == "error":
                raise httpx.ReadTimeout(FAKE_KEY)
            if mode == "http":
                return httpx.Response(429, stream=Chunks([FAKE_KEY.encode()]))
            value = response()
            count = self.measured.input_tokens + (1 if mode == "mismatch" else 0)
            value["usage"] = {"prompt_tokens": count, "total_tokens": count}
            if mode == "invalid":
                value["data"] = []
            raw = b"{}" if mode == "unknown" else encode(value)
            return httpx.Response(200, headers={"content-type": "application/json", "x-request-id": "req_fixture"},
                                  stream=Chunks([raw]))
        return OpenAiEmbeddingTransport(FAKE_KEY, self.tokenizer, enabled=True,
                                       test_transport=httpx.MockTransport(handler))

    async def test_local_preparation_is_counted_bounded_and_deterministic(self):
        self.assertEqual(self.receipt, prepare(self.body, self.tokenizer)[0])
        self.assertEqual(self.measured.input_tokens, self.receipt["inputTokens"])
        self.assertEqual(0, self.receipt["maximumOutputTokens"])
        self.assertLessEqual(len(self.dispatch), 98304)
        self.assertEqual(0, self.calls)

    async def test_exact_raw_response_is_preserved_once_and_known_usage_is_separate_from_vectors(self):
        for mode in ("vectors", "invalid", "mismatch", "unknown"):
            before = self.calls
            result = await propose(self.dispatch, self.tokenizer, self.transport(mode))
            self.assertEqual(before + 1, self.calls)
            self.assertEqual(mode != "unknown", result["accountingKnown"])
            self.assertEqual(mode in ("unknown", "mismatch"), result["requiresReconciliation"])
            self.assertEqual({"vectors": "vectors", "invalid": "invalid_vectors", "mismatch": "usage_mismatch",
                              "unknown": "accounting_unavailable"}[mode], result["outcome"])
            self.assertFalse(result["publicationAllowed"])
            self.assertNotIn("vectors", result)  # Only the immutable raw payload stores vector arrays.
            raw = base64.b64decode(result["responseUtf8"], validate=True)
            self.assertIsInstance(json.loads(raw), dict)
            self.assertLessEqual(len(encode(result)), 524288)
            self.assertEqual(self.receipt["bindingSha256"], result["bindingSha256"])
            self.assertNotIn(FAKE_KEY, encode(result).decode())

    async def test_malformed_duplicate_or_changed_preparation_never_dispatches(self):
        invalid = [b"{}", b'{"schemaVersion":"x","schemaVersion":"y"}', b"x" * 49153]
        value = source()
        value["model"]["dimension"] = 1536.0
        invalid.append(encode(value))
        value = source()
        value["context"]["purpose"] = "query"  # Two inputs are not one query.
        invalid.append(encode(value))
        for body in invalid:
            with self.assertRaises(EmbeddingProtocolError):
                prepare(body, self.tokenizer)
        dispatch = json.loads(self.dispatch)
        dispatch["preparation"]["inputTokens"] += 1
        with self.assertRaises(EmbeddingProtocolError):
            await propose(encode(dispatch), self.tokenizer, self.transport())
        with self.assertRaises(EmbeddingProtocolError):
            await propose(b"x" * 98305, self.tokenizer, self.transport())
        self.assertEqual(0, self.calls)

    async def test_key_order_is_not_semantic_but_raw_source_hash_is_exact(self):
        reordered = encode(dict(reversed(list(source().items()))))
        receipt, measured = prepare(reordered, self.tokenizer)
        self.assertEqual(measured.body_sha256, self.measured.body_sha256)
        self.assertNotEqual(receipt["sourceSha256"], self.receipt["sourceSha256"])

    async def test_failure_is_unknown_not_free_and_cancellation_propagates(self):
        for mode in ("error", "http"):
            before = self.calls
            result = await propose(self.dispatch, self.tokenizer, self.transport(mode))
            self.assertEqual(before + 1, self.calls)
            self.assertIsNone(result["inputTokens"])
            self.assertIsNone(result["responseUtf8"])
            self.assertFalse(result["accountingKnown"])
            self.assertTrue(result["requiresReconciliation"])
            self.assertNotIn(FAKE_KEY, encode(result).decode())
        with self.assertRaises(asyncio.CancelledError):
            await propose(self.dispatch, self.tokenizer, self.transport("cancel"))

    async def test_maximum_raw_response_fits_existing_journal_even_when_json_is_invalid(self):
        class MaximumReply:
            async def send(inner, request):
                return EmbeddingHttpResponse(b"x" * (256 * 1024), "req_fixture")
        result = await propose(self.dispatch, self.tokenizer, MaximumReply())
        self.assertEqual("accounting_unavailable", result["outcome"])
        self.assertEqual(256 * 1024, len(base64.b64decode(result["responseUtf8"])))
        self.assertLessEqual(len(encode(result)), 524288)
