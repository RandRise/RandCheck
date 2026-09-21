"""Private synthetic pilot bridge. Preparation is offline; all HTTP follows dispatch.

The trusted ASP.NET caller owns authorization and the committed ledger operation.
This module is not registered in the public/default application.
"""
import base64
import hashlib
import json

from .cited_answer_routes import _source, _keys, _unique
from .cited_answers import prepare_answer_request
from .cited_openai_protocol import (
    PROTOCOL_VERSION, MAX_INPUT_TOKENS, parse_openai_count,
    prepare_openai_request, inspect_openai_reply, OpenAiProtocolError,
)
from .cited_openai_transport import OpenAiTransportError

COUNTER_VERSION = "openai-count-after-reservation-v1"


def _b64(raw):
    return base64.b64encode(raw).decode("ascii") if raw is not None else None


def prepare(body, reasoning_effort="minimal"):
    args, limit, model = _source(body)
    source = prepare_answer_request(*args, limit)
    wire = prepare_openai_request(source, reasoning_effort)
    # This is a reservation ceiling, explicitly NOT a measured token count.
    receipt = {"schemaVersion": "1.0", "requestSha256": hashlib.sha256(body).hexdigest(),
               "model": model, "promptSha256": source.prompt_sha256,
               "promptVersion": source.prompt_version, "counterVersion": COUNTER_VERSION,
               "inputTokens": MAX_INPUT_TOKENS, "maximumOutputTokens": limit,
               "openAi": {"protocolVersion": PROTOCOL_VERSION,
                          "generationSha256": wire.generation_sha256,
                          "countingSha256": wire.counting_sha256}}
    if reasoning_effort == "low":
        receipt["openAi"]["reasoningEffort"] = "low"
    return receipt, wire


async def propose(dispatch_bytes, transport, reasoning_effort="minimal"):
    if type(dispatch_bytes) is not bytes or not 1 <= len(dispatch_bytes) <= 98304:
        raise ValueError()
    dispatch = _keys(json.loads(dispatch_bytes.decode(), object_pairs_hook=_unique), "requestUtf8 preparation")
    body = base64.b64decode(dispatch["requestUtf8"], validate=True)
    receipt, wire = prepare(body, reasoning_effort)
    if json.dumps(receipt, sort_keys=True) != json.dumps(dispatch["preparation"], sort_keys=True):
        raise ValueError()
    accounting = {"generationSha256": wire.generation_sha256, "accountingKnown": False,
                  "countRequestId": None, "countResponseUtf8": None, "measuredInputTokens": None,
                  "providerRequestId": None, "responseId": None, "responseSha256": None,
                  "responseUtf8": None, "cachedInputTokens": 0, "reasoningTokens": 0,
                  "providerStatus": None, "httpStatus": None}
    result = {"schemaVersion": "1.0", "requestSha256": receipt["requestSha256"],
              "promptSha256": receipt["promptSha256"], "model": receipt["model"],
              "outcome": "transport_uncertain", "proposalUtf8": "",
              "inputTokens": 0, "outputTokens": 0, "openAi": accounting}
    try:
        counted = await transport.send_counting(wire)
        accounting["countRequestId"] = counted.provider_request_id
        accounting["countResponseUtf8"] = _b64(counted.body_utf8)
        count = parse_openai_count(wire, counted.body_utf8)
        accounting["measuredInputTokens"] = count.input_tokens
        # No generation for an oversized/invalid count, and never count twice.
        response = await transport.send_generation(wire)
        accounting["providerRequestId"] = response.provider_request_id
        accounting["responseUtf8"] = _b64(response.body_utf8)
        accounting["responseSha256"] = hashlib.sha256(response.body_utf8).hexdigest()
        reply = inspect_openai_reply(wire, response.body_utf8, response.provider_request_id)
        accounting.update(accountingKnown=True, responseId=reply.response_id,
                          cachedInputTokens=reply.usage.cached_input_tokens,
                          reasoningTokens=reply.usage.reasoning_tokens, providerStatus=reply.status)
        result.update(inputTokens=reply.usage.input_tokens, outputTokens=reply.usage.output_tokens,
                      outcome="completed" if reply.proposal_outcome == "proposal" else reply.proposal_outcome,
                      proposalUtf8=_b64(reply.proposal_utf8) or "")
    except OpenAiTransportError as error:
        accounting["transportErrorCode"] = error.code if error.code in {
            "invalid_openai_request", "openai_transport_uncertain", "openai_response_rejected", "openai_http_rejected"
        } else "unclassified"
        if error.failure_kind in {"deadline", "http_timeout", "network", "protocol", "other"}:
            accounting["transportFailureKind"] = error.failure_kind
        accounting["httpStatus"] = error.http_status
        if accounting["measuredInputTokens"] is None:
            accounting["countRequestId"] = error.provider_request_id
        else:
            accounting["providerRequestId"] = error.provider_request_id
    except OpenAiProtocolError:
        result["outcome"] = "accounting_unavailable"
    return result
