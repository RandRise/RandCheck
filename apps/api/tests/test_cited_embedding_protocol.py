"""No credentials/network: deterministic fixtures, not embedding quality evidence."""
from dataclasses import replace
from decimal import Decimal
import hashlib
import json
import unittest

from app.cited_embedding_protocol import (
    MODEL, MAX_RESPONSE_BYTES, EmbeddingProtocolError, EmbeddingScope,
    prepare_embedding_request, inspect_embedding_reply,
)
from app.cited_embeddings import EmbeddingInput


def uid(value):
    return f"00000000-0000-0000-0000-{value:012d}"


def item(value, text):
    return EmbeddingInput(uid(value), text, hashlib.sha256(text.encode()).hexdigest())


SCOPE = EmbeddingScope(uid(1), uid(2), uid(3), "chunk", (uid(4),))
INPUTS = (item(5, "supplier_trn[0]: 100123456700003"), item(6, "Amount due: AED 8820.00"))


def response():
    return {"object": "list", "model": MODEL.model,
            "data": [{"object": "embedding", "index": index,
                      "embedding": [1.0] + [0.0] * (MODEL.dimension - 1)} for index in (1, 0)],
            "usage": {"prompt_tokens": 50, "total_tokens": 50}}


class EmbeddingProtocolTests(unittest.TestCase):
    def setUp(self):
        self.request = prepare_embedding_request(SCOPE, INPUTS)

    def inspect(self, value=None, request_id="req_fixture"):
        raw = json.dumps(response() if value is None else value).encode()
        return inspect_embedding_reply(self.request, raw, request_id)

    def test_exact_wire_and_local_binding_are_deterministic_and_hide_identity_from_provider(self):
        self.assertEqual(self.request, prepare_embedding_request(SCOPE, INPUTS))
        self.assertEqual({"model": "text-embedding-3-small", "dimensions": 1536,
                          "encoding_format": "float", "input": [x.text for x in INPUTS]},
                         json.loads(self.request.body_utf8))
        self.assertNotIn(SCOPE.organization_id.encode(), self.request.body_utf8)
        self.assertNotIn(INPUTS[0].text, repr(self.request))
        self.assertEqual(hashlib.sha256(self.request.body_utf8).hexdigest(), self.request.body_sha256)

    def test_scope_purpose_input_identity_and_order_cannot_reuse_binding(self):
        for scope in (replace(SCOPE, organization_id=uid(20)), replace(SCOPE, actor_id=uid(20)),
                      replace(SCOPE, operation_id=uid(20)), replace(SCOPE, index_ids=(uid(20),))):
            changed = prepare_embedding_request(scope, INPUTS)
            self.assertEqual(self.request.body_utf8, changed.body_utf8)
            self.assertNotEqual(self.request.binding_sha256, changed.binding_sha256)
        for inputs in ((replace(INPUTS[0], chunk_id=uid(20)), INPUTS[1]), tuple(reversed(INPUTS))):
            self.assertNotEqual(self.request.binding_sha256, prepare_embedding_request(SCOPE, inputs).binding_sha256)
        chunk = prepare_embedding_request(SCOPE, INPUTS[:1])
        query = prepare_embedding_request(replace(SCOPE, purpose="query"), INPUTS[:1])
        self.assertEqual(chunk.body_utf8, query.body_utf8)
        self.assertNotEqual(chunk.binding_sha256, query.binding_sha256)

    def test_bad_scope_ids_and_batch_bounds_fail_without_truncation(self):
        scopes = (replace(SCOPE, purpose="other"), replace(SCOPE, organization_id="bad"),
                  replace(SCOPE, index_ids=()), replace(SCOPE, index_ids=(uid(4), uid(4))),
                  replace(SCOPE, index_ids=(uid(5), uid(4))), replace(SCOPE, purpose="query"))
        for scope in scopes:
            with self.subTest(scope=scope), self.assertRaises(EmbeddingProtocolError):
                prepare_embedding_request(scope, INPUTS)
        batches = ((), (INPUTS[0], INPUTS[0]), (replace(INPUTS[0], input_sha256="0" * 64),),
                   (item(5, " "),), (item(5, "é" * 4001),), tuple(item(i + 10, "x") for i in range(33)),
                   tuple(item(i + 10, "x" * 7999) for i in range(9)))
        for inputs in batches:
            with self.subTest(count=len(inputs)), self.assertRaises(EmbeddingProtocolError):
                prepare_embedding_request(SCOPE, inputs)

    def test_response_index_reorders_and_binds_vectors_to_exact_input_hashes(self):
        result = self.inspect()
        self.assertEqual("vectors", result.outcome)
        self.assertTrue(result.accounting_known)
        self.assertEqual([x.chunk_id for x in INPUTS], [x.chunk_id for x in result.vectors])
        self.assertEqual([x.input_sha256 for x in INPUTS], [x.input_sha256 for x in result.vectors])
        self.assertEqual(Decimal("0.000001"), result.estimated_usd)
        self.assertFalse(result.publication_allowed)
        self.assertEqual(hashlib.sha256(result.response_utf8).hexdigest(), result.response_sha256)
        self.assertNotIn('"embedding"', repr(result))

    def test_invalid_vectors_preserve_known_usage_and_never_return_partial_batch(self):
        mutations = [lambda r: r["data"].pop(),
                     lambda r: r["data"][0].update(index=0),
                     lambda r: r["data"][0].update(index=True),
                     lambda r: r["data"][0].update(index=2),
                     lambda r: r["data"][0].update(embedding=[1.0]),
                     lambda r: r["data"][0].update(embedding=[0.0] * MODEL.dimension),
                     lambda r: r["data"][0].update(embedding=[True] * MODEL.dimension),
                     lambda r: r["data"][0].update(embedding=[2.0] * MODEL.dimension),
                     lambda r: r["data"][0].update(extra="untrusted"),
                     lambda r: r.update(data=None)]
        for mutation in mutations:
            value = response()
            mutation(value)
            result = self.inspect(value)
            self.assertEqual("invalid_vectors", result.outcome)
            self.assertEqual(50, result.input_tokens)
            self.assertEqual(Decimal("0.000001"), result.estimated_usd)
            self.assertIsNone(result.vectors)

    def test_unknown_model_usage_fields_or_missing_request_id_never_become_free(self):
        mutations = [lambda r: r.update(model="different"), lambda r: r.update(usage=None),
                     lambda r: r.update(extra_charge=1),
                     lambda r: r["usage"].update(total_tokens=51),
                     lambda r: r["usage"].update(prompt_tokens=True),
                     lambda r: r["usage"].update(prompt_tokens=0, total_tokens=0),
                     lambda r: r["usage"].update(cached_tokens=1)]
        for mutation in mutations:
            value = response()
            mutation(value)
            result = self.inspect(value)
            self.assertFalse(result.accounting_known)
            self.assertIsNone(result.estimated_usd)
            self.assertIsNone(result.vectors)
        for request_id in (None, "bad", "req_secret\nheader"):
            self.assertFalse(self.inspect(request_id=request_id).accounting_known)

    def test_duplicate_keys_invalid_json_and_nonfinite_values_are_not_accepted(self):
        for raw in (b'not JSON', b'\xff', b'{"usage":{},"usage":{}}',
                    b'{"a":NaN}', b'[' * 1100 + b']' * 1100):
            result = inspect_embedding_reply(self.request, raw, "req_fixture")
            self.assertEqual("accounting_unavailable", result.outcome)
            self.assertIsNone(result.estimated_usd)
        raw = json.dumps(response()).replace('1.0', '1e309').encode()
        result = inspect_embedding_reply(self.request, raw, "req_fixture")
        self.assertEqual("invalid_vectors", result.outcome)
        self.assertTrue(result.accounting_known)

    def test_binding_and_response_size_tampering_are_rejected(self):
        for request in (replace(self.request, body_utf8=b"{}"), replace(self.request, binding_sha256="0" * 64),
                        replace(self.request, model=replace(MODEL, dimension=3))):
            with self.assertRaises(EmbeddingProtocolError):
                inspect_embedding_reply(request, b"{}", "req_fixture")
        for raw in (b"", b"x" * (MAX_RESPONSE_BYTES + 1)):
            with self.assertRaises(EmbeddingProtocolError):
                inspect_embedding_reply(self.request, raw, "req_fixture")

    def test_usage_over_reservation_is_not_clamped_and_cost_rounds_up(self):
        value = response()
        value["usage"] = {"prompt_tokens": 100001, "total_tokens": 100001}
        result = self.inspect(value)
        self.assertEqual(100001, result.input_tokens)
        self.assertEqual(Decimal("0.002001"), result.estimated_usd)
