"""Replaceable embedding boundary. No production provider or model is selected."""
from __future__ import annotations

from dataclasses import dataclass
import math
from typing import Protocol

from .cited_chunking import content_hash, _id


class EmbeddingError(ValueError):
    pass


@dataclass(frozen=True)
class EmbeddingModel:
    provider_alias: str
    model: str
    version: str
    dimension: int
    distance: str = "cosine"


@dataclass(frozen=True)
class EmbeddingInput:
    chunk_id: str
    text: str
    input_sha256: str


@dataclass(frozen=True)
class EmbeddedVector:
    chunk_id: str
    input_sha256: str
    values: tuple[float, ...]


@dataclass(frozen=True)
class EmbeddingBatch:
    model: EmbeddingModel
    vectors: tuple[EmbeddedVector, ...]
    duration_ms: int
    estimated_cost: float | None = None
    cost_currency: str | None = None


class EmbeddingAdapter(Protocol):
    async def embed(self, inputs: tuple[EmbeddingInput, ...]) -> EmbeddingBatch: ...


class DisabledEmbeddingAdapter:
    async def embed(self, inputs: tuple[EmbeddingInput, ...]) -> EmbeddingBatch:
        raise EmbeddingError("Embedding provider has not been selected")


async def embed_checked(adapter: EmbeddingAdapter, model: EmbeddingModel,
                        inputs: tuple[EmbeddingInput, ...]) -> EmbeddingBatch:
    """Validate identities and every vector before returning any batch to a caller."""
    if (any(not isinstance(x, str) or not x.strip() or len(x) > 100
            for x in (model.provider_alias, model.model, model.version)) or
            type(model.dimension) is not int or not 1 <= model.dimension <= 65536 or
            model.distance != "cosine"):
        raise EmbeddingError("Invalid model descriptor")
    if not 1 <= len(inputs) <= 128 or len({x.chunk_id for x in inputs}) != len(inputs):
        raise EmbeddingError("Batch requires 1..128 unique chunk IDs")
    expected = {}
    total = 0
    for item in inputs:
        if _id(item.chunk_id) != item.chunk_id or not item.text.strip():
            raise EmbeddingError("Invalid embedding input")
        size = len(item.text.encode("utf-8"))
        total += size
        if size > 65536 or total > 1048576 or content_hash(item.text) != item.input_sha256:
            raise EmbeddingError("Input hash or byte limit mismatch")
        expected[item.chunk_id] = item.input_sha256
    result = await adapter.embed(inputs)
    if (not isinstance(result, EmbeddingBatch) or result.model != model or
            not isinstance(result.vectors, (tuple, list)) or len(result.vectors) != len(inputs)):
        raise EmbeddingError("Provider model or batch size mismatch")
    if (type(result.duration_ms) is not int or result.duration_ms < 0 or
            (result.estimated_cost is None) != (result.cost_currency is None)):
        raise EmbeddingError("Invalid provider metrics")
    if result.estimated_cost is not None and (
            type(result.estimated_cost) not in (float, int) or
            not math.isfinite(result.estimated_cost) or result.estimated_cost < 0 or
            not isinstance(result.cost_currency, str) or len(result.cost_currency) != 3 or
            any(c < "A" or c > "Z" for c in result.cost_currency)):
        raise EmbeddingError("Invalid provider cost")
    vectors = {}
    for vector in result.vectors:
        if (not isinstance(vector, EmbeddedVector) or
                not isinstance(vector.values, (tuple, list)) or
                vector.chunk_id in vectors or vector.chunk_id not in expected or
                expected[vector.chunk_id] != vector.input_sha256 or
                len(vector.values) != model.dimension or
                any(type(x) not in (float, int) or not math.isfinite(x) for x in vector.values) or
                not any(x != 0 for x in vector.values)):
            raise EmbeddingError("Invalid vector identity, hash, dimension, or values")
        vectors[vector.chunk_id] = EmbeddedVector(vector.chunk_id, vector.input_sha256, tuple(vector.values))
    # Providers may return out of order. Preserve the requested order after identity checks.
    return EmbeddingBatch(model, tuple(vectors[item.chunk_id] for item in inputs),
                          result.duration_ms, result.estimated_cost, result.cost_currency)
