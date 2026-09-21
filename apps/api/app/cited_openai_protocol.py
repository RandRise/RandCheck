"""Pure OpenAI wire/accounting boundary; deliberately no HTTP or key access.

These values are not dispatch permissions, authenticated receipts or verified bills.
The coordinator must durably bind them to its ledger before a transport is enabled.
"""
from dataclasses import dataclass, field
from datetime import datetime, timezone
from decimal import Decimal, ROUND_CEILING
import hashlib
import json
import re

from .cited_answers import (
    AnswerEvidence, AnswerModel, PreparedAnswerRequest, prepare_answer_request,
    MAX_INPUT_TOKENS, MAX_REQUEST_BYTES, MAX_RESPONSE_BYTES,
)
from .cited_source_context import source_from_wire

MODEL = AnswerModel("openai", "gpt-5-nano", "gpt-5-nano-2025-08-07")
PROTOCOL_VERSION = "openai-cited-responses-v1"
RATE_VERSION = "openai-nano-standard-usd-2026-09-10"
RATE_FROM = datetime(2026, 9, 10, tzinfo=timezone.utc)
RATE_UNTIL = datetime(2026, 9, 11, tzinfo=timezone.utc)
MICRO_USD = Decimal("0.000001")
MAX_PROVIDER_RESPONSE_BYTES = 262144
COUNT_FIELDS = frozenset({"model", "input", "instructions", "reasoning", "text",
                          "tools", "tool_choice", "parallel_tool_calls", "truncation"})


class OpenAiProtocolError(ValueError):
    def __init__(self, code):
        self.code = code
        super().__init__("OpenAI request or accounting data was rejected.")


@dataclass(frozen=True)
class OpenAiRequest:
    source: PreparedAnswerRequest = field(repr=False)
    generation_utf8: bytes = field(repr=False)
    counting_utf8: bytes = field(repr=False)
    generation_sha256: str
    counting_sha256: str
    protocol_version: str = PROTOCOL_VERSION
    reasoning_effort: str = "minimal"


@dataclass(frozen=True)
class OpenAiCount:
    source_request_sha256: str
    generation_sha256: str
    counting_sha256: str
    input_tokens: int
    protocol_version: str = PROTOCOL_VERSION


@dataclass(frozen=True)
class OpenAiUsage:
    input_tokens: int
    cached_input_tokens: int
    output_tokens: int  # Includes reasoning; never add reasoning_tokens again.
    reasoning_tokens: int


@dataclass(frozen=True)
class OpenAiReply:
    response_id: str
    provider_request_id: str
    response_sha256: str
    source_request_sha256: str
    generation_sha256: str
    model: str
    status: str
    usage: OpenAiUsage
    proposal_outcome: str
    proposal_utf8: bytes | None = field(repr=False)

    @property
    def publication_allowed(self):
        return False


@dataclass(frozen=True)
class NanoCost:
    exact_usd: Decimal
    ledger_usd: Decimal  # Ceiling to the existing PostgreSQL microdollar scale.
    rate_version: str = RATE_VERSION


def _json(value):
    return json.dumps(value, ensure_ascii=False, allow_nan=False,
                      separators=(",", ":")).encode("utf-8")


def _hash(raw):
    return hashlib.sha256(raw).hexdigest()


def _unique(pairs):
    value = {}
    for key, item in pairs:
        if key in value:
            raise ValueError()
        value[key] = item
    return value


def _constant(_):
    raise ValueError()


def _load(raw, limit):
    if type(raw) is not bytes or not 1 <= len(raw) <= limit:
        raise ValueError()
    result = json.loads(raw.decode("utf-8"), object_pairs_hook=_unique,
                        parse_constant=_constant)
    if type(result) is not dict:
        raise ValueError()
    return result


def _int(value, minimum=0):
    if type(value) is not int or not minimum <= value <= 2_147_483_647:
        raise ValueError()
    return value


def _id(value, prefix):
    if (type(value) is not str or not value.startswith(prefix)
            or not re.fullmatch(r"[A-Za-z0-9_-]{1,200}", value)
            or len(value) <= len(prefix)):
        raise ValueError()
    return value


def prepare_openai_request(source: PreparedAnswerRequest, reasoning_effort="minimal") -> OpenAiRequest:
    try:
        if type(source) is not PreparedAnswerRequest or source.model != MODEL or reasoning_effort not in ("minimal", "low"):
            raise ValueError()
        payload = _load(source.evidence_json, MAX_REQUEST_BYTES)
        evidence = tuple(AnswerEvidence(
            source.context.organization_id, source.context.retrieval_run_id,
            item["hitId"], tuple(item["spanIds"]), item["content"],
            _hash(item["content"].encode("utf-8")),
            source_from_wire(item["source"]) if payload["schemaVersion"] == "1.1" else None)
            for item in payload["evidence"])
        # Recheck all source/packet/context hashes and versioned safety controls.
        rebuilt = prepare_answer_request(source.context, source.model,
                                         payload["question"], evidence, source.maximum_output_tokens)
        if rebuilt != source:
            raise ValueError()
        packet = _load(source.prompt_json, MAX_REQUEST_BYTES)
        body = {
            "model": MODEL.version, "instructions": packet["instructions"],
            "input": packet["input"],
            "text": {"format": {"type": "json_schema", "name": "cited_answer_v4" if source.prompt_version == "cited-answer-prompt-v4" else "cited_answer_v3",
                                "strict": True, "schema": packet["responseSchema"]}},
            "reasoning": {"effort": reasoning_effort},
            "tools": [], "tool_choice": "none", "parallel_tool_calls": False,
            "truncation": "disabled", "max_output_tokens": source.maximum_output_tokens,
            "store": False, "background": False, "stream": False, "service_tier": "default",
        }
        generation = _json(body)
        counting = _json({key: value for key, value in body.items() if key in COUNT_FIELDS})
        if len(generation) > MAX_REQUEST_BYTES or len(counting) > MAX_REQUEST_BYTES:
            raise ValueError()
        return OpenAiRequest(source, generation, counting, _hash(generation), _hash(counting), reasoning_effort=reasoning_effort)
    except (ValueError, TypeError, KeyError, AttributeError, UnicodeError, RecursionError):
        raise OpenAiProtocolError("invalid_openai_request") from None


def _request(request):
    if type(request) is not OpenAiRequest or prepare_openai_request(request.source, request.reasoning_effort) != request:
        raise ValueError()


def parse_openai_count(request: OpenAiRequest, raw: bytes) -> OpenAiCount:
    try:
        _request(request)
        value = _load(raw, 1024)
        if (set(value) != {"object", "input_tokens"}
                or value["object"] != "response.input_tokens"
                or not 1 <= _int(value["input_tokens"]) <= MAX_INPUT_TOKENS):
            raise ValueError()
        return OpenAiCount(request.source.request_sha256, request.generation_sha256, request.counting_sha256,
                           value["input_tokens"])
    except (ValueError, TypeError, KeyError, AttributeError, UnicodeError, RecursionError):
        raise OpenAiProtocolError("invalid_openai_count") from None


def _usage(value):
    if type(value) is not dict or set(value) != {
        "input_tokens", "input_tokens_details", "output_tokens", "output_tokens_details", "total_tokens"
    }:
        raise ValueError()
    inputs = _int(value["input_tokens"])
    outputs = _int(value["output_tokens"])
    details = value["input_tokens_details"]
    output_details = value["output_tokens_details"]
    if (type(details) is not dict
            or set(details) not in ({"cached_tokens"}, {"cached_tokens", "cache_write_tokens"})
            or _int(details.get("cache_write_tokens", 0)) != 0
            or type(output_details) is not dict or set(output_details) != {"reasoning_tokens"}):
        # Unknown billable fields or cache-write rates must never be silently ignored.
        raise ValueError()
    cached = _int(details["cached_tokens"])
    reasoning = _int(output_details["reasoning_tokens"])
    if (cached > inputs or reasoning > outputs
            or _int(value["total_tokens"]) != inputs + outputs):
        raise ValueError()
    return OpenAiUsage(inputs, cached, outputs, reasoning)


def _proposal(value):
    if value["status"] != "completed":
        return "provider_incomplete", None
    if value.get("error") is not None or value.get("incomplete_details") is not None:
        return "invalid_output", None
    try:
        output = value["output"]
        if type(output) is not list:
            raise ValueError()
        messages = []
        for item in output:
            if type(item) is not dict or item.get("type") not in {"reasoning", "message"}:
                raise ValueError()
            if item["type"] == "message":
                messages.append(item)
        if len(messages) != 1:
            raise ValueError()
        message = messages[0]
        if message.get("role") != "assistant" or message.get("status") != "completed":
            raise ValueError()
        content = message["content"]
        if type(content) is not list or len(content) != 1 or type(content[0]) is not dict:
            raise ValueError()
        if content[0].get("type") == "refusal":
            return "provider_refusal", None
        if (content[0].get("type") != "output_text"
                or content[0].get("annotations", []) != []
                or type(content[0].get("text")) is not str):
            raise ValueError()
        raw = content[0]["text"].encode("utf-8")
        wrapper = _load(raw, MAX_RESPONSE_BYTES)  # Reject duplicate keys at every depth.
        if set(wrapper) != {"answer"} or type(wrapper["answer"]) is not dict:
            raise ValueError()
        # v3 wire envelope only. Do not repair statuses, lists, duplicates or claims.
        # The original provider response bytes remain in the accounting journal.
        return "proposal", _json(wrapper["answer"])
    except (ValueError, TypeError, KeyError, UnicodeError, RecursionError):
        return "invalid_output", None


def inspect_openai_reply(request: OpenAiRequest, raw: bytes, provider_request_id: str) -> OpenAiReply:
    try:
        _request(request)
        value = _load(raw, MAX_PROVIDER_RESPONSE_BYTES)
        if (value.get("object") != "response" or value.get("model") != MODEL.version
                or value.get("status") not in {"completed", "incomplete", "failed", "cancelled"}
                or value.get("service_tier") != "default" or value.get("tools") != []):
            raise ValueError()
        if type(value.get("output")) is list and any(
            type(item) is dict and item.get("type") not in {"reasoning", "message"}
            for item in value["output"]
        ):
            raise ValueError()  # Unexpected tools/modalities may carry non-token charges.
        if type(value.get("output")) is list:
            for item in value["output"]:
                if type(item) is dict and type(item.get("content")) is list and any(
                    type(part) is dict and part.get("type") not in {"output_text", "refusal"}
                    for part in item["content"]
                ):
                    raise ValueError()
        usage = _usage(value["usage"])
        outcome, proposal = _proposal(value)
        return OpenAiReply(_id(value["id"], "resp_"), _id(provider_request_id, "req_"),
                           _hash(raw), request.source.request_sha256, request.generation_sha256,
                           MODEL.version, value["status"],
                           usage, outcome, proposal)
    except (ValueError, TypeError, KeyError, AttributeError, UnicodeError, RecursionError):
        raise OpenAiProtocolError("openai_accounting_unavailable") from None


def _valid_usage(usage):
    if type(usage) is not OpenAiUsage:
        raise ValueError()
    _int(usage.input_tokens)
    _int(usage.output_tokens)
    if (_int(usage.cached_input_tokens) > usage.input_tokens
            or _int(usage.reasoning_tokens) > usage.output_tokens):
        raise ValueError()


def price_nano_usage(usage: OpenAiUsage, now: datetime) -> NanoCost:
    """Published standard token-rate arithmetic, not authoritative billing reconciliation."""
    try:
        _valid_usage(usage)
        if (type(now) is not datetime or now.tzinfo is None
                or not RATE_FROM <= now < RATE_UNTIL):
            raise ValueError()
        cost = ((usage.input_tokens - usage.cached_input_tokens) * Decimal("0.05")
                + usage.cached_input_tokens * Decimal("0.005")
                + usage.output_tokens * Decimal("0.40")) / Decimal(1_000_000)
        return NanoCost(cost, cost.quantize(MICRO_USD, rounding=ROUND_CEILING))
    except (ValueError, TypeError, AttributeError):
        raise OpenAiProtocolError("nano_rate_unavailable") from None


def quote_nano_request(request: OpenAiRequest, count: OpenAiCount, now: datetime) -> NanoCost:
    try:
        _request(request)
        if (type(count) is not OpenAiCount or count.protocol_version != PROTOCOL_VERSION
                or count.source_request_sha256 != request.source.request_sha256
                or count.generation_sha256 != request.generation_sha256
                or count.counting_sha256 != request.counting_sha256
                or not 1 <= _int(count.input_tokens) <= MAX_INPUT_TOKENS):
            raise ValueError()
        return price_nano_usage(OpenAiUsage(count.input_tokens, 0,
                                            request.source.maximum_output_tokens, 0), now)
    except (ValueError, TypeError, AttributeError):
        raise OpenAiProtocolError("invalid_nano_quote") from None


def exceeds_reserved_tokens(reply: OpenAiReply, request: OpenAiRequest, count: OpenAiCount) -> bool:
    try:
        _request(request)
        if (type(reply) is not OpenAiReply or type(count) is not OpenAiCount
                or reply.source_request_sha256 != request.source.request_sha256
                or count.source_request_sha256 != request.source.request_sha256
                or reply.model != MODEL.version or count.protocol_version != PROTOCOL_VERSION
                or reply.generation_sha256 != request.generation_sha256
                or count.generation_sha256 != request.generation_sha256
                or count.counting_sha256 != request.counting_sha256
                or not 1 <= _int(count.input_tokens) <= MAX_INPUT_TOKENS):
            raise ValueError()
        _valid_usage(reply.usage)
        return (reply.usage.input_tokens > count.input_tokens
                or reply.usage.output_tokens > request.source.maximum_output_tokens)
    except (ValueError, TypeError, AttributeError):
        raise OpenAiProtocolError("invalid_openai_binding") from None
