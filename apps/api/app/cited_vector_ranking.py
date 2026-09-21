"""Pure exact cosine/RRF for a caller-authorized bounded corpus, never a DB query.

ASP.NET must load eligible organization/source-scoped vectors before calling,
and recheck eligibility before persisting hits. Matching IDs here is not auth.
"""
from dataclasses import dataclass, field
import math
from uuid import UUID

from .cited_embeddings import EmbeddingModel

POLICY_VERSION = "exact-cosine-rrf-v1"
MAX_CANDIDATES = 2000
MAX_COMPONENTS = 3_072_000


class VectorRankingError(ValueError):
    def __init__(self):
        super().__init__("The scoped vector ranking input was rejected.")


@dataclass(frozen=True)
class VectorCandidate:
    organization_id: str
    index_id: str
    chunk_id: str
    input_sha256: str
    model: EmbeddingModel
    values: tuple[float, ...] = field(repr=False)


@dataclass(frozen=True)
class VectorRank:
    chunk_id: str
    rank: int
    score: float
    lexical_rank: int | None = None
    vector_rank: int | None = None


def _id(value):
    if type(value) is not str or str(UUID(value)) != value or UUID(value).int == 0:
        raise ValueError()


def _unit(values, dimension):
    if (type(values) is not tuple or len(values) != dimension
            or any(type(x) not in (int, float) or not math.isfinite(x) for x in values)):
        raise ValueError()
    scale = max(abs(x) for x in values)
    if scale == 0:
        raise ValueError()
    scaled = tuple(x / scale for x in values)
    norm = math.sqrt(math.fsum(x * x for x in scaled))
    return tuple(x / norm for x in scaled)


def exact_cosine(organization_id: str, index_ids: tuple[str, ...], model: EmbeddingModel,
                 query: tuple[float, ...], candidates: tuple[VectorCandidate, ...],
                 limit: int = 20) -> tuple[VectorRank, ...]:
    try:
        _id(organization_id)
        if (type(index_ids) is not tuple or not 1 <= len(index_ids) <= 20
                or len(set(index_ids)) != len(index_ids) or type(model) is not EmbeddingModel
                or type(model.dimension) is not int or not 1 <= model.dimension <= 65536
                or model.distance != "cosine" or type(candidates) is not tuple
                or len(candidates) > MAX_CANDIDATES or len(candidates) * model.dimension > MAX_COMPONENTS
                or type(limit) is not int or not 1 <= limit <= MAX_CANDIDATES):
            raise ValueError()
        for identifier in (model.provider_alias, model.model, model.version):
            if (type(identifier) is not str or not identifier.strip() or identifier != identifier.strip()
                    or len(identifier) > 100 or any(ord(c) < 32 or ord(c) == 127 for c in identifier)):
                raise ValueError()
        for index_id in index_ids:
            _id(index_id)
        unit_query = _unit(query, model.dimension)
        scores = []
        seen = set()
        for candidate in candidates:
            if (type(candidate) is not VectorCandidate or candidate.organization_id != organization_id
                    or candidate.index_id not in index_ids or candidate.model != model
                    or candidate.chunk_id in seen or type(candidate.input_sha256) is not str
                    or len(candidate.input_sha256) != 64
                    or any(c not in "0123456789abcdef" for c in candidate.input_sha256)):
                raise ValueError()
            _id(candidate.chunk_id)
            seen.add(candidate.chunk_id)
            vector = _unit(candidate.values, model.dimension)
            score = max(-1.0, min(1.0, math.fsum(a * b for a, b in zip(unit_query, vector))))
            scores.append((candidate.chunk_id, score))
        # Validate every supplied candidate before truncation, including low-ranked ones.
        scores.sort(key=lambda item: (-item[1], item[0]))
        return tuple(VectorRank(chunk, rank, score, vector_rank=rank)
                     for rank, (chunk, score) in enumerate(scores[:limit], 1))
    except (ValueError, TypeError, AttributeError, OverflowError):
        raise VectorRankingError() from None


def reciprocal_rank_fusion(eligible_chunk_ids: frozenset[str], lexical_ids: tuple[str, ...],
                           vector_ids: tuple[str, ...], top_k: int = 5,
                           rrf_k: int = 60) -> tuple[VectorRank, ...]:
    try:
        if (type(eligible_chunk_ids) is not frozenset or len(eligible_chunk_ids) > MAX_CANDIDATES
                or type(top_k) is not int or not 1 <= top_k <= 20
                or type(rrf_k) is not int or not 1 <= rrf_k <= 1000):
            raise ValueError()
        for chunk_id in eligible_chunk_ids:
            _id(chunk_id)
        for ranking in (lexical_ids, vector_ids):
            if (type(ranking) is not tuple or len(ranking) > MAX_CANDIDATES
                    or len(set(ranking)) != len(ranking) or not set(ranking) <= eligible_chunk_ids):
                raise ValueError()
        lexical = {chunk: rank for rank, chunk in enumerate(lexical_ids, 1)}
        vector = {chunk: rank for rank, chunk in enumerate(vector_ids, 1)}
        scores = [(chunk, (1 / (rrf_k + lexical[chunk]) if chunk in lexical else 0)
                   + (1 / (rrf_k + vector[chunk]) if chunk in vector else 0))
                  for chunk in lexical.keys() | vector.keys()]
        scores.sort(key=lambda item: (-item[1], item[0]))
        return tuple(VectorRank(chunk, rank, score, lexical.get(chunk), vector.get(chunk))
                     for rank, (chunk, score) in enumerate(scores[:top_k], 1))
    except (ValueError, TypeError, AttributeError):
        raise VectorRankingError() from None
