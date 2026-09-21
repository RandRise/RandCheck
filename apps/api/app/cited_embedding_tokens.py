"""Local, pinned embedding tokenization; no network or ambient cache downloads.

The pilot sends token IDs, not text requiring a second implicit tokenization.
Measurement is not authorization, a reservation or evidence of provider billing.
"""
import base64
from dataclasses import dataclass, field, replace
import hashlib
import json
from pathlib import Path

from .cited_embedding_protocol import (
    MODEL, MAX_REQUEST_BYTES, PreparedEmbeddingRequest, EmbeddingProtocolError,
    prepare_embedding_request, inspect_embedding_reply,
)

TIKTOKEN_VERSION = "0.12.0"
ENCODING = "cl100k_base"
VOCABULARY_SHA256 = "223921b76ee99bde995b7ff738513eef100fb51d18c93597a113bcffe865b2a7"
COUNTER_VERSION = "tiktoken-0.12.0-cl100k-ordinary-v1"
TOKEN_WIRE_VERSION = "openai-embeddings-token-ids-v1"
MAX_PILOT_INPUTS = 4
MAX_PILOT_TOKENS = 8000  # Total, not per input; same shared-ledger admission ceiling.
MAX_VOCABULARY_BYTES = 2 * 1024 * 1024
# Exact ordinary-text pattern from pinned tiktoken_ext/openai_public.py::cl100k_base.
# Special markers in documents are treated as literal text, never privileged tokens.
PATTERN = r"'(?i:[sdmt]|ll|ve|re)|[^\r\n\p{L}\p{N}]?+\p{L}++|\p{N}{1,3}+| ?[^\s\p{L}\p{N}]++[\r\n]*+|\s++$|\s*[\r\n]|\s+(?!\S)|\s"


@dataclass(frozen=True)
class MeasuredEmbeddingRequest:
    source: PreparedEmbeddingRequest = field(repr=False)
    body_utf8: bytes = field(repr=False)
    body_sha256: str
    binding_sha256: str
    token_counts: tuple[int, ...]
    counter_version: str = COUNTER_VERSION
    vocabulary_sha256: str = VOCABULARY_SHA256
    wire_version: str = TOKEN_WIRE_VERSION

    @property
    def input_tokens(self):
        return sum(self.token_counts)


class LocalEmbeddingTokenizer:
    def __init__(self, vocabulary_path: Path):
        try:
            import tiktoken
            from tiktoken.model import encoding_name_for_model

            if tiktoken.__version__ != TIKTOKEN_VERSION or encoding_name_for_model(MODEL.model) != ENCODING:
                raise ValueError()
            # Read once, hash those bytes, then construct from the same bytes. No
            # get_encoding/load_tiktoken_bpe call that could fetch an absent cache.
            with Path(vocabulary_path).open("rb") as source:
                raw = source.read(MAX_VOCABULARY_BYTES + 1)
            if len(raw) > MAX_VOCABULARY_BYTES or hashlib.sha256(raw).hexdigest() != VOCABULARY_SHA256:
                raise ValueError()
            ranks = {}
            for line in raw.splitlines():
                encoded, rank = line.split()
                token = base64.b64decode(encoded, validate=True)
                if token in ranks:
                    raise ValueError()
                ranks[token] = int(rank)
            if len(ranks) != 100256 or set(ranks.values()) != set(range(100256)):
                raise ValueError()
            self._encoding = tiktoken.Encoding(name=ENCODING, pat_str=PATTERN,
                                               mergeable_ranks=ranks, special_tokens={})
        except Exception:
            raise EmbeddingProtocolError("embedding_tokenizer_unavailable") from None

    def __repr__(self):
        return "LocalEmbeddingTokenizer(cl100k_base, pinned vocabulary)"

    def measure(self, source: PreparedEmbeddingRequest) -> MeasuredEmbeddingRequest:
        try:
            if (type(source) is not PreparedEmbeddingRequest
                    or type(source.body_utf8) is not bytes
                    or prepare_embedding_request(source.scope, source.inputs) != source
                    or len(source.inputs) > MAX_PILOT_INPUTS):
                raise ValueError()
            encoded = []
            for item in source.inputs:
                ids = self._encoding.encode_ordinary(item.text)
                if (not ids or self._encoding.decode_bytes(ids) != item.text.encode("utf-8")
                        or any(type(token) is not int or not 0 <= token < 100256 for token in ids)):
                    raise ValueError()
                encoded.append(ids)
            counts = tuple(len(ids) for ids in encoded)
            if not 1 <= sum(counts) <= MAX_PILOT_TOKENS:
                raise EmbeddingProtocolError("embedding_token_limit")
            body = json.dumps({"model": MODEL.model, "input": encoded, "dimensions": MODEL.dimension,
                               "encoding_format": "float"}, separators=(",", ":")).encode("utf-8")
            if len(body) > MAX_REQUEST_BYTES:
                raise ValueError()
            body_hash = hashlib.sha256(body).hexdigest()
            binding = json.dumps([TOKEN_WIRE_VERSION, source.binding_sha256, COUNTER_VERSION,
                                  VOCABULARY_SHA256, body_hash, counts], separators=(",", ":")).encode()
            return MeasuredEmbeddingRequest(source, body, body_hash, hashlib.sha256(binding).hexdigest(), counts)
        except EmbeddingProtocolError:
            raise
        except Exception:
            raise EmbeddingProtocolError("invalid_embedding_measurement") from None

    def verify(self, request: MeasuredEmbeddingRequest):
        if (type(request) is not MeasuredEmbeddingRequest or type(request.body_utf8) is not bytes
                or type(request.token_counts) is not tuple
                or any(type(count) is not int for count in request.token_counts)
                or self.measure(request.source) != request):
            raise EmbeddingProtocolError("invalid_embedding_measurement")


def inspect_measured_embedding_reply(tokenizer: LocalEmbeddingTokenizer, request: MeasuredEmbeddingRequest,
                                     raw: bytes, provider_request_id: str | None):
    if type(tokenizer) is not LocalEmbeddingTokenizer:
        raise EmbeddingProtocolError("embedding_tokenizer_unavailable")
    tokenizer.verify(request)
    outcome = inspect_embedding_reply(request.source, raw, provider_request_id)
    outcome = replace(outcome, binding_sha256=request.binding_sha256)
    if outcome.accounting_known and outcome.input_tokens != request.input_tokens:
        # Keep actual reported usage for settlement; never clamp it to our count.
        # The future coordinator must freeze/reconcile even a smaller discrepancy.
        return replace(outcome, vectors=None, outcome="usage_mismatch")
    return outcome
