"""Private answer bridge. Disabled dependency by default; no live adapter/config.

The worker token authenticates the service boundary, not end-user membership.
ASP.NET alone loads authorized evidence and owns ledger admission/publication.
"""
import asyncio
import base64
import hashlib
import json

from fastapi import APIRouter, Depends, HTTPException, Request
from .config import get_settings
from .service_security import authorize
from .cited_answers import (AnswerAdapter, AnswerBoundaryError, AnswerContext,
                           AnswerEvidence, AnswerModel, DisabledAnswerAdapter,
                           prepare_answer_request, propose_prepared_checked)
from .cited_prompt_measurement import DisabledPromptCounter, PromptCounter, measure_prompt
from .cited_source_context import source_from_wire

router = APIRouter()


def get_answer_adapter():
    # No request field, environment variable or model name enables generation.
    return DisabledAnswerAdapter()


def get_prompt_counter():
    return DisabledPromptCounter()


def _unique(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError()
        result[key] = value
    return result


def _keys(value, keys):
    if type(value) is not dict or set(value) != set(keys.split()):
        raise ValueError()
    return value


def _authorize(request):
    authorize(request, get_settings())


async def _body(request, limit):
    body = bytearray()
    async with asyncio.timeout(15):
        async for part in request.stream():
            if len(body) + len(part) > limit:
                raise HTTPException(413, "Answer request exceeds the byte limit")
            body.extend(part)
    return bytes(body)


def _source(body):
    if len(body) > 65536:
        raise ValueError()
    payload = _keys(json.loads(body.decode("utf-8"), object_pairs_hook=_unique),
                    "schemaVersion context model question evidence maximumOutputTokens")
    if payload["schemaVersion"] not in ("1.0", "1.1") or type(payload["evidence"]) is not list:
        raise ValueError()
    context_data = _keys(payload["context"], "organizationId actorId retrievalRunId")
    model_data = _keys(payload["model"], "providerAlias model version")
    context = AnswerContext(context_data["organizationId"], context_data["actorId"], context_data["retrievalRunId"])
    model = AnswerModel(model_data["providerAlias"], model_data["model"], model_data["version"])
    evidence = []
    source_aware = payload["schemaVersion"] == "1.1"
    for item in payload["evidence"]:
        _keys(item, "organizationId retrievalRunId hitId spanIds content contentSha256" + (" source" if source_aware else ""))
        if type(item["spanIds"]) is not list:
            raise ValueError()
        evidence.append(AnswerEvidence(item["organizationId"], item["retrievalRunId"], item["hitId"],
                                       tuple(item["spanIds"]), item["content"], item["contentSha256"],
                                       source_from_wire(item["source"]) if source_aware else None))
    args = (context, model, payload["question"], tuple(evidence))
    return args, payload["maximumOutputTokens"], model_data


async def _prepare(body, counter):
    args, limit, model_data = _source(body)
    prepared = prepare_answer_request(*args, limit)
    count = await measure_prompt(prepared, counter)
    receipt = {"schemaVersion": "1.0", "requestSha256": hashlib.sha256(body).hexdigest(),
               "model": model_data, "promptSha256": prepared.prompt_sha256,
               "promptVersion": prepared.prompt_version, "counterVersion": counter.version,
               "inputTokens": count, "maximumOutputTokens": limit}
    return receipt, prepared


@router.post("/internal/cited/answer-prepare")
async def answer_prepare(request: Request, counter: PromptCounter = Depends(get_prompt_counter)):
    _authorize(request)
    if type(counter) is DisabledPromptCounter:
        raise HTTPException(503, "Prompt measurement is disabled")
    try:
        receipt, _ = await _prepare(await _body(request, 65536), counter)
        return receipt
    except TimeoutError:
        raise HTTPException(408, "Answer request timed out") from None
    except (AnswerBoundaryError, ValueError, TypeError, KeyError, UnicodeError, RecursionError):
        raise HTTPException(422, "Prompt preparation was rejected") from None


@router.post("/internal/cited/answer-proposal")
async def answer_proposal(request: Request, adapter: AnswerAdapter = Depends(get_answer_adapter),
                          counter: PromptCounter = Depends(get_prompt_counter)):
    _authorize(request)
    if type(adapter) is DisabledAnswerAdapter or type(counter) is DisabledPromptCounter:
        raise HTTPException(503, "Answer generation is disabled")
    try:
        dispatch = _keys(json.loads((await _body(request, 98304)).decode("utf-8"), object_pairs_hook=_unique),
                         "requestUtf8 preparation")
        body = base64.b64decode(dispatch["requestUtf8"], validate=True)
        receipt, prepared = await _prepare(body, counter)
        # Compare canonical JSON, not Python dict equality (True must not equal 1).
        if json.dumps(dispatch["preparation"], sort_keys=True) != json.dumps(receipt, sort_keys=True):
            raise ValueError()
        result = await propose_prepared_checked(prepared, adapter)
        if result.input_tokens > receipt["inputTokens"]:
            raise AnswerBoundaryError("unreconciled_input_usage")
        return {"schemaVersion": "1.0", "requestSha256": receipt["requestSha256"],
                "promptSha256": receipt["promptSha256"], "model": receipt["model"], "outcome": result.outcome,
                "proposalUtf8": base64.b64encode(result.proposal_utf8).decode("ascii"),
                "inputTokens": result.input_tokens, "outputTokens": result.output_tokens}
    except TimeoutError:
        raise HTTPException(408, "Answer request timed out") from None
    except AnswerBoundaryError as error:
        status = 422 if error.code == "invalid_answer_request" else 502
        raise HTTPException(status, "Answer proposal was rejected") from None
    except (ValueError, TypeError, KeyError, UnicodeError, RecursionError):
        raise HTTPException(422, "Invalid answer request") from None
