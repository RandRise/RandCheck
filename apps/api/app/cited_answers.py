"""Provider-independent answer boundary; no route, network adapter or publication.

ASP.NET must load authorized frozen evidence and owns final citation validation.
This module checks the envelope, not authenticated membership or semantic truth.
"""
from __future__ import annotations

import asyncio
from dataclasses import dataclass, field
import hashlib
import json
from typing import Protocol
from uuid import UUID
from .cited_prompt import PROMPT_VERSION, assemble_prompt
from .cited_source_context import AnswerSource, source_to_wire


MAX_REQUEST_BYTES = 65536
MAX_RESPONSE_BYTES = 65536
MAX_INPUT_TOKENS = 8000
MAX_OUTPUT_TOKENS = 4000
BOUNDARY_VERSION = "answer-envelope-v2"


class AnswerBoundaryError(ValueError):
    def __init__(self, code: str):
        self.code = code
        super().__init__("Answer preparation or provider result was rejected.")


@dataclass(frozen=True)
class AnswerContext:
    organization_id: str
    actor_id: str
    retrieval_run_id: str


@dataclass(frozen=True)
class AnswerModel:
    provider_alias: str
    model: str
    version: str


@dataclass(frozen=True)
class AnswerEvidence:
    organization_id: str
    retrieval_run_id: str
    hit_id: str
    span_ids: tuple[str, ...]
    content: str = field(repr=False)
    content_sha256: str
    source: AnswerSource | None = field(default=None, repr=False)


@dataclass(frozen=True)
class PreparedAnswerRequest:
    context: AnswerContext
    model: AnswerModel
    request_sha256: str
    evidence_json: bytes = field(repr=False)
    maximum_output_tokens: int
    prompt_json: bytes = field(repr=False)
    prompt_sha256: str
    prompt_version: str = PROMPT_VERSION
    boundary_version: str = BOUNDARY_VERSION


@dataclass(frozen=True)
class AnswerProposalEnvelope:
    model: AnswerModel
    request_sha256: str
    proposal_utf8: bytes = field(repr=False)
    input_tokens: int
    output_tokens: int  # Must include billable reasoning, not only visible text.
    outcome: str = "completed"

    @property
    def semantic_support_established(self) -> bool:
        return False


class AnswerAdapter(Protocol):
    async def propose(self, request: PreparedAnswerRequest) -> AnswerProposalEnvelope: ...


class DisabledAnswerAdapter:
    async def propose(self, request: PreparedAnswerRequest) -> AnswerProposalEnvelope:
        raise AnswerBoundaryError("generation_disabled")


def _uuid(value: str) -> None:
    if not isinstance(value, str) or str(UUID(value)) != value or UUID(value).int == 0:
        raise ValueError()


def _identifier(value: str) -> None:
    if (not isinstance(value, str) or not value or len(value) > 100
            or value != value.strip() or any(ord(c) < 32 or ord(c) == 127 for c in value)):
        raise ValueError()


def prepare_answer_request(context: AnswerContext, model: AnswerModel, question: str,
                           evidence: tuple[AnswerEvidence, ...],
                           maximum_output_tokens: int = MAX_OUTPUT_TOKENS) -> PreparedAnswerRequest:
    try:
        if type(context) is not AnswerContext or type(model) is not AnswerModel:
            raise ValueError()
        for value in (context.organization_id, context.actor_id, context.retrieval_run_id):
            _uuid(value)
        for value in (model.provider_alias, model.model, model.version):
            _identifier(value)
        if (not isinstance(question, str) or not question.strip()
                or len(question.encode("utf-16-le")) // 2 > 4000
                or type(maximum_output_tokens) is not int
                or not 1 <= maximum_output_tokens <= MAX_OUTPUT_TOKENS
                or type(evidence) is not tuple or not 1 <= len(evidence) <= 20):
            raise ValueError()
        hits: set[str] = set()
        source_aware = any(item.source is not None for item in evidence)
        items = []
        for item in evidence:
            if type(item) is not AnswerEvidence:
                raise ValueError()
            _uuid(item.hit_id)
            if (item.organization_id != context.organization_id
                    or item.retrieval_run_id != context.retrieval_run_id
                    or item.hit_id in hits or type(item.span_ids) is not tuple
                    or len(item.span_ids) > 128 or len(set(item.span_ids)) != len(item.span_ids)):
                raise ValueError()
            hits.add(item.hit_id)
            for span_id in item.span_ids:
                _uuid(span_id)
            if not isinstance(item.content, str) or not item.content.strip():
                raise ValueError()
            raw = item.content.encode("utf-8")
            if len(raw) > MAX_REQUEST_BYTES or hashlib.sha256(raw).hexdigest() != item.content_sha256:
                raise ValueError()
            wire = {"hitId": item.hit_id, "spanIds": item.span_ids, "content": item.content}
            if source_aware:
                wire["source"] = source_to_wire(item.source, item.span_ids, item.content)
            items.append(wire)
        # Data only. Future provider prompt assembly must treat every content string
        # as untrusted evidence; JSON encoding alone is not an injection defense.
        payload = json.dumps({"schemaVersion": "1.1" if source_aware else "1.0", "question": question, "evidence": items},
                             ensure_ascii=False, separators=(",", ":")).encode("utf-8")
        if len(payload) > MAX_REQUEST_BYTES:
            raise ValueError()
        prompt, prompt_hash = assemble_prompt(payload, model, maximum_output_tokens)
        if len(prompt) > MAX_REQUEST_BYTES:
            raise ValueError()
        prompt_version = "cited-answer-prompt-v4" if source_aware else PROMPT_VERSION
        binding = json.dumps([BOUNDARY_VERSION, prompt_version, prompt_hash, context.organization_id, context.actor_id,
                              context.retrieval_run_id, model.provider_alias, model.model,
                              model.version, maximum_output_tokens], separators=(",", ":")).encode()
        digest = hashlib.sha256(binding + b"\n" + payload).hexdigest()
        return PreparedAnswerRequest(context, model, digest, payload, maximum_output_tokens, prompt, prompt_hash, prompt_version)
    except (ValueError, TypeError, AttributeError, UnicodeError):
        raise AnswerBoundaryError("invalid_answer_request") from None


async def propose_answer_checked(context: AnswerContext, model: AnswerModel, question: str,
                                 evidence: tuple[AnswerEvidence, ...],
                                 adapter: AnswerAdapter | None = None,
                                 maximum_output_tokens: int = MAX_OUTPUT_TOKENS) -> AnswerProposalEnvelope:
    request = prepare_answer_request(context, model, question, evidence, maximum_output_tokens)
    return await propose_prepared_checked(request, adapter)


async def propose_prepared_checked(request: PreparedAnswerRequest,
                                    adapter: AnswerAdapter | None = None) -> AnswerProposalEnvelope:
    # Dispatch the exact immutable packet measured by preflight, not a newly assembled prompt.
    model = request.model
    maximum_output_tokens = request.maximum_output_tokens
    try:
        async with asyncio.timeout(30):
            result = await (adapter if adapter is not None else DisabledAnswerAdapter()).propose(request)
    except asyncio.CancelledError:
        raise
    except AnswerBoundaryError:
        # Only the local disabled adapter gets a distinct safe error. Never trust
        # an external adapter's error code/message as public diagnostics.
        code = "generation_disabled" if adapter is None or type(adapter) is DisabledAnswerAdapter else "answer_provider_failed"
        raise AnswerBoundaryError(code) from None
    except TimeoutError:
        raise AnswerBoundaryError("answer_provider_timeout") from None
    except Exception:
        raise AnswerBoundaryError("answer_provider_failed") from None

    if type(result) is not AnswerProposalEnvelope:
        raise AnswerBoundaryError("invalid_answer_envelope")
    if result.outcome != "completed":
        # A transport/model refusal or truncation is not a grounded insufficiency answer.
        raise AnswerBoundaryError("answer_provider_incomplete")
    try:
        if (result.model != model or result.request_sha256 != request.request_sha256
                or type(result.proposal_utf8) is not bytes
                or not 1 <= len(result.proposal_utf8) <= MAX_RESPONSE_BYTES
                or type(result.input_tokens) is not int or not 1 <= result.input_tokens <= MAX_INPUT_TOKENS
                or type(result.output_tokens) is not int or not 1 <= result.output_tokens <= maximum_output_tokens):
            raise ValueError()
        result.proposal_utf8.decode("utf-8")
    except (ValueError, TypeError, UnicodeError):
        raise AnswerBoundaryError("invalid_answer_envelope") from None
    # Deliberately not parsed/published: ASP.NET must perform structural citation
    # checks and the later semantic-support gate before accepting any claims.
    return result
