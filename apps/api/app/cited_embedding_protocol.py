"""Offline OpenAI embedding wire/audit boundary. No HTTP, key, route or dispatch.

The future coordinator must authorize the corpus and reserve INPUT-ONLY spend in
the shared ledger. A prepared request is not a dispatch permit or token count.
"""
from dataclasses import dataclass, field
from decimal import Decimal, ROUND_CEILING
import hashlib
import json
import math
import re
from uuid import UUID

from .cited_embeddings import EmbeddedVector, EmbeddingInput, EmbeddingModel

MODEL = EmbeddingModel("openai", "text-embedding-3-small", "text-embedding-3-small", 1536)
PROTOCOL_VERSION = "openai-embeddings-float-v1"
RATE_REFERENCE = "openai-embedding-small-standard-usd-reviewed-2026-09-10"
INPUT_USD_PER_MILLION = Decimal("0.02")  # Arithmetic reference, never admission authority.
MAX_REQUEST_BYTES = 65536
MAX_RESPONSE_BYTES = 4 * 1024 * 1024
MAX_BATCH_INPUTS = 32


class EmbeddingProtocolError(ValueError):
    def __init__(self, code):
        self.code = code
        super().__init__("Embedding request or response was rejected.")


@dataclass(frozen=True)
class EmbeddingScope:
    organization_id: str
    actor_id: str
    operation_id: str
    purpose: str  # chunk or query; query embeddings must not populate chunk storage.
    index_ids: tuple[str, ...]


@dataclass(frozen=True)
class PreparedEmbeddingRequest:
    scope: EmbeddingScope
    inputs: tuple[EmbeddingInput, ...] = field(repr=False)
    body_utf8: bytes = field(repr=False)
    body_sha256: str
    binding_sha256: str  # Includes local identities absent from provider wire data.
    model: EmbeddingModel = MODEL
    protocol_version: str = PROTOCOL_VERSION


@dataclass(frozen=True)
class EmbeddingAuditOutcome:
    binding_sha256: str
    response_sha256: str
    provider_request_id: str | None
    response_utf8: bytes = field(repr=False)
    input_tokens: int | None
    vectors: tuple[EmbeddedVector, ...] | None = field(repr=False)
    outcome: str
    rate_reference: str = RATE_REFERENCE

    @property
    def accounting_known(self):
        return self.input_tokens is not None

    @property
    def estimated_usd(self):
        if self.input_tokens is None:
            return None
        return (self.input_tokens * INPUT_USD_PER_MILLION / Decimal(1_000_000)).quantize(
            Decimal("0.000001"), rounding=ROUND_CEILING)

    @property
    def publication_allowed(self):
        return False


def _uuid(value):
    if type(value) is not str or str(UUID(value)) != value or UUID(value).int == 0:
        raise ValueError()


def _json(value):
    return json.dumps(value, ensure_ascii=False, allow_nan=False, separators=(",", ":")).encode("utf-8")


def _hash(raw):
    return hashlib.sha256(raw).hexdigest()


def prepare_embedding_request(scope: EmbeddingScope, inputs: tuple[EmbeddingInput, ...]) -> PreparedEmbeddingRequest:
    try:
        if type(scope) is not EmbeddingScope or scope.purpose not in {"chunk", "query"}:
            raise ValueError()
        for value in (scope.organization_id, scope.actor_id, scope.operation_id):
            _uuid(value)
        if (type(scope.index_ids) is not tuple or not 1 <= len(scope.index_ids) <= 20
                or tuple(sorted(set(scope.index_ids))) != scope.index_ids):
            raise ValueError()
        for index_id in scope.index_ids:
            _uuid(index_id)
        if (type(inputs) is not tuple or not 1 <= len(inputs) <= MAX_BATCH_INPUTS
                or scope.purpose == "query" and len(inputs) != 1):
            raise ValueError()
        seen = set()
        for item in inputs:
            if type(item) is not EmbeddingInput:
                raise ValueError()
            _uuid(item.chunk_id)
            if (item.chunk_id in seen or type(item.text) is not str or not item.text.strip()
                    or len(item.text.encode("utf-8")) > 8000
                    or _hash(item.text.encode("utf-8")) != item.input_sha256):
                raise ValueError()
            seen.add(item.chunk_id)
        # Explicit dimensions/float format, no invented store:false field or user PII.
        body = _json({"model": MODEL.model, "input": [item.text for item in inputs],
                      "dimensions": MODEL.dimension, "encoding_format": "float"})
        if len(body) > MAX_REQUEST_BYTES:
            raise ValueError()
        body_hash = _hash(body)
        binding = _json([PROTOCOL_VERSION, scope.organization_id, scope.actor_id,
                         scope.operation_id, scope.purpose, scope.index_ids,
                         MODEL.provider_alias, MODEL.model, MODEL.version, MODEL.dimension,
                         MODEL.distance, body_hash, [(item.chunk_id, item.input_sha256) for item in inputs]])
        return PreparedEmbeddingRequest(scope, inputs, body, body_hash, _hash(binding))
    except (ValueError, TypeError, AttributeError, UnicodeError):
        raise EmbeddingProtocolError("invalid_embedding_request") from None


def _unique(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError()
        result[key] = value
    return result


def _invalid_constant(_):
    raise ValueError()


def inspect_embedding_reply(request: PreparedEmbeddingRequest, raw: bytes,
                            provider_request_id: str | None) -> EmbeddingAuditOutcome:
    try:
        if (type(request) is not PreparedEmbeddingRequest
                or prepare_embedding_request(request.scope, request.inputs) != request
                or type(raw) is not bytes or not 1 <= len(raw) <= MAX_RESPONSE_BYTES):
            raise ValueError()
    except (ValueError, TypeError, AttributeError):
        raise EmbeddingProtocolError("invalid_embedding_binding") from None

    request_id = provider_request_id if type(provider_request_id) is str and re.fullmatch(
        r"req_[A-Za-z0-9_-]{1,196}", provider_request_id) else None
    tokens = None
    vectors = None
    outcome = "accounting_unavailable"
    try:
        value = json.loads(raw.decode("utf-8"), object_pairs_hook=_unique, parse_constant=_invalid_constant)
        # Unexpected top-level fields might be additional charges, never ignore them.
        if (type(value) is not dict or set(value) != {"object", "model", "data", "usage"}
                or value["object"] != "list" or value["model"] != MODEL.model or request_id is None):
            raise ValueError()
        usage = value["usage"]
        if (type(usage) is not dict or set(usage) != {"prompt_tokens", "total_tokens"}
                or type(usage["prompt_tokens"]) is not int or type(usage["total_tokens"]) is not int
                or not 1 <= usage["prompt_tokens"] <= 2_147_483_647
                or usage["total_tokens"] != usage["prompt_tokens"]):
            raise ValueError()
        tokens = usage["prompt_tokens"]
        outcome = "invalid_vectors"  # Known billable usage survives invalid vector output.
        data = value["data"]
        if type(data) is not list or len(data) != len(request.inputs):
            raise ValueError()
        by_index = {}
        for item in data:
            if (type(item) is not dict or set(item) != {"object", "index", "embedding"}
                    or item["object"] != "embedding" or type(item["index"]) is not int
                    or not 0 <= item["index"] < len(request.inputs) or item["index"] in by_index):
                raise ValueError()
            values = item["embedding"]
            if (type(values) is not list or len(values) != MODEL.dimension
                    or any(type(x) not in (int, float) or not math.isfinite(x) or abs(x) > 1 for x in values)
                    or not any(values)):
                raise ValueError()
            source = request.inputs[item["index"]]
            by_index[item["index"]] = EmbeddedVector(source.chunk_id, source.input_sha256, tuple(values))
        vectors = tuple(by_index[i] for i in range(len(request.inputs)))
        outcome = "vectors"
    except (ValueError, TypeError, KeyError, UnicodeError, OverflowError, RecursionError):
        pass  # Fixed outcome codes; raw data stays out of errors/log representations.
    return EmbeddingAuditOutcome(request.binding_sha256, _hash(raw), request_id, raw, tokens, vectors, outcome)
