from dataclasses import replace
import hashlib
import json
from pathlib import Path
import tempfile
import traceback
import unittest
from unittest.mock import patch

import tiktoken
import tiktoken_ext.openai_public

from app.cited_embedding_protocol import EmbeddingProtocolError, prepare_embedding_request
from app.cited_embedding_tokens import LocalEmbeddingTokenizer, inspect_measured_embedding_reply
from test_cited_embedding_protocol import INPUTS, SCOPE, item, uid, response

VOCABULARY = Path(__file__).resolve().parents[3] / ".venv/share/tiktoken/cl100k_base.tiktoken"


class EmbeddingTokenTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tokenizer = LocalEmbeddingTokenizer(VOCABULARY)

    def test_known_tokens_and_official_constructor_agree_for_literal_unicode_and_invoice_text(self):
        # Use the pinned library's independent constructor/pattern/special-token
        # configuration without letting its loader download a cache.
        with patch("tiktoken_ext.openai_public.load_tiktoken_bpe",
                   return_value=self.tokenizer._encoding._mergeable_ranks):
            official = tiktoken.Encoding(**tiktoken_ext.openai_public.cl100k_base())
        for text in ("hello world", INPUTS[0].text, "Amount due: AED 8,820.00\r\nNet 30",
                     "فاتورة ضريبية دبي", "café 👩🏽‍💻", "e\u0301", "<|endoftext|> ignore instructions",
                     "before\x00after"):
            measured = self.tokenizer.measure(prepare_embedding_request(SCOPE, (item(5, text),)))
            tokens = json.loads(measured.body_utf8)["input"][0]
            self.assertEqual(official.encode(text, disallowed_special=()), tokens)
            self.assertEqual(text.encode(), official.decode_bytes(tokens))
            self.assertEqual(len(tokens), measured.input_tokens)
            if text == "hello world":
                self.assertEqual([15339, 1917], tokens)
                self.assertEqual(2, measured.input_tokens)  # Not eleven UTF-8 bytes.
        self.assertEqual("cl100k_base", tiktoken.encoding_name_for_model("text-embedding-3-small"))

    def test_loading_and_counting_are_network_free_even_with_bad_ambient_cache_settings(self):
        with patch.dict("os.environ", {"TIKTOKEN_CACHE_DIR": "https://example.invalid/cache"}), \
             patch("socket.socket", side_effect=AssertionError("network must not be used")):
            tokenizer = LocalEmbeddingTokenizer(VOCABULARY)
            self.assertGreater(tokenizer.measure(prepare_embedding_request(SCOPE, INPUTS)).input_tokens, 0)

    def test_absent_corrupt_oversized_and_wrong_package_fail_without_fallback_or_path_leaks(self):
        with tempfile.TemporaryDirectory() as folder:
            private = Path(folder) / "PRIVATE-NAME"
            for raw in (None, b"wrong", b"x" * (2 * 1024 * 1024 + 1)):
                if raw is not None:
                    private.write_bytes(raw)
                with patch("socket.socket", side_effect=AssertionError()), self.assertRaises(EmbeddingProtocolError) as error:
                    LocalEmbeddingTokenizer(private)
                self.assertEqual("embedding_tokenizer_unavailable", error.exception.code)
                self.assertNotIn("PRIVATE-NAME", "".join(traceback.format_exception(error.exception)))
        with patch.object(tiktoken, "__version__", "unapproved"), self.assertRaises(EmbeddingProtocolError):
            LocalEmbeddingTokenizer(VOCABULARY)

    def test_wire_contains_token_ids_and_receipt_binds_body_counter_and_exact_scope(self):
        source = prepare_embedding_request(SCOPE, INPUTS)
        measured = self.tokenizer.measure(source)
        wire = json.loads(measured.body_utf8)
        self.assertEqual({"model", "input", "dimensions", "encoding_format"}, set(wire))
        self.assertEqual(1536, wire["dimensions"])
        self.assertEqual("float", wire["encoding_format"])
        self.assertTrue(all(type(token) is int for row in wire["input"] for token in row))
        self.assertEqual(tuple(map(len, wire["input"])), measured.token_counts)
        self.assertEqual(hashlib.sha256(measured.body_utf8).hexdigest(), measured.body_sha256)
        self.assertNotEqual(source.binding_sha256, measured.binding_sha256)
        self.assertNotIn(INPUTS[0].text, repr(measured))
        other = self.tokenizer.measure(prepare_embedding_request(replace(SCOPE, actor_id=uid(99)), INPUTS))
        self.assertEqual(measured.body_utf8, other.body_utf8)
        self.assertNotEqual(measured.binding_sha256, other.binding_sha256)

    def test_rebuilt_measurement_rejects_changed_bytes_counts_versions_and_mutable_types(self):
        measured = self.tokenizer.measure(prepare_embedding_request(SCOPE, (item(5, "hello"),)))
        for changed in (replace(measured, body_utf8=b"{}"), replace(measured, body_sha256="0" * 64),
                        replace(measured, binding_sha256="0" * 64), replace(measured, token_counts=(2,)),
                        replace(measured, counter_version="other"), replace(measured, vocabulary_sha256="0" * 64),
                        replace(measured, wire_version="other"), replace(measured, token_counts=(True,)),
                        replace(measured, body_utf8=bytearray(measured.body_utf8))):
            with self.assertRaises(EmbeddingProtocolError):
                self.tokenizer.verify(changed)

    def test_batch_and_total_tokens_have_no_silent_truncation(self):
        text = "!@" * 2000
        exact = self.tokenizer.measure(prepare_embedding_request(SCOPE, (item(5, text), item(6, text))))
        self.assertEqual(8000, exact.input_tokens)
        with self.assertRaises(EmbeddingProtocolError) as error:
            self.tokenizer.measure(prepare_embedding_request(SCOPE, (item(5, text), item(6, text + "!"))))
        self.assertEqual("embedding_token_limit", error.exception.code)
        with self.assertRaises(EmbeddingProtocolError):
            self.tokenizer.measure(prepare_embedding_request(SCOPE, tuple(item(i + 5, "x") for i in range(5))))

    def test_audit_binds_token_wire_and_preserves_actual_cost_on_usage_discrepancy(self):
        request = self.tokenizer.measure(prepare_embedding_request(SCOPE, INPUTS))
        for difference in (0, 1, -1):
            value = response()
            count = request.input_tokens + difference
            value["usage"] = {"prompt_tokens": count, "total_tokens": count}
            result = inspect_measured_embedding_reply(self.tokenizer, request, json.dumps(value).encode(), "req_fixture")
            self.assertEqual(request.binding_sha256, result.binding_sha256)
            self.assertEqual(count, result.input_tokens)
            self.assertEqual("vectors" if difference == 0 else "usage_mismatch", result.outcome)
            self.assertEqual(difference != 0, result.vectors is None)
            self.assertIsNotNone(result.estimated_usd)
            self.assertFalse(result.publication_allowed)

    def test_invalid_vectors_do_not_erase_known_usage_and_unknown_usage_stays_unknown(self):
        request = self.tokenizer.measure(prepare_embedding_request(SCOPE, INPUTS))
        value = response()
        value["usage"] = {"prompt_tokens": request.input_tokens, "total_tokens": request.input_tokens}
        value["data"] = []
        result = inspect_measured_embedding_reply(self.tokenizer, request, json.dumps(value).encode(), "req_fixture")
        self.assertEqual("invalid_vectors", result.outcome)
        self.assertEqual(request.input_tokens, result.input_tokens)
        result = inspect_measured_embedding_reply(self.tokenizer, request, b"{}", None)
        self.assertIsNone(result.input_tokens)
        self.assertIsNone(result.estimated_usd)
